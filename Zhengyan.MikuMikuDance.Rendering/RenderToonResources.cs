using System.Numerics;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderToonResource(
    int Index,
    string Name,
    string Uri,
    int Width,
    int Height,
    byte[] RgbaPixels)
{
    public int ByteLength => RgbaPixels.Length;
}

public static class RenderToonResources
{
    public const int SharedToonCount = 10;
    public const int Width = 256;
    public const int Height = 1;
    public const string UriPrefix = "@builtin/toon";

    private static readonly Vector3[] BaseColors =
    [
        new(1.00f, 0.86f, 0.78f),
        new(0.95f, 0.74f, 0.66f),
        new(0.82f, 0.92f, 1.00f),
        new(0.80f, 0.84f, 1.00f),
        new(0.92f, 0.82f, 1.00f),
        new(0.84f, 0.96f, 0.82f),
        new(1.00f, 0.92f, 0.72f),
        new(0.88f, 0.88f, 0.88f),
        new(0.72f, 0.78f, 0.86f),
        new(0.64f, 0.64f, 0.64f)
    ];

    public static string? ResolveSharedToonUri(int index)
    {
        return IsSharedToonIndex(index) ? $"{UriPrefix}{index + 1:D2}" : null;
    }

    public static bool IsBuiltInToonUri(string? uri)
    {
        return TryGetSharedToonIndex(uri, out _);
    }

    public static bool TryGetSharedToonIndex(string? uri, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        var normalized = uri.Trim();
        if (normalized.StartsWith(UriPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized[UriPrefix.Length..], out var oneBasedIndex))
        {
            index = oneBasedIndex - 1;
            return IsSharedToonIndex(index);
        }

        if (normalized.StartsWith("toon", StringComparison.OrdinalIgnoreCase) &&
            normalized.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized[4..^4], out oneBasedIndex))
        {
            index = oneBasedIndex - 1;
            return IsSharedToonIndex(index);
        }

        return false;
    }

    public static RenderToonResource GetSharedToon(int index)
    {
        if (!IsSharedToonIndex(index))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Shared toon index must be between 0 and 9.");
        }

        return new RenderToonResource(
            index,
            $"toon{index + 1:D2}.bmp",
            ResolveSharedToonUri(index)!,
            Width,
            Height,
            CreateGradient(BaseColors[index]));
    }

    private static bool IsSharedToonIndex(int index)
    {
        return index >= 0 && index < SharedToonCount;
    }

    private static byte[] CreateGradient(Vector3 baseColor)
    {
        var pixels = new byte[Width * Height * 4];
        for (var x = 0; x < Width; x++)
        {
            var t = x / (float)(Width - 1);
            var shade = 0.35f + t * 0.65f;
            var offset = x * 4;
            pixels[offset] = ToByte(baseColor.X * shade);
            pixels[offset + 1] = ToByte(baseColor.Y * shade);
            pixels[offset + 2] = ToByte(baseColor.Z * shade);
            pixels[offset + 3] = 255;
        }

        return pixels;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
