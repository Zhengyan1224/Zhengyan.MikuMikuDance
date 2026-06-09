using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderBackgroundLayoutTests
{
    [Fact]
    public void FitsImageInsideViewportPreservingAspectRatio()
    {
        var quad = RenderBackgroundLayout.FitImage(
            viewportWidth: 800,
            viewportHeight: 600,
            imageWidth: 1920,
            imageHeight: 1080,
            scale: 1f,
            offsetX: 0,
            offsetY: 0);

        Assert.NotNull(quad);
        Assert.Equal(800f, quad.Width, precision: 3);
        Assert.Equal(450f, quad.Height, precision: 3);
        Assert.Equal(new Vector2(400f, 300f), quad.Center);
    }

    [Fact]
    public void AppliesScaleAndPixelOffset()
    {
        var quad = RenderBackgroundLayout.FitImage(
            viewportWidth: 800,
            viewportHeight: 600,
            imageWidth: 400,
            imageHeight: 200,
            scale: 0.5f,
            offsetX: 12,
            offsetY: -8);

        Assert.NotNull(quad);
        Assert.Equal(400f, quad.Width, precision: 3);
        Assert.Equal(200f, quad.Height, precision: 3);
        Assert.Equal(new Vector2(412f, 292f), quad.Center);
    }

    [Fact]
    public void RejectsInvalidSizes()
    {
        Assert.Null(RenderBackgroundLayout.FitImage(0, 600, 400, 200, 1f, 0, 0));
        Assert.Null(RenderBackgroundLayout.FitImage(800, 600, 0, 200, 1f, 0, 0));
    }

    [Fact]
    public void FillImageCoversViewportPreservingAspectRatio()
    {
        var quad = RenderBackgroundLayout.FitImage(
            viewportWidth: 800,
            viewportHeight: 600,
            imageWidth: 1920,
            imageHeight: 1080,
            scale: 1f,
            offsetX: 0,
            offsetY: 0,
            layoutMode: BackgroundImageLayoutMode.Fill);

        Assert.NotNull(quad);
        Assert.Equal(1066.667f, quad.Width, precision: 3);
        Assert.Equal(600f, quad.Height, precision: 3);
        Assert.Equal(new Vector2(400f, 300f), quad.Center);
    }
}
