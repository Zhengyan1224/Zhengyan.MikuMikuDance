using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlMeshBuffer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vertexBuffer;
    private readonly uint _indexBuffer;

    public OpenGlMeshBuffer(GL gl, RenderMesh mesh, BufferUsageARB usage)
    {
        _gl = gl;
        Mesh = mesh;
        VertexArray = gl.GenVertexArray();
        _vertexBuffer = gl.GenBuffer();
        _indexBuffer = gl.GenBuffer();
        Upload(mesh, usage);
    }

    public RenderMesh Mesh { get; private set; }

    public uint VertexArray { get; }

    public void UpdateVertices(RenderMesh mesh)
    {
        Mesh = mesh;
        var packedVertices = mesh.Vertices.Select(PackedVertex.From).ToArray();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        unsafe
        {
            fixed (PackedVertex* verticesPtr = packedVertices)
            {
                _gl.BufferSubData(
                    BufferTargetARB.ArrayBuffer,
                    0,
                    (nuint)(packedVertices.Length * sizeof(PackedVertex)),
                    verticesPtr);
            }
        }
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vertexBuffer);
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteVertexArray(VertexArray);
    }

    private unsafe void Upload(RenderMesh mesh, BufferUsageARB usage)
    {
        var packedVertices = mesh.Vertices.Select(PackedVertex.From).ToArray();
        var indices = mesh.Indices.ToArray();

        _gl.BindVertexArray(VertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        fixed (PackedVertex* verticesPtr = packedVertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(packedVertices.Length * sizeof(PackedVertex)),
                verticesPtr,
                usage);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        fixed (uint* indicesPtr = indices)
        {
            _gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                indicesPtr,
                BufferUsageARB.StaticDraw);
        }

        const uint positionLocation = 0;
        const uint normalLocation = 1;
        const uint uvLocation = 2;
        var stride = (uint)sizeof(PackedVertex);
        _gl.EnableVertexAttribArray(positionLocation);
        _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(normalLocation);
        _gl.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(uvLocation);
        _gl.VertexAttribPointer(uvLocation, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
        _gl.BindVertexArray(0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PackedVertex
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Vector2 Uv;

        private PackedVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            Position = position;
            Normal = normal;
            Uv = uv;
        }

        public static PackedVertex From(RenderVertex vertex)
        {
            return new PackedVertex(vertex.Position, vertex.Normal, vertex.Uv);
        }
    }
}
