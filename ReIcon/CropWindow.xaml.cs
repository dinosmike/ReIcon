using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ReIcon.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace ReIcon;

public partial class CropWindow
{
    public Image<Rgba32>? CropResult { get; private set; }

    /// <summary>Рамка при последнем «Обрезать» — для восстановления при возврате с шага скругления.</summary>
    public ImgRectangle? SavedCropRectangle { get; private set; }

    public CropWindow(Image<Rgba32> image, ImgRectangle? restoreCrop = null)
    {
        InitializeComponent();
        CropPanel.SetImage(image, restoreCrop);
        CropPanel.CropRequested += (_, _) => ApplyCrop();
        SourceInitialized += CropWindow_SourceInitialized;
    }

    private void CropWindow_SourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= CropWindow_SourceInitialized;

        int pw = CropPanel.SourcePixelWidth;
        int ph = CropPanel.SourcePixelHeight;
        if (pw < 1 || ph < 1)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dipW = pw / dpi.DpiScaleX;
        double dipH = ph / dpi.DpiScaleY;

        double pad = ImageCropPanel.CropHandleInsetDip * 2;
        CropPanel.Width = dipW + pad;
        CropPanel.Height = dipH + pad;

        const double chromeW = 30;
        const double chromeH = 160;
        const double minWinW = 400;
        var wa = SystemParameters.WorkArea;
        double maxW = Math.Max(200, wa.Width - 40);
        double maxH = Math.Max(200, wa.Height - 40);
        double winW = Math.Min(Math.Max(dipW + pad + chromeW, minWinW), maxW);
        double winH = Math.Min(dipH + pad + chromeH, maxH);

        WindowWorkAreaHelper.CenterInWorkArea(this, winW, winH);
    }

    private void BtnCrop_OnClick(object sender, RoutedEventArgs e) => ApplyCrop();

    private void ApplyCrop()
    {
        SavedCropRectangle = CropPanel.CropRectangle;
        CropResult = CropPanel.ExtractCroppedSquare();
        DialogResult = true;
        Close();
    }

    private void BtnBack_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnBack_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BtnBack_OnClick(sender, e);
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
        CropPanel.ClearImage();
        base.OnClosed(e);
    }
}
