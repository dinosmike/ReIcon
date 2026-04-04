using System;
using System.Windows;

namespace ReIcon;

/// <summary>Центрирование окна в рабочей области (в т.ч. до первого кадра — через перегрузку с явным размером).</summary>
internal static class WindowWorkAreaHelper
{
    /// <summary>После измерения: по ActualWidth/ActualHeight или по Width/Height.</summary>
    public static void CenterInWorkArea(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.UpdateLayout();

        double w = window.ActualWidth;
        double h = window.ActualHeight;
        if (double.IsNaN(w) || w <= 0)
            w = window.Width;
        if (double.IsNaN(h) || h <= 0)
            h = window.Height;
        if (double.IsNaN(w) || w <= 0 || double.IsNaN(h) || h <= 0)
            return;

        SetManualPosition(window, w, h);
    }

    /// <summary>До первого кадра: задать размер и позицию без ожидания layout (без мигания).</summary>
    public static void CenterInWorkArea(Window window, double width, double height)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;
        SetManualPosition(window, width, height);
    }

    private static void SetManualPosition(Window window, double width, double height)
    {
        var wa = SystemParameters.WorkArea;
        double left = wa.Left + (wa.Width - width) / 2;
        double top = wa.Top + (wa.Height - height) / 2;
        left = Math.Max(wa.Left, Math.Min(left, wa.Right - width));
        top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - height));
        window.Left = left;
        window.Top = top;
    }
}
