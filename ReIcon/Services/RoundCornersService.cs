using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReIcon.Services;

/// <summary>Скругление всех четырёх углов прямоугольника (маска по альфа, RGB сохраняются).</summary>
public static class RoundCornersService
{
    public static float MaxRadiusPx(int width, int height) =>
        Math.Min(width, height) / 2f;

    /// <summary>Максимальная толщина внутренней обводки (пиксели исходного изображения).</summary>
    public static float MaxStrokeWidthPx(int width, int height) =>
        Math.Max(0f, Math.Min(width, height) / 2f - 0.5f);

    /// <summary>Возвращает новое изображение с обрезанными по скруглению углам пикселями (вне — A=0). Опционально — внутренняя обводка по контуру.</summary>
    public static Image<Rgba32> Apply(Image<Rgba32> source, float radiusPx, float strokeWidthPx = 0f, Rgba32? strokeColor = null)
    {
        var result = source.Clone();
        int w = result.Width;
        int h = result.Height;
        float r = Math.Clamp(radiusPx, 0f, MaxRadiusPx(w, h));

        if (r > 0f)
        {
            result.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (!IsInsideRoundedRect(x, y, w, h, r))
                        {
                            var p = row[x];
                            p.A = 0;
                            row[x] = p;
                        }
                    }
                }
            });
        }

        if (strokeWidthPx <= 0f || strokeColor is null)
            return result;

        var stroke = strokeColor.Value;
        float sw = Math.Min(strokeWidthPx, MaxStrokeWidthPx(w, h));

        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var p = row[x];
                    if (p.A == 0)
                        continue;

                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float d = DistanceToRoundedRectBoundary(px, py, w, h, r);
                    if (d <= sw)
                    {
                        p.R = stroke.R;
                        p.G = stroke.G;
                        p.B = stroke.B;
                        p.A = stroke.A;
                        row[x] = p;
                    }
                }
            }
        });

        return result;
    }

    /// <summary>Кратчайшее расстояние от точки внутри формы до внешней границы (внутрь — для inside-обводки).</summary>
    private static float DistanceToRoundedRectBoundary(float px, float py, int w, int h, float r)
    {
        if (r <= 0)
            return Math.Min(Math.Min(px, py), Math.Min(w - px, h - py));

        float d = float.MaxValue;

        if (px >= r && px <= w - r && py >= r && py <= h - r)
        {
            d = Math.Min(d, px);
            d = Math.Min(d, py);
            d = Math.Min(d, w - px);
            d = Math.Min(d, h - py);
        }

        if (px >= r && px <= w - r && py < r)
            d = Math.Min(d, py);
        if (px >= r && px <= w - r && py > h - r)
            d = Math.Min(d, h - py);
        if (py >= r && py <= h - r && px < r)
            d = Math.Min(d, px);
        if (py >= r && py <= h - r && px > w - r)
            d = Math.Min(d, w - px);

        if (px < r && py < r)
        {
            float arc = r - MathF.Sqrt((px - r) * (px - r) + (py - r) * (py - r));
            d = Math.Min(d, Math.Min(py, Math.Min(px, arc)));
        }

        if (px > w - r && py < r)
        {
            float cx = w - r;
            float arc = r - MathF.Sqrt((px - cx) * (px - cx) + (py - r) * (py - r));
            d = Math.Min(d, Math.Min(py, Math.Min(w - px, arc)));
        }

        if (px < r && py > h - r)
        {
            float cy = h - r;
            float arc = r - MathF.Sqrt((px - r) * (px - r) + (py - cy) * (py - cy));
            d = Math.Min(d, Math.Min(px, Math.Min(h - py, arc)));
        }

        if (px > w - r && py > h - r)
        {
            float cx = w - r;
            float cy = h - r;
            float arc = r - MathF.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            d = Math.Min(d, Math.Min(w - px, Math.Min(h - py, arc)));
        }

        return d;
    }

    /// <summary>Центр пикселя (x,y) внутри скруглённого прямоугольника [0,w)×[0,h).</summary>
    private static bool IsInsideRoundedRect(int x, int y, int w, int h, float r)
    {
        float px = x + 0.5f;
        float py = y + 0.5f;

        if (px >= r && px < w - r)
            return py >= 0 && py < h;
        if (py >= r && py < h - r)
            return px >= 0 && px < w;

        float cxTr = w - r;
        float cyTr = r;
        float cxBl = r;
        float cyBl = h - r;
        float cxBr = w - r;
        float cyBr = h - r;

        if (px < r && py < r)
            return DistSq(px, py, r, r) <= r * r;
        if (px >= w - r && py < r)
            return DistSq(px, py, cxTr, cyTr) <= r * r;
        if (px < r && py >= h - r)
            return DistSq(px, py, cxBl, cyBl) <= r * r;
        if (px >= w - r && py >= h - r)
            return DistSq(px, py, cxBr, cyBr) <= r * r;

        return false;
    }

    private static float DistSq(float x, float y, float cx, float cy)
    {
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy;
    }
}
