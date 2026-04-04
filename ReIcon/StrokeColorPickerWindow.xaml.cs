using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReIcon.Services;
using SixLabors.ImageSharp.PixelFormats;

namespace ReIcon;

public partial class StrokeColorPickerWindow
{
    private const int SvPlaneSize = 168;
    private const double CursorHalf = 7;
    private const double HueThumbSize = 12;

    private readonly RoundCornersWindow _owner;
    private readonly Rgba32 _initialColor;
    private bool _suppressHex;
    private bool _svDragging;
    private bool _hueDragging;
    private double _hue;
    private double _s;
    private double _v;

    private static readonly string[] PresetHexes = new[]
    {
        "#000000", "#212121", "#424242", "#757575", "#9e9e9e", "#bdbdbd", "#e0e0e0", "#ffffff",
        "#ffeb3b", "#ffc107", "#ff9800", "#ff5722", "#f44336", "#e91e63", "#9c27b0", "#673ab7",
        "#3f51b5", "#2196f3", "#03a9f4", "#00bcd4", "#009688", "#4caf50", "#8bc34a", "#cddc39",
        "#795548", "#5d4037", "#3e2723", "#263238", "#37474f", "#455a64", "#546e7a", "#607d8b",
        "#d32f2f", "#c2185b", "#7b1fa2", "#512da8", "#303f9f", "#1976d2", "#0288d1", "#0097a7",
        "#00796b", "#388e3c", "#689f38", "#f57c00", "#e64a19", "#5d4037", "#00e676", "#ff1744"
    };

    public StrokeColorPickerWindow(RoundCornersWindow owner)
    {
        _owner = owner;
        _initialColor = owner.GetStrokeColorRgb();
        InitializeComponent();
        Owner = owner;
        BuildPresetGrid();
        SyncFromMain(_initialColor);
    }

    private void StrokeColorPickerWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionBesideOwner();
        UpdateHueThumbPosition();
    }

    /// <summary>
    /// Размещает палитру справа от окна скругления, по вертикали по центру относительно него,
    /// чтобы не перекрывать превью. При нехватке места справа — слева от окна, с удержанием в рабочей области.
    /// </summary>
    private void PositionBesideOwner()
    {
        var owner = _owner;
        const double gap = 8;
        double left = owner.Left + owner.ActualWidth + gap;
        double top = owner.Top + Math.Max(0, (owner.ActualHeight - ActualHeight) / 2);

        var work = SystemParameters.WorkArea;
        if (left + ActualWidth > work.Right)
            left = owner.Left - ActualWidth - gap;

        if (left < work.Left)
            left = work.Left;
        if (left + ActualWidth > work.Right)
            left = Math.Max(work.Left, work.Right - ActualWidth);
        if (top + ActualHeight > work.Bottom)
            top = Math.Max(work.Top, work.Bottom - ActualHeight);
        if (top < work.Top)
            top = work.Top;

        Left = left;
        Top = top;
    }

    private void BuildPresetGrid()
    {
        foreach (var hex in PresetHexes)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var b = new Border
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(1),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Cursor = Cursors.Hand,
                ToolTip = hex
            };
            try
            {
                b.BorderBrush = (Brush)FindResource("Rs.Border");
            }
            catch
            {
                b.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }

            b.Background = new SolidColorBrush(color);
            b.MouseLeftButtonDown += (_, _) =>
            {
                var rgba = new Rgba32(color.R, color.G, color.B, 255);
                _owner.SetStrokeColorRgb(rgba, skipPickerSync: true);
                SyncFromMain(rgba);
            };
            PresetGrid.Children.Add(b);
        }
    }

    internal void SyncFromMain(Rgba32 c)
    {
        _suppressHex = true;
        HexBox.Text = $"{c.R:X2}{c.G:X2}{c.B:X2}";
        _suppressHex = false;
        ApplyPreviewBorder(c);
        ColorHsvHelper.RgbToHsv(c.R, c.G, c.B, out double h, out double s, out double v);
        _s = s;
        _v = v;
        if (!double.IsFinite(h))
            h = 0;
        _hue = Math.Clamp(h, 0, 360);
        RebuildSvPlane();
        UpdateSvCursor();
        UpdateHueThumbPosition();
    }

    private void ApplyPreviewBorder(Rgba32 c)
    {
        var brush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        brush.Freeze();
        ColorPreviewBorder.Background = brush;
    }

    private void RebuildSvPlane()
    {
        var wb = new WriteableBitmap(SvPlaneSize, SvPlaneSize, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[SvPlaneSize * SvPlaneSize * 4];
        int stride = SvPlaneSize * 4;
        for (int y = 0; y < SvPlaneSize; y++)
        {
            for (int x = 0; x < SvPlaneSize; x++)
            {
                double s = SvPlaneSize <= 1 ? 0 : x / (double)(SvPlaneSize - 1);
                double vv = SvPlaneSize <= 1 ? 0 : 1 - y / (double)(SvPlaneSize - 1);
                var col = ColorHsvHelper.HsvToColor(_hue, s, vv);
                int i = y * stride + x * 4;
                pixels[i] = col.B;
                pixels[i + 1] = col.G;
                pixels[i + 2] = col.R;
                pixels[i + 3] = 255;
            }
        }

        wb.WritePixels(new Int32Rect(0, 0, SvPlaneSize, SvPlaneSize), pixels, stride, 0);
        wb.Freeze();
        SvPlaneImage.Source = wb;
    }

    private void UpdateSvCursor()
    {
        double cx = _s * SvPlaneSize - CursorHalf;
        double cy = (1 - _v) * SvPlaneSize - CursorHalf;
        Canvas.SetLeft(SvCursorEllipse, cx);
        Canvas.SetTop(SvCursorEllipse, cy);
    }

    private void UpdateHueThumbPosition()
    {
        if (HueStripCanvas == null)
            return;
        double w = HueStripCanvas.ActualWidth;
        double h = HueStripCanvas.ActualHeight;
        if (w <= 1 || h <= 1)
            return;
        double x = _hue / 360.0 * (w - HueThumbSize);
        x = Math.Clamp(x, 0, Math.Max(0, w - HueThumbSize));
        double y = (h - HueThumbSize) / 2;
        Canvas.SetLeft(HueThumbEllipse, x);
        Canvas.SetTop(HueThumbEllipse, y);
    }

    private void HueStripCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateHueThumbPosition();
    }

    private void HueStripCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueStripCanvas.CaptureMouse();
        _hueDragging = true;
        e.Handled = true;
        UpdateHueFromStripMouse(e.GetPosition(HueStripCanvas));
    }

    private void HueStripCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_hueDragging || e.LeftButton != MouseButtonState.Pressed)
            return;
        e.Handled = true;
        UpdateHueFromStripMouse(e.GetPosition(HueStripCanvas));
    }

    private void HueStripCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hueDragging)
            return;
        _hueDragging = false;
        HueStripCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HueStripCanvas_OnLostMouseCapture(object sender, EventArgs e)
    {
        _hueDragging = false;
    }

    private void UpdateHueFromStripMouse(System.Windows.Point p)
    {
        double w = HueStripCanvas.ActualWidth;
        if (w <= 1)
            return;
        _hue = Math.Clamp(p.X / w * 360, 0, 360);
        UpdateHueThumbPosition();
        RebuildSvPlane();
        CommitColorFromHsv();
    }

    private void CommitColorFromHsv()
    {
        var col = ColorHsvHelper.HsvToColor(_hue, _s, _v);
        var rgba = new Rgba32(col.R, col.G, col.B, 255);
        _suppressHex = true;
        HexBox.Text = $"{rgba.R:X2}{rgba.G:X2}{rgba.B:X2}";
        _suppressHex = false;
        ApplyPreviewBorder(rgba);
        _owner.SetStrokeColorRgb(rgba, skipPickerSync: true);
    }

    private void SvPlaneCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _svDragging = true;
        SvPlaneCanvas.CaptureMouse();
        e.Handled = true;
        UpdateSvFromMouse(e.GetPosition(SvPlaneCanvas));
    }

    private void SvPlaneCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_svDragging || e.LeftButton != MouseButtonState.Pressed)
            return;
        e.Handled = true;
        UpdateSvFromMouse(e.GetPosition(SvPlaneCanvas));
    }

    private void SvPlaneCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_svDragging)
            return;
        _svDragging = false;
        SvPlaneCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SvPlaneCanvas_OnLostMouseCapture(object sender, EventArgs e)
    {
        _svDragging = false;
    }

    private void UpdateSvFromMouse(System.Windows.Point p)
    {
        double w = SvPlaneCanvas.ActualWidth;
        double h = SvPlaneCanvas.ActualHeight;
        if (w <= 1 || h <= 1)
            return;
        _s = Math.Clamp(p.X / w, 0, 1);
        _v = Math.Clamp(1 - p.Y / h, 0, 1);
        UpdateSvCursor();
        CommitColorFromHsv();
    }

    private void HexBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHex)
            return;
        if (!TryParseRgbHex(HexBox.Text, out var c))
            return;
        ColorHsvHelper.RgbToHsv(c.R, c.G, c.B, out double hue, out double s, out double v);
        _s = s;
        _v = v;
        if (!double.IsFinite(hue))
            hue = 0;
        _hue = Math.Clamp(hue, 0, 360);
        RebuildSvPlane();
        UpdateSvCursor();
        UpdateHueThumbPosition();
        ApplyPreviewBorder(c);
        _owner.SetStrokeColorRgb(c, skipPickerSync: true);
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

    private void BtnOk_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        _owner.SetStrokeColorRgb(_initialColor, skipPickerSync: true);
        Close();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        BtnCancel_OnClick(sender, e);
    }
}
