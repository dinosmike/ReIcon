using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReIcon.Services;

/// <summary>Загрузка растровых форматов через ImageSharp (в т.ч. WebP, PNG с альфой, JPEG, BMP, GIF, TGA и др.).</summary>
public static class ImageDecodeService
{
    public static Image<Rgba32> Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Image.Load<Rgba32>(fs);
    }

    public static Image<Rgba32> Load(Stream stream)
    {
        return Image.Load<Rgba32>(stream);
    }
}
