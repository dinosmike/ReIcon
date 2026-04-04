using System.Buffers.Binary;
using System.IO;

namespace ReIcon.Services;

/// <summary>
/// Запись ICO в формате Windows Vista+: каждый кадр — отдельный PNG с альфой внутри контейнера ICO.
/// Итоговый файл — один .ico с несколькими вложенными PNG (16…256 px), подходит для exe и оболочки Windows.
/// </summary>
public static class IcoFileWriter
{
    /// <param name="frames">Размер стороны квадрата и байты PNG (уже закодированного изображения).</param>
    public static void Write(Stream output, IReadOnlyList<(int Size, byte[] PngBytes)> frames)
    {
        if (frames.Count == 0)
            throw new ArgumentException("Нужен хотя бы один кадр.", nameof(frames));

        var ordered = frames.OrderBy(f => f.Size).ToArray();
        ushort count = (ushort)ordered.Length;
        const int iconDirSize = 6;
        int entryTableSize = 16 * count;
        int headerSize = iconDirSize + entryTableSize;

        using var ms = new MemoryStream();
        WriteUInt16LE(ms, 0);
        WriteUInt16LE(ms, 1);
        WriteUInt16LE(ms, count);

        var offset = headerSize;
        foreach (var f in ordered)
        {
            var size = f.Size;
            var png = f.PngBytes;
            byte bW = size >= 256 ? (byte)0 : (byte)size;
            byte bH = bW;
            ms.WriteByte(bW);
            ms.WriteByte(bH);
            ms.WriteByte(0);
            ms.WriteByte(0);
            WriteUInt16LE(ms, 1);
            WriteUInt16LE(ms, 32);
            WriteUInt32LE(ms, (uint)png.Length);
            WriteUInt32LE(ms, (uint)offset);
            offset += png.Length;
        }

        foreach (var f in ordered)
            ms.Write(f.PngBytes);

        ms.Position = 0;
        ms.CopyTo(output);
    }

    private static void WriteUInt16LE(Stream s, ushort v)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, v);
        s.Write(b);
    }

    private static void WriteUInt32LE(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        s.Write(b);
    }
}
