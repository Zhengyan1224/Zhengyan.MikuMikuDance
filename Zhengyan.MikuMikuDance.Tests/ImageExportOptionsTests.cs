using System.Numerics;
using Zhengyan.MikuMikuDance.App;
using Zhengyan.MikuMikuDance.Rendering.OpenGL;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class ImageExportOptionsTests
{
    [Fact]
    public void ParsesLegacyDimensions()
    {
        using var errors = new StringWriter();

        var parsed = ImageExportOptions.TryParse(
            ["--export-image", "model.pmx", "frame.png", "640", "360"],
            errors,
            out var options);

        Assert.True(parsed);
        Assert.Equal("model.pmx", options!.InputPath);
        Assert.Equal("frame.png", options.OutputPath);
        Assert.Equal(640, options.Width);
        Assert.Equal(360, options.Height);
        Assert.Equal(OpenGlRenderHostOptions.Default.ClearColor, options.ClearColor);
        Assert.False(options.TransparentFramebuffer);
        Assert.Equal(string.Empty, errors.ToString());
    }

    [Fact]
    public void ParsesTransparentClearColor()
    {
        using var errors = new StringWriter();

        var parsed = ImageExportOptions.TryParse(
            ["--export-image", "scene.zmm", "frame.png", "--transparent"],
            errors,
            out var options);

        Assert.True(parsed);
        Assert.Equal(OpenGlRenderHostOptions.Default.Width, options!.Width);
        Assert.Equal(OpenGlRenderHostOptions.Default.Height, options.Height);
        Assert.Equal(Vector4.Zero, options.ClearColor);
        Assert.True(options.TransparentFramebuffer);
    }

    [Fact]
    public void ParsesHexClearColorWithAlpha()
    {
        using var errors = new StringWriter();

        var parsed = ImageExportOptions.TryParse(
            ["--export-image", "scene.zmm", "frame.png", "800", "600", "--clear-color", "#33669980"],
            errors,
            out var options);

        Assert.True(parsed);
        Assert.Equal(800, options!.Width);
        Assert.Equal(600, options.Height);
        Assert.Equal(0x33 / 255f, options.ClearColor.X, precision: 6);
        Assert.Equal(0x66 / 255f, options.ClearColor.Y, precision: 6);
        Assert.Equal(0x99 / 255f, options.ClearColor.Z, precision: 6);
        Assert.Equal(0x80 / 255f, options.ClearColor.W, precision: 6);
        Assert.True(options.TransparentFramebuffer);
    }

    [Fact]
    public void TransparentOptionKeepsColorButClearsAlpha()
    {
        using var errors = new StringWriter();

        var parsed = ImageExportOptions.TryParse(
            ["--export-image", "scene.zmm", "frame.png", "--clear-color=#112233", "--transparent"],
            errors,
            out var options);

        Assert.True(parsed);
        Assert.Equal(0x11 / 255f, options!.ClearColor.X, precision: 6);
        Assert.Equal(0x22 / 255f, options.ClearColor.Y, precision: 6);
        Assert.Equal(0x33 / 255f, options.ClearColor.Z, precision: 6);
        Assert.Equal(0f, options.ClearColor.W);
        Assert.True(options.TransparentFramebuffer);
    }

    [Fact]
    public void RejectsInvalidOption()
    {
        using var errors = new StringWriter();

        var parsed = ImageExportOptions.TryParse(
            ["--export-image", "scene.zmm", "frame.png", "--frame", "12"],
            errors,
            out var options);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Contains("Unknown image export option", errors.ToString());
    }
}
