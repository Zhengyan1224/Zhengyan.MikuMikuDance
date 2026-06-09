using System.Buffers.Binary;
using System.IO.Compression;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderPngWriter
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static void WriteFrame(string path, RenderCaptureFrame frame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(frame);
        WriteRgba(path, frame.Width, frame.Height, frame.RgbaPixels);
    }

    public static void WriteRgba(string path, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, EncodeRgba(width, height, rgbaPixels));
    }

    public static byte[] EncodeRgba(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        if (!RenderCapture.IsValidRgbaFrame(width, height, rgbaPixels.Length))
        {
            throw new ArgumentException("RGBA pixel buffer size does not match the requested PNG dimensions.", nameof(rgbaPixels));
        }

        using var output = new MemoryStream();
        output.Write(PngSignature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[..4], checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), checked((uint)height));
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(output, "IHDR", ihdr);

        var scanlineStride = checked(width * RenderCapture.RgbaBytesPerPixel);
        var filteredStride = checked(scanlineStride + 1);
        var filteredPixels = new byte[checked(filteredStride * height)];
        for (var y = 0; y < height; y++)
        {
            var sourceOffset = checked(y * scanlineStride);
            var targetOffset = checked(y * filteredStride + 1);
            rgbaPixels.Slice(sourceOffset, scanlineStride).CopyTo(filteredPixels.AsSpan(targetOffset, scanlineStride));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(filteredPixels);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        if (type.Length != 4)
        {
            throw new ArgumentException("PNG chunk type must contain exactly four ASCII characters.", nameof(type));
        }

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        for (var i = 0; i < typeBytes.Length; i++)
        {
            typeBytes[i] = checked((byte)type[i]);
        }

        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, CalculateCrc32(typeBytes, data));
        stream.Write(crc);
    }

    private static uint CalculateCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        foreach (var value in type)
        {
            crc = UpdateCrc32(crc, value);
        }

        foreach (var value in data)
        {
            crc = UpdateCrc32(crc, value);
        }

        return ~crc;
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0
                ? 0xedb88320u ^ (crc >> 1)
                : crc >> 1;
        }

        return crc;
    }
}
