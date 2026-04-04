using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ReIcon.Controls;

/// <summary>
/// Курсор-пипетка для выбора цвета с превью (Eyedropper.cur, rw-designer.com, Free Art license, author nibbler).
/// </summary>
internal static class PipetteCursor
{
    private static Cursor? _instance;

    public static Cursor Instance => _instance ??= LoadOrFallback();

    private static Cursor LoadOrFallback()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/eyedropper.cur", UriKind.Absolute);
            var info = Application.GetResourceStream(uri);
            if (info?.Stream == null)
                return Cursors.Cross;

            using (info.Stream)
            {
                var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                ms.Position = 0;
                return new Cursor(ms);
            }
        }
        catch
        {
            return Cursors.Cross;
        }
    }
}
