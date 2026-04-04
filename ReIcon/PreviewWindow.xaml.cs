using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ReIcon.Controls;
using ReIcon.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReIcon;

public partial class PreviewWindow
{
    // Иконки кнопки переключения фона превью (шрифт: Segoe MDL2 Assets — Fonts/SegMDL2.ttf).
    // Меняйте только код после \u: в XAML тот же код записывают как &#xE706; (должен совпадать с «тёмным превью»).
    // Таблица / список символов MDL2: https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font
    // В Windows: приложение «Таблица символов» (charmap.exe) → шрифт «Segoe MDL2 Assets».
    // По умолчанию: E706 = Brightness (солнце), E708 = QuietHours (луна / ночь в оболочке Windows). E992–E995 — Volume0…3, не луна.
    private const string ToggleGlyphWhenPreviewDark = "\uE706";
    private const string ToggleGlyphWhenPreviewLight = "\uE708";

    /// <summary>Светло-серый фон круглой кнопки при светлом превью (чёрная иконка читается лучше, чем на акцентном фоне).</summary>
    private static readonly SolidColorBrush ToggleButtonLightGraySurfaceBrush = FrozenBrushFromHex("#4e6f6b");

    private static readonly SolidColorBrush LightPreviewBackgroundBrush = FrozenBrushFromHex("#F6F8FA");
    private static readonly SolidColorBrush LightPreviewLabelBrush = FrozenBrushFromHex("#57606A");

    /// <summary>Фон области превью (как Rs.Window.Background и светлый превью).</summary>
    private static readonly System.Windows.Media.Color PreviewAreaDarkColor =
        System.Windows.Media.Color.FromRgb(0x0D, 0x11, 0x17);

    private static readonly System.Windows.Media.Color PreviewAreaLightColor =
        System.Windows.Media.Color.FromRgb(0xF6, 0xF8, 0xFA);

    private const int PreviewAreaBackgroundAnimationMs = 999;

    private Image<Rgba32> _square;
    private readonly List<TextBlock> _previewSizeLabels = new();
    private bool _previewLightBg;
    private bool _previewAreaFirstPaint = true;

    /// <summary>После закрытия: вернуться к окну скругления (шаг 3), а не завершать поток.</summary>
    public bool NavigateBackToRoundCorners { get; private set; }

    /// <summary>Правый клик по «Назад»: вернуться на первый шаг (главное окно).</summary>
    public bool NavigateToMainWindow { get; private set; }

    public PreviewWindow(Image<Rgba32> croppedSquare)
    {
        _square = croppedSquare;
        InitializeComponent();
        SourceInitialized += PreviewWindow_SourceInitialized;
        Loaded += PreviewWindow_OnLoaded;
    }

    private void PreviewWindow_SourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= PreviewWindow_SourceInitialized;
        WindowWorkAreaHelper.CenterInWorkArea(this, Width, Height);
    }

    private void PreviewWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PreviewWindow_OnLoaded;
        BuildPreviews();
    }

    /// <summary>Цвет в формате #RRGGBB или #AARRGGBB (как в HTML).</summary>
    private static SolidColorBrush FrozenBrushFromHex(string hex)
    {
        var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void BuildPreviews()
    {
        BuildPreviewRows();
        ApplyPreviewBackgroundUi();
    }

    private void BuildPreviewRows()
    {
        PreviewRow.Children.Clear();
        _previewSizeLabels.Clear();

        var dpi = VisualTreeHelper.GetDpi(this);

        foreach (var s in IcoBuildService.IconSizes)
        {
            using var frame = _square.Clone(ctx =>
                ctx.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(s, s),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3
                }));

            var bmp = WpfBitmapHelper.ToBitmapSource(frame);

            var col = new StackPanel
            {
                Margin = new Thickness(0, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Размер в DIP так, чтобы на экране было ровно s×s физических пикселей (1:1 с растром).
            var wDip = s / dpi.DpiScaleX;
            var hDip = s / dpi.DpiScaleY;

            var img = new System.Windows.Controls.Image
            {
                Width = wDip,
                Height = hDip,
                Source = bmp,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = false
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Fant);

            col.Children.Add(img);

            var label = new TextBlock
            {
                Text = $"{s} px",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Brush)FindResource("Rs.Text.Muted")
            };
            _previewSizeLabels.Add(label);
            col.Children.Add(label);

            PreviewRow.Children.Add(col);
        }
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
            foreach (var tb in _previewSizeLabels)
                tb.Foreground = LightPreviewLabelBrush;
            TogglePreviewBgGlyph.Text = ToggleGlyphWhenPreviewLight;
            TogglePreviewBgGlyph.Foreground = Brushes.Black;
            TogglePreviewBgButton.Background = ToggleButtonLightGraySurfaceBrush;
            TogglePreviewBgButton.ToolTip = LocalizationService.T("PreviewWindow.PreviewDarkTooltip");
        }
        else
        {
            var muted = (Brush)FindResource("Rs.Text.Muted");
            foreach (var tb in _previewSizeLabels)
                tb.Foreground = muted;
            TogglePreviewBgGlyph.Text = ToggleGlyphWhenPreviewDark;
            TogglePreviewBgGlyph.Foreground = Brushes.White;
            TogglePreviewBgButton.Background = Brushes.Transparent;
            TogglePreviewBgButton.ToolTip = LocalizationService.T("PreviewWindow.PreviewLightTooltip");
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

        // Не вызывать BeginAnimation(..., null): снимает анимацию и возвращает Color к
        // локальному значению кисти (остаётся тёмным) — переход «светлый → тёмный» выглядел мгновенным.
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

    private void PlaySaveSuccessFeedback()
    {
        SaveSuccessCheckGlyph.BeginAnimation(UIElement.OpacityProperty, null);
        SaveSuccessCheckGlyph.Visibility = Visibility.Visible;
        SaveSuccessCheckGlyph.Opacity = 1;

        var anim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(2),
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) =>
        {
            SaveSuccessCheckGlyph.BeginAnimation(UIElement.OpacityProperty, null);
            SaveSuccessCheckGlyph.Visibility = Visibility.Collapsed;
            SaveSuccessCheckGlyph.Opacity = 0;
        };
        SaveSuccessCheckGlyph.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void BtnSave_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = LocalizationService.T("PreviewWindow.SaveIcoDialogTitle"),
            Filter = LocalizationService.T("PreviewWindow.SaveIcoDialogFilter"),
            DefaultExt = ".ico",
            FileName = "app.ico",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            IcoBuildService.SaveIcoFile(dlg.FileName, _square);
            PlaySaveSuccessFeedback();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.T("PreviewWindow.MsgSaveIcoFailed", ex.Message),
                LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateBackToRoundCorners = true;
        DialogResult = false;
        Close();
    }

    private void BtnBack_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        NavigateToMainWindow = true;
        NavigateBackToRoundCorners = false;
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
        _square.Dispose();
        base.OnClosed(e);
    }
}
