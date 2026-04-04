using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfPoint = System.Windows.Point;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;
using WpfRect = System.Windows.Shapes.Rectangle;

namespace ReIcon.Controls;

public partial class ImageCropPanel
{
    private Image<Rgba32>? _image;
    private ImgRectangle _crop;
    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;
    private int _srcW;
    private int _srcH;

    private DragMode _dragMode = DragMode.None;
    private int _lastIx;
    private int _lastIy;

    private DateTime _lastMoveLeftDownUtc = DateTime.MinValue;
    private WpfPoint _lastMoveLeftDownPos;

    /// <summary>Срабатывает при двойном клике внутри рамки (зона перетаскивания), без ручек.</summary>
    public event EventHandler? CropRequested;

    /// <summary>Визуальный размер квадратика ручки (DIP), совпадает с координатами оверлея.</summary>
    public const double HandleVisualSizeDip = 12;

    /// <summary>Отступ вокруг картинки (половина ручки), чтобы ручки у края не обрезались.</summary>
    public const double CropHandleInsetDip = HandleVisualSizeDip / 2;

    private const int MinCropSide = 16;

    private static readonly Brush DimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(199, 0, 0, 0));
    private static readonly Brush CropStrokeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 212, 195));
    private static readonly Brush HandleFillBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 212, 195));

    public ImageCropPanel()
    {
        InitializeComponent();
        ContentInner.Margin = new Thickness(CropHandleInsetDip);
        SizeChanged += (_, _) =>
        {
            UpdateLayoutMapping();
            UpdateOverlayVisuals();
        };
        Loaded += (_, _) =>
        {
            UpdateLayoutMapping();
            UpdateOverlayVisuals();
        };
    }

    public bool HasImage => _image != null;

    public int SourcePixelWidth => _image?.Width ?? 0;

    public int SourcePixelHeight => _image?.Height ?? 0;

    /// <summary>Текущая квадратная рамка в пикселях исходника.</summary>
    public ImgRectangle CropRectangle => _crop;

    /// <summary>Заменяет текущее изображение (владение переданным экземпляром переходит панели).</summary>
    /// <param name="initialCrop">Если задано — восстановить рамку (например после «Назад» со скругления).</param>
    public void SetImage(Image<Rgba32> image, ImgRectangle? initialCrop = null)
    {
        _image?.Dispose();
        _image = image;
        PreviewImage.Source = WpfBitmapHelper.ToBitmapSource(_image);
        if (initialCrop.HasValue)
        {
            _crop = initialCrop.Value;
            ClampCrop();
        }
        else
            InitCrop();
        UpdateLayoutMapping();
        UpdateOverlayVisuals();
    }

    public void ClearImage()
    {
        _image?.Dispose();
        _image = null;
        PreviewImage.Source = null;
        OverlayCanvas.Children.Clear();
    }

    /// <summary>Квадратный фрагмент по текущей рамке (клон).</summary>
    public Image<Rgba32>? ExtractCroppedSquare()
    {
        if (_image == null)
            return null;
        var copy = _image.Clone();
        copy.Mutate(ctx => ctx.Crop(_crop));
        return copy;
    }

    private void InitCrop()
    {
        if (_image == null)
            return;

        var side = Math.Min(_image.Width, _image.Height);
        var x = (_image.Width - side) / 2;
        var y = (_image.Height - side) / 2;
        _crop = new ImgRectangle(x, y, side, side);
    }

    private void UpdateLayoutMapping()
    {
        if (_image == null || OverlayCanvas.ActualWidth < 1 || OverlayCanvas.ActualHeight < 1)
            return;

        _srcW = _image.Width;
        _srcH = _image.Height;
        _scale = Math.Min(OverlayCanvas.ActualWidth / _srcW, OverlayCanvas.ActualHeight / _srcH);
        var dispW = _srcW * _scale;
        var dispH = _srcH * _scale;
        _offsetX = (OverlayCanvas.ActualWidth - dispW) / 2;
        _offsetY = (OverlayCanvas.ActualHeight - dispH) / 2;
    }

    /// <summary>Координаты в пикселях исходника, зажатые в границах (для перетаскивания за край кадра).</summary>
    private void GetClampedPixel(WpfPoint mouse, out int ix, out int iy)
    {
        if (_image == null || _scale <= 0)
        {
            ix = iy = 0;
            return;
        }

        var x = (mouse.X - _offsetX) / _scale;
        var y = (mouse.Y - _offsetY) / _scale;
        ix = (int)Math.Clamp(Math.Floor(x), 0, _srcW - 1);
        iy = (int)Math.Clamp(Math.Floor(y), 0, _srcH - 1);
    }

    private void ClampCrop()
    {
        if (_image == null)
            return;

        var w = _crop.Width;
        var maxSide = Math.Min(_image.Width, _image.Height);
        w = Math.Clamp(w, MinCropSide, maxSide);
        var x = Math.Clamp(_crop.X, 0, _image.Width - w);
        var y = Math.Clamp(_crop.Y, 0, _image.Height - w);
        _crop = new ImgRectangle(x, y, w, w);
    }

    private void UpdateOverlayVisuals()
    {
        OverlayCanvas.Children.Clear();
        if (_image == null || OverlayCanvas.ActualWidth < 1)
            return;

        var cx = _crop.X * _scale + _offsetX;
        var cy = _crop.Y * _scale + _offsetY;
        var cw = _crop.Width * _scale;
        var W = OverlayCanvas.ActualWidth;
        var H = OverlayCanvas.ActualHeight;

        // Одна геометрия «весь кадр минус окно кропа» — без швов между четырьмя прямоугольниками (полоски 1px).
        AddDimOverlay(W, H, cx, cy, cw);

        var border = new WpfRect
        {
            Width = cw,
            Height = cw,
            Stroke = CropStrokeBrush,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, cx);
        Canvas.SetTop(border, cy);
        OverlayCanvas.Children.Add(border);

        var hs = HandleVisualSizeDip;
        AddHandle(cx - hs / 2, cy - hs / 2, hs);
        AddHandle(cx + cw - hs / 2, cy - hs / 2, hs);
        AddHandle(cx - hs / 2, cy + cw - hs / 2, hs);
        AddHandle(cx + cw - hs / 2, cy + cw - hs / 2, hs);
    }

    private void AddDimOverlay(double W, double H, double cx, double cy, double cw)
    {
        if (W < 1 || H < 1 || cw <= 0)
            return;

        var outer = new RectangleGeometry(new Rect(0, 0, W, H));
        var inner = new RectangleGeometry(new Rect(cx, cy, cw, cw));
        var geom = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        var path = new Path
        {
            Fill = DimBrush,
            Data = geom,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        OverlayCanvas.Children.Add(path);
    }

    private void AddHandle(double x, double y, double s)
    {
        var r = new WpfRect
        {
            Width = s,
            Height = s,
            Fill = HandleFillBrush,
            Stroke = CropStrokeBrush,
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        OverlayCanvas.Children.Add(r);
    }

    private void RootGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_image == null)
            return;

        // Координаты относительно OverlayCanvas: ручки могут быть с отрицательными X/Y или за пределами Canvas,
        // поэтому события вешаем на RootGrid (поля от Margin), а не на сам Canvas.
        var p = e.GetPosition(OverlayCanvas);
        var mode = HitTest(p);

        if (mode == DragMode.Move)
        {
            var now = DateTime.UtcNow;
            if (_lastMoveLeftDownUtc != DateTime.MinValue)
            {
                var ms = (now - _lastMoveLeftDownUtc).TotalMilliseconds;
                if (ms <= 500 && ms >= 0 && (p - _lastMoveLeftDownPos).Length <= 8)
                {
                    CropRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    _lastMoveLeftDownUtc = DateTime.MinValue;
                    _dragMode = DragMode.None;
                    RootGrid.ReleaseMouseCapture();
                    return;
                }
            }

            _lastMoveLeftDownUtc = now;
            _lastMoveLeftDownPos = p;
        }
        else
            _lastMoveLeftDownUtc = DateTime.MinValue;

        if (mode == DragMode.None)
            return;

        GetClampedPixel(p, out var ix, out var iy);
        _dragMode = mode;
        _lastIx = ix;
        _lastIy = iy;
        RootGrid.CaptureMouse();
        e.Handled = true;
    }

    private DragMode HitTest(WpfPoint mouseDisp)
    {
        if (_image == null)
            return DragMode.None;

        var cx = _crop.X * _scale + _offsetX;
        var cy = _crop.Y * _scale + _offsetY;
        var cw = _crop.Width * _scale;
        var hs = HandleVisualSizeDip;

        // Прямоугольники ручек (как в AddHandle), чтобы зона захвата совпадала с квадратиками, в т.ч. за пределами растра.
        double nwx = cx - hs / 2;
        double nwy = cy - hs / 2;
        if (PointInRect(mouseDisp, nwx, nwy, hs, hs))
            return DragMode.ResizeNw;
        if (PointInRect(mouseDisp, cx + cw - hs / 2, nwy, hs, hs))
            return DragMode.ResizeNe;
        if (PointInRect(mouseDisp, nwx, cy + cw - hs / 2, hs, hs))
            return DragMode.ResizeSw;
        if (PointInRect(mouseDisp, cx + cw - hs / 2, cy + cw - hs / 2, hs, hs))
            return DragMode.ResizeSe;

        if (mouseDisp.X >= cx && mouseDisp.X <= cx + cw && mouseDisp.Y >= cy && mouseDisp.Y <= cy + cw)
            return DragMode.Move;

        return DragMode.None;
    }

    private static bool PointInRect(WpfPoint p, double x, double y, double w, double h) =>
        p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h;

    private void RootGrid_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_image == null)
            return;

        var p = e.GetPosition(OverlayCanvas);

        if (_dragMode == DragMode.None)
        {
            var mode = HitTest(p);
            RootGrid.Cursor = mode switch
            {
                DragMode.Move => Cursors.SizeAll,
                DragMode.ResizeNw => Cursors.SizeNWSE,
                DragMode.ResizeSe => Cursors.SizeNWSE,
                DragMode.ResizeNe => Cursors.SizeNESW,
                DragMode.ResizeSw => Cursors.SizeNESW,
                _ => Cursors.Arrow
            };
            return;
        }

        GetClampedPixel(p, out var ix, out var iy);

        switch (_dragMode)
        {
            case DragMode.Move:
            {
                var dx = ix - _lastIx;
                var dy = iy - _lastIy;
                _lastIx = ix;
                _lastIy = iy;
                _crop = new ImgRectangle(_crop.X + dx, _crop.Y + dy, _crop.Width, _crop.Width);
                ClampCrop();
                break;
            }
            case DragMode.ResizeSe:
            {
                var size = Math.Min(ix - _crop.X, iy - _crop.Y);
                size = Math.Clamp(size, MinCropSide, Math.Min(_image.Width - _crop.X, _image.Height - _crop.Y));
                _crop = new ImgRectangle(_crop.X, _crop.Y, size, size);
                ClampCrop();
                break;
            }
            case DragMode.ResizeNw:
            {
                var right = _crop.X + _crop.Width;
                var bottom = _crop.Y + _crop.Width;
                var size = Math.Min(right - ix, bottom - iy);
                size = Math.Clamp(size, MinCropSide, Math.Min(right, bottom));
                _crop = new ImgRectangle(right - size, bottom - size, size, size);
                ClampCrop();
                break;
            }
            case DragMode.ResizeNe:
            {
                // Неподвижен SW; NE = (X+W, Y). Новый размер: min(ix−X, (Y+W)−iy). Верхняя граница — не текущий W,
                // а пределы кадра: справа imgW, сверху Y+W (верх левого угла ≥ 0).
                var bottomY = _crop.Y + _crop.Width;
                var size = Math.Min(ix - _crop.X, bottomY - iy);
                var maxNe = Math.Min(_image.Width - _crop.X, bottomY);
                size = Math.Clamp(size, MinCropSide, maxNe);
                _crop = new ImgRectangle(_crop.X, bottomY - size, size, size);
                ClampCrop();
                break;
            }
            case DragMode.ResizeSw:
            {
                // Неподвижен NE; SW = (X, Y+W). Новый размер: min((X+W)−ix, iy−Y).
                var rightX = _crop.X + _crop.Width;
                var size = Math.Min(rightX - ix, iy - _crop.Y);
                var maxSw = Math.Min(rightX, _image.Height - _crop.Y);
                size = Math.Clamp(size, MinCropSide, maxSw);
                _crop = new ImgRectangle(rightX - size, _crop.Y, size, size);
                ClampCrop();
                break;
            }
        }

        UpdateOverlayVisuals();
    }

    private void RootGrid_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None)
            return;

        _dragMode = DragMode.None;
        RootGrid.ReleaseMouseCapture();
        RootGrid.Cursor = Cursors.Arrow;
    }

    private void RootGrid_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_dragMode != DragMode.None && e.LeftButton != MouseButtonState.Pressed)
        {
            _dragMode = DragMode.None;
            RootGrid.ReleaseMouseCapture();
        }

        if (_dragMode == DragMode.None)
            RootGrid.Cursor = Cursors.Arrow;
    }

    private enum DragMode
    {
        None,
        Move,
        ResizeNw,
        ResizeNe,
        ResizeSw,
        ResizeSe
    }
}
