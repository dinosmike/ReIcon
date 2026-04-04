using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ReIcon.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace ReIcon;

public partial class MainWindow
{
    private const int VkKeyV = 0x56;

    /// <summary>Цвет подсказки «или / Ctrl+V» под картинкой, например <c>#ffffff</c> или <c>#8c8c96</c>.</summary>
    private const string DropPasteHintColorHex = "#2e343a";

    private static readonly System.Windows.Media.Color DropPasteHintColor =
        (System.Windows.Media.Color)ColorConverter.ConvertFromString(DropPasteHintColorHex)!;

    /// <summary>
    /// Высота окна = ширина + это значение, чтобы внутренняя сетка под заголовком была квадратом (шапка 52 px, Margin 20,16,20,20, Border 1).
    /// </summary>
    private const double SquareContentHeightExtraOverWidth = 48;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private Image<Rgba32>? _loadedImage;
    private bool _dropMouseOver;
    private bool _dropDragOver;
    private bool _suppressDropZoneClickAfterDrop;

    public MainWindow()
    {
        InitializeComponent();
        DropPasteHintText.Foreground = new SolidColorBrush(DropPasteHintColor);
        Height = Width + SquareContentHeightExtraOverWidth;
        LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;
        Unloaded += (_, _) => LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;
    }

    private void OnLocalizationLanguageChanged() => UpdateLangButtonLabel();

    private void UpdateLangButtonLabel()
    {
        var c = LocalizationService.CurrentLanguageCode;
        TitleBarLangButton.Content = string.Equals(c, "ru", StringComparison.OrdinalIgnoreCase) ? "Ru" : "En";
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLangButtonLabel();
        Focus();
        UpdateDropZoneSquareSize();
    }

    private void Window_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateDropZoneSquareSize();

    private void UpdateDropZoneSquareSize()
    {
        if (!IsLoaded || MainContentGrid.ActualWidth <= 0 || MainContentGrid.ActualHeight <= 0)
            return;

        var side = Math.Min(MainContentGrid.ActualWidth, MainContentGrid.ActualHeight);
        DropZoneBorder.Width = side;
        DropZoneBorder.Height = side;
    }

    private void DropZone_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            if (!_dropDragOver)
            {
                _dropDragOver = true;
                RefreshDropZoneVisual();
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
            if (_dropDragOver)
            {
                _dropDragOver = false;
                RefreshDropZoneVisual();
            }
        }
    }

    private void DropZone_OnDragLeave(object sender, DragEventArgs e)
    {
        _dropDragOver = false;
        RefreshDropZoneVisual();
    }

    private void DropZone_OnMouseEnter(object sender, MouseEventArgs e)
    {
        _dropMouseOver = true;
        RefreshDropZoneVisual();
    }

    private void DropZone_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _dropMouseOver = false;
        RefreshDropZoneVisual();
    }

    private void RefreshDropZoneVisual()
    {
        var accent = _dropMouseOver || _dropDragOver;
        if (accent)
        {
            DropZoneBorder.Background = (Brush)(TryFindResource("Rs.Button.Accent.Background") ?? Brushes.Transparent);
            DropZoneStroke.Stroke = (Brush)(TryFindResource("Rs.Accent.Dim") ?? Brushes.Transparent);
        }
        else
        {
            DropZoneBorder.Background = (Brush)(TryFindResource("Rs.Surface") ?? Brushes.Transparent);
            DropZoneStroke.Stroke = (Brush)(TryFindResource("Rs.Border") ?? Brushes.Transparent);
        }
    }

    private void DropZone_OnDrop(object sender, DragEventArgs e)
    {
        _dropDragOver = false;
        RefreshDropZoneVisual();
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        _suppressDropZoneClickAfterDrop = true;
        var path = paths[0];
        // Нельзя сразу Hide/ShowDialog из Drop — OLE drag не успевает завершиться, курсор «залипает» на перетаскивании.
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            if (!IsLoaded)
                return;
            TryLoadImage(path);
        });
    }

    private void DropZone_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (_suppressDropZoneClickAfterDrop)
        {
            _suppressDropZoneClickAfterDrop = false;
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.T("MainWindow.OpenFileDialogTitle"),
            Filter = LocalizationService.T("MainWindow.OpenFileDialogFilter")
        };

        if (dlg.ShowDialog() == true)
            TryLoadImage(dlg.FileName);
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (e.IsRepeat)
            return;

        if (!IsPasteKey(e))
            return;

        if (!TryLoadImageFromClipboard(out var image))
            return;

        AcceptImageAndStartFlow(image);
        e.Handled = true;
    }

    private static bool IsPasteKey(KeyEventArgs e)
    {
        if (e.Key == Key.V)
            return true;
        if (e.Key == Key.System && e.SystemKey == Key.V)
            return true;
        return (GetAsyncKeyState(VkKeyV) & 0x8000) != 0;
    }

    private static bool TryLoadImageFromClipboard(out Image<Rgba32> image)
    {
        image = null!;

        try
        {
            if (Clipboard.ContainsData("PNG"))
            {
                var data = Clipboard.GetData("PNG");
                switch (data)
                {
                    case MemoryStream msPng:
                    {
                        msPng.Position = 0;
                        image = SixLabors.ImageSharp.Image.Load<Rgba32>(msPng);
                        return true;
                    }
                    case byte[] bytes:
                        image = SixLabors.ImageSharp.Image.Load<Rgba32>(new MemoryStream(bytes));
                        return true;
                    case Stream stream:
                    {
                        using var copy = new MemoryStream();
                        stream.CopyTo(copy);
                        copy.Position = 0;
                        image = SixLabors.ImageSharp.Image.Load<Rgba32>(copy);
                        return true;
                    }
                }
            }

            if (Clipboard.ContainsImage())
            {
                var bmp = Clipboard.GetImage();
                if (bmp != null)
                {
                    using var ms = new MemoryStream();
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(bmp));
                    enc.Save(ms);
                    ms.Position = 0;
                    image = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
                    return true;
                }
            }

            if (Clipboard.ContainsData(DataFormats.Bitmap))
            {
                if (Clipboard.GetData(DataFormats.Bitmap) is System.Drawing.Bitmap gdiBmp)
                {
                    using (gdiBmp)
                    {
                        using var ms = new MemoryStream();
                        gdiBmp.Save(ms, ImageFormat.Png);
                        ms.Position = 0;
                        image = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private void StartCropFlow()
    {
        if (_loadedImage == null)
            return;

        App.ExitRequestedFromChildTitleBar = false;
        _dropMouseOver = false;
        _dropDragOver = false;
        RefreshDropZoneVisual();

        ImgRectangle? lastCropRect = null;

        var forCrop = _loadedImage.Clone();
        Hide();

        var crop = new CropWindow(forCrop, lastCropRect);
        crop.Owner = this;
        if (crop.ShowDialog() != true)
        {
            if (!SkipShowMainAfterDialog())
            {
                Show();
                ResetToStart();
            }
            return;
        }

        Image<Rgba32>? cropped = crop.CropResult;
        lastCropRect = crop.SavedCropRectangle;
        if (cropped == null)
        {
            if (!SkipShowMainAfterDialog())
            {
                Show();
                ResetToStart();
            }
            return;
        }

        try
        {
            for (;;)
            {
                var round = new RoundCornersWindow(cropped.Clone())
                {
                    Owner = this
                };
                round.ShowDialog();

                if (round.NavigateToMainWindow)
                {
                    cropped?.Dispose();
                    cropped = null;
                    if (!SkipShowMainAfterDialog())
                    {
                        Show();
                        ResetToStart();
                    }

                    return;
                }

                if (round.NavigateBackToCrop)
                {
                    cropped.Dispose();
                    cropped = null;

                    var forCropAgain = _loadedImage.Clone();
                    var cropAgain = new CropWindow(forCropAgain, lastCropRect)
                    {
                        Owner = this
                    };
                    if (cropAgain.ShowDialog() != true)
                    {
                        if (!SkipShowMainAfterDialog())
                        {
                            Show();
                            ResetToStart();
                        }
                        return;
                    }

                    cropped = cropAgain.CropResult;
                    lastCropRect = cropAgain.SavedCropRectangle;
                    if (cropped == null)
                    {
                        if (!SkipShowMainAfterDialog())
                        {
                            Show();
                            ResetToStart();
                        }
                        return;
                    }

                    continue;
                }

                if (round.DialogResult != true || round.Result == null)
                {
                    if (!SkipShowMainAfterDialog())
                    {
                        Show();
                        ResetToStart();
                    }
                    return;
                }

                var preview = new PreviewWindow(round.Result)
                {
                    Owner = this
                };
                preview.ShowDialog();

                if (preview.NavigateToMainWindow)
                {
                    cropped?.Dispose();
                    cropped = null;
                    if (!SkipShowMainAfterDialog())
                    {
                        Show();
                        ResetToStart();
                    }

                    return;
                }

                if (preview.NavigateBackToRoundCorners)
                    continue;

                if (!SkipShowMainAfterDialog())
                {
                    ResetToStart();
                    Show();
                }
                return;
            }
        }
        finally
        {
            cropped?.Dispose();
        }
    }

    /// <summary>После крестика на дочернем окне Shutdown() ещё не выставил HasShutdownStarted — см. <see cref="App.ExitRequestedFromChildTitleBar"/>.</summary>
    private static bool SkipShowMainAfterDialog() =>
        App.ExitRequestedFromChildTitleBar ||
        Application.Current?.Dispatcher.HasShutdownStarted == true;

    private void TryLoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            var img = ImageDecodeService.Load(path);
            AcceptImageAndStartFlow(img);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T("MainWindow.MsgOpenFileFailed", ex.Message),
                LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AcceptImageAndStartFlow(Image<Rgba32> img)
    {
        _loadedImage?.Dispose();
        _loadedImage = img;
        StartCropFlow();
    }

    private void ResetToStart()
    {
        _loadedImage?.Dispose();
        _loadedImage = null;
        _dropMouseOver = false;
        _dropDragOver = false;
        RefreshDropZoneVisual();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarAbout_OnClick(object sender, RoutedEventArgs e) => About.Show(this);

    private void TitleBarAbout_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        About.Show(this);
        e.Handled = true;
    }

    private void TitleBarMinimize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e) =>
        Close();

    private void TitleBarLanguage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.ContextMenu != null)
        {
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.Placement = PlacementMode.Bottom;
            b.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void LanguageMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not string tag)
            return;
        var code = AppLanguageCatalog.Normalize(tag);
        LocalizationService.Apply(code);
        var settings = SettingsStorage.Load();
        settings.Ui.Language = code;
        SettingsStorage.Save(settings);
    }

    protected override void OnClosed(EventArgs e)
    {
        _loadedImage?.Dispose();
        base.OnClosed(e);
    }
}
