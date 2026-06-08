using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class ImGuiEditorHost : IDisposable
{
    private readonly ImGuiEditorState _state;
    private readonly ImGuiEditorHostOptions _options;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly ImGuiEditorShell _shell = new();
    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private ImGuiOpenGlController? _controller;

    public ImGuiEditorHost(MmdProject project, string? projectPath = null, ImGuiEditorHostOptions? options = null, EditorPreferencesStore? preferencesStore = null)
    {
        _preferencesStore = preferencesStore ?? new EditorPreferencesStore();
        var preferences = _preferencesStore.Load();
        _state = new ImGuiEditorState(project, preferences, projectPath);
        _options = options ?? ImGuiEditorHostOptions.FromPreferences(preferences);
    }

    public void Run()
    {
        GlfwWindowing.RegisterPlatform();

        var windowOptions = WindowOptions.Default;
        windowOptions.Title = _options.Title;
        windowOptions.Size = new Vector2D<int>(_options.Width, _options.Height);
        windowOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
        windowOptions.PreferredDepthBufferBits = 24;
        windowOptions.PreferredStencilBufferBits = 8;
        windowOptions.VSync = _options.VSync;

        _window = Window.Create(windowOptions);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
        _window.Run();
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _input?.Dispose();
        _window?.Dispose();
    }

    private void OnLoad()
    {
        if (_window is null)
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

        _controller = new ImGuiOpenGlController(_gl, _window, _input);
        _controller.ApplyTheme(_state.Preferences);
        OnFramebufferResize(_window.FramebufferSize);
    }

    private void OnRender(double deltaTime)
    {
        if (_gl is null || _controller is null || _window is null)
        {
            return;
        }

        var color = _options.ClearColor;
        if (_state.PreferencesDirty)
        {
            _window.VSync = _state.Preferences.VSync;
            _controller.ApplyTheme(_state.Preferences);
            _state.PreferencesDirty = false;
        }

        color = _state.Preferences.ClearColor.ToVector4();
        _gl.ClearColor(color.X, color.Y, color.Z, color.W);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _controller.NewFrame(deltaTime);
        _shell.Draw(_state, _window.Close);
        _controller.Render();
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
    }

    private void OnClosing()
    {
        if (_window is not null)
        {
            _state.Preferences.WindowWidth = Math.Max(1, _window.Size.X);
            _state.Preferences.WindowHeight = Math.Max(1, _window.Size.Y);
        }

        _preferencesStore.Save(_state.Preferences);
        _controller?.Dispose();
        _controller = null;
        _input?.Dispose();
        _input = null;
    }
}
