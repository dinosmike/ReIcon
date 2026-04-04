using System;
using System.Windows.Media;

namespace ReIcon.Services;

/// <summary>Преобразование RGB ↔ HSV (H в градусах 0–360, S и V 0–1).</summary>
internal static class ColorHsvHelper
{
    public static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rn = r / 255.0;
        double gn = g / 255.0;
        double bn = b / 255.0;
        double max = Math.Max(rn, Math.Max(gn, bn));
        double min = Math.Min(rn, Math.Min(gn, bn));
        double d = max - min;
        v = max;
        s = max <= 1e-9 ? 0 : d / max;

        if (d < 1e-9)
        {
            h = 0;
            return;
        }

        if (max == rn)
        {
            double mod = (gn - bn) / d;
            mod %= 6;
            if (mod < 0)
                mod += 6;
            h = 60 * mod;
        }
        else if (max == gn)
            h = 60 * ((bn - rn) / d + 2);
        else
            h = 60 * ((rn - gn) / d + 4);
    }

    public static Color HsvToColor(double h, double s, double v)
    {
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        h = ((h % 360) + 360) % 360;

        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;

        double r1, g1, b1;
        if (h < 60)
        {
            r1 = c;
            g1 = x;
            b1 = 0;
        }
        else if (h < 120)
        {
            r1 = x;
            g1 = c;
            b1 = 0;
        }
        else if (h < 180)
        {
            r1 = 0;
            g1 = c;
            b1 = x;
        }
        else if (h < 240)
        {
            r1 = 0;
            g1 = x;
            b1 = c;
        }
        else if (h < 300)
        {
            r1 = x;
            g1 = 0;
            b1 = c;
        }
        else
        {
            r1 = c;
            g1 = 0;
            b1 = x;
        }

        byte R = (byte)Math.Clamp(Math.Round((r1 + m) * 255), 0, 255);
        byte G = (byte)Math.Clamp(Math.Round((g1 + m) * 255), 0, 255);
        byte B = (byte)Math.Clamp(Math.Round((b1 + m) * 255), 0, 255);
        return Color.FromRgb(R, G, B);
    }
}
