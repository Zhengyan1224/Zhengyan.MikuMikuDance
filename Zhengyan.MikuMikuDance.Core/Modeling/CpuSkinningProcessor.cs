using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public static class CpuSkinningProcessor
{
    public static SkinnedVertex SkinVertex(Vertex vertex, ModelPose pose)
    {
        return SkinVertex(vertex, pose, Vector3.Zero, Vector4.Zero);
    }

    public static SkinnedVertex SkinVertex(Vertex vertex, ModelPose pose, Vector3 vertexOffset, Vector4 uvOffset)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var transform = BlendSkinningTransform(vertex.Skinning, pose);
        var morphedPosition = vertex.Position + vertexOffset;
        var morphedUv = vertex.Uv + new Vector2(uvOffset.X, uvOffset.Y);
        return new SkinnedVertex(
            Vector3.Transform(morphedPosition, transform),
            Vector3.Normalize(Vector3.TransformNormal(vertex.Normal, transform)),
            morphedUv);
    }

    public static IReadOnlyList<SkinnedVertex> SkinVertices(MmdModel model, ModelPose pose)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pose);
        return model.Vertices.Select(vertex => SkinVertex(vertex, pose)).ToArray();
    }

    public static IReadOnlyList<SkinnedVertex> SkinVertices(MmdModel model, ModelPose pose, MorphApplication morphs)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(morphs);
        return model.Vertices
            .Select((vertex, index) => SkinVertex(
                vertex,
                pose,
                index < morphs.VertexOffsets.Count ? morphs.VertexOffsets[index] : Vector3.Zero,
                index < morphs.UvOffsets.Count ? morphs.UvOffsets[index] : Vector4.Zero))
            .ToArray();
    }

    private static Matrix4x4 BlendSkinningTransform(SkinningWeights skinning, ModelPose pose)
    {
        var transform = new Matrix4x4();
        var totalWeight = 0f;
        for (var i = 0; i < skinning.BoneIndices.Count; i++)
        {
            var weight = i < skinning.Weights.Count ? skinning.Weights[i] : DefaultWeight(skinning, i);
            if (weight <= 0)
            {
                continue;
            }

            if (!pose.TryGetBone(skinning.BoneIndices[i], out var bone))
            {
                continue;
            }

            transform += bone.SkinningTransform * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? transform * (1f / totalWeight) : Matrix4x4.Identity;
    }

    private static float DefaultWeight(SkinningWeights skinning, int weightIndex)
    {
        return skinning.Type == VertexSkinningType.Bdef1 && weightIndex == 0 ? 1f : 0f;
    }
}

public sealed record SkinnedVertex(Vector3 Position, Vector3 Normal, Vector2 Uv);
