namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderTextureAlpha
{
    public static bool HasTransparentPixels(ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var pixelByteLength = (long)width * height * 4;
        if (pixelByteLength > rgbaPixels.Length)
        {
            return false;
        }

        for (var i = 3; i < (int)pixelByteLength; i += 4)
        {
            if (rgbaPixels[i] < byte.MaxValue)
            {
                return true;
            }
        }

        return false;
    }
}
