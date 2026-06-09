using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlBackgroundImagePass : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGlTextureCache _textures;
    private readonly uint _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private int _textureUniform;
    private int _opacityUniform;

    public OpenGlBackgroundImagePass(GL gl, string? textureBaseDirectory = null)
    {
        _gl = gl;
        _textures = new OpenGlTextureCache(gl, textureBaseDirectory);
        _program = CreateProgram(VertexShaderSource, FragmentShaderSource);
        _textureUniform = _gl.GetUniformLocation(_program, "uTexture");
        _opacityUniform = _gl.GetUniformLocation(_program, "uOpacity");
    }

    public void Draw(RenderFrameContext context)
    {
        var background = context.Project.Background;
        if (!ShouldDraw(background, context.Width, context.Height))
        {
            return;
        }

        var texture = _textures.GetTextureInfo(background.ImageSource!.ToString());
        if (texture.Texture == _textures.WhiteTexture || texture.Width <= 0 || texture.Height <= 0)
        {
            return;
        }

        var quad = RenderBackgroundLayout.FitImage(
            context.Width,
            context.Height,
            texture.Width,
            texture.Height,
            background.ImageScale,
            background.ImageOffsetX,
            background.ImageOffsetY,
            background.ImageLayoutMode);
        if (quad is null)
        {
            return;
        }

        DrawQuad(texture.Texture, quad, context.Width, context.Height, background.ImageOpacity);
    }

    public void Dispose()
    {
        if (_vertexBuffer != 0)
        {
            _gl.DeleteBuffer(_vertexBuffer);
            _vertexBuffer = 0;
        }

        if (_vertexArray != 0)
        {
            _gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _textures.Dispose();
    }

    private static bool ShouldDraw(SceneBackground background, int width, int height)
    {
        return width > 0 && height > 0 && background.ImageEnabled && background.ImageSource is not null;
    }

    private void DrawQuad(uint texture, RenderBackgroundQuad quad, int viewportWidth, int viewportHeight, float opacity)
    {
        var left = ToClipX(quad.Left, viewportWidth);
        var right = ToClipX(quad.Right, viewportWidth);
        var top = ToClipY(quad.Top, viewportHeight);
        var bottom = ToClipY(quad.Bottom, viewportHeight);
        var vertices = new[]
        {
            left, bottom, 0f, 1f,
            right, bottom, 1f, 1f,
            left, top, 0f, 0f,
            right, top, 1f, 0f
        };

        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.UseProgram(_program);
        _gl.BindVertexArray(GetVertexArray());
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        unsafe
        {
            fixed (float* ptr = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), ptr);
            }
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.Uniform1(_textureUniform, 0);
        _gl.Uniform1(_opacityUniform, Math.Clamp(opacity, 0f, 1f));
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
    }

    private uint GetVertexArray()
    {
        if (_vertexArray != 0)
        {
            return _vertexArray;
        }

        _vertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(16 * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        }

        const uint stride = 4 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        return _vertexArray;
    }

    private static float ToClipX(float x, int viewportWidth)
    {
        return (x / viewportWidth * 2f) - 1f;
    }

    private static float ToClipY(float y, int viewportHeight)
    {
        return 1f - (y / viewportHeight * 2f);
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
            throw new InvalidOperationException($"Failed to link OpenGL background image program: {log}");
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
            throw new InvalidOperationException($"Failed to compile {shaderType}: {log}");
        }

        return shader;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aUv;

        out vec2 vUv;

        void main()
        {
            vUv = aUv;
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vUv;

        uniform sampler2D uTexture;
        uniform float uOpacity;

        out vec4 FragColor;

        void main()
        {
            vec4 color = texture(uTexture, vUv);
            FragColor = vec4(color.rgb, color.a * clamp(uOpacity, 0.0, 1.0));
        }
        """;
}
