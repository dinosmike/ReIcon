using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ReIcon.Controls;
using ReIcon.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReIcon;

public partial class RoundCornersWindow
{
    /// <summary>Ползунок, для которого начали перетаскивание с дорожки (не с бегунка).</summary>
    private Slider? _sliderTrackDrag;

    private readonly Image<Rgba32> _image;
    private Image<Rgba32>? _previewImageForSampling;
    private bool _pipetteDragging;
    private bool _suppressStrokeHexSync;
    private StrokeColorPickerWindow? _pickerWindow;
    private Rgba32 _strokeColor;
    /// <summary>Результат после «Конвертировать в ICO»; вызывающий забирает владение.</summary>
    public Image<Rgba32>? Result { get; private set; }

    /// <summary>«Назад» — снова открыть шаг обрезки (не выход на главный экран).</summary>
    public bool NavigateBackToCrop { get; private set; }

    /// <summary>Правый клик по «Назад»: вернуться на первый шаг (главное окно).</summary>
    public bool NavigateToMainWindow { get; private set; }

    // Фон области превью (как на шаге 4): Segoe MDL2 E706 / E708.
    private const string ToggleGlyphWhenPreviewDark = "\uE706";
    private const string ToggleGlyphWhenPreviewLight = "\uE708";

    private static readonly SolidColorBrush ToggleButtonLightGraySurfaceBrush = FrozenBrushFromHex("#4e6f6b");

    private static readonly System.Windows.Media.Color PreviewAreaDarkColor =
        System.Windows.Media.Color.FromRgb(0x0D, 0x11, 0x17);

    private static readonly System.Windows.Media.Color PreviewAreaLightColor =
        System.Windows.Media.Color.FromRgb(0xF6, 0xF8, 0xFA);

    private const int PreviewAreaBackgroundAnimationMs = 999;

    private bool _previewLightBg;
    private bool _previewAreaFirstPaint = true;

    public RoundCornersWindow(Image<Rgba32> image)
    {
        _image = image;
        _strokeColor = DefaultStrokeColorFromImage(_image);
        InitializeComponent();
        PreviewImage.Cursor = PipetteCursor.Instance;
        SourceInitialized += RoundCornersWindow_SourceInitialized;
        Loaded += RoundCornersWindow_OnLoaded;
        LayoutUpdated += RoundCornersWindow_OnLayoutUpdated;
    }

    /// <summary>Вертикально центрирует только кнопку фона превью относительно ряда кнопок «Назад / PNG / ICO».</summary>
    private void RoundCornersWindow_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (TogglePreviewBgButton.Parent is not Grid bottomGrid)
            return;
        if (bottomGrid.Children.Count < 2)
            return;
        if (bottomGrid.Children[1] is not StackPanel col1Stack)
            return;
        if (col1Stack.Children.Count < 2)
            return;
        if (col1Stack.Children[0] is not FrameworkElement sliderBlock)
            return;
        if (col1Stack.Children[1] is not FrameworkElement buttonRow)
            return;

        double hSlider = sliderBlock.ActualHeight;
        double hBtn = buttonRow.ActualHeight;
        double hToggle = TogglePreviewBgButton.ActualHeight;
        if (hSlider <= 0 || hBtn <= 0 || hToggle <= 0)
            return;

        double gapBelowSliders = sliderBlock.Margin.Bottom > 0 ? sliderBlock.Margin.Bottom : 12;
        double top = hSlider + gapBelowSliders + Math.Max(0, (hBtn - hToggle) / 2);
        TogglePreviewBgButton.Margin = new Thickness(0, top, 0, 0);
    }

    private void RoundCornersWindow_SourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= RoundCornersWindow_SourceInitialized;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dipW = _image.Width / dpi.DpiScaleX;
        double dipH = _image.Height / dpi.DpiScaleY;

        // Превью по размеру изображения; короткая сторона не меньше 256 dip (равномерное масштабирование).
        const double minPreviewSideDip = 256;
        double dispW = dipW;
        double dispH = dipH;
        double minDim = Math.Min(dipW, dipH);
        if (minDim > 0 && minDim < minPreviewSideDip)
        {
            double scale = minPreviewSideDip / minDim;
            dispW = dipW * scale;
            dispH = dipH * scale;
        }

        PreviewImage.Width = dispW;
        PreviewImage.Height = dispH;
        PreviewImage.Stretch = Stretch.Fill;

        // Отступы вокруг превью (поля Grid, рамка, Padding ScrollViewer) + фиксированная ширина панели с двумя ползунками.
        const double chromeW = 68;
        const double chromeH = 340;
        const double minWinW = 600;
        const double minWinH = 600;

        const double workAreaMarginDip = 40;
        var wa = SystemParameters.WorkArea;
        double maxWinW = Math.Max(100, wa.Width - workAreaMarginDip);
        double maxWinH = Math.Max(100, wa.Height - workAreaMarginDip);

        double winW = Math.Min(Math.Max(dispW + chromeW, minWinW), maxWinW);
        double winH = Math.Min(Math.Max(dispH + chromeH, minWinH), maxWinH);

        WindowWorkAreaHelper.CenterInWorkArea(this, winW, winH);
    }

    /// <summary>Цвет обводки по умолчанию — из центрального пикселя; при полной прозрачности — ближайший непрозрачный, иначе белый.</summary>
    private static Rgba32 DefaultStrokeColorFromImage(Image<Rgba32> image)
    {
        int w = image.Width;
        int h = image.Height;
        if (w <= 0 || h <= 0)
            return new Rgba32(255, 255, 255, 255);

        int cx = (w - 1) / 2;
        int cy = (h - 1) / 2;
        int maxR = Math.Min(64, Math.Max(w, h));
        for (int r = 0; r <= maxR; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (r > 0 && Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        continue;
                    int x = cx + dx;
                    int y = cy + dy;
                    if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                        continue;
                    var p = image[x, y];
                    if (p.A == 0)
                        continue;
                    return new Rgba32(p.R, p.G, p.B, 255);
                }
            }
        }

        return new Rgba32(255, 255, 255, 255);
    }

    private void RoundCornersWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= RoundCornersWindow_OnLoaded;

        float maxR = RoundCornersService.MaxRadiusPx(_image.Width, _image.Height);
        RadiusSlider.Maximum = maxR;
        RadiusSlider.Value = 0;

        float maxSw = RoundCornersService.MaxStrokeWidthPx(_image.Width, _image.Height);
        StrokeWidthSlider.Maximum = Math.Max(1, Math.Min(128, (int)Math.Floor(maxSw)));
        StrokeWidthSlider.Value = 0;

        SyncStrokeHexFromColor();
        UpdateStrokeSwatchBrush();
        UpdatePreview();
        ApplyPreviewBackgroundUi();
    }

    private static SolidColorBrush FrozenBrushFromHex(string hex)
    {
        var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void TogglePreviewBg_OnClick(object sender, RoutedEventArgs e)
    {
        _previewLightBg = !_previewLightBg;
        ApplyPreviewBackgroundUi();
    }

    private void ApplyPreviewBackgroundUi(bool animateBackground = true)
    {
        var targetBg = _previewLightBg ? PreviewAreaLightColor : PreviewAreaDarkColor;
        if (_previewAreaFirstPaint)
        {
            SetPreviewAreaBackgroundSolid(targetBg);
            _previewAreaFirstPaint = false;
        }
        else if (animateBackground)
            AnimatePreviewAreaBackground(targetBg);
        else
            SetPreviewAreaBackgroundSolid(targetBg);

        if (_previewLightBg)
        {
            TogglePreviewBgGlyph.Text = ToggleGlyphWhenPreviewLight;
            TogglePreviewBgGlyph.Foreground = Brushes.Black;
            TogglePreviewBgButton.Background = ToggleButtonLightGraySurfaceBrush;
            TogglePreviewBgButton.ToolTip = LocalizationService.T("RoundCornersWindow.PreviewDarkTooltip");
        }
        else
        {
            TogglePreviewBgGlyph.Text = ToggleGlyphWhenPreviewDark;
            TogglePreviewBgGlyph.Foreground = Brushes.White;
            TogglePreviewBgButton.Background = Brushes.Transparent;
            TogglePreviewBgButton.ToolTip = LocalizationService.T("RoundCornersWindow.PreviewLightTooltip");
        }
    }

    private void SetPreviewAreaBackgroundSolid(System.Windows.Media.Color c)
    {
        PreviewAnimatedBackground.Background = new SolidColorBrush(c);
    }

    private void AnimatePreviewAreaBackground(System.Windows.Media.Color target)
    {
        SolidColorBrush brush;
        if (PreviewAnimatedBackground.Background is SolidColorBrush existing && !existing.IsFrozen)
        {
            brush = existing;
        }
        else
        {
            System.Windows.Media.Color from = PreviewAreaDarkColor;
            if (PreviewAnimatedBackground.Background is SolidColorBrush b)
                from = (System.Windows.Media.Color)b.GetValue(SolidColorBrush.ColorProperty);

            brush = new SolidColorBrush(from);
            PreviewAnimatedBackground.Background = brush;
        }

        var fromColor = (System.Windows.Media.Color)brush.GetValue(SolidColorBrush.ColorProperty);

        var anim = new ColorAnimation
        {
            From = fromColor,
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(PreviewAreaBackgroundAnimationMs)),
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    private void SyncStrokeHexFromColor()
    {
        _suppressStrokeHexSync = true;
        StrokeColorHex.Text = $"{_strokeColor.R:X2}{_strokeColor.G:X2}{_strokeColor.B:X2}";
        _suppressStrokeHexSync = false;
        _pickerWindow?.SyncFromMain(_strokeColor);
    }

    internal Rgba32 GetStrokeColorRgb() => _strokeColor;

    /// <param name="skipPickerSync">Не обновлять немодальное окно цвета (когда цвет пришёл из его же поля ввода).</param>
    internal void SetStrokeColorRgb(Rgba32 c, bool skipPickerSync = false)
    {
        _strokeColor = c;
        _suppressStrokeHexSync = true;
        StrokeColorHex.Text = $"{c.R:X2}{c.G:X2}{c.B:X2}";
        _suppressStrokeHexSync = false;
        UpdateStrokeSwatchBrush();
        UpdatePreview();
        if (!skipPickerSync)
            _pickerWindow?.SyncFromMain(c);
    }

    private static bool IsThumbOrDescendant(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Thumb)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static void SetSliderValueFromMouse(Slider slider, System.Windows.Point pos)
    {
        if (slider.ActualWidth <= 0)
            return;

        var fraction = pos.X / slider.ActualWidth;
        fraction = Math.Clamp(fraction, 0, 1);
        var newValue = slider.Minimum + fraction * (slider.Maximum - slider.Minimum);
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
        {
            var tick = slider.TickFrequency;
            newValue = Math.Round(newValue / tick) * tick;
        }

        newValue = Math.Clamp(newValue, slider.Minimum, slider.Maximum);
        slider.Value = newValue;
    }

    private void RoundCornersSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
            return;
        if (IsThumbOrDescendant(e.OriginalSource as DependencyObject))
            return;

        SetSliderValueFromMouse(slider, e.GetPosition(slider));
        _sliderTrackDrag = slider;
        slider.CaptureMouse();
        e.Handled = true;
    }

    private void RoundCornersSlider_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Slider slider || !ReferenceEquals(_sliderTrackDrag, slider))
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        SetSliderValueFromMouse(slider, e.GetPosition(slider));
    }

    private void RoundCornersSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
            return;
        if (!ReferenceEquals(_sliderTrackDrag, slider))
            return;
        _sliderTrackDrag = null;
        slider.ReleaseMouseCapture();
    }

    private void RoundCornersSlider_LostMouseCapture(object sender, EventArgs e)
    {
        if (sender is Slider slider && ReferenceEquals(_sliderTrackDrag, slider))
            _sliderTrackDrag = null;
    }

    private void RoundCornersSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Slider slider || !slider.IsEnabled)
            return;

        double step = slider.TickFrequency > 0 ? slider.TickFrequency : 1;
        double direction = e.Delta > 0 ? 1 : -1;
        double newVal = slider.Value + direction * step;
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
            newVal = Math.Round(newVal / slider.TickFrequency) * slider.TickFrequency;
        newVal = Math.Clamp(newVal, slider.Minimum, slider.Maximum);
        if (Math.Abs(newVal - slider.Value) < double.Epsilon)
        {
            e.Handled = true;
            return;
        }

        slider.Value = newVal;
        e.Handled = true;
    }

    private void UpdateStrokeSwatchBrush()
    {
        var brush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(_strokeColor.A, _strokeColor.R, _strokeColor.G, _strokeColor.B));
        brush.Freeze();
        StrokeColorSwatch.Background = brush;
    }

    private void StrokeColorSwatch_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_pickerWindow != null)
        {
            _pickerWindow.Activate();
            e.Handled = true;
            return;
        }

        var w = new StrokeColorPickerWindow(this);
        _pickerWindow = w;
        w.Closed += (_, _) =>
        {
            if (ReferenceEquals(_pickerWindow, w))
                _pickerWindow = null;
        };
        w.Show();
        e.Handled = true;
    }

    private void PreviewImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_previewImageForSampling == null)
            return;
        _pipetteDragging = true;
        PreviewImage.CaptureMouse();
        e.Handled = true;
        SamplePipetteAt(e.GetPosition(PreviewImage));
    }

    private void PreviewImage_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pipetteDragging || e.LeftButton != MouseButtonState.Pressed)
            return;
        e.Handled = true;
        SamplePipetteAt(e.GetPosition(PreviewImage));
    }

    private void PreviewImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pipetteDragging)
            return;
        _pipetteDragging = false;
        PreviewImage.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void PreviewImage_OnLostMouseCapture(object sender, EventArgs e)
    {
        _pipetteDragging = false;
    }

    private void SamplePipetteAt(System.Windows.Point pos)
    {
        if (_previewImageForSampling == null)
            return;

        double w = PreviewImage.ActualWidth;
        double h = PreviewImage.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        double u = Math.Clamp(pos.X / w, 0, 1);
        double v = Math.Clamp(pos.Y / h, 0, 1);
        int iw = _previewImageForSampling.Width;
        int ih = _previewImageForSampling.Height;
        int px = (int)Math.Clamp(Math.Floor(u * iw), 0, iw - 1);
        int py = (int)Math.Clamp(Math.Floor(v * ih), 0, ih - 1);
        var c = _previewImageForSampling[px, py];
        if (c.A == 0)
            return;

        SetStrokeColorRgb(c);
    }

    private static bool TryParseRgbHex(string raw, out Rgba32 c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        string s = raw.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s.Substring(1);
        if (s.Length != 6)
            return false;
        for (int i = 0; i < 6; i++)
        {
            char ch = s[i];
            bool hex = char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
            if (!hex)
                return false;
        }

        byte r = byte.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        c = new Rgba32(r, g, b, 255);
        return true;
    }

    private void StrokeColorHex_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded || _suppressStrokeHexSync)
            return;
        if (!TryParseRgbHex(StrokeColorHex.Text, out var c))
            return;
        _strokeColor = c;
        UpdateStrokeSwatchBrush();
        UpdatePreview();
        _pickerWindow?.SyncFromMain(_strokeColor);
    }

    private void StrokeColorHex_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (TryParseRgbHex(StrokeColorHex.Text, out var c))
        {
            _strokeColor = c;
            SyncStrokeHexFromColor();
            UpdateStrokeSwatchBrush();
            UpdatePreview();
        }
        else
            SyncStrokeHexFromColor();
    }

    private void StrokeColorHex_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            StrokeColorHex_OnLostFocus(sender, e);
            e.Handled = true;
        }
    }

    private void RadiusSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;
        RadiusValueText.Text = $"{(int)Math.Round(RadiusSlider.Value)} px";
        UpdatePreview();
    }

    private void StrokeWidthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;
        StrokeWidthValueText.Text = $"{(int)Math.Round(StrokeWidthSlider.Value)} px";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        _previewImageForSampling?.Dispose();
        float sw = (float)StrokeWidthSlider.Value;
        Rgba32? stroke = sw > 0 ? _strokeColor : null;
        _previewImageForSampling = RoundCornersService.Apply(_image, (float)RadiusSlider.Value, sw, stroke);
        PreviewImage.Source = WpfBitmapHelper.ToBitmapSource(_previewImageForSampling);
    }

    private void BtnSavePng_OnClick(object sender, RoutedEventArgs e)
    {
        float sw = (float)StrokeWidthSlider.Value;
        Rgba32? stroke = sw > 0 ? _strokeColor : null;

        var dlg = new SaveFileDialog
        {
            Title = LocalizationService.T("RoundCornersWindow.SavePngDialogTitle"),
            Filter = "PNG|*.png",
            DefaultExt = ".png",
            FileName = "image.png",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            using var outImg = RoundCornersService.Apply(_image, (float)RadiusSlider.Value, sw, stroke);
            outImg.SaveAsPng(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T("RoundCornersWindow.MsgSavePngFailed", ex.Message),
                LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BtnConvertToIco_OnClick(object sender, RoutedEventArgs e)
    {
        float sw = (float)StrokeWidthSlider.Value;
        Rgba32? stroke = sw > 0 ? _strokeColor : null;
        Result = RoundCornersService.Apply(_image, (float)RadiusSlider.Value, sw, stroke);
        DialogResult = true;
        Close();
    }

    private void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateBackToCrop = true;
        DialogResult = false;
        Close();
    }

    private void BtnBack_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        NavigateToMainWindow = true;
        NavigateBackToCrop = false;
        DialogResult = false;
        Close();
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

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        App.ExitRequestedFromChildTitleBar = true;
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _pickerWindow?.Close();
        _pickerWindow = null;
        _previewImageForSampling?.Dispose();
        _previewImageForSampling = null;
        _image.Dispose();
        base.OnClosed(e);
    }
}
