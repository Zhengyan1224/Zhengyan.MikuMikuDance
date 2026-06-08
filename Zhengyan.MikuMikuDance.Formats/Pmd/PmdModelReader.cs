using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Formats.Binary;

namespace Zhengyan.MikuMikuDance.Formats.Pmd;

public sealed class PmdModelReader
{
    private const string Magic = "Pmd";

    public MmdModel Read(ReadOnlyMemory<byte> data)
    {
        var reader = new BinarySpanReader(data);
        var signature = reader.ReadFixedString(3, EncodingProvider.ShiftJis);
        if (signature != Magic)
        {
            throw new MmdFormatException($"Invalid PMD signature '{signature}'.");
        }

        var model = new MmdModel(ModelFormat.Pmd)
        {
            Comment = string.Empty,
            EnglishComment = string.Empty
        };

        _ = reader.ReadSingle();
        model.Name = reader.ReadFixedString(20, EncodingProvider.ShiftJis);
        model.Comment = reader.ReadFixedString(256, EncodingProvider.ShiftJis);

        ReadVertices(reader, model);
        ReadIndices(reader, model);
        ReadMaterials(reader, model);
        ReadBones(reader, model);
        ReadIkConstraints(reader, model);
        ReadMorphs(reader, model);
        ReadLabels(reader, model);
        ReadEnglishExtension(reader, model);
        ReadToonTextures(reader, model);
        ReadRigidBodies(reader, model);
        ReadJoints(reader, model);

        return model;
    }

    public MmdModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray());
    }

    private static void ReadVertices(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var position = reader.ReadVector3();
            var normal = reader.ReadVector3();
            var uv = reader.ReadVector2();
            var bone0 = reader.ReadUInt16();
            var bone1 = reader.ReadUInt16();
            var weight = reader.ReadByte() / 100f;
            var edgeFlag = reader.ReadByte();
            model.AddVertex(new Vertex(
                position,
                normal,
                uv,
                [],
                new SkinningWeights(VertexSkinningType.Bdef2, [bone0, bone1], [weight, 1f - weight]),
                edgeFlag == 0 ? 1f : 0f));
        }
    }

    private static void ReadIndices(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddIndex(reader.ReadUInt16());
        }
    }

    private static void ReadMaterials(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var diffuseRgb = reader.ReadVector3();
            var alpha = reader.ReadSingle();
            var specularPower = reader.ReadSingle();
            var specular = reader.ReadVector3();
            var ambient = reader.ReadVector3();
            var toonIndex = reader.ReadSByte();
            var edgeEnabled = reader.ReadByte() != 0;
            var vertexCount = reader.ReadInt32();
            var textureNames = reader.ReadFixedString(20, EncodingProvider.ShiftJis);
            var textureIndex = -1;
            var sphereTextureIndex = -1;
            foreach (var textureName in SplitPmdTextureNames(textureNames))
            {
                if (textureName.EndsWith(".sph", StringComparison.OrdinalIgnoreCase) ||
                    textureName.EndsWith(".spa", StringComparison.OrdinalIgnoreCase))
                {
                    sphereTextureIndex = model.AddTexture(textureName);
                }
                else
                {
                    textureIndex = model.AddTexture(textureName);
                }
            }

            model.AddMaterial(new Material(
                $"material_{i}",
                string.Empty,
                new Vector4(diffuseRgb, alpha),
                specular,
                specularPower,
                ambient,
                false,
                true,
                true,
                true,
                edgeEnabled,
                new Vector4(0, 0, 0, 1),
                1f,
                textureIndex,
                sphereTextureIndex,
                SphereTextureMode.Disabled,
                toonIndex,
                true,
                vertexCount,
                string.Empty));
        }
    }

    private static void ReadBones(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadFixedString(20, EncodingProvider.ShiftJis);
            var parentIndex = NullIfPmdIndex(reader.ReadUInt16());
            var tailIndex = NullIfPmdIndex(reader.ReadUInt16());
            var type = reader.ReadByte();
            var ikTargetIndex = NullIfPmdIndex(reader.ReadUInt16());
            var origin = reader.ReadVector3();
            var flags = BoneFlags.Visible | BoneFlags.Enabled;
            if (type is 0 or 2 or 4 or 7 or 9)
            {
                flags |= BoneFlags.Rotatable;
            }

            if (type is 1 or 2 or 8 or 9)
            {
                flags |= BoneFlags.Movable;
            }

            var connectionOffset = Vector3.Zero;
            if (tailIndex < 0 || tailIndex == ikTargetIndex)
            {
                flags &= ~BoneFlags.IndexedTailPosition;
            }
            else
            {
                flags |= BoneFlags.IndexedTailPosition;
            }

            model.AddBone(new Bone(
                name,
                string.Empty,
                origin,
                parentIndex,
                0,
                flags,
                tailIndex,
                connectionOffset,
                -1,
                0f,
                null,
                null,
                0,
                null));
        }
    }

    private static void ReadIkConstraints(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadUInt16();
        var constraints = new Dictionary<int, IkConstraint>();
        for (var i = 0; i < count; i++)
        {
            var targetBoneIndex = reader.ReadUInt16();
            var effectorBoneIndex = reader.ReadUInt16();
            var linkCount = reader.ReadByte();
            var iterations = reader.ReadUInt16();
            var angleLimit = reader.ReadSingle();
            var links = new List<IkLink>(linkCount);
            for (var linkIndex = 0; linkIndex < linkCount; linkIndex++)
            {
                links.Add(new IkLink(reader.ReadUInt16(), false, Vector3.Zero, Vector3.Zero));
            }

            constraints[targetBoneIndex] = new IkConstraint(effectorBoneIndex, iterations, angleLimit, links);
        }

        if (constraints.Count == 0)
        {
            return;
        }

        var updatedBones = new List<Bone>(model.Bones);
        foreach (var (boneIndex, constraint) in constraints)
        {
            if (boneIndex < 0 || boneIndex >= updatedBones.Count)
            {
                continue;
            }

            var bone = updatedBones[boneIndex];
            updatedBones[boneIndex] = bone with
            {
                Flags = bone.Flags | BoneFlags.Ik,
                Ik = constraint
            };
        }

        model.ReplaceBones(updatedBones);
    }

    private static void ReadMorphs(BinarySpanReader reader, MmdModel model)
    {
        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadFixedString(20, EncodingProvider.ShiftJis);
            var vertexCount = reader.ReadInt32();
            var category = MapPmdMorphCategory(reader.ReadByte());
            var offsets = new List<MorphOffset>(vertexCount);
            for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                offsets.Add(new VertexMorphOffset(reader.ReadInt32(), reader.ReadVector3()));
            }

            model.AddMorph(new Morph(name, string.Empty, category, MorphType.Vertex, offsets));
        }
    }

    private static void ReadLabels(BinarySpanReader reader, MmdModel model)
    {
        var morphDisplayCount = reader.ReadByte();
        var morphItems = new List<ModelLabelItem>(morphDisplayCount);
        for (var i = 0; i < morphDisplayCount; i++)
        {
            morphItems.Add(new ModelLabelItem(LabelItemType.Morph, reader.ReadUInt16()));
        }

        model.AddLabel(new ModelLabel("Morph", string.Empty, true, morphItems));

        var boneCategoryCount = reader.ReadByte();
        var labels = new List<ModelLabel>(boneCategoryCount);
        for (var i = 0; i < boneCategoryCount; i++)
        {
            labels.Add(new ModelLabel(reader.ReadFixedString(50, EncodingProvider.ShiftJis), string.Empty, i < 2, []));
        }

        var mappingCount = reader.ReadInt32();
        var itemsByLabel = labels.Select(_ => new List<ModelLabelItem>()).ToList();
        for (var i = 0; i < mappingCount; i++)
        {
            var boneIndex = reader.ReadUInt16();
            var labelIndex = reader.ReadByte() - 1;
            if (labelIndex >= 0 && labelIndex < itemsByLabel.Count)
            {
                itemsByLabel[labelIndex].Add(new ModelLabelItem(LabelItemType.Bone, boneIndex));
            }
        }

        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            model.AddLabel(label with { Items = itemsByLabel[i] });
        }
    }

    private static void ReadEnglishExtension(BinarySpanReader reader, MmdModel model)
    {
        if (reader.EndOfBuffer)
        {
            return;
        }

        var enabled = reader.ReadByte() != 0;
        if (!enabled)
        {
            return;
        }

        model.EnglishName = reader.ReadFixedString(20, EncodingProvider.ShiftJis);
        model.EnglishComment = reader.ReadFixedString(256, EncodingProvider.ShiftJis);
        var bones = model.Bones.Select(bone => bone with
        {
            EnglishName = reader.ReadFixedString(20, EncodingProvider.ShiftJis)
        }).ToList();
        model.ReplaceBones(bones);

        var morphs = new List<Morph>(model.Morphs);
        for (var i = 1; i < morphs.Count; i++)
        {
            morphs[i] = morphs[i] with { EnglishName = reader.ReadFixedString(20, EncodingProvider.ShiftJis) };
        }

        model.ReplaceMorphs(morphs);

        var labels = model.Labels.Select(label => label with
        {
            EnglishName = reader.ReadFixedString(50, EncodingProvider.ShiftJis)
        }).ToList();
        model.ReplaceLabels(labels);
    }

    private static void ReadToonTextures(BinarySpanReader reader, MmdModel model)
    {
        if (reader.Remaining < 1000)
        {
            return;
        }

        for (var i = 0; i < 10; i++)
        {
            model.SetSharedToonTexture(i, reader.ReadFixedString(100, EncodingProvider.ShiftJis));
        }
    }

    private static void ReadRigidBodies(BinarySpanReader reader, MmdModel model)
    {
        if (reader.EndOfBuffer)
        {
            return;
        }

        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddRigidBody(new RigidBody(
                reader.ReadFixedString(20, EncodingProvider.ShiftJis),
                string.Empty,
                NullIfPmdIndex(reader.ReadUInt16()),
                reader.ReadByte(),
                reader.ReadUInt16(),
                (RigidBodyShapeType)reader.ReadByte(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                (RigidBodyTransformType)reader.ReadByte()));
        }
    }

    private static void ReadJoints(BinarySpanReader reader, MmdModel model)
    {
        if (reader.EndOfBuffer)
        {
            return;
        }

        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddJoint(new Joint(
                reader.ReadFixedString(20, EncodingProvider.ShiftJis),
                string.Empty,
                JointType.Generic6DofSpring,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3(),
                reader.ReadVector3()));
        }
    }

    private static IEnumerable<string> SplitPmdTextureNames(string value)
    {
        return value
            .Split(['*'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }

    private static int NullIfPmdIndex(ushort value)
    {
        return value == 0xffff ? -1 : value;
    }

    private static MorphCategory MapPmdMorphCategory(byte value)
    {
        return value switch
        {
            1 => MorphCategory.Eyebrow,
            2 => MorphCategory.Eye,
            3 => MorphCategory.Lip,
            4 => MorphCategory.Other,
            _ => MorphCategory.System
        };
    }
}
