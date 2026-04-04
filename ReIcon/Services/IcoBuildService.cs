using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReIcon.Services;

/// <summary>Сборка ICO из квадратного изображения: несколько размеров, PNG с альфой.</summary>
public static class IcoBuildService
{
    /// <summary>Размеры стороны квадрата в пикселях (от меньшего к большему в файле).</summary>
    public static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    /// <summary>
    /// Создаёт байты ICO из уже квадратного фрагмента (RGBA).
    /// </summary>
    public static byte[] BuildIcoBytes(Image<Rgba32> squareSource)
    {
        if (squareSource.Width != squareSource.Height)
            throw new ArgumentException("Источник должен быть квадратным.", nameof(squareSource));

        var frames = new List<(int Size, byte[] Png)>(IconSizes.Length);
        foreach (var s in IconSizes)
        {
            using var frame = squareSource.Clone(ctx =>
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(s, s),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3
                }));

            using var pngMs = new MemoryStream();
            frame.SaveAsPng(pngMs);
            frames.Add((s, pngMs.ToArray()));
        }

        using var icoMs = new MemoryStream();
        IcoFileWriter.Write(icoMs, frames);
        return icoMs.ToArray();
    }

    public static void SaveIcoFile(string path, Image<Rgba32> squareSource)
    {
        var bytes = BuildIcoBytes(squareSource);
        File.WriteAllBytes(path, bytes);
    }
}
