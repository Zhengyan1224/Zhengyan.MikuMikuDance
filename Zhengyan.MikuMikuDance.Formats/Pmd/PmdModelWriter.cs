using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Formats.Binary;

namespace Zhengyan.MikuMikuDance.Formats.Pmd;

public sealed class PmdModelWriter
{
    private const ushort NullIndex = 0xffff;

    public byte[] Write(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        using var stream = new MemoryStream();
        Write(model, stream);
        return stream.ToArray();
    }

    public void Write(MmdModel model, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new BinaryWriter(stream, EncodingProvider.ShiftJis, leaveOpen: true);
        writer.Write("Pmd"u8);
        writer.Write(1.0f);
        WriteFixed(writer, model.Name, 20);
        WriteFixed(writer, model.Comment, 256);
        WriteVertices(writer, model.Vertices);
        WriteIndices(writer, model.Indices);
        WriteMaterials(writer, model);
        WriteBones(writer, model.Bones);
        WriteIkConstraints(writer, model.Bones);
        WriteMorphs(writer, model.Morphs);
        WriteLabels(writer, model);
        WriteEnglishExtension(writer, model);
        WriteToonTextures(writer, model.SharedToonTextures);
        WriteRigidBodies(writer, model.RigidBodies);
        WriteJoints(writer, model.Joints);
    }

    private static void WriteVertices(BinaryWriter writer, IReadOnlyList<Vertex> vertices)
    {
        writer.Write(checked(vertices.Count));
        foreach (var vertex in vertices)
        {
            WriteVector3(writer, vertex.Position);
            WriteVector3(writer, vertex.Normal);
            writer.Write(vertex.Uv.X);
            writer.Write(vertex.Uv.Y);
            var (bone0, bone1, weight) = ToPmdSkinning(vertex.Skinning);
            writer.Write(ToUInt16Index(bone0));
            writer.Write(ToUInt16Index(bone1));
            writer.Write((byte)Math.Clamp(MathF.Round(weight * 100f), 0, 100));
            writer.Write((byte)(vertex.EdgeSize > 0 ? 0 : 1));
        }
    }

    private static void WriteIndices(BinaryWriter writer, IReadOnlyList<int> indices)
    {
        writer.Write(checked(indices.Count));
        foreach (var index in indices)
        {
            writer.Write(ToUInt16Index(index));
        }
    }

    private static void WriteMaterials(BinaryWriter writer, MmdModel model)
    {
        writer.Write(checked(model.Materials.Count));
        foreach (var material in model.Materials)
        {
            writer.Write(material.Diffuse.X);
            writer.Write(material.Diffuse.Y);
            writer.Write(material.Diffuse.Z);
            writer.Write(material.Diffuse.W);
            writer.Write(material.SpecularPower);
            WriteVector3(writer, material.Specular);
            WriteVector3(writer, material.Ambient);
            writer.Write((sbyte)Math.Clamp(material.ToonTextureIndex, sbyte.MinValue, sbyte.MaxValue));
            writer.Write((byte)(material.EdgeEnabled ? 1 : 0));
            writer.Write(checked(material.VertexCount));
            WriteFixed(writer, GetPmdTextureName(model, material), 20);
        }
    }

    private static void WriteBones(BinaryWriter writer, IReadOnlyList<Bone> bones)
    {
        writer.Write(checked((ushort)bones.Count));
        foreach (var bone in bones)
        {
            WriteFixed(writer, bone.Name, 20);
            writer.Write(ToNullableUInt16Index(bone.ParentBoneIndex));
            writer.Write(ToNullableUInt16Index(bone.ConnectionIndex));
            writer.Write(GetPmdBoneType(bone));
            writer.Write(ToNullableUInt16Index(bone.Ik?.EffectorBoneIndex ?? -1));
            WriteVector3(writer, bone.Origin);
        }
    }

    private static void WriteIkConstraints(BinaryWriter writer, IReadOnlyList<Bone> bones)
    {
        var ikBones = bones
            .Select((bone, index) => (bone, index))
            .Where(item => item.bone.Ik is not null)
            .ToArray();
        writer.Write(checked((ushort)ikBones.Length));
        foreach (var (bone, index) in ikBones)
        {
            var ik = bone.Ik!;
            writer.Write(ToUInt16Index(index));
            writer.Write(ToUInt16Index(ik.EffectorBoneIndex));
            writer.Write(checked((byte)ik.Links.Count));
            writer.Write(checked((ushort)ik.IterationCount));
            writer.Write(ik.AngleLimit);
            foreach (var link in ik.Links)
            {
                writer.Write(ToUInt16Index(link.BoneIndex));
            }
        }
    }

    private static void WriteMorphs(BinaryWriter writer, IReadOnlyList<Morph> morphs)
    {
        var pmdMorphs = morphs
            .Where(morph => morph.Type == MorphType.Vertex)
            .ToArray();
        writer.Write(checked((ushort)pmdMorphs.Length));
        foreach (var morph in pmdMorphs)
        {
            var offsets = morph.Offsets.OfType<VertexMorphOffset>().ToArray();
            WriteFixed(writer, morph.Name, 20);
            writer.Write(checked(offsets.Length));
            writer.Write(MapPmdMorphCategory(morph.Category));
            foreach (var offset in offsets)
            {
                writer.Write(offset.VertexIndex);
                WriteVector3(writer, offset.Translation);
            }
        }
    }

    private static void WriteLabels(BinaryWriter writer, MmdModel model)
    {
        var morphItems = model.Labels
            .SelectMany(label => label.Items)
            .Where(item => item.Type == LabelItemType.Morph)
            .ToArray();
        writer.Write(checked((byte)Math.Min(morphItems.Length, byte.MaxValue)));
        foreach (var item in morphItems.Take(byte.MaxValue))
        {
            writer.Write(ToUInt16Index(item.Index));
        }

        var boneLabels = model.Labels
            .Where(label => label.Items.Any(item => item.Type == LabelItemType.Bone))
            .ToArray();
        writer.Write(checked((byte)Math.Min(boneLabels.Length, byte.MaxValue)));
        foreach (var label in boneLabels.Take(byte.MaxValue))
        {
            WriteFixed(writer, label.Name, 50);
        }

        var mappings = boneLabels
            .Take(byte.MaxValue)
            .SelectMany((label, labelIndex) => label.Items
                .Where(item => item.Type == LabelItemType.Bone)
                .Select(item => (item.Index, LabelIndex: labelIndex + 1)))
            .ToArray();
        writer.Write(checked(mappings.Length));
        foreach (var mapping in mappings)
        {
            writer.Write(ToUInt16Index(mapping.Index));
            writer.Write(checked((byte)mapping.LabelIndex));
        }
    }

    private static void WriteEnglishExtension(BinaryWriter writer, MmdModel model)
    {
        var enabled = !string.IsNullOrEmpty(model.EnglishName) ||
            !string.IsNullOrEmpty(model.EnglishComment) ||
            model.Bones.Any(bone => !string.IsNullOrEmpty(bone.EnglishName)) ||
            model.Morphs.Skip(1).Any(morph => !string.IsNullOrEmpty(morph.EnglishName)) ||
            model.Labels.Any(label => !string.IsNullOrEmpty(label.EnglishName));
        writer.Write((byte)(enabled ? 1 : 0));
        if (!enabled)
        {
            return;
        }

        WriteFixed(writer, model.EnglishName, 20);
        WriteFixed(writer, model.EnglishComment, 256);
        foreach (var bone in model.Bones)
        {
            WriteFixed(writer, bone.EnglishName, 20);
        }

        foreach (var morph in model.Morphs.Skip(1))
        {
            WriteFixed(writer, morph.EnglishName, 20);
        }

        foreach (var label in model.Labels)
        {
            WriteFixed(writer, label.EnglishName, 50);
        }
    }

    private static void WriteToonTextures(BinaryWriter writer, IReadOnlyList<string> sharedToonTextures)
    {
        for (var i = 0; i < 10; i++)
        {
            var texture = i < sharedToonTextures.Count ? sharedToonTextures[i] : string.Empty;
            WriteFixed(writer, texture, 100);
        }
    }

    private static void WriteRigidBodies(BinaryWriter writer, IReadOnlyList<RigidBody> rigidBodies)
    {
        writer.Write(checked(rigidBodies.Count));
        foreach (var rigidBody in rigidBodies)
        {
            WriteFixed(writer, rigidBody.Name, 20);
            writer.Write(ToNullableUInt16Index(rigidBody.BoneIndex));
            writer.Write(rigidBody.CollisionGroupId);
            writer.Write(rigidBody.CollisionMask);
            writer.Write((byte)rigidBody.ShapeType);
            WriteVector3(writer, rigidBody.ShapeSize);
            WriteVector3(writer, rigidBody.Translation);
            WriteVector3(writer, rigidBody.Orientation);
            writer.Write(rigidBody.Mass);
            writer.Write(rigidBody.LinearDamping);
            writer.Write(rigidBody.AngularDamping);
            writer.Write(rigidBody.Restitution);
            writer.Write(rigidBody.Friction);
            writer.Write((byte)rigidBody.TransformType);
        }
    }

    private static void WriteJoints(BinaryWriter writer, IReadOnlyList<Joint> joints)
    {
        writer.Write(checked(joints.Count));
        foreach (var joint in joints)
        {
            WriteFixed(writer, joint.Name, 20);
            writer.Write(joint.RigidBodyAIndex);
            writer.Write(joint.RigidBodyBIndex);
            WriteVector3(writer, joint.Translation);
            WriteVector3(writer, joint.Orientation);
            WriteVector3(writer, joint.LinearLowerLimit);
            WriteVector3(writer, joint.LinearUpperLimit);
            WriteVector3(writer, joint.AngularLowerLimit);
            WriteVector3(writer, joint.AngularUpperLimit);
            WriteVector3(writer, joint.LinearStiffness);
            WriteVector3(writer, joint.AngularStiffness);
        }
    }

    private static (int Bone0, int Bone1, float Weight) ToPmdSkinning(SkinningWeights skinning)
    {
        var bone0 = skinning.BoneIndices.Count > 0 ? skinning.BoneIndices[0] : 0;
        var bone1 = skinning.BoneIndices.Count > 1 ? skinning.BoneIndices[1] : bone0;
        var weight = skinning.Weights.Count > 0 ? skinning.Weights[0] : 1f;
        return (bone0, bone1, Math.Clamp(weight, 0f, 1f));
    }

    private static string GetPmdTextureName(MmdModel model, Material material)
    {
        var texture = GetTexturePath(model, material.TextureIndex);
        var sphere = GetTexturePath(model, material.SphereTextureIndex);
        if (!string.IsNullOrEmpty(texture) && !string.IsNullOrEmpty(sphere))
        {
            return $"{texture}*{sphere}";
        }

        return !string.IsNullOrEmpty(texture) ? texture : sphere;
    }

    private static string GetTexturePath(MmdModel model, int index)
    {
        return index >= 0 && index < model.Textures.Count ? model.Textures[index] : string.Empty;
    }

    private static byte GetPmdBoneType(Bone bone)
    {
        if (bone.Ik is not null)
        {
            return 2;
        }

        if (bone.Flags.HasFlag(BoneFlags.Movable))
        {
            return 1;
        }

        return 0;
    }

    private static byte MapPmdMorphCategory(MorphCategory category)
    {
        return category switch
        {
            MorphCategory.Eyebrow => 1,
            MorphCategory.Eye => 2,
            MorphCategory.Lip => 3,
            MorphCategory.Other => 4,
            _ => 0
        };
    }

    private static ushort ToUInt16Index(int index)
    {
        if (index < 0 || index > ushort.MaxValue)
        {
            throw new InvalidDataException($"PMD index {index} is outside UInt16 range.");
        }

        return (ushort)index;
    }

    private static ushort ToNullableUInt16Index(int index)
    {
        return index < 0 ? NullIndex : ToUInt16Index(index);
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteFixed(BinaryWriter writer, string value, int length)
    {
        var bytes = EncodingProvider.ShiftJis.GetBytes(value ?? string.Empty);
        if (bytes.Length > length)
        {
            Array.Resize(ref bytes, length);
        }

        writer.Write(bytes);
        if (bytes.Length < length)
        {
            writer.Write(new byte[length - bytes.Length]);
        }
    }
}
