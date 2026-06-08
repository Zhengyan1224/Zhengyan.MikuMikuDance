using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.Binary;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Pmx;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class PmmLegacyProjectReader
{
    private const string PmmV1Signature = "Polygon Movie maker 0001";
    private const string PmmV2Signature = "Polygon Movie maker 0002";
    private const int SignatureSize = 30;
    private const int PathSize = 256;
    private const int AccessoryNameSize = 100;
    private const int MaxReasonableCount = 1_000_000;

    public PmmLegacyDocument Inspect(ReadOnlyMemory<byte> data, string? projectPath = null)
    {
        var reader = new BinarySpanReader(data);
        var signature = reader.ReadFixedString(SignatureSize, EncodingProvider.ShiftJis);
        var version = signature switch
        {
            var value when value.StartsWith(PmmV2Signature, StringComparison.Ordinal) => 2,
            var value when value.StartsWith(PmmV1Signature, StringComparison.Ordinal) => 1,
            _ => throw new MmdFormatException($"Invalid PMM signature '{signature}'.")
        };

        return ReadDocument(reader, version, new PmmReadContext(projectPath));
    }

    public PmmLegacyDocument Inspect(Stream stream, string? projectPath = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Inspect(memory.ToArray(), projectPath);
    }

    public MmdProject Read(ReadOnlyMemory<byte> data, string? projectPath = null, bool loadResources = true)
    {
        return ToProject(Inspect(data, projectPath), projectPath, loadResources);
    }

    public MmdProject Read(Stream stream, string? projectPath = null, bool loadResources = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray(), projectPath, loadResources);
    }

    public MmdProject ReadFile(string path, bool loadResources = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Read(stream, path, loadResources);
    }

    private static PmmLegacyDocument ReadDocument(BinarySpanReader reader, int version, PmmReadContext context)
    {
        var outputWidth = reader.ReadInt32();
        var outputHeight = reader.ReadInt32();
        var timelineWidth = reader.ReadInt32();
        var cameraFieldOfView = reader.ReadSingle();

        _ = reader.ReadByte(); // editing CLA
        _ = reader.ReadByte(); // camera panel expanded
        _ = reader.ReadByte(); // light panel expanded
        _ = reader.ReadByte(); // accessory panel expanded
        _ = reader.ReadByte(); // bone panel expanded
        _ = reader.ReadByte(); // morph panel expanded
        if (version > 1)
        {
            _ = reader.ReadByte(); // self-shadow panel expanded
        }

        var (selectedModelIndex, models) = ReadModels(reader, version, context);
        var camera = ReadCamera(reader, version);
        var light = ReadLight(reader);
        var selectedAccessoryIndex = reader.ReadByte();
        _ = reader.ReadInt32(); // horizontal accessory scroll
        var accessories = ReadAccessories(reader, version);

        var currentFrameIndex = reader.ReadInt32();
        _ = reader.ReadInt32(); // horizontal timeline scroll
        _ = reader.ReadInt32(); // horizontal timeline thumb
        _ = reader.ReadInt32(); // editing mode
        _ = reader.ReadByte(); // camera look mode
        var loopEnabled = reader.ReadByte() != 0;
        var beginFrameEnabled = reader.ReadByte() != 0;
        var endFrameEnabled = reader.ReadByte() != 0;
        var beginFrameIndex = reader.ReadInt32();
        var endFrameIndex = reader.ReadInt32();
        var audioEnabled = reader.ReadByte() != 0;
        var audioPath = ReadFixedString(reader, PathSize);
        _ = reader.ReadInt32(); // background video offset X
        _ = reader.ReadInt32(); // background video offset Y
        _ = reader.ReadSingle(); // background video scale
        var backgroundVideoPath = ReadFixedString(reader, PathSize);
        var backgroundVideoEnabled = reader.ReadInt32() != 0;
        _ = reader.ReadInt32(); // background image offset X
        _ = reader.ReadInt32(); // background image offset Y
        _ = reader.ReadSingle(); // background image scale
        var backgroundImagePath = ReadFixedString(reader, PathSize);
        var backgroundImageEnabled = reader.ReadByte() != 0;
        _ = reader.ReadByte(); // information shown
        var gridAndAxisShown = reader.ReadByte() != 0;
        var groundShadowShown = reader.ReadByte() != 0;
        var preferredFps = reader.ReadSingle();
        _ = reader.ReadInt32(); // screen capture mode
        _ = reader.ReadInt32(); // accessory index after models
        _ = reader.ReadSingle(); // ground shadow brightness

        PmmLegacyGravity? gravity = null;
        PmmLegacySelfShadow? selfShadow = null;
        var edgeColor = Vector3.Zero;
        if (version > 1)
        {
            _ = reader.ReadByte(); // translucent ground shadow
            _ = reader.ReadByte(); // physics simulation mode
            gravity = ReadGravity(reader);
            selfShadow = ReadSelfShadow(reader);
            edgeColor = new Vector3(
                reader.ReadInt32() / 255f,
                reader.ReadInt32() / 255f,
                reader.ReadInt32() / 255f);
            _ = reader.ReadByte(); // black background
            _ = reader.ReadInt32(); // camera look-at model index
            _ = reader.ReadInt32(); // camera look-at model bone index
            reader.Skip(sizeof(float) * 16);
            _ = reader.ReadByte(); // following look-at
            _ = reader.ReadByte(); // unknown boolean
            _ = reader.ReadByte(); // physics ground
            _ = reader.ReadInt32(); // current frame in text field
            if (!reader.EndOfBuffer && reader.ReadByte() != 0)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadInt32();
                }
            }
        }

        return new PmmLegacyDocument
        {
            Version = version,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight,
            TimelineWidth = timelineWidth,
            CameraFieldOfView = cameraFieldOfView,
            SelectedModelIndex = selectedModelIndex,
            SelectedAccessoryIndex = selectedAccessoryIndex,
            CurrentFrameIndex = currentFrameIndex,
            BeginFrameIndex = beginFrameIndex,
            EndFrameIndex = endFrameIndex,
            BeginFrameEnabled = beginFrameEnabled,
            EndFrameEnabled = endFrameEnabled,
            LoopEnabled = loopEnabled,
            AudioEnabled = audioEnabled,
            AudioPath = audioPath,
            BackgroundVideoEnabled = backgroundVideoEnabled,
            BackgroundVideoPath = backgroundVideoPath,
            BackgroundImageEnabled = backgroundImageEnabled,
            BackgroundImagePath = backgroundImagePath,
            PreferredFps = preferredFps,
            GridAndAxisShown = gridAndAxisShown,
            GroundShadowShown = groundShadowShown,
            EdgeColor = edgeColor,
            Camera = camera,
            Light = light,
            Gravity = gravity,
            SelfShadow = selfShadow,
            Models = models,
            Accessories = accessories
        };
    }

    private static (int SelectedModelIndex, IReadOnlyList<PmmLegacyModel> Models) ReadModels(
        BinarySpanReader reader,
        int version,
        PmmReadContext context)
    {
        var selectedModelIndex = reader.ReadByte();
        var count = reader.ReadByte();
        var models = new List<PmmLegacyModel>(count);
        if (version == 1)
        {
            reader.Skip(20 * count);
        }

        for (var i = 0; i < count; i++)
        {
            models.Add(ReadModel(reader, version, context));
        }

        return (selectedModelIndex, models);
    }

    private static PmmLegacyModel ReadModel(BinarySpanReader reader, int version, PmmReadContext context)
    {
        var index = reader.ReadByte();
        if (version == 1)
        {
            return ReadModelV1(reader, index, version, context);
        }

        return ReadModelV2(reader, index, version);
    }

    private static PmmLegacyModel ReadModelV2(BinarySpanReader reader, int index, int version)
    {
        var name = ReadVariableString(reader);
        var englishName = ReadVariableString(reader);
        var path = ReadFixedString(reader, PathSize);
        _ = reader.ReadByte(); // number of fixed tracks

        var boneNames = ReadVariableStringList(reader, ReadCount(reader, "model bones"));
        var morphNames = ReadVariableStringList(reader, ReadCount(reader, "model morphs"));
        var constraintBoneIndices = ReadInt32List(reader, ReadCount(reader, "model constraint bones"));
        var outsideParentSubjectBoneIndices = ReadInt32List(reader, ReadCount(reader, "model outside-parent bones"));
        var drawOrderIndex = reader.ReadByte();
        var visible = reader.ReadByte() != 0;
        _ = reader.ReadInt32(); // selected bone index
        _ = reader.ReadInt32(); // selected eyebrow morph index
        _ = reader.ReadInt32(); // selected eye morph index
        _ = reader.ReadInt32(); // selected lip morph index
        _ = reader.ReadInt32(); // selected other morph index
        reader.Skip(reader.ReadByte());
        _ = reader.ReadInt32(); // vertical scroll
        var lastFrameIndex = reader.ReadInt32();

        var boneKeyframes = new List<PmmLegacyBoneKeyframe>();
        for (var i = 0; i < boneNames.Count; i++)
        {
            boneKeyframes.Add(ReadBoneKeyframe(reader, includeIndex: false, i, boneNames, version));
        }

        var boneKeyframeCount = ReadCount(reader, "model bone keyframes");
        for (var i = 0; i < boneKeyframeCount; i++)
        {
            boneKeyframes.Add(ReadBoneKeyframe(reader, includeIndex: true, -1, boneNames, version));
        }

        var morphKeyframes = new List<PmmLegacyMorphKeyframe>();
        for (var i = 0; i < morphNames.Count; i++)
        {
            morphKeyframes.Add(ReadMorphKeyframe(reader, includeIndex: false, i, morphNames));
        }

        var morphKeyframeCount = ReadCount(reader, "model morph keyframes");
        for (var i = 0; i < morphKeyframeCount; i++)
        {
            morphKeyframes.Add(ReadMorphKeyframe(reader, includeIndex: true, -1, morphNames));
        }

        var modelKeyframes = new List<PmmLegacyModelKeyframe>
        {
            ReadModelKeyframe(
                reader,
                includeIndex: false,
                constraintBoneIndices,
                outsideParentSubjectBoneIndices.Count,
                boneNames,
                version)
        };
        var modelKeyframeCount = ReadCount(reader, "model visibility keyframes");
        for (var i = 0; i < modelKeyframeCount; i++)
        {
            modelKeyframes.Add(ReadModelKeyframe(
                reader,
                includeIndex: true,
                constraintBoneIndices,
                outsideParentSubjectBoneIndices.Count,
                boneNames,
                version));
        }

        for (var i = 0; i < boneNames.Count; i++)
        {
            ReadBoneState(reader, version);
        }

        reader.Skip(sizeof(float) * morphNames.Count);
        reader.Skip(constraintBoneIndices.Count);
        reader.Skip(16 * outsideParentSubjectBoneIndices.Count);

        var blendEnabled = reader.ReadByte() != 0;
        var edgeWidth = reader.ReadSingle();
        var selfShadowEnabled = reader.ReadByte() != 0;
        var transformOrderIndex = reader.ReadByte();

        return new PmmLegacyModel
        {
            Index = index,
            Name = name,
            EnglishName = englishName,
            Path = path,
            BoneNames = boneNames,
            MorphNames = morphNames,
            ConstraintBoneIndices = constraintBoneIndices,
            OutsideParentSubjectBoneIndices = outsideParentSubjectBoneIndices,
            DrawOrderIndex = drawOrderIndex,
            TransformOrderIndex = transformOrderIndex,
            Visible = visible,
            LastFrameIndex = lastFrameIndex,
            EdgeWidth = edgeWidth,
            BlendEnabled = blendEnabled,
            SelfShadowEnabled = selfShadowEnabled,
            BoneKeyframes = boneKeyframes,
            MorphKeyframes = morphKeyframes,
            ModelKeyframes = modelKeyframes
        };
    }

    private static PmmLegacyModel ReadModelV1(
        BinarySpanReader reader,
        int index,
        int version,
        PmmReadContext context)
    {
        var name = ReadFixedString(reader, 20);
        var path = ReadFixedString(reader, PathSize);
        var loadedModel = context.LoadRequiredModel(path);
        var boneNames = loadedModel.Bones.Select(item => item.Name).ToArray();
        var morphNames = loadedModel.Morphs.Select(item => item.Name).ToArray();
        var constraintBoneIndices = loadedModel.Bones
            .Select((bone, boneIndex) => (bone, boneIndex))
            .Where(item => item.bone.Ik is not null)
            .Select(item => item.boneIndex)
            .ToArray();
        var drawOrderIndex = reader.ReadByte();
        var visible = reader.ReadByte() != 0;
        _ = reader.ReadInt32(); // selected bone index
        _ = reader.ReadInt32(); // selected eyebrow morph index
        _ = reader.ReadInt32(); // selected eye morph index
        _ = reader.ReadInt32(); // selected lip morph index
        _ = reader.ReadInt32(); // selected other morph index
        reader.Skip(reader.ReadByte());
        _ = reader.ReadInt32(); // vertical scroll
        var lastFrameIndex = reader.ReadInt32();

        var boneKeyframes = new List<PmmLegacyBoneKeyframe>();
        for (var i = 0; i < boneNames.Length; i++)
        {
            boneKeyframes.Add(ReadBoneKeyframe(reader, includeIndex: false, i, boneNames, version));
        }

        var boneKeyframeCount = ReadCount(reader, "model bone keyframes");
        for (var i = 0; i < boneKeyframeCount; i++)
        {
            boneKeyframes.Add(ReadBoneKeyframe(reader, includeIndex: true, -1, boneNames, version));
        }

        var morphKeyframes = new List<PmmLegacyMorphKeyframe>();
        for (var i = 0; i < morphNames.Length; i++)
        {
            morphKeyframes.Add(ReadMorphKeyframe(reader, includeIndex: false, i, morphNames));
        }

        var morphKeyframeCount = ReadCount(reader, "model morph keyframes");
        for (var i = 0; i < morphKeyframeCount; i++)
        {
            morphKeyframes.Add(ReadMorphKeyframe(reader, includeIndex: true, -1, morphNames));
        }

        var modelKeyframes = new List<PmmLegacyModelKeyframe>
        {
            ReadModelKeyframe(
                reader,
                includeIndex: false,
                constraintBoneIndices,
                outsideParentSubjectBoneCount: 0,
                boneNames,
                version)
        };
        var modelKeyframeCount = ReadCount(reader, "model visibility keyframes");
        for (var i = 0; i < modelKeyframeCount; i++)
        {
            modelKeyframes.Add(ReadModelKeyframe(
                reader,
                includeIndex: true,
                constraintBoneIndices,
                outsideParentSubjectBoneCount: 0,
                boneNames,
                version));
        }

        for (var i = 0; i < boneNames.Length; i++)
        {
            ReadBoneState(reader, version);
        }

        reader.Skip(sizeof(float) * morphNames.Length);
        reader.Skip(constraintBoneIndices.Length);

        return new PmmLegacyModel
        {
            Index = index,
            Name = string.IsNullOrWhiteSpace(name) ? loadedModel.Name : name,
            EnglishName = loadedModel.EnglishName,
            Path = path,
            BoneNames = boneNames,
            MorphNames = morphNames,
            ConstraintBoneIndices = constraintBoneIndices,
            OutsideParentSubjectBoneIndices = [],
            DrawOrderIndex = drawOrderIndex,
            TransformOrderIndex = 0,
            Visible = visible,
            LastFrameIndex = lastFrameIndex,
            EdgeWidth = 0,
            BlendEnabled = false,
            SelfShadowEnabled = false,
            BoneKeyframes = boneKeyframes,
            MorphKeyframes = morphKeyframes,
            ModelKeyframes = modelKeyframes
        };
    }

    private static PmmLegacyCamera ReadCamera(BinarySpanReader reader, int version)
    {
        var keyframes = new List<PmmLegacyCameraKeyframe>
        {
            ReadCameraKeyframe(reader, includeIndex: false, version)
        };
        var count = ReadCount(reader, "camera keyframes");
        for (var i = 0; i < count; i++)
        {
            keyframes.Add(ReadCameraKeyframe(reader, includeIndex: true, version));
        }

        return new PmmLegacyCamera
        {
            LookAt = reader.ReadVector3(),
            Position = reader.ReadVector3(),
            Angle = reader.ReadVector3(),
            PerspectiveEnabled = reader.ReadByte() == 0,
            Keyframes = keyframes
        };
    }

    private static PmmLegacyLight ReadLight(BinarySpanReader reader)
    {
        var keyframes = new List<PmmLegacyLightKeyframe>
        {
            ReadLightKeyframe(reader, includeIndex: false)
        };
        var count = ReadCount(reader, "light keyframes");
        for (var i = 0; i < count; i++)
        {
            keyframes.Add(ReadLightKeyframe(reader, includeIndex: true));
        }

        return new PmmLegacyLight
        {
            Color = reader.ReadVector3(),
            Direction = reader.ReadVector3(),
            Keyframes = keyframes
        };
    }

    private static IReadOnlyList<PmmLegacyAccessory> ReadAccessories(BinarySpanReader reader, int version)
    {
        var count = reader.ReadByte();
        for (var i = 0; i < count; i++)
        {
            _ = ReadFixedString(reader, AccessoryNameSize);
        }

        var accessories = new List<PmmLegacyAccessory>(count);
        for (var i = 0; i < count; i++)
        {
            accessories.Add(ReadAccessory(reader, version));
        }

        return accessories;
    }

    private static PmmLegacyAccessory ReadAccessory(BinarySpanReader reader, int version)
    {
        var index = reader.ReadByte();
        var name = ReadFixedString(reader, AccessoryNameSize);
        var path = ReadFixedString(reader, PathSize);
        var drawOrderIndex = reader.ReadByte();
        var keyframes = new List<PmmLegacyAccessoryKeyframe>
        {
            ReadAccessoryKeyframe(reader, name, includeIndex: false)
        };
        var count = ReadCount(reader, "accessory keyframes");
        for (var i = 0; i < count; i++)
        {
            keyframes.Add(ReadAccessoryKeyframe(reader, name, includeIndex: true));
        }

        var (opacity, visible) = UnpackOpacityAndVisible(reader.ReadByte());
        var parentModelIndex = reader.ReadInt32();
        var parentModelBoneIndex = reader.ReadInt32();
        var translation = reader.ReadVector3();
        var scale = reader.ReadSingle();
        var orientation = reader.ReadVector3();
        var shadowEnabled = reader.ReadByte() != 0;
        var addBlendEnabled = version > 1 && reader.ReadByte() != 0;

        return new PmmLegacyAccessory
        {
            Index = index,
            Name = name,
            Path = path,
            DrawOrderIndex = drawOrderIndex,
            Visible = visible,
            Opacity = opacity,
            Translation = translation,
            Orientation = orientation,
            Scale = scale,
            ParentModelIndex = parentModelIndex,
            ParentModelBoneIndex = parentModelBoneIndex,
            ShadowEnabled = shadowEnabled,
            AddBlendEnabled = addBlendEnabled,
            Keyframes = keyframes
        };
    }

    private static PmmLegacyGravity ReadGravity(BinarySpanReader reader)
    {
        var acceleration = reader.ReadSingle();
        var noise = reader.ReadInt32();
        var direction = reader.ReadVector3();
        var noiseEnabled = reader.ReadByte() != 0;
        ReadGravityKeyframe(reader, includeIndex: false);
        var count = ReadCount(reader, "gravity keyframes");
        for (var i = 0; i < count; i++)
        {
            ReadGravityKeyframe(reader, includeIndex: true);
        }

        return new PmmLegacyGravity
        {
            Acceleration = acceleration,
            Noise = noise,
            Direction = direction,
            NoiseEnabled = noiseEnabled
        };
    }

    private static PmmLegacySelfShadow ReadSelfShadow(BinarySpanReader reader)
    {
        var enabled = reader.ReadByte() != 0;
        var distance = reader.ReadSingle();
        var keyframes = new List<PmmLegacySelfShadowKeyframe>
        {
            ReadSelfShadowKeyframe(reader, includeIndex: false)
        };
        var count = ReadCount(reader, "self-shadow keyframes");
        for (var i = 0; i < count; i++)
        {
            keyframes.Add(ReadSelfShadowKeyframe(reader, includeIndex: true));
        }

        return new PmmLegacySelfShadow
        {
            Enabled = enabled,
            Distance = distance,
            Keyframes = keyframes
        };
    }

    private static PmmLegacyBoneKeyframe ReadBoneKeyframe(
        BinarySpanReader reader,
        bool includeIndex,
        int defaultIndex,
        IReadOnlyList<string> boneNames,
        int version)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var boneIndex = includeIndex ? baseKeyframe.ObjectIndex : defaultIndex;
        var interpolation = new PmmLegacyBoneInterpolation(
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader));
        var translation = reader.ReadVector3();
        var orientation = reader.ReadQuaternionXyzw();
        var selected = reader.ReadByte() != 0;
        var physicsSimulationEnabled = version <= 1 || reader.ReadByte() == 0;
        return new PmmLegacyBoneKeyframe(
            ResolveIndexedName(boneNames, boneIndex, "Bone"),
            baseKeyframe.FrameIndex,
            translation,
            orientation,
            interpolation,
            physicsSimulationEnabled,
            selected);
    }

    private static PmmLegacyMorphKeyframe ReadMorphKeyframe(
        BinarySpanReader reader,
        bool includeIndex,
        int defaultIndex,
        IReadOnlyList<string> morphNames)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var morphIndex = includeIndex ? baseKeyframe.ObjectIndex : defaultIndex;
        var weight = reader.ReadSingle();
        var selected = reader.ReadByte() != 0;
        return new PmmLegacyMorphKeyframe(
            ResolveIndexedName(morphNames, morphIndex, "Morph"),
            baseKeyframe.FrameIndex,
            weight,
            selected);
    }

    private static PmmLegacyModelKeyframe ReadModelKeyframe(
        BinarySpanReader reader,
        bool includeIndex,
        IReadOnlyList<int> constraintBoneIndices,
        int outsideParentSubjectBoneCount,
        IReadOnlyList<string> boneNames,
        int version)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var visible = reader.ReadByte() != 0;
        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var i = 0; i < constraintBoneIndices.Count; i++)
        {
            var name = ResolveIndexedName(boneNames, constraintBoneIndices[i], "Constraint");
            states[name] = reader.ReadByte() != 0;
        }

        if (version > 1)
        {
            reader.Skip(8 * outsideParentSubjectBoneCount);
        }

        var selected = reader.ReadByte() != 0;
        return new PmmLegacyModelKeyframe(baseKeyframe.FrameIndex, visible, states, selected);
    }

    private static PmmLegacyAccessoryKeyframe ReadAccessoryKeyframe(
        BinarySpanReader reader,
        string accessoryName,
        bool includeIndex)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var (opacity, visible) = UnpackOpacityAndVisible(reader.ReadByte());
        var parentModelIndex = reader.ReadInt32();
        var parentModelBoneIndex = reader.ReadInt32();
        var translation = reader.ReadVector3();
        var orientation = reader.ReadVector3();
        var scale = reader.ReadSingle();
        var shadowEnabled = reader.ReadByte() != 0;
        var selected = reader.ReadByte() != 0;
        return new PmmLegacyAccessoryKeyframe(
            accessoryName,
            baseKeyframe.FrameIndex,
            visible,
            opacity,
            parentModelIndex,
            parentModelBoneIndex,
            translation,
            orientation,
            scale,
            shadowEnabled,
            selected);
    }

    private static PmmLegacyCameraKeyframe ReadCameraKeyframe(
        BinarySpanReader reader,
        bool includeIndex,
        int version)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var distance = reader.ReadSingle();
        var lookAt = reader.ReadVector3();
        var angle = reader.ReadVector3();
        var parentModelIndex = -1;
        var parentModelBoneIndex = -1;
        if (version > 1)
        {
            parentModelIndex = reader.ReadInt32();
            parentModelBoneIndex = reader.ReadInt32();
        }

        var interpolation = new PmmLegacyCameraInterpolation(
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader),
            ReadBezierCurve(reader));
        var perspectiveEnabled = reader.ReadByte() == 0;
        var fieldOfView = reader.ReadInt32();
        var selected = reader.ReadByte() != 0;
        return new PmmLegacyCameraKeyframe(
            baseKeyframe.FrameIndex,
            distance,
            lookAt,
            angle,
            parentModelIndex,
            parentModelBoneIndex,
            interpolation,
            perspectiveEnabled,
            fieldOfView,
            selected);
    }

    private static PmmLegacyLightKeyframe ReadLightKeyframe(BinarySpanReader reader, bool includeIndex)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var color = reader.ReadVector3();
        var direction = reader.ReadVector3();
        var selected = reader.ReadByte() != 0;
        return new PmmLegacyLightKeyframe(baseKeyframe.FrameIndex, color, direction, selected);
    }

    private static void ReadGravityKeyframe(BinarySpanReader reader, bool includeIndex)
    {
        _ = ReadBaseKeyframe(reader, includeIndex);
        _ = reader.ReadByte();
        _ = reader.ReadInt32();
        _ = reader.ReadSingle();
        _ = reader.ReadVector3();
        _ = reader.ReadByte();
    }

    private static PmmLegacySelfShadowKeyframe ReadSelfShadowKeyframe(BinarySpanReader reader, bool includeIndex)
    {
        var baseKeyframe = ReadBaseKeyframe(reader, includeIndex);
        var mode = reader.ReadByte();
        var distance = reader.ReadSingle();
        var selected = reader.ReadByte() != 0;
        return new PmmLegacySelfShadowKeyframe(baseKeyframe.FrameIndex, mode, distance, selected);
    }

    private static BaseKeyframe ReadBaseKeyframe(BinarySpanReader reader, bool includeIndex)
    {
        var objectIndex = includeIndex ? reader.ReadInt32() : -1;
        var frameIndex = reader.ReadInt32();
        _ = reader.ReadInt32(); // previous keyframe index
        _ = reader.ReadInt32(); // next keyframe index
        return new BaseKeyframe(objectIndex, frameIndex);
    }

    private static PmmLegacyBezierCurve ReadBezierCurve(BinarySpanReader reader)
    {
        return new PmmLegacyBezierCurve(
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte());
    }

    private static void ReadBoneState(BinarySpanReader reader, int version)
    {
        reader.Skip((sizeof(float) * 3) + (sizeof(float) * 4));
        reader.Skip(version > 1 ? 3 : 6);
    }

    private static (float Opacity, bool Visible) UnpackOpacityAndVisible(byte packed)
    {
        var opacity = (100 - ((packed & 0xfe) >> 1)) * 0.01f;
        return (Math.Clamp(opacity, 0, 1), (packed & 0x1) != 0);
    }

    private static string ReadFixedString(BinarySpanReader reader, int length)
    {
        return reader.ReadFixedString(length, EncodingProvider.ShiftJis);
    }

    private static string ReadVariableString(BinarySpanReader reader)
    {
        var length = reader.ReadByte();
        return length == 0 ? string.Empty : EncodingProvider.ShiftJis.GetString(reader.ReadBytes(length));
    }

    private static IReadOnlyList<string> ReadVariableStringList(BinarySpanReader reader, int count)
    {
        var values = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(ReadVariableString(reader));
        }

        return values;
    }

    private static IReadOnlyList<int> ReadInt32List(BinarySpanReader reader, int count)
    {
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(reader.ReadInt32());
        }

        return values;
    }

    private static int ReadCount(BinarySpanReader reader, string name)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > MaxReasonableCount)
        {
            throw new MmdFormatException($"Invalid PMM {name} count {count} at byte offset {reader.Position - sizeof(int)}.");
        }

        return count;
    }

    private static string ResolveIndexedName(IReadOnlyList<string> names, int index, string fallbackPrefix)
    {
        if (index >= 0 && index < names.Count && !string.IsNullOrWhiteSpace(names[index]))
        {
            return names[index];
        }

        return $"{fallbackPrefix}{Math.Max(0, index)}";
    }

    private static MmdProject ToProject(PmmLegacyDocument document, string? projectPath, bool loadResources)
    {
        var project = new MmdProject
        {
            Name = string.IsNullOrWhiteSpace(projectPath)
                ? "PMM Project"
                : Path.GetFileNameWithoutExtension(projectPath)
        };
        project.Timeline.Seek(document.CurrentFrameIndex);
        project.Timeline.LoopEnabled = document.LoopEnabled;
        project.Timeline.SetPlaybackRange(
            document.BeginFrameEnabled ? document.BeginFrameIndex : 0,
            document.EndFrameEnabled ? document.EndFrameIndex : CalculateDuration(document));
        project.Background.VideoSource = CreateSourceUri(
            document.BackgroundVideoPath,
            ResolvePath(document.BackgroundVideoPath, projectPath));
        project.Background.VideoEnabled = document.BackgroundVideoEnabled;
        project.Background.ImageSource = CreateSourceUri(
            document.BackgroundImagePath,
            ResolvePath(document.BackgroundImagePath, projectPath));
        project.Background.ImageEnabled = document.BackgroundImageEnabled;
        project.Background.Normalize();

        foreach (var model in document.Models.OrderBy(item => item.DrawOrderIndex))
        {
            AddModel(project, model, projectPath, loadResources);
        }

        foreach (var accessory in document.Accessories.OrderBy(item => item.DrawOrderIndex))
        {
            AddAccessory(project, document, accessory, projectPath, loadResources);
        }

        ApplyCameraParent(project.Camera, document);
        var motion = BuildMotion(document, projectPath);
        project.AddMotion(motion);
        SceneMotionApplier.Apply(project, MotionSampler.Sample(motion, document.CurrentFrameIndex));
        return project;
    }

    private static void AddModel(
        MmdProject project,
        PmmLegacyModel source,
        string? projectPath,
        bool loadResources)
    {
        var resolvedPath = ResolvePath(source.Path, projectPath);
        var model = TryLoadModel(resolvedPath, loadResources)
            ?? CreatePlaceholderModel(source, resolvedPath);
        model.Source = CreateSourceUri(source.Path, resolvedPath);
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = source.Name;
        }

        if (string.IsNullOrWhiteSpace(model.EnglishName))
        {
            model.EnglishName = source.EnglishName;
        }

        var instance = project.AddModel(model);
        instance.Name = string.IsNullOrWhiteSpace(source.Name) ? model.Name : source.Name;
        instance.Visible = source.Visible;
        instance.TransformOrder = source.TransformOrderIndex;
    }

    private static void AddAccessory(
        MmdProject project,
        PmmLegacyDocument document,
        PmmLegacyAccessory source,
        string? projectPath,
        bool loadResources)
    {
        var resolvedPath = ResolvePath(source.Path, projectPath);
        if (loadResources && !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath) &&
            string.Equals(Path.GetExtension(resolvedPath), ".x", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(resolvedPath);
            var mesh = new DirectXAccessoryReader().Read(stream, Path.GetFileName(resolvedPath)) with
            {
                Source = CreateSourceUri(source.Path, resolvedPath)
            };
            project.AddAccessoryMesh(mesh);
        }

        var accessory = new Accessory(string.IsNullOrWhiteSpace(source.Name)
            ? Path.GetFileNameWithoutExtension(source.Path)
            : source.Name)
        {
            Source = CreateSourceUri(source.Path, resolvedPath),
            Visible = source.Visible,
            Opacity = source.Opacity,
            Translation = source.Translation,
            Orientation = source.Orientation,
            Scale = source.Scale,
            ParentModelName = ResolveModelName(document, source.ParentModelIndex),
            ParentBoneName = ResolveBoneName(document, source.ParentModelIndex, source.ParentModelBoneIndex)
        };
        project.AddAccessory(accessory);
    }

    private static void ApplyCameraParent(Camera camera, PmmLegacyDocument document)
    {
        var initialFrame = document.Camera.Keyframes.FirstOrDefault(frame => frame.FrameIndex == 0);
        if (initialFrame is null)
        {
            return;
        }

        camera.ParentModelName = ResolveModelName(document, initialFrame.ParentModelIndex);
        camera.ParentBoneName = ResolveBoneName(document, initialFrame.ParentModelIndex, initialFrame.ParentModelBoneIndex);
    }

    private static MmdModel CreatePlaceholderModel(PmmLegacyModel source, string? resolvedPath)
    {
        var model = new MmdModel(ModelFormatFromPath(resolvedPath ?? source.Path))
        {
            Name = source.Name,
            EnglishName = source.EnglishName
        };

        for (var i = 0; i < source.BoneNames.Count; i++)
        {
            var flags = BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled;
            if (source.OutsideParentSubjectBoneIndices.Contains(i))
            {
                flags |= BoneFlags.OutsideParent;
            }

            model.AddBone(new Bone(
                source.BoneNames[i],
                string.Empty,
                Vector3.Zero,
                -1,
                0,
                flags,
                -1,
                Vector3.Zero,
                -1,
                0,
                null,
                null,
                0,
                null));
        }

        foreach (var morphName in source.MorphNames)
        {
            model.AddMorph(new Morph(morphName, string.Empty, MorphCategory.Other, MorphType.Vertex, []));
        }

        return model;
    }

    private static MmdModel? TryLoadModel(string? resolvedPath, bool loadResources)
    {
        if (!loadResources || string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        using var stream = File.OpenRead(resolvedPath);
        return Path.GetExtension(resolvedPath).ToLowerInvariant() switch
        {
            ".pmd" => new PmdModelReader().Read(stream),
            ".pmx" => new PmxModelReader().Read(stream),
            _ => null
        };
    }

    private static Motion BuildMotion(PmmLegacyDocument document, string? projectPath)
    {
        var motion = new Motion("PMM Timeline", MotionFormat.Unknown)
        {
            Source = string.IsNullOrWhiteSpace(projectPath) ? null : new Uri(Path.GetFullPath(projectPath))
        };
        motion.Annotations["pmm.version"] = document.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        motion.Annotations["pmm.outputWidth"] = document.OutputWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
        motion.Annotations["pmm.outputHeight"] = document.OutputHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        motion.Annotations["pmm.preferredFps"] = document.PreferredFps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(document.AudioPath))
        {
            motion.Annotations["pmm.audioPath"] = document.AudioPath;
        }

        foreach (var frame in document.Camera.Keyframes)
        {
            motion.Add(new CameraKeyframe(
                frame.FrameIndex,
                frame.LookAt,
                frame.Angle,
                frame.Distance,
                frame.FieldOfView,
                ConvertInterpolation(frame.Interpolation),
                frame.PerspectiveEnabled) with
            {
                IsSelected = frame.IsSelected
            });
        }

        foreach (var frame in document.Light.Keyframes)
        {
            motion.Add(new LightKeyframe(frame.FrameIndex, frame.Color, frame.Direction) with
            {
                IsSelected = frame.IsSelected
            });
        }

        if (document.SelfShadow is not null)
        {
            foreach (var frame in document.SelfShadow.Keyframes)
            {
                motion.Add(new SelfShadowKeyframe(frame.FrameIndex, frame.Mode, frame.Distance) with
                {
                    IsSelected = frame.IsSelected
                });
            }
        }

        foreach (var model in document.Models)
        {
            foreach (var frame in model.BoneKeyframes)
            {
                motion.Add(new BoneKeyframe(
                    frame.BoneName,
                    frame.FrameIndex,
                    frame.Translation,
                    frame.Orientation,
                    ConvertInterpolation(frame.Interpolation),
                    frame.PhysicsSimulationEnabled) with
                {
                    IsSelected = frame.IsSelected,
                    Annotations = BuildTargetAnnotations(model.Name)
                });
            }

            foreach (var frame in model.MorphKeyframes)
            {
                motion.Add(new MorphKeyframe(frame.MorphName, frame.FrameIndex, frame.Weight) with
                {
                    IsSelected = frame.IsSelected,
                    Annotations = BuildTargetAnnotations(model.Name)
                });
            }

            foreach (var frame in model.ModelKeyframes)
            {
                motion.Add(new ModelKeyframe(frame.FrameIndex, frame.Visible, frame.ConstraintStates) with
                {
                    IsSelected = frame.IsSelected,
                    Annotations = BuildTargetAnnotations(model.Name)
                });
            }
        }

        foreach (var accessory in document.Accessories)
        {
            foreach (var frame in accessory.Keyframes)
            {
                motion.Add(new AccessoryKeyframe(
                    frame.AccessoryName,
                    frame.FrameIndex,
                    frame.Visible,
                    frame.Translation,
                    frame.Orientation,
                    frame.Scale,
                    frame.Opacity,
                    ResolveModelName(document, frame.ParentModelIndex),
                    ResolveBoneName(document, frame.ParentModelIndex, frame.ParentModelBoneIndex)) with
                {
                    IsSelected = frame.IsSelected
                });
            }
        }

        return motion;
    }

    private static IReadOnlyDictionary<string, string> BuildTargetAnnotations(string modelName)
    {
        return string.IsNullOrWhiteSpace(modelName)
            ? new Dictionary<string, string>(0, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pmm.model"] = modelName
            };
    }

    private static BoneInterpolation ConvertInterpolation(PmmLegacyBoneInterpolation interpolation)
    {
        return new BoneInterpolation(
            ConvertCurve(interpolation.TranslationX),
            ConvertCurve(interpolation.TranslationY),
            ConvertCurve(interpolation.TranslationZ),
            ConvertCurve(interpolation.Orientation));
    }

    private static CameraInterpolation ConvertInterpolation(PmmLegacyCameraInterpolation interpolation)
    {
        return new CameraInterpolation(
            ConvertCurve(interpolation.LookAtX),
            ConvertCurve(interpolation.LookAtY),
            ConvertCurve(interpolation.LookAtZ),
            ConvertCurve(interpolation.Angle),
            ConvertCurve(interpolation.Distance),
            ConvertCurve(interpolation.FieldOfView));
    }

    private static BezierCurve ConvertCurve(PmmLegacyBezierCurve curve)
    {
        return new BezierCurve(
            new BezierControlPoint(curve.X0, curve.Y0),
            new BezierControlPoint(curve.X1, curve.Y1));
    }

    private static int CalculateDuration(PmmLegacyDocument document)
    {
        var max = document.CurrentFrameIndex;
        foreach (var keyframe in document.Camera.Keyframes)
        {
            max = Math.Max(max, keyframe.FrameIndex);
        }

        foreach (var keyframe in document.Light.Keyframes)
        {
            max = Math.Max(max, keyframe.FrameIndex);
        }

        if (document.SelfShadow is not null)
        {
            foreach (var keyframe in document.SelfShadow.Keyframes)
            {
                max = Math.Max(max, keyframe.FrameIndex);
            }
        }

        foreach (var model in document.Models)
        {
            max = Math.Max(max, model.LastFrameIndex);
            max = Math.Max(max, model.BoneKeyframes.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max());
            max = Math.Max(max, model.MorphKeyframes.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max());
            max = Math.Max(max, model.ModelKeyframes.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max());
        }

        foreach (var accessory in document.Accessories)
        {
            max = Math.Max(max, accessory.Keyframes.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max());
        }

        return max;
    }

    private static string? ResolveModelName(PmmLegacyDocument document, int modelIndex)
    {
        var model = document.Models.FirstOrDefault(item => item.Index == modelIndex);
        return string.IsNullOrWhiteSpace(model?.Name) ? null : model.Name;
    }

    private static string? ResolveBoneName(PmmLegacyDocument document, int modelIndex, int boneIndex)
    {
        var model = document.Models.FirstOrDefault(item => item.Index == modelIndex);
        if (model is null || boneIndex < 0 || boneIndex >= model.BoneNames.Count)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(model.BoneNames[boneIndex]) ? null : model.BoneNames[boneIndex];
    }

    private static ModelFormat ModelFormatFromPath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pmd" => ModelFormat.Pmd,
            ".pmx" => ModelFormat.Pmx,
            _ => ModelFormat.Unknown
        };
    }

    private static string? ResolvePath(string pmmPath, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(pmmPath))
        {
            return null;
        }

        if (Path.IsPathFullyQualified(pmmPath))
        {
            return Path.GetFullPath(pmmPath);
        }

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                return Path.GetFullPath(Path.Combine(baseDirectory, pmmPath));
            }
        }

        return pmmPath;
    }

    private static Uri? CreateSourceUri(string originalPath, string? resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(resolvedPath) && Path.IsPathFullyQualified(resolvedPath))
        {
            return new Uri(resolvedPath);
        }

        return new Uri(originalPath, UriKind.RelativeOrAbsolute);
    }

    private sealed class PmmReadContext(string? projectPath)
    {
        private readonly Dictionary<string, MmdModel> _loadedModels = new(StringComparer.OrdinalIgnoreCase);

        public MmdModel LoadRequiredModel(string pmmPath)
        {
            var resolvedPath = ResolvePath(pmmPath, projectPath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                throw new MmdFormatException("PMM v1 model records require a model path.");
            }

            if (!Path.IsPathFullyQualified(resolvedPath) && string.IsNullOrWhiteSpace(projectPath))
            {
                throw new MmdFormatException(
                    $"PMM v1 requires a project path to resolve relative model path '{pmmPath}'.");
            }

            var key = Path.IsPathFullyQualified(resolvedPath) ? Path.GetFullPath(resolvedPath) : resolvedPath;
            if (_loadedModels.TryGetValue(key, out var model))
            {
                return model;
            }

            model = TryLoadModel(key, loadResources: true)
                ?? throw new MmdFormatException($"Unable to load PMM v1 model resource '{pmmPath}'.");
            _loadedModels.Add(key, model);
            return model;
        }
    }

    private readonly record struct BaseKeyframe(int ObjectIndex, int FrameIndex);
}
