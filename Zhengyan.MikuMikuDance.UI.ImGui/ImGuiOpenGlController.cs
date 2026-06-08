using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed unsafe class ImGuiOpenGlController : IDisposable
{
    private readonly GL _gl;
    private readonly IWindow _window;
    private readonly IInputContext _input;
    private readonly uint _vertexArray;
    private readonly uint _vertexBuffer;
    private readonly uint _indexBuffer;
    private readonly uint _shader;
    private readonly int _attribLocationTex;
    private readonly int _attribLocationProjMtx;
    private readonly int _attribLocationVtxPos;
    private readonly int _attribLocationVtxUv;
    private readonly int _attribLocationVtxColor;
    private readonly List<char> _pressedChars = [];
    private Vector2 _scrollDelta;
    private uint _fontTexture;
    private bool _disposed;

    public ImGuiOpenGlController(GL gl, IWindow window, IInputContext input)
    {
        _gl = gl;
        _window = window;
        _input = input;

        ImGuiApi.CreateContext();
        var io = ImGuiApi.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        ImGuiApi.StyleColorsDark();

        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyChar += OnKeyChar;
        }

        foreach (var mouse in _input.Mice)
        {
            mouse.Scroll += OnMouseScroll;
        }

        _shader = CreateShader();
        _attribLocationTex = _gl.GetUniformLocation(_shader, "Texture");
        _attribLocationProjMtx = _gl.GetUniformLocation(_shader, "ProjMtx");
        _attribLocationVtxPos = _gl.GetAttribLocation(_shader, "Position");
        _attribLocationVtxUv = _gl.GetAttribLocation(_shader, "UV");
        _attribLocationVtxColor = _gl.GetAttribLocation(_shader, "Color");

        _vertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();

        RecreateFontDeviceTexture();
    }

    public void ApplyTheme(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Normalize();
        switch (preferences.Theme)
        {
            case EditorPreferences.LightTheme:
                ImGuiApi.StyleColorsLight();
                break;
            case EditorPreferences.ClassicTheme:
                ImGuiApi.StyleColorsClassic();
                break;
            default:
                ImGuiApi.StyleColorsDark();
                break;
        }
    }

    public void NewFrame(double deltaTimeSeconds)
    {
        var io = ImGuiApi.GetIO();
        io.DisplaySize = new Vector2(_window.Size.X, _window.Size.Y);
        var framebufferSize = _window.FramebufferSize;
        if (_window.Size.X > 0 && _window.Size.Y > 0)
        {
            io.DisplayFramebufferScale = new Vector2(
                framebufferSize.X / (float)_window.Size.X,
                framebufferSize.Y / (float)_window.Size.Y);
        }

        io.DeltaTime = deltaTimeSeconds > 0 ? (float)deltaTimeSeconds : 1f / 60f;
        UpdateInput(io);
        ImGuiApi.NewFrame();
    }

    public void Render()
    {
        ImGuiApi.Render();
        RenderDrawData(ImGuiApi.GetDrawData());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyChar -= OnKeyChar;
        }

        foreach (var mouse in _input.Mice)
        {
            mouse.Scroll -= OnMouseScroll;
        }

        _gl.DeleteTexture(_fontTexture);
        _gl.DeleteBuffer(_vertexBuffer);
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
        _gl.DeleteProgram(_shader);
        ImGuiApi.DestroyContext();
        _disposed = true;
    }

    private void UpdateInput(ImGuiIOPtr io)
    {
        foreach (var c in _pressedChars)
        {
            io.AddInputCharacter(c);
        }

        _pressedChars.Clear();

        var keyboard = _input.Keyboards.Count > 0 ? _input.Keyboards[0] : null;
        if (keyboard is not null)
        {
            foreach (var mapping in KeyMappings)
            {
                io.AddKeyEvent(mapping.ImGuiKey, keyboard.IsKeyPressed(mapping.SilkKey));
            }

            io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight));
            io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
            io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight));
            io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight));
        }

        var mouse = _input.Mice.Count > 0 ? _input.Mice[0] : null;
        if (mouse is not null)
        {
            io.AddMousePosEvent(mouse.Position.X, mouse.Position.Y);
            io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
            io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            io.AddMouseButtonEvent(2, mouse.IsButtonPressed(MouseButton.Middle));
            io.AddMouseWheelEvent(_scrollDelta.X, _scrollDelta.Y);
            _scrollDelta = Vector2.Zero;
        }
    }

    private void RecreateFontDeviceTexture()
    {
        var io = ImGuiApi.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out var width, out var height, out _);

        _fontTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        _gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            InternalFormat.Rgba,
            (uint)width,
            (uint)height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels);
        io.Fonts.SetTexID((nint)_fontTexture);
        io.Fonts.ClearTexData();
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        var framebufferWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var framebufferHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
        {
            return;
        }

        drawData.ScaleClipRects(drawData.FramebufferScale);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ScissorTest);

        _gl.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);

        var left = drawData.DisplayPos.X;
        var right = drawData.DisplayPos.X + drawData.DisplaySize.X;
        var top = drawData.DisplayPos.Y;
        var bottom = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
        Matrix4x4 projection = new(
            2.0f / (right - left), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (top - bottom), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (right + left) / (left - right), (top + bottom) / (bottom - top), 0.0f, 1.0f);

        _gl.UseProgram(_shader);
        _gl.Uniform1(_attribLocationTex, 0);
        _gl.UniformMatrix4(_attribLocationProjMtx, 1, false, (float*)&projection);
        _gl.BindSampler(0, 0);
        _gl.BindVertexArray(_vertexArray);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        var vertexSize = sizeof(ImDrawVert);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxUv);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        _gl.VertexAttribPointer((uint)_attribLocationVtxPos, 2, VertexAttribPointerType.Float, false, (uint)vertexSize, (void*)0);
        _gl.VertexAttribPointer((uint)_attribLocationVtxUv, 2, VertexAttribPointerType.Float, false, (uint)vertexSize, (void*)8);
        _gl.VertexAttribPointer((uint)_attribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, (uint)vertexSize, (void*)16);

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var commandList = drawData.CmdLists[n];
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(commandList.VtxBuffer.Size * vertexSize),
                (void*)commandList.VtxBuffer.Data,
                BufferUsageARB.StreamDraw);
            _gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(commandList.IdxBuffer.Size * sizeof(ushort)),
                (void*)commandList.IdxBuffer.Data,
                BufferUsageARB.StreamDraw);

            for (var commandIndex = 0; commandIndex < commandList.CmdBuffer.Size; commandIndex++)
            {
                var command = commandList.CmdBuffer[commandIndex];
                if (command.UserCallback != IntPtr.Zero)
                {
                    continue;
                }

                var clipRect = command.ClipRect;
                _gl.Scissor(
                    (int)(clipRect.X - drawData.DisplayPos.X),
                    (int)(framebufferHeight - (clipRect.W - drawData.DisplayPos.Y)),
                    (uint)(clipRect.Z - clipRect.X),
                    (uint)(clipRect.W - clipRect.Y));
                _gl.BindTexture(TextureTarget.Texture2D, (uint)command.TextureId);
                _gl.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    command.ElemCount,
                    DrawElementsType.UnsignedShort,
                    (void*)(command.IdxOffset * sizeof(ushort)),
                    (int)command.VtxOffset);
            }
        }

        _gl.DisableVertexAttribArray((uint)_attribLocationVtxPos);
        _gl.DisableVertexAttribArray((uint)_attribLocationVtxUv);
        _gl.DisableVertexAttribArray((uint)_attribLocationVtxColor);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.Blend);
    }

    private uint CreateShader()
    {
        const string vertexShaderSource = """
            #version 330 core
            uniform mat4 ProjMtx;
            layout (location = 0) in vec2 Position;
            layout (location = 1) in vec2 UV;
            layout (location = 2) in vec4 Color;
            out vec2 Frag_UV;
            out vec4 Frag_Color;
            void main()
            {
                Frag_UV = UV;
                Frag_Color = Color;
                gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            uniform sampler2D Texture;
            in vec2 Frag_UV;
            in vec4 Frag_Color;
            out vec4 Out_Color;
            void main()
            {
                Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
            }
            """;

        var vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);
        var shader = _gl.CreateProgram();
        _gl.AttachShader(shader, vertexShader);
        _gl.AttachShader(shader, fragmentShader);
        _gl.LinkProgram(shader);
        _gl.GetProgram(shader, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var info = _gl.GetProgramInfoLog(shader);
            throw new InvalidOperationException($"Failed to link ImGui shader: {info}");
        }

        _gl.DetachShader(shader, vertexShader);
        _gl.DetachShader(shader, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
        return shader;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var info = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"Failed to compile ImGui {type} shader: {info}");
        }

        return shader;
    }

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        _pressedChars.Add(character);
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        _scrollDelta += new Vector2(scrollWheel.X, scrollWheel.Y);
    }

    private static readonly KeyMapping[] KeyMappings =
    [
        new(Key.Tab, ImGuiKey.Tab),
        new(Key.Left, ImGuiKey.LeftArrow),
        new(Key.Right, ImGuiKey.RightArrow),
        new(Key.Up, ImGuiKey.UpArrow),
        new(Key.Down, ImGuiKey.DownArrow),
        new(Key.PageUp, ImGuiKey.PageUp),
        new(Key.PageDown, ImGuiKey.PageDown),
        new(Key.Home, ImGuiKey.Home),
        new(Key.End, ImGuiKey.End),
        new(Key.Insert, ImGuiKey.Insert),
        new(Key.Delete, ImGuiKey.Delete),
        new(Key.Backspace, ImGuiKey.Backspace),
        new(Key.Space, ImGuiKey.Space),
        new(Key.Enter, ImGuiKey.Enter),
        new(Key.Escape, ImGuiKey.Escape),
        new(Key.A, ImGuiKey.A),
        new(Key.C, ImGuiKey.C),
        new(Key.V, ImGuiKey.V),
        new(Key.X, ImGuiKey.X),
        new(Key.Y, ImGuiKey.Y),
        new(Key.Z, ImGuiKey.Z)
    ];

    private readonly record struct KeyMapping(Key SilkKey, ImGuiKey ImGuiKey);
}
