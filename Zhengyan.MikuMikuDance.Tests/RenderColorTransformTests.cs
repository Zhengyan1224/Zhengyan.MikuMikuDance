using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderColorTransformTests
{
    [Fact]
    public void IdentityTransformPreservesColor()
    {
        var transform = new SceneColorTransform();
        var color = new Vector4(0.2f, 0.4f, 0.8f, 0.5f);

        var transformed = RenderColorTransform.Apply(color, transform);

        Assert.Equal(color.X, transformed.X, precision: 3);
        Assert.Equal(color.Y, transformed.Y, precision: 3);
        Assert.Equal(color.Z, transformed.Z, precision: 3);
        Assert.Equal(color.W, transformed.W, precision: 3);
    }

    [Fact]
    public void AppliesBrightnessContrastSaturationAndGamma()
    {
        var transform = new SceneColorTransform
        {
            Brightness = 0.1f,
            Contrast = 1.2f,
            Saturation = 0f,
            Gamma = 2f
        };

        var transformed = RenderColorTransform.Apply(new Vector4(0.2f, 0.4f, 0.6f, 1f), transform);

        Assert.Equal(transformed.X, transformed.Y, precision: 3);
        Assert.Equal(transformed.Y, transformed.Z, precision: 3);
        Assert.InRange(transformed.X, 0f, 1f);
        Assert.Equal(1f, transformed.W);
    }

    [Fact]
    public void NormalizesInvalidParameters()
    {
        var transform = new SceneColorTransform
        {
            Brightness = float.NaN,
            Contrast = float.PositiveInfinity,
            Saturation = -1f,
            Gamma = 0f
        };

        var parameters = RenderColorTransform.Parameters(transform);

        Assert.Equal(0f, parameters.X);
        Assert.Equal(1f, parameters.Y);
        Assert.Equal(0f, parameters.Z);
        Assert.Equal(0.01f, parameters.W);
    }
}
