using Zhengyan.MikuMikuDance.Rendering.OpenGL;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EffectRenderTargetCacheTests
{
    [Fact]
    public void ResolvesCommonMmeRenderTargetAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["explicitScene"] = "s_sceneGraphRenderTarget"
        };

        Assert.True(OpenGlEffectRenderTargetNameResolver.TryResolve(
            "s_sceneGraphMap",
            ["s_sceneGraphRenderTarget"],
            aliases,
            out var sceneMapTarget));
        Assert.Equal("s_sceneGraphRenderTarget", sceneMapTarget);

        Assert.True(OpenGlEffectRenderTargetNameResolver.TryResolve(
            "s_sceneGraphTarget",
            ["s_sceneGraphRenderTarget"],
            aliases,
            out var sceneTarget));
        Assert.Equal("s_sceneGraphRenderTarget", sceneTarget);

        Assert.True(OpenGlEffectRenderTargetNameResolver.TryResolve(
            "explicitScene",
            ["s_sceneGraphRenderTarget"],
            aliases,
            out var explicitTarget));
        Assert.Equal("s_sceneGraphRenderTarget", explicitTarget);

        Assert.True(OpenGlEffectRenderTargetNameResolver.TryResolve(
            "Depth",
            ["DepthBuffer"],
            EmptyAliases,
            out var depthTarget));
        Assert.Equal("DepthBuffer", depthTarget);
        Assert.False(OpenGlEffectRenderTargetNameResolver.TryResolve("Unknown", ["DepthBuffer"], EmptyAliases, out _));
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
