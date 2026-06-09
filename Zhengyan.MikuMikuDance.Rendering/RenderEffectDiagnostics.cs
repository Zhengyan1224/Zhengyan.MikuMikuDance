namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderEffectDiagnostic(
    RenderEffectDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? SourceName = null,
    string? Subject = null);

public enum RenderEffectDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public static class RenderEffectDiagnostics
{
    public static IReadOnlyList<RenderEffectDiagnostic> Analyze(RenderEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        var diagnostics = new List<RenderEffectDiagnostic>();
        if (effect.Techniques.Count == 0)
        {
            diagnostics.Add(Error(
                "MME0001",
                "Effect has no techniques.",
                effect.SourceName));
        }

        foreach (var technique in effect.Techniques)
        {
            if (technique.Passes.Count == 0)
            {
                diagnostics.Add(Warning(
                    "MME0002",
                    $"Technique '{technique.Name}' has no passes.",
                    effect.SourceName,
                    technique.Name));
            }

            foreach (var pass in technique.Passes)
            {
                if (!string.IsNullOrWhiteSpace(pass.VertexShaderState) &&
                    !string.IsNullOrWhiteSpace(pass.PixelShaderState) &&
                    pass.Shader is null)
                {
                    diagnostics.Add(Warning(
                        "MME0003",
                        $"Pass '{technique.Name}/{pass.Name}' could not be translated to GLSL.",
                        effect.SourceName,
                        pass.Name));
                }
            }
        }

        foreach (var parameter in effect.Parameters)
        {
            if (parameter.Kind == RenderEffectParameterKind.Unknown)
            {
                diagnostics.Add(Warning(
                    "MME0101",
                    $"Parameter '{parameter.Name}' uses unsupported type '{parameter.Type}'.",
                    effect.SourceName,
                    parameter.Name));
            }

            if (!string.IsNullOrWhiteSpace(parameter.RawSemantic) &&
                parameter.Semantic == RenderEffectSemantic.Unknown)
            {
                diagnostics.Add(Warning(
                    "MME0102",
                    $"Parameter '{parameter.Name}' uses unknown semantic '{parameter.RawSemantic}'.",
                    effect.SourceName,
                    parameter.Name));
            }

            if (parameter.Semantic == RenderEffectSemantic.OffscreenRenderTarget &&
                parameter.OffscreenTarget is null)
            {
                diagnostics.Add(Error(
                    "MME0201",
                    $"Offscreen render target '{parameter.Name}' has no metadata.",
                    effect.SourceName,
                    parameter.Name));
            }
        }

        return diagnostics;
    }

    public static RenderEffectDiagnostic Info(string code, string message, string? sourceName = null, string? subject = null)
    {
        return new RenderEffectDiagnostic(RenderEffectDiagnosticSeverity.Info, code, message, sourceName, subject);
    }

    public static RenderEffectDiagnostic Warning(string code, string message, string? sourceName = null, string? subject = null)
    {
        return new RenderEffectDiagnostic(RenderEffectDiagnosticSeverity.Warning, code, message, sourceName, subject);
    }

    public static RenderEffectDiagnostic Error(string code, string message, string? sourceName = null, string? subject = null)
    {
        return new RenderEffectDiagnostic(RenderEffectDiagnosticSeverity.Error, code, message, sourceName, subject);
    }
}
