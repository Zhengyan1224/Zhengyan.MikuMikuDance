using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderTextureAlphaTests
{
    [Fact]
    public void DetectsTransparentRgbaPixels()
    {
        byte[] pixels =
        [
            255, 255, 255, 255,
            255, 255, 255, 128
        ];

        Assert.True(RenderTextureAlpha.HasTransparentPixels(pixels, width: 2, height: 1));
    }

    [Fact]
    public void TreatsFullyOpaqueRgbaPixelsAsOpaque()
    {
        byte[] pixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255
        ];

        Assert.False(RenderTextureAlpha.HasTransparentPixels(pixels, width: 2, height: 1));
    }

    [Fact]
    public void IgnoresIncompleteRgbaBuffers()
    {
        Assert.False(RenderTextureAlpha.HasTransparentPixels([255, 255, 255], width: 1, height: 1));
    }
}
