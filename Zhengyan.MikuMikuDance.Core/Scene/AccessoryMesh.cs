using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed record AccessoryMeshDocument(
    string SourceName,
    IReadOnlyList<AccessoryMesh> Meshes)
{
    public Uri? Source { get; init; }

    public int VertexCount => Meshes.Sum(mesh => mesh.Vertices.Count);

    public int FaceCount => Meshes.Sum(mesh => mesh.Faces.Count);

    public int MaterialCount => Meshes.Sum(mesh => mesh.Materials.Count);
}

public sealed record AccessoryMesh(
    string Name,
    IReadOnlyList<Vector3> Vertices,
    IReadOnlyList<AccessoryFace> Faces,
    IReadOnlyList<Vector3> Normals,
    IReadOnlyList<Vector2> TextureCoordinates,
    IReadOnlyList<AccessoryMaterial> Materials,
    IReadOnlyList<int> FaceMaterialIndices);

public sealed record AccessoryFace(IReadOnlyList<int> Indices);

public sealed record AccessoryMaterial(
    string Name,
    Vector4 Diffuse,
    Vector3 Emissive,
    Vector3 Specular,
    float Shininess,
    string? TextureFilename,
    string? NormalMapFilename);
