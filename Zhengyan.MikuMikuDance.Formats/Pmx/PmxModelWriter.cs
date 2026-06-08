using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Formats.Pmx;

public sealed class PmxModelWriter
{
    private static readonly Encoding Utf8 = Encoding.UTF8;

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
        var header = CreateHeader(model);
        using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        writer.Write("PMX "u8);
        writer.Write(2.1f);
        writer.Write((byte)8);
        writer.Write((byte)header.TextEncoding);
        writer.Write((byte)header.AdditionalUvCount);
        writer.Write((byte)header.VertexIndexSize);
        writer.Write((byte)header.TextureIndexSize);
        writer.Write((byte)header.MaterialIndexSize);
        writer.Write((byte)header.BoneIndexSize);
        writer.Write((byte)header.MorphIndexSize);
        writer.Write((byte)header.RigidBodyIndexSize);

        WriteString(writer, model.Name);
        WriteString(writer, model.EnglishName);
        WriteString(writer, model.Comment);
        WriteString(writer, model.EnglishComment);
        WriteVertices(writer, header, model.Vertices);
        WriteIndices(writer, header, model.Indices);
        WriteTextures(writer, model.Textures);
        WriteMaterials(writer, header, model.Materials);
        WriteBones(writer, header, model.Bones);
        WriteMorphs(writer, header, model.Morphs);
        WriteLabels(writer, header, model.Labels);
        WriteRigidBodies(writer, header, model.RigidBodies);
        WriteJoints(writer, header, model.Joints);
        WriteSoftBodies(writer, header, model.SoftBodies);
    }

    private static PmxHeader CreateHeader(MmdModel model)
    {
        var additionalUvCount = model.Vertices.Count == 0 ? 0 : model.Vertices.Max(vertex => vertex.AdditionalUvs.Count);
        return new PmxHeader(
            2.1f,
            PmxTextEncoding.Utf8,
            Math.Clamp(additionalUvCount, 0, 4),
            GetUnsignedIndexSize(model.Vertices.Count),
            GetSignedIndexSize(model.Textures.Count),
            GetSignedIndexSize(model.Materials.Count),
            GetSignedIndexSize(model.Bones.Count),
            GetSignedIndexSize(model.Morphs.Count),
            GetSignedIndexSize(model.RigidBodies.Count));
    }

    private static void WriteVertices(BinaryWriter writer, PmxHeader header, IReadOnlyList<Vertex> vertices)
    {
        writer.Write(vertices.Count);
        foreach (var vertex in vertices)
        {
            WriteVector3(writer, vertex.Position);
            WriteVector3(writer, vertex.Normal);
            writer.Write(vertex.Uv.X);
            writer.Write(vertex.Uv.Y);
            for (var i = 0; i < header.AdditionalUvCount; i++)
            {
                var value = i < vertex.AdditionalUvs.Count ? vertex.AdditionalUvs[i] : Vector4.Zero;
                WriteVector4(writer, value);
            }

            writer.Write((byte)vertex.Skinning.Type);
            WriteSkinning(writer, header, vertex.Skinning);
            writer.Write(vertex.EdgeSize);
        }
    }

    private static void WriteSkinning(BinaryWriter writer, PmxHeader header, SkinningWeights skinning)
    {
        switch (skinning.Type)
        {
            case VertexSkinningType.Bdef1:
                WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, 0));
                break;
            case VertexSkinningType.Bdef2:
                WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, 0));
                WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, 1));
                writer.Write(GetWeight(skinning, 0, 1));
                break;
            case VertexSkinningType.Bdef4:
            case VertexSkinningType.Qdef:
                for (var i = 0; i < 4; i++)
                {
                    WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, i));
                }

                for (var i = 0; i < 4; i++)
                {
                    writer.Write(GetWeight(skinning, i, i == 0 ? 1 : 0));
                }

                break;
            case VertexSkinningType.Sdef:
                WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, 0));
                WriteSizedIndex(writer, header.BoneIndexSize, GetBoneIndex(skinning, 1));
                writer.Write(GetWeight(skinning, 0, 1));
                WriteVector3(writer, skinning.Sdef?.C ?? Vector3.Zero);
                WriteVector3(writer, skinning.Sdef?.R0 ?? Vector3.Zero);
                WriteVector3(writer, skinning.Sdef?.R1 ?? Vector3.Zero);
                break;
            default:
                throw new InvalidDataException($"Unsupported PMX skinning type {skinning.Type}.");
        }
    }

    private static void WriteIndices(BinaryWriter writer, PmxHeader header, IReadOnlyList<int> indices)
    {
        writer.Write(indices.Count);
        foreach (var index in indices)
        {
            WriteSizedVertexIndex(writer, header.VertexIndexSize, index);
        }
    }

    private static void WriteTextures(BinaryWriter writer, IReadOnlyList<string> textures)
    {
        writer.Write(textures.Count);
        foreach (var texture in textures)
        {
            WriteString(writer, texture);
        }
    }

    private static void WriteMaterials(BinaryWriter writer, PmxHeader header, IReadOnlyList<Material> materials)
    {
        writer.Write(materials.Count);
        foreach (var material in materials)
        {
            WriteString(writer, material.Name);
            WriteString(writer, material.EnglishName);
            WriteVector4(writer, material.Diffuse);
            WriteVector3(writer, material.Specular);
            writer.Write(material.SpecularPower);
            WriteVector3(writer, material.Ambient);
            writer.Write(GetMaterialFlags(material));
            WriteVector4(writer, material.EdgeColor);
            writer.Write(material.EdgeSize);
            WriteSizedIndex(writer, header.TextureIndexSize, material.TextureIndex);
            WriteSizedIndex(writer, header.TextureIndexSize, material.SphereTextureIndex);
            writer.Write((byte)material.SphereTextureMode);
            writer.Write((byte)(material.ToonTextureShared ? 1 : 0));
            if (material.ToonTextureShared)
            {
                writer.Write((byte)Math.Clamp(material.ToonTextureIndex, 0, 9));
            }
            else
            {
                WriteSizedIndex(writer, header.TextureIndexSize, material.ToonTextureIndex);
            }

            WriteString(writer, material.Memo);
            writer.Write(material.VertexCount);
        }
    }

    private static void WriteBones(BinaryWriter writer, PmxHeader header, IReadOnlyList<Bone> bones)
    {
        writer.Write(bones.Count);
        foreach (var bone in bones)
        {
            WriteString(writer, bone.Name);
            WriteString(writer, bone.EnglishName);
            WriteVector3(writer, bone.Origin);
            WriteSizedIndex(writer, header.BoneIndexSize, bone.ParentBoneIndex);
            writer.Write(bone.LayerIndex);
            writer.Write((ushort)bone.Flags);
            if (bone.Flags.HasFlag(BoneFlags.IndexedTailPosition))
            {
                WriteSizedIndex(writer, header.BoneIndexSize, bone.ConnectionIndex);
            }
            else
            {
                WriteVector3(writer, bone.ConnectionOffset);
            }

            if ((bone.Flags & (BoneFlags.InherentOrientation | BoneFlags.InherentTranslation)) != 0)
            {
                WriteSizedIndex(writer, header.BoneIndexSize, bone.InherentParentIndex);
                writer.Write(bone.InherentCoefficient);
            }

            if (bone.Flags.HasFlag(BoneFlags.FixedAxis))
            {
                WriteVector3(writer, bone.AxisConstraint?.Axis ?? Vector3.UnitX);
            }

            if (bone.Flags.HasFlag(BoneFlags.LocalAxis))
            {
                WriteVector3(writer, bone.LocalAxes?.X ?? Vector3.UnitX);
                WriteVector3(writer, bone.LocalAxes?.Z ?? Vector3.UnitZ);
            }

            if (bone.Flags.HasFlag(BoneFlags.OutsideParent))
            {
                writer.Write(bone.Key);
            }

            if (bone.Flags.HasFlag(BoneFlags.Ik))
            {
                var ik = bone.Ik ?? new IkConstraint(-1, 0, 0, []);
                WriteSizedIndex(writer, header.BoneIndexSize, ik.EffectorBoneIndex);
                writer.Write(ik.IterationCount);
                writer.Write(ik.AngleLimit);
                writer.Write(ik.Links.Count);
                foreach (var link in ik.Links)
                {
                    WriteSizedIndex(writer, header.BoneIndexSize, link.BoneIndex);
                    writer.Write((byte)(link.AngleLimitEnabled ? 1 : 0));
                    if (link.AngleLimitEnabled)
                    {
                        WriteVector3(writer, link.LowerLimit);
                        WriteVector3(writer, link.UpperLimit);
                    }
                }
            }
        }
    }

    private static void WriteMorphs(BinaryWriter writer, PmxHeader header, IReadOnlyList<Morph> morphs)
    {
        writer.Write(morphs.Count);
        foreach (var morph in morphs)
        {
            WriteString(writer, morph.Name);
            WriteString(writer, morph.EnglishName);
            writer.Write((byte)morph.Category);
            writer.Write((byte)morph.Type);
            writer.Write(morph.Offsets.Count);
            foreach (var offset in morph.Offsets)
            {
                WriteMorphOffset(writer, header, offset);
            }
        }
    }

    private static void WriteMorphOffset(BinaryWriter writer, PmxHeader header, MorphOffset offset)
    {
        switch (offset)
        {
            case GroupMorphOffset group:
                WriteSizedIndex(writer, header.MorphIndexSize, group.MorphIndex);
                writer.Write(group.Weight);
                break;
            case VertexMorphOffset vertex:
                WriteSizedVertexIndex(writer, header.VertexIndexSize, vertex.VertexIndex);
                WriteVector3(writer, vertex.Translation);
                break;
            case BoneMorphOffset bone:
                WriteSizedIndex(writer, header.BoneIndexSize, bone.BoneIndex);
                WriteVector3(writer, bone.Translation);
                WriteQuaternion(writer, bone.Orientation);
                break;
            case UvMorphOffset uv:
                WriteSizedVertexIndex(writer, header.VertexIndexSize, uv.VertexIndex);
                WriteVector4(writer, uv.Offset);
                break;
            case MaterialMorphOffset material:
                WriteSizedIndex(writer, header.MaterialIndexSize, material.MaterialIndex);
                writer.Write((byte)(material.IsAdditive ? 1 : 0));
                WriteVector4(writer, material.Diffuse);
                WriteVector3(writer, material.Specular);
                writer.Write(material.SpecularPower);
                WriteVector3(writer, material.Ambient);
                WriteVector4(writer, material.EdgeColor);
                writer.Write(material.EdgeSize);
                WriteVector4(writer, material.TextureCoefficient);
                WriteVector4(writer, material.SphereTextureCoefficient);
                WriteVector4(writer, material.ToonTextureCoefficient);
                break;
            case FlipMorphOffset flip:
                WriteSizedIndex(writer, header.MorphIndexSize, flip.MorphIndex);
                writer.Write(flip.Weight);
                break;
            case ImpulseMorphOffset impulse:
                WriteSizedIndex(writer, header.RigidBodyIndexSize, impulse.RigidBodyIndex);
                writer.Write((byte)(impulse.Local ? 1 : 0));
                WriteVector3(writer, impulse.Velocity);
                WriteVector3(writer, impulse.Torque);
                break;
            default:
                throw new InvalidDataException($"Unsupported PMX morph offset {offset.GetType().Name}.");
        }
    }

    private static void WriteLabels(BinaryWriter writer, PmxHeader header, IReadOnlyList<ModelLabel> labels)
    {
        writer.Write(labels.Count);
        foreach (var label in labels)
        {
            WriteString(writer, label.Name);
            WriteString(writer, label.EnglishName);
            writer.Write((byte)(label.Special ? 1 : 0));
            writer.Write(label.Items.Count);
            foreach (var item in label.Items)
            {
                writer.Write((byte)item.Type);
                if (item.Type == LabelItemType.Bone)
                {
                    WriteSizedIndex(writer, header.BoneIndexSize, item.Index);
                }
                else
                {
                    WriteSizedIndex(writer, header.MorphIndexSize, item.Index);
                }
            }
        }
    }

    private static void WriteRigidBodies(BinaryWriter writer, PmxHeader header, IReadOnlyList<RigidBody> rigidBodies)
    {
        writer.Write(rigidBodies.Count);
        foreach (var rigidBody in rigidBodies)
        {
            WriteString(writer, rigidBody.Name);
            WriteString(writer, rigidBody.EnglishName);
            WriteSizedIndex(writer, header.BoneIndexSize, rigidBody.BoneIndex);
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

    private static void WriteJoints(BinaryWriter writer, PmxHeader header, IReadOnlyList<Joint> joints)
    {
        writer.Write(joints.Count);
        foreach (var joint in joints)
        {
            WriteString(writer, joint.Name);
            WriteString(writer, joint.EnglishName);
            writer.Write((byte)joint.Type);
            WriteSizedIndex(writer, header.RigidBodyIndexSize, joint.RigidBodyAIndex);
            WriteSizedIndex(writer, header.RigidBodyIndexSize, joint.RigidBodyBIndex);
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

    private static void WriteSoftBodies(BinaryWriter writer, PmxHeader header, IReadOnlyList<SoftBody> softBodies)
    {
        writer.Write(softBodies.Count);
        foreach (var softBody in softBodies)
        {
            WriteString(writer, softBody.Name);
            WriteString(writer, softBody.EnglishName);
            writer.Write((byte)0);
            WriteSizedIndex(writer, header.MaterialIndexSize, softBody.MaterialIndex);
            writer.Write((byte)Math.Clamp(softBody.GroupId, 0, byte.MaxValue));
            writer.Write((ushort)Math.Clamp(softBody.CollisionMask, 0, ushort.MaxValue));
            writer.Write((byte)0);
            for (var i = 0; i < 4; i++)
            {
                writer.Write(0f);
            }

            for (var i = 0; i < 3; i++)
            {
                writer.Write(0);
            }

            for (var i = 0; i < 12; i++)
            {
                writer.Write(0f);
            }

            writer.Write(0);
            writer.Write(0);
        }
    }

    private static byte GetMaterialFlags(Material material)
    {
        var flags = 0;
        if (material.IsDoubleSided)
        {
            flags |= 0x01;
        }

        if (material.GroundShadowEnabled)
        {
            flags |= 0x02;
        }

        if (material.SelfShadowMapEnabled)
        {
            flags |= 0x04;
        }

        if (material.SelfShadowEnabled)
        {
            flags |= 0x08;
        }

        if (material.EdgeEnabled)
        {
            flags |= 0x10;
        }

        return (byte)flags;
    }

    private static int GetBoneIndex(SkinningWeights skinning, int index)
    {
        return index < skinning.BoneIndices.Count ? skinning.BoneIndices[index] : -1;
    }

    private static float GetWeight(SkinningWeights skinning, int index, float fallback)
    {
        return index < skinning.Weights.Count ? skinning.Weights[index] : fallback;
    }

    private static int GetUnsignedIndexSize(int count)
    {
        return count <= byte.MaxValue + 1 ? 1 : count <= ushort.MaxValue + 1 ? 2 : 4;
    }

    private static int GetSignedIndexSize(int count)
    {
        return count <= sbyte.MaxValue ? 1 : count <= short.MaxValue ? 2 : 4;
    }

    private static void WriteSizedIndex(BinaryWriter writer, int size, int value)
    {
        switch (size)
        {
            case 1:
                writer.Write(value < 0 ? (sbyte)-1 : checked((sbyte)value));
                break;
            case 2:
                writer.Write(value < 0 ? (short)-1 : checked((short)value));
                break;
            case 4:
                writer.Write(value);
                break;
            default:
                throw new InvalidDataException($"Unsupported PMX signed index size {size}.");
        }
    }

    private static void WriteSizedVertexIndex(BinaryWriter writer, int size, int value)
    {
        switch (size)
        {
            case 1:
                writer.Write(checked((byte)value));
                break;
            case 2:
                writer.Write(checked((ushort)value));
                break;
            case 4:
                writer.Write(value);
                break;
            default:
                throw new InvalidDataException($"Unsupported PMX vertex index size {size}.");
        }
    }

    private static void WriteString(BinaryWriter writer, string? value)
    {
        var bytes = Utf8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteVector4(BinaryWriter writer, Vector4 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    private static void WriteQuaternion(BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }
}
