using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using ReIcon.Models;

namespace ReIcon.Services;

/// <summary>Иконка приложения — готовый <c>app.ico</c> в проекте (без генерации из кода).</summary>
public static class AppIconFactory
{
    public static Icon CreateApplicationIcon() => CreateApplicationIcon(UiTheme.Dark);

    /// <summary>Иконка для заголовка окна — та же, что встроена в .exe (через ApplicationIcon).</summary>
    public static Icon CreateApplicationIcon(UiTheme theme)
    {
        _ = theme;
        var fromExe = TryIconFromCurrentExecutable();
        if (fromExe != null)
            return fromExe;

        var fromEmbedded = TryIconFromEmbeddedResource();
        if (fromEmbedded != null)
            return fromEmbedded;

        throw new InvalidOperationException("Не удалось загрузить иконку приложения.");
    }

    private static Icon? TryIconFromCurrentExecutable()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
                path = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            using var extracted = Icon.ExtractAssociatedIcon(path);
            if (extracted != null)
                return (Icon)extracted.Clone();
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static Icon? TryIconFromEmbeddedResource()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames();
            var name = Array.Find(names, static n =>
                n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(name))
                return null;
            using var s = asm.GetManifestResourceStream(name);
            if (s == null)
                return null;
            return new Icon(s);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Копирует готовый .ico (для утилиты tools/IconGen).</summary>
    public static void CopyIconFile(string sourceIcoPath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourceIcoPath) || !File.Exists(sourceIcoPath))
            throw new FileNotFoundException("ICO not found.", sourceIcoPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        File.Copy(sourceIcoPath, destinationPath, overwrite: true);
    }
}
