using System.Numerics;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderVertex(Vector3 Position, Vector3 Normal, Vector2 Uv);

public sealed record RenderMaterial(
    string Name,
    Vector4 Diffuse,
    string? TexturePath = null,
    string? SphereTexturePath = null,
    SphereTextureBlendMode SphereTextureMode = SphereTextureBlendMode.Disabled,
    string? ToonTexturePath = null,
    bool EdgeEnabled = false,
    Vector4 EdgeColor = default,
    float EdgeSize = 0,
    bool DoubleSided = false,
    bool GroundShadowEnabled = false)
{
    public bool IsTransparent => Diffuse.W < 0.999f;
}

public enum SphereTextureBlendMode
{
    Disabled,
    Multiply,
    Add,
    SubTexture
}

public sealed record RenderMeshBatch(int StartIndex, int IndexCount, RenderMaterial Material);

public sealed record RenderMesh(
    string Name,
    IReadOnlyList<RenderVertex> Vertices,
    IReadOnlyList<uint> Indices,
    IReadOnlyList<RenderMeshBatch> Batches)
{
    public Matrix4x4 WorldTransform { get; init; } = Matrix4x4.Identity;

    public RenderEffect? Effect { get; init; }
}
