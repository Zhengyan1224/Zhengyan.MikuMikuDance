using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

public sealed class OpenGlRenderHost : IRenderHost
{
    private readonly MmdProject _project;
    private readonly OpenGlRenderHostOptions _hostOptions;
    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private IRenderer? _renderer;

    public OpenGlRenderHost(MmdProject project, OpenGlRenderHostOptions? hostOptions = null)
    {
        _project = project;
        _hostOptions = hostOptions ?? OpenGlRenderHostOptions.Default;
    }

    public void Run(IRenderer renderer)
    {
        _renderer = renderer;
        GlfwWindowing.RegisterPlatform();

        var options = WindowOptions.Default;
        options.Title = _hostOptions.Title;
        options.Size = new Vector2D<int>(_hostOptions.Width, _hostOptions.Height);
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
        options.PreferredDepthBufferBits = 24;
        options.PreferredStencilBufferBits = 8;
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
        _window.Run();
    }

    public void Dispose()
    {
        _input?.Dispose();
        _renderer?.Dispose();
        _window?.Dispose();
    }

    private void OnLoad()
    {
        if (_window is null || _renderer is null)
        {
            return;
        }

        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();
        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                if (key == Key.Escape)
                {
                    _window.Close();
                }
            };
        }

        var deviceInfo = new RenderDeviceInfo(
            "OpenGL",
            _gl.GetStringS(StringName.Version) ?? string.Empty,
            _gl.GetStringS(StringName.Vendor) ?? string.Empty,
            _gl.GetStringS(StringName.Renderer) ?? string.Empty);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        if (_renderer is IOpenGlRenderer openGlRenderer)
        {
            openGlRenderer.Load(_gl, deviceInfo);
        }
        else
        {
            _renderer.Load(deviceInfo);
        }
        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnRender(double deltaTime)
    {
        if (_gl is null || _window is null || _renderer is null)
        {
            return;
        }

        var color = _hostOptions.ClearColor;
        _gl.ClearColor(color.X, color.Y, color.Z, color.W);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var size = _window.FramebufferSize;
        _renderer.Render(new RenderFrameContext(_project, size.X, size.Y, deltaTime));
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        if (_gl is null || _renderer is null)
        {
            return;
        }

        _gl.Viewport(size);
        _renderer.Resize(size.X, size.Y);
    }

    private void OnClosing()
    {
        _input?.Dispose();
        _input = null;
    }
}
