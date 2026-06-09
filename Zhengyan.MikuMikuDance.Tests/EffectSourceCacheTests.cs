using Zhengyan.MikuMikuDance.Core.Effects;
using Zhengyan.MikuMikuDance.Rendering;
using Zhengyan.MikuMikuDance.Rendering.OpenGL;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EffectSourceCacheTests
{
    [Fact]
    public void LoadsCompilesAndCachesRelativeEffects()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var effectDirectory = Path.Combine(directory, "effects");
            Directory.CreateDirectory(effectDirectory);
            var effectPath = Path.Combine(effectDirectory, "main.fx");
            File.WriteAllText(effectPath, """
                float4x4 WorldViewProjection : WORLDVIEWPROJECTION;

                float4 VS(float4 position : POSITION) : POSITION
                {
                    return mul(position, WorldViewProjection);
                }

                float4 PS() : COLOR0
                {
                    return float4(1, 1, 1, 1);
                }

                technique Main {
                  pass P0 {
                    VertexShader = compile vs_3_0 VS();
                    PixelShader = compile ps_3_0 PS();
                  }
                }
                """);

            var cache = new OpenGlEffectSourceCache(directory);

            Assert.True(cache.TryGetEffect("effects/main.fx", out var effect));
            Assert.Equal(Path.GetFullPath(effectPath), effect.SourceName);
            Assert.NotNull(effect.DefaultTechnique!.Passes.Single().Shader);

            Assert.True(cache.TryGetEffect(@"effects\main.fx", out var cachedEffect));
            Assert.Same(effect, cachedEffect);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReloadsEffectAndRefreshesDiagnostics()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var effectPath = Path.Combine(directory, "main.fx");
            File.WriteAllText(effectPath, """
                float UnknownParameter : UNKNOWNSEMANTIC = 1;
                technique Main {
                  pass P0 {
                  }
                }
                """);
            var cache = new OpenGlEffectSourceCache(directory);

            Assert.True(cache.TryGetEffect("main.fx", out var first));
            Assert.Contains(cache.GetDiagnostics("main.fx"), diagnostic => diagnostic.Code == "MME0102");

            File.WriteAllText(effectPath, """
                float KnownParameter : DIFFUSE = 1;
                technique Main {
                  pass P0 {
                  }
                }
                """);
            Assert.True(cache.TryGetEffect("main.fx", out var cached));
            Assert.Same(first, cached);
            Assert.Contains(cache.GetDiagnostics("main.fx"), diagnostic => diagnostic.Code == "MME0102");

            Assert.True(cache.ReloadEffect("main.fx", out var reloaded));
            Assert.NotSame(first, reloaded);
            Assert.DoesNotContain(cache.GetDiagnostics("main.fx"), diagnostic => diagnostic.Code == "MME0102");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolvesExternalEffectsRelativeToOwnerEffectDirectory()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var effectDirectory = Path.Combine(directory, "effects");
            Directory.CreateDirectory(effectDirectory);
            var effectPath = Path.Combine(effectDirectory, "main.fx");
            File.WriteAllText(effectPath, """
                technique Main {
                  pass P0 {
                  }
                }
                """);

            var owner = new RenderEffect(
                Path.Combine(directory, "owner.fx"),
                [],
                [],
                new RenderEffectScriptMetadata(
                    RenderEffectScriptClass.SceneObject,
                    RenderEffectScriptOrder.Standard,
                    "color",
                    []));
            var cache = new OpenGlEffectSourceCache(Path.Combine(directory, "fallback"));

            Assert.Equal(
                Path.GetFullPath(effectPath),
                cache.ResolvePathForTesting("effects/main.fx", owner));
            Assert.True(cache.TryGetEffect("effects/main.fx", owner, out var effect));
            Assert.Equal(Path.GetFullPath(effectPath), effect.SourceName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingOrHiddenEffectsReturnFalse()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var cache = new OpenGlEffectSourceCache(directory);

            Assert.False(cache.TryGetEffect("missing.fx", out _));
            Assert.False(cache.TryGetEffect("hide", out _));
            Assert.False(cache.TryGetEffect("", out _));
            Assert.Contains(cache.GetDiagnostics("missing.fx"), diagnostic =>
                diagnostic.Severity == RenderEffectDiagnosticSeverity.Error &&
                diagnostic.Code == "MME1001");
            Assert.Contains(cache.GetDiagnostics("hide"), diagnostic =>
                diagnostic.Severity == RenderEffectDiagnosticSeverity.Error &&
                diagnostic.Code == "MME1000");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReportsMalformedEffectStructureDiagnostics()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "invalid.fx"), "technique Main { pass P0 {");
            var cache = new OpenGlEffectSourceCache(directory);

            Assert.True(cache.TryGetEffect("invalid.fx", out var effect));

            Assert.Empty(effect.Techniques);
            Assert.Contains(cache.GetDiagnostics("invalid.fx"), diagnostic =>
                diagnostic.Severity == RenderEffectDiagnosticSeverity.Error &&
                diagnostic.Code == "MME0001");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnalyzesCompiledEffectStructureDiagnostics()
    {
        var effect = new RenderEffect(
            "diagnostic.fx",
            [
                new RenderEffectParameter(
                    "Broken",
                    "unsupportedType",
                    RenderEffectParameterKind.Unknown,
                    RenderEffectSemantic.Unknown,
                    "UNKNOWNSEMANTIC",
                    null,
                    new Dictionary<string, EffectValue>(StringComparer.Ordinal))
            ],
            [],
            new RenderEffectScriptMetadata(
                RenderEffectScriptClass.SceneObject,
                RenderEffectScriptOrder.Standard,
                "color",
                []));

        var diagnostics = RenderEffectDiagnostics.Analyze(effect);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == RenderEffectDiagnosticSeverity.Error &&
            diagnostic.Code == "MME0001");
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == RenderEffectDiagnosticSeverity.Warning &&
            diagnostic.Code == "MME0101");
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == RenderEffectDiagnosticSeverity.Warning &&
            diagnostic.Code == "MME0102");
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Zhengyan.MikuMikuDance.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
