using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

public sealed class BasicMeshRenderer : IRenderer, IOpenGlRenderer
{
    private readonly IReadOnlyList<RenderMesh> _meshes;
    private readonly string? _textureBaseDirectory;
    private readonly List<OpenGlMeshBuffer> _glMeshes = [];
    private OpenGlMeshProgram? _program;

    public BasicMeshRenderer(IReadOnlyList<RenderMesh> meshes, string? textureBaseDirectory = null)
    {
        _meshes = meshes;
        _textureBaseDirectory = textureBaseDirectory;
    }

    public void Load(RenderDeviceInfo deviceInfo)
    {
        throw new InvalidOperationException("BasicMeshRenderer requires an OpenGL render host.");
    }

    public void Load(GL gl, RenderDeviceInfo deviceInfo)
    {
        _program = new OpenGlMeshProgram(gl, _textureBaseDirectory);
        foreach (var mesh in _meshes)
        {
            if (mesh.Vertices.Count > 0 && mesh.Indices.Count > 0)
            {
                _glMeshes.Add(new OpenGlMeshBuffer(gl, mesh, BufferUsageARB.StaticDraw));
            }
        }
    }

    public void Resize(int width, int height)
    {
    }

    public void Render(RenderFrameContext context)
    {
        if (_program is null)
        {
            return;
        }

        _program.Use(context);
        _program.DrawScene(_glMeshes);
        _program.End();
    }

    public void Dispose()
    {
        foreach (var mesh in _glMeshes)
        {
            mesh.Dispose();
        }

        _glMeshes.Clear();
        _program?.Dispose();
        _program = null;
    }
}
