using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlEffectProgramCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<RenderEffectShaderProgram, CachedProgram?> _programs = [];

    public OpenGlEffectProgramCache(GL gl)
    {
        _gl = gl;
    }

    public bool TryGetProgram(RenderEffectShaderProgram? shader, out OpenGlEffectProgram program)
    {
        program = default;
        if (shader is null)
        {
            return false;
        }

        if (_programs.TryGetValue(shader, out var cached))
        {
            program = cached?.Program ?? default;
            return cached is not null;
        }

        try
        {
            var programHandle = CreateProgram(shader.VertexShader.Source, shader.PixelShader.Source);
            program = new OpenGlEffectProgram(
                programHandle,
                shader.Uniforms.ToDictionary(
                    uniform => uniform.Name,
                    uniform => new OpenGlEffectUniform(uniform, _gl.GetUniformLocation(programHandle, uniform.Name)),
                    StringComparer.Ordinal));
            _programs[shader] = new CachedProgram(program);
            return true;
        }
        catch (InvalidOperationException)
        {
            _programs[shader] = null;
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var cached in _programs.Values)
        {
            if (cached is not null && cached.Program.Program != 0)
            {
                _gl.DeleteProgram(cached.Program.Program);
            }
        }

        _programs.Clear();
    }

    private uint CreateProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);
        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetProgramInfoLog(program);
            _gl.DetachShader(program, vertexShader);
            _gl.DetachShader(program, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);
            _gl.DeleteProgram(program);
            throw new InvalidOperationException($"Failed to link effect shader program: {log}");
        }

        _gl.DetachShader(program, vertexShader);
        _gl.DetachShader(program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
        return program;
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        var shader = _gl.CreateShader(shaderType);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"Failed to compile effect {shaderType}: {log}");
        }

        return shader;
    }

}

internal sealed record CachedProgram(OpenGlEffectProgram Program);

internal readonly record struct OpenGlEffectProgram(
    uint Program,
    IReadOnlyDictionary<string, OpenGlEffectUniform> Uniforms);

internal readonly record struct OpenGlEffectUniform(
    RenderEffectShaderUniform Metadata,
    int Location);
