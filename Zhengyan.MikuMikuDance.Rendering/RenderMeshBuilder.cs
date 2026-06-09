using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderMeshBuilder
{
    private static readonly RenderMaterial DefaultMaterial = new("Default", new Vector4(0.7f, 0.7f, 0.72f, 1f));

    public static RenderMesh FromModel(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var vertices = model.Vertices
            .Select(vertex => new RenderVertex(vertex.Position, vertex.Normal, vertex.Uv))
            .ToArray();
        var indices = model.Indices.Select(index => checked((uint)index)).ToArray();
        var batches = BuildModelBatches(model, indices.Length);
        return new RenderMesh(string.IsNullOrWhiteSpace(model.Name) ? model.Format.ToString() : model.Name, vertices, indices, batches);
    }

    public static RenderMesh FromModel(ModelInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.MorphWeights.Count == 0)
        {
            return FromModel(instance.Model) with
            {
                Name = string.IsNullOrWhiteSpace(instance.Name) ? instance.Model.Name : instance.Name,
                WorldTransform = instance.Transform.CreateMatrix(),
                EffectParameterOverrides = CopyEffectOverrides(instance.EffectParameterOverrides)
            };
        }

        var morphs = MorphEvaluator.Evaluate(instance.Model, instance.MorphWeights);
        return FromModel(instance.Model, ModelPose.BindPose(instance.Model), morphs) with
        {
            Name = string.IsNullOrWhiteSpace(instance.Name) ? instance.Model.Name : instance.Name,
            WorldTransform = instance.Transform.CreateMatrix(),
            EffectParameterOverrides = CopyEffectOverrides(instance.EffectParameterOverrides)
        };
    }

    public static RenderMesh FromModel(MmdModel model, ModelPose pose)
    {
        return FromModel(model, pose, MorphApplication.Empty(model));
    }

    public static RenderMesh FromModel(MmdModel model, ModelPose pose, MorphApplication morphs)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(morphs);
        var skinnedVertices = CpuSkinningProcessor.SkinVertices(model, pose, morphs);
        var vertices = skinnedVertices
            .Select(vertex => new RenderVertex(vertex.Position, vertex.Normal, vertex.Uv))
            .ToArray();
        var indices = model.Indices.Select(index => checked((uint)index)).ToArray();
        var batches = BuildModelBatches(model, indices.Length);
        return new RenderMesh(string.IsNullOrWhiteSpace(model.Name) ? model.Format.ToString() : model.Name, vertices, indices, batches);
    }

    public static RenderMesh FromModel(ModelInstance instance, ModelPose pose)
    {
        return FromModel(instance, pose, MorphApplication.Empty(instance.Model));
    }

    public static RenderMesh FromModel(ModelInstance instance, ModelPose pose, MorphApplication morphs)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(morphs);
        return FromModel(instance.Model, pose, morphs) with
        {
            Name = string.IsNullOrWhiteSpace(instance.Name) ? instance.Model.Name : instance.Name,
            WorldTransform = instance.Transform.CreateMatrix(),
            EffectParameterOverrides = CopyEffectOverrides(instance.EffectParameterOverrides)
        };
    }

    public static IReadOnlyList<RenderMesh> FromAccessory(AccessoryMeshDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Meshes.Select(mesh => FromAccessoryMesh(document.SourceName, mesh)).ToArray();
    }

    public static IReadOnlyList<RenderMesh> FromAccessory(AccessoryMeshDocument document, Accessory accessory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(accessory);
        return document.Meshes.Select(mesh => FromAccessoryMesh(document.SourceName, mesh) with
        {
            WorldTransform = accessory.CreateWorldMatrix(),
            EffectParameterOverrides = CopyEffectOverrides(accessory.EffectParameterOverrides)
        }).ToArray();
    }

    private static RenderMesh FromAccessoryMesh(string sourceName, AccessoryMesh mesh)
    {
        var vertices = new List<RenderVertex>();
        var indices = new List<uint>();
        var faceMaterialIndices = new List<int>();

        for (var faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
        {
            var face = mesh.Faces[faceIndex];
            if (face.Indices.Count < 3)
            {
                continue;
            }

            var materialIndex = faceIndex < mesh.FaceMaterialIndices.Count ? mesh.FaceMaterialIndices[faceIndex] : 0;
            for (var i = 1; i < face.Indices.Count - 1; i++)
            {
                AddAccessoryVertex(mesh, face.Indices[0], vertices, indices);
                AddAccessoryVertex(mesh, face.Indices[i], vertices, indices);
                AddAccessoryVertex(mesh, face.Indices[i + 1], vertices, indices);
                faceMaterialIndices.Add(materialIndex);
            }
        }

        var batches = BuildAccessoryBatches(mesh, faceMaterialIndices);
        var name = string.IsNullOrWhiteSpace(mesh.Name) ? sourceName : mesh.Name;
        return new RenderMesh(name, vertices, indices, batches);
    }

    private static void AddAccessoryVertex(AccessoryMesh mesh, int vertexIndex, List<RenderVertex> vertices, List<uint> indices)
    {
        var position = mesh.Vertices[vertexIndex];
        var normal = vertexIndex < mesh.Normals.Count ? mesh.Normals[vertexIndex] : Vector3.UnitY;
        var uv = vertexIndex < mesh.TextureCoordinates.Count ? mesh.TextureCoordinates[vertexIndex] : Vector2.Zero;
        indices.Add(checked((uint)vertices.Count));
        vertices.Add(new RenderVertex(position, normal, uv));
    }

    private static IReadOnlyList<RenderMeshBatch> BuildModelBatches(MmdModel model, int totalIndexCount)
    {
        if (model.Materials.Count == 0)
        {
            return [new RenderMeshBatch(0, totalIndexCount, DefaultMaterial)];
        }

        var batches = new List<RenderMeshBatch>(model.Materials.Count);
        var startIndex = 0;
        foreach (var material in model.Materials)
        {
            var indexCount = Math.Min(material.VertexCount, Math.Max(0, totalIndexCount - startIndex));
            if (indexCount <= 0)
            {
                continue;
            }

            batches.Add(new RenderMeshBatch(
                startIndex,
                indexCount,
                new RenderMaterial(
                    material.Name,
                    material.Diffuse,
                    ResolveTexturePath(model, material.TextureIndex),
                    ResolveTexturePath(model, material.SphereTextureIndex),
                    ToRenderSphereMode(material.SphereTextureMode),
                    ResolveToonTexturePath(model, material),
                    material.EdgeEnabled,
                    material.EdgeColor,
                    material.EdgeSize,
                    material.IsDoubleSided,
                    material.GroundShadowEnabled)));
            startIndex += indexCount;
        }

        if (batches.Count == 0 && totalIndexCount > 0)
        {
            batches.Add(new RenderMeshBatch(0, totalIndexCount, DefaultMaterial));
        }

        return batches;
    }

    private static IReadOnlyList<RenderMeshBatch> BuildAccessoryBatches(AccessoryMesh mesh, IReadOnlyList<int> faceMaterialIndices)
    {
        if (faceMaterialIndices.Count == 0)
        {
            return [new RenderMeshBatch(0, mesh.Faces.Count * 3, DefaultMaterial)];
        }

        var batches = new List<RenderMeshBatch>();
        var currentMaterialIndex = faceMaterialIndices[0];
        var startTriangle = 0;
        for (var i = 1; i <= faceMaterialIndices.Count; i++)
        {
            if (i < faceMaterialIndices.Count && faceMaterialIndices[i] == currentMaterialIndex)
            {
                continue;
            }

            batches.Add(new RenderMeshBatch(
                startTriangle * 3,
                (i - startTriangle) * 3,
                ToRenderMaterial(mesh, currentMaterialIndex)));
            if (i < faceMaterialIndices.Count)
            {
                currentMaterialIndex = faceMaterialIndices[i];
                startTriangle = i;
            }
        }

        return batches;
    }

    private static RenderMaterial ToRenderMaterial(AccessoryMesh mesh, int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= mesh.Materials.Count)
        {
            return DefaultMaterial;
        }

        var material = mesh.Materials[materialIndex];
        return new RenderMaterial(material.Name, material.Diffuse, material.TextureFilename);
    }

    private static string? ResolveTexturePath(MmdModel model, int textureIndex)
    {
        return textureIndex >= 0 && textureIndex < model.Textures.Count
            ? model.Textures[textureIndex]
            : null;
    }

    private static string? ResolveToonTexturePath(MmdModel model, Material material)
    {
        if (!material.ToonTextureShared)
        {
            return ResolveTexturePath(model, material.ToonTextureIndex);
        }

        if (material.ToonTextureIndex < 0)
        {
            return null;
        }

        if (material.ToonTextureIndex < model.SharedToonTextures.Count &&
            !string.IsNullOrWhiteSpace(model.SharedToonTextures[material.ToonTextureIndex]))
        {
            return model.SharedToonTextures[material.ToonTextureIndex];
        }

        return RenderToonResources.ResolveSharedToonUri(material.ToonTextureIndex);
    }

    private static SphereTextureBlendMode ToRenderSphereMode(SphereTextureMode mode)
    {
        return mode switch
        {
            SphereTextureMode.Multiply => SphereTextureBlendMode.Multiply,
            SphereTextureMode.Add => SphereTextureBlendMode.Add,
            SphereTextureMode.SubTexture => SphereTextureBlendMode.SubTexture,
            _ => SphereTextureBlendMode.Disabled
        };
    }

    private static IReadOnlyDictionary<string, MotionEffectParameterValue> CopyEffectOverrides(
        EffectParameterOverrideSet overrides)
    {
        return overrides.Count == 0
            ? new Dictionary<string, MotionEffectParameterValue>(0, StringComparer.Ordinal)
            : overrides.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
