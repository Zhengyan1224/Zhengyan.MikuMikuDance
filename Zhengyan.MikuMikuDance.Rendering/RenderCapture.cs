namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderCapture
{
    public const int RgbaBytesPerPixel = 4;

    public static byte[] FlipRgbaRows(ReadOnlySpan<byte> bottomLeftRows, int width, int height)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        var stride = checked(width * RgbaBytesPerPixel);
        var expected = checked(stride * height);
        if (bottomLeftRows.Length < expected)
        {
            throw new ArgumentException("Pixel buffer is smaller than the requested image size.", nameof(bottomLeftRows));
        }

        var output = new byte[expected];
        for (var y = 0; y < height; y++)
        {
            var sourceOffset = checked((height - 1 - y) * stride);
            var targetOffset = checked(y * stride);
            bottomLeftRows.Slice(sourceOffset, stride).CopyTo(output.AsSpan(targetOffset, stride));
        }

        return output;
    }

    public static bool IsValidRgbaFrame(int width, int height, int byteLength)
    {
        if (width <= 0 || height <= 0 || byteLength < 0)
        {
            return false;
        }

        return byteLength == checked(width * height * RgbaBytesPerPixel);
    }
}
