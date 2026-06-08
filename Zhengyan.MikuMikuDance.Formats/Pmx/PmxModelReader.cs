using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Formats.Binary;

namespace Zhengyan.MikuMikuDance.Formats.Pmx;

public sealed class PmxModelReader
{
    private const string Magic = "PMX ";

    static PmxModelReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public MmdModel Read(ReadOnlyMemory<byte> data)
    {
        var reader = new BinarySpanReader(data);
        var signature = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (signature != Magic)
        {
            throw new MmdFormatException($"Invalid PMX signature '{signature}'.");
        }

        var header = ReadHeader(reader);
        var encoding = header.TextEncoding == PmxTextEncoding.Utf8
            ? Encoding.UTF8
            : Encoding.Unicode;

        var model = new MmdModel(ModelFormat.Pmx)
        {
            Name = reader.ReadLengthPrefixedString(encoding),
            EnglishName = reader.ReadLengthPrefixedString(encoding),
            Comment = reader.ReadLengthPrefixedString(encoding),
            EnglishComment = reader.ReadLengthPrefixedString(encoding)
        };

        ReadVertices(reader, header, model);
        ReadIndices(reader, header, model);
        ReadTextures(reader, encoding, model);
        ReadMaterials(reader, header, encoding, model);
        ReadBones(reader, header, encoding, model);
        ReadMorphs(reader, header, encoding, model);
        ReadLabels(reader, header, encoding, model);
        ReadRigidBodies(reader, header, encoding, model);
        ReadJoints(reader, header, encoding, model);
        if (!reader.EndOfBuffer)
        {
            ReadSoftBodies(reader, header, encoding, model);
        }

        return model;
    }

    public MmdModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray());
    }

    public PmxHeader ReadHeader(ReadOnlyMemory<byte> data)
    {
        var reader = new BinarySpanReader(data);
        var signature = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (signature != Magic)
        {
            throw new MmdFormatException($"Invalid PMX signature '{signature}'.");
        }

        return ReadHeader(reader);
    }

    private static PmxHeader ReadHeader(BinarySpanReader reader)
    {
        var version = reader.ReadSingle();
        var infoSize = reader.ReadByte();
        if (infoSize < 8)
        {
            throw new MmdFormatException($"Invalid PMX globals size {infoSize}.");
        }

        var globals = reader.ReadBytes(infoSize);
        return new PmxHeader(
            version,
            (PmxTextEncoding)globals[0],
            globals[1],
            globals[2],
            globals[3],
            globals[4],
            globals[5],
            globals[6],
            globals[7]);
    }

    private static void ReadVertices(BinarySpanReader reader, PmxHeader header, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var position = reader.ReadVector3();
            var normal = reader.ReadVector3();
            var uv = reader.ReadVector2();
            var additionalUvs = new List<Vector4>(header.AdditionalUvCount);
            for (var uvIndex = 0; uvIndex < header.AdditionalUvCount; uvIndex++)
            {
                additionalUvs.Add(reader.ReadVector4());
            }

            var skinningType = (VertexSkinningType)reader.ReadByte();
            var skinning = ReadSkinning(reader, header, skinningType);
            var edgeSize = reader.ReadSingle();
            model.AddVertex(new Vertex(position, normal, uv, additionalUvs, skinning, edgeSize));
        }
    }

    private static SkinningWeights ReadSkinning(BinarySpanReader reader, PmxHeader header, VertexSkinningType type)
    {
        return type switch
        {
            VertexSkinningType.Bdef1 => new SkinningWeights(
                type,
                [reader.ReadSizedIndex(header.BoneIndexSize)],
                [1f]),
            VertexSkinningType.Bdef2 => ReadBdef2Skinning(reader, header, type),
            VertexSkinningType.Bdef4 or VertexSkinningType.Qdef => ReadFourBoneSkinning(reader, header, type),
            VertexSkinningType.Sdef => ReadSdefSkinning(reader, header, type),
            _ => throw new MmdFormatException($"Unsupported PMX skinning type {type}.")
        };
    }

    private static SkinningWeights ReadFourBoneSkinning(BinarySpanReader reader, PmxHeader header, VertexSkinningType type)
    {
        var bones = new[]
        {
            reader.ReadSizedIndex(header.BoneIndexSize),
            reader.ReadSizedIndex(header.BoneIndexSize),
            reader.ReadSizedIndex(header.BoneIndexSize),
            reader.ReadSizedIndex(header.BoneIndexSize)
        };
        var weights = new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() };
        return new SkinningWeights(type, bones, weights);
    }

    private static SkinningWeights ReadBdef2Skinning(BinarySpanReader reader, PmxHeader header, VertexSkinningType type)
    {
        var bone0 = reader.ReadSizedIndex(header.BoneIndexSize);
        var bone1 = reader.ReadSizedIndex(header.BoneIndexSize);
        var weight = reader.ReadSingle();
        return new SkinningWeights(type, [bone0, bone1], [weight, 1f - weight]);
    }

    private static SkinningWeights ReadSdefSkinning(BinarySpanReader reader, PmxHeader header, VertexSkinningType type)
    {
        var bone0 = reader.ReadSizedIndex(header.BoneIndexSize);
        var bone1 = reader.ReadSizedIndex(header.BoneIndexSize);
        var weight = reader.ReadSingle();
        var c = reader.ReadVector3();
        var r0 = reader.ReadVector3();
        var r1 = reader.ReadVector3();
        return new SkinningWeights(type, [bone0, bone1], [weight, 1f - weight], new SdefParameters(c, r0, r1));
    }

    private static void ReadIndices(BinarySpanReader reader, PmxHeader header, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddIndex(reader.ReadSizedVertexIndex(header.VertexIndexSize));
        }
    }

    private static void ReadTextures(BinarySpanReader reader, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddTexture(reader.ReadLengthPrefixedString(encoding));
        }
    }

    private static void ReadMaterials(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadLengthPrefixedString(encoding);
            var englishName = reader.ReadLengthPrefixedString(encoding);
            var diffuse = reader.ReadVector4();
            var specular = reader.ReadVector3();
            var specularPower = reader.ReadSingle();
            var ambient = reader.ReadVector3();
            var flags = reader.ReadByte();
            var edgeColor = reader.ReadVector4();
            var edgeSize = reader.ReadSingle();
            var textureIndex = reader.ReadSizedIndex(header.TextureIndexSize);
            var sphereTextureIndex = reader.ReadSizedIndex(header.TextureIndexSize);
            var sphereTextureMode = (SphereTextureMode)reader.ReadByte();
            var toonShared = reader.ReadByte() != 0;
            var toonTextureIndex = toonShared
                ? reader.ReadByte()
                : reader.ReadSizedIndex(header.TextureIndexSize);
            var memo = reader.ReadLengthPrefixedString(encoding);
            var vertexCount = reader.ReadInt32();

            model.AddMaterial(new Material(
                name,
                englishName,
                diffuse,
                specular,
                specularPower,
                ambient,
                (flags & 0x01) != 0,
                (flags & 0x02) != 0,
                (flags & 0x04) != 0,
                (flags & 0x08) != 0,
                (flags & 0x10) != 0,
                edgeColor,
                edgeSize,
                textureIndex,
                sphereTextureIndex,
                sphereTextureMode,
                toonTextureIndex,
                toonShared,
                vertexCount,
                memo));
        }
    }

    private static void ReadBones(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadLengthPrefixedString(encoding);
            var englishName = reader.ReadLengthPrefixedString(encoding);
            var origin = reader.ReadVector3();
            var parentBoneIndex = reader.ReadSizedIndex(header.BoneIndexSize);
            var layerIndex = reader.ReadInt32();
            var flags = (BoneFlags)reader.ReadUInt16();
            var connectionIndex = -1;
            var connectionOffset = Vector3.Zero;
            if ((flags & BoneFlags.IndexedTailPosition) != 0)
            {
                connectionIndex = reader.ReadSizedIndex(header.BoneIndexSize);
            }
            else
            {
                connectionOffset = reader.ReadVector3();
            }

            var inherentParentIndex = -1;
            var inherentCoefficient = 0f;
            if ((flags & (BoneFlags.InherentOrientation | BoneFlags.InherentTranslation)) != 0)
            {
                inherentParentIndex = reader.ReadSizedIndex(header.BoneIndexSize);
                inherentCoefficient = reader.ReadSingle();
            }

            AxisConstraint? axisConstraint = null;
            if ((flags & BoneFlags.FixedAxis) != 0)
            {
                axisConstraint = new AxisConstraint(reader.ReadVector3());
            }

            LocalAxes? localAxes = null;
            if ((flags & BoneFlags.LocalAxis) != 0)
            {
                localAxes = new LocalAxes(reader.ReadVector3(), reader.ReadVector3());
            }

            var key = 0;
            if ((flags & BoneFlags.OutsideParent) != 0)
            {
                key = reader.ReadInt32();
            }

            IkConstraint? ik = null;
            if ((flags & BoneFlags.Ik) != 0)
            {
                var effector = reader.ReadSizedIndex(header.BoneIndexSize);
                var iterations = reader.ReadInt32();
                var angleLimit = reader.ReadSingle();
                var linkCount = reader.ReadInt32();
                var links = new List<IkLink>(linkCount);
                for (var linkIndex = 0; linkIndex < linkCount; linkIndex++)
                {
                    var boneIndex = reader.ReadSizedIndex(header.BoneIndexSize);
                    var hasLimit = reader.ReadByte() != 0;
                    var lower = hasLimit ? reader.ReadVector3() : Vector3.Zero;
                    var upper = hasLimit ? reader.ReadVector3() : Vector3.Zero;
                    links.Add(new IkLink(boneIndex, hasLimit, lower, upper));
                }

                ik = new IkConstraint(effector, iterations, angleLimit, links);
            }

            model.AddBone(new Bone(
                name,
                englishName,
                origin,
                parentBoneIndex,
                layerIndex,
                flags,
                connectionIndex,
                connectionOffset,
                inherentParentIndex,
                inherentCoefficient,
                axisConstraint,
                localAxes,
                key,
                ik));
        }
    }

    private static void ReadMorphs(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadLengthPrefixedString(encoding);
            var englishName = reader.ReadLengthPrefixedString(encoding);
            var category = (MorphCategory)reader.ReadByte();
            var type = (MorphType)reader.ReadByte();
            var offsetCount = reader.ReadInt32();
            var offsets = new List<MorphOffset>(offsetCount);
            for (var offsetIndex = 0; offsetIndex < offsetCount; offsetIndex++)
            {
                offsets.Add(ReadMorphOffset(reader, header, type));
            }

            model.AddMorph(new Morph(name, englishName, category, type, offsets));
        }
    }

    private static MorphOffset ReadMorphOffset(BinarySpanReader reader, PmxHeader header, MorphType type)
    {
        return type switch
        {
            MorphType.Group => new GroupMorphOffset(reader.ReadSizedIndex(header.MorphIndexSize), reader.ReadSingle()),
            MorphType.Vertex => new VertexMorphOffset(reader.ReadSizedVertexIndex(header.VertexIndexSize), reader.ReadVector3()),
            MorphType.Bone => new BoneMorphOffset(reader.ReadSizedIndex(header.BoneIndexSize), reader.ReadVector3(), reader.ReadQuaternionXyzw()),
            MorphType.Uv or MorphType.Uv1 or MorphType.Uv2 or MorphType.Uv3 or MorphType.Uv4 => new UvMorphOffset(
                reader.ReadSizedVertexIndex(header.VertexIndexSize),
                reader.ReadVector4()),
            MorphType.Material => new MaterialMorphOffset(
                reader.ReadSizedIndex(header.MaterialIndexSize),
                reader.ReadByte() != 0,
                reader.ReadVector4(),
                reader.ReadVector3(),
                reader.ReadSingle(),
                reader.ReadVector3(),
                reader.ReadVector4(),
                reader.ReadSingle(),
                reader.ReadVector4(),
                reader.ReadVector4(),
                reader.ReadVector4()),
            MorphType.Flip => new FlipMorphOffset(reader.ReadSizedIndex(header.MorphIndexSize), reader.ReadSingle()),
            MorphType.Impulse => new ImpulseMorphOffset(reader.ReadSizedIndex(header.RigidBodyIndexSize), reader.ReadByte() != 0, reader.ReadVector3(), reader.ReadVector3()),
            _ => throw new MmdFormatException($"Unsupported PMX morph type {type}.")
        };
    }

    private static void ReadLabels(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadLengthPrefixedString(encoding);
            var englishName = reader.ReadLengthPrefixedString(encoding);
            var special = reader.ReadByte() != 0;
            var itemCount = reader.ReadInt32();
            var items = new List<ModelLabelItem>(itemCount);
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                var type = (LabelItemType)reader.ReadByte();
                var index = type == LabelItemType.Bone
                    ? reader.ReadSizedIndex(header.BoneIndexSize)
                    : reader.ReadSizedIndex(header.MorphIndexSize);
                items.Add(new ModelLabelItem(type, index));
            }

            model.AddLabel(new ModelLabel(name, englishName, special, items));
        }
    }

    private static void ReadRigidBodies(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddRigidBody(new RigidBody(
                reader.ReadLengthPrefixedString(encoding),
                reader.ReadLengthPrefixedString(encoding),
                reader.ReadSizedIndex(header.BoneIndexSize),
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

    private static void ReadJoints(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            model.AddJoint(new Joint(
                reader.ReadLengthPrefixedString(encoding),
                reader.ReadLengthPrefixedString(encoding),
                (JointType)reader.ReadByte(),
                reader.ReadSizedIndex(header.RigidBodyIndexSize),
                reader.ReadSizedIndex(header.RigidBodyIndexSize),
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

    private static void ReadSoftBodies(BinarySpanReader reader, PmxHeader header, Encoding encoding, MmdModel model)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadLengthPrefixedString(encoding);
            var englishName = reader.ReadLengthPrefixedString(encoding);
            var shapeType = reader.ReadByte();
            var materialIndex = reader.ReadSizedIndex(header.MaterialIndexSize);
            var groupId = reader.ReadByte();
            var collisionMask = reader.ReadUInt16();
            reader.Skip(1 + (4 * sizeof(float)) + (3 * sizeof(int)) + (12 * sizeof(float)));
            var anchorCount = reader.ReadInt32();
            for (var anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
            {
                reader.ReadSizedIndex(header.RigidBodyIndexSize);
                reader.ReadSizedVertexIndex(header.VertexIndexSize);
                reader.ReadByte();
            }

            var pinVertexCount = reader.ReadInt32();
            for (var pinIndex = 0; pinIndex < pinVertexCount; pinIndex++)
            {
                reader.ReadSizedVertexIndex(header.VertexIndexSize);
            }

            model.AddSoftBody(new SoftBody(name, englishName, materialIndex, groupId, collisionMask, pinVertexCount, anchorCount));
            _ = shapeType;
        }
    }
}
