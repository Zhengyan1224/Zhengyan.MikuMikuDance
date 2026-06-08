using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Formats.Nmd;

public sealed class NmdMotionReader
{
    public Motion Read(ReadOnlyMemory<byte> data, int frameOffset = 0)
    {
        var document = ReadMotionDocument(data);
        var motion = new Motion(document.Name, MotionFormat.Nmd);
        foreach (var annotation in document.Annotations)
        {
            motion.Annotations[annotation.Key] = annotation.Value;
        }

        foreach (var bundle in document.Bundles)
        {
            switch (bundle.Kind)
            {
                case NmdBundleKind.Accessory:
                    ReadAccessoryBundle(motion, bundle.Payload, document.GlobalTracks, frameOffset);
                    break;
                case NmdBundleKind.Bone:
                    ReadBoneBundle(motion, bundle.Payload, frameOffset);
                    break;
                case NmdBundleKind.Camera:
                    ReadCameraBundle(motion, bundle.Payload, frameOffset);
                    break;
                case NmdBundleKind.Light:
                    ReadLightBundle(motion, bundle.Payload, frameOffset);
                    break;
                case NmdBundleKind.Model:
                    ReadModelBundle(motion, bundle.Payload, document.GlobalTracks, frameOffset);
                    break;
                case NmdBundleKind.Morph:
                    ReadMorphBundle(motion, bundle.Payload, frameOffset);
                    break;
                case NmdBundleKind.SelfShadow:
                    ReadSelfShadowBundle(motion, bundle.Payload, frameOffset);
                    break;
            }
        }

        return motion;
    }

    public Motion Read(Stream stream, int frameOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray(), frameOffset);
    }

    private static NmdMotionDocument ReadMotionDocument(ReadOnlyMemory<byte> data)
    {
        var name = string.Empty;
        var annotations = new Dictionary<string, string>(StringComparer.Ordinal);
        var globalTracks = new Dictionary<ulong, string>();
        var bundles = new List<NmdBundle>();
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    var annotation = ReadAnnotation(field.Payload);
                    annotations[annotation.Key] = annotation.Value;
                    break;
                case 2:
                    name = ProtoReader.ReadUtf8(field.Payload.Span);
                    break;
                case 4:
                    var track = ReadTrack(field.Payload);
                    globalTracks[track.Index] = track.Name;
                    break;
                case 5:
                    if (TryReadBundle(field.Payload, out var bundle))
                    {
                        bundles.Add(bundle);
                    }

                    break;
            }
        }

        return new NmdMotionDocument(name, annotations, globalTracks, bundles);
    }

    private static void ReadAccessoryBundle(
        Motion motion,
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> globalTracks,
        int frameOffset)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number != 2)
            {
                continue;
            }

            var keyframe = ReadAccessoryKeyframe(field.Payload, globalTracks, frameOffset);
            motion.Add(keyframe);
        }
    }

    private static AccessoryKeyframe ReadAccessoryKeyframe(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> globalTracks,
        int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var trackIndex = 0UL;
        var translation = Vector3.Zero;
        var orientation = Quaternion.Identity;
        var scale = 1f;
        var opacity = 1f;
        var visible = true;
        var effectParameters = new List<MotionEffectParameter>();
        string? parentModelName = null;
        string? parentBoneName = null;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    trackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 3:
                    translation = ReadVector3(field.Payload);
                    break;
                case 4:
                    orientation = ReadQuaternion(field.Payload);
                    break;
                case 5:
                    scale = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 6:
                    opacity = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 7:
                    effectParameters.Add(ReadEffectParameter(field.Payload, globalTracks));
                    break;
                case 8:
                    var binding = ReadModelBinding(field.Payload);
                    globalTracks.TryGetValue(binding.GlobalObjectTrackIndex, out parentModelName);
                    globalTracks.TryGetValue(binding.GlobalBoneTrackIndex, out parentBoneName);
                    break;
                case 9:
                    visible = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
            }
        }

        return new AccessoryKeyframe(
            ResolveTrackName(globalTracks, trackIndex, "Accessory"),
            common.FrameIndex,
            visible,
            translation,
            QuaternionToEuler(orientation),
            scale,
            opacity,
            parentModelName,
            parentBoneName)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations,
            EffectParameters = effectParameters
        };
    }

    private static void ReadBoneBundle(Motion motion, ReadOnlyMemory<byte> data, int frameOffset)
    {
        var localTracks = new Dictionary<ulong, string>();
        var keyframes = new List<ReadOnlyMemory<byte>>();
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 2:
                    var track = ReadTrack(field.Payload);
                    localTracks[track.Index] = track.Name;
                    break;
                case 3:
                    keyframes.Add(field.Payload);
                    break;
            }
        }

        foreach (var dataItem in keyframes)
        {
            motion.Add(ReadBoneKeyframe(dataItem, localTracks, frameOffset));
        }
    }

    private static BoneKeyframe ReadBoneKeyframe(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> localTracks,
        int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var trackIndex = 0UL;
        var translation = Vector3.Zero;
        var orientation = Quaternion.Identity;
        var interpolation = BoneInterpolation.Linear;
        var physicsEnabled = true;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    trackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 3:
                    translation = ReadVector3(field.Payload);
                    break;
                case 4:
                    orientation = ReadQuaternion(field.Payload);
                    break;
                case 5:
                    interpolation = ReadBoneInterpolation(field.Payload);
                    break;
                case 7:
                    physicsEnabled = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
            }
        }

        return new BoneKeyframe(
            ResolveTrackName(localTracks, trackIndex, "Bone"),
            common.FrameIndex,
            translation,
            orientation,
            interpolation,
            physicsEnabled)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations
        };
    }

    private static void ReadCameraBundle(Motion motion, ReadOnlyMemory<byte> data, int frameOffset)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number == 2)
            {
                motion.Add(ReadCameraKeyframe(field.Payload, frameOffset));
            }
        }
    }

    private static CameraKeyframe ReadCameraKeyframe(ReadOnlyMemory<byte> data, int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var lookAt = Vector3.Zero;
        var angle = Vector3.Zero;
        var fov = 45;
        var distance = 0f;
        var interpolation = CameraInterpolation.Linear;
        var perspectiveEnabled = true;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    lookAt = ReadVector3(field.Payload);
                    break;
                case 3:
                    angle = ReadVector3(field.Payload);
                    break;
                case 4:
                    fov = (int)MathF.Round(ProtoReader.ReadSingle(field.Payload.Span));
                    break;
                case 5:
                    distance = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 6:
                    interpolation = ReadCameraInterpolation(field.Payload);
                    break;
                case 8:
                    perspectiveEnabled = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
            }
        }

        return new CameraKeyframe(common.FrameIndex, lookAt, angle, distance, fov, interpolation, perspectiveEnabled)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations
        };
    }

    private static void ReadLightBundle(Motion motion, ReadOnlyMemory<byte> data, int frameOffset)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number == 2)
            {
                motion.Add(ReadLightKeyframe(field.Payload, frameOffset));
            }
        }
    }

    private static LightKeyframe ReadLightKeyframe(ReadOnlyMemory<byte> data, int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var color = Vector3.One;
        var direction = new Vector3(-0.5f, -1, 0.5f);
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    color = ReadVector3(field.Payload);
                    break;
                case 3:
                    direction = ReadVector3(field.Payload);
                    break;
            }
        }

        return new LightKeyframe(common.FrameIndex, color, direction)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations
        };
    }

    private static void ReadModelBundle(
        Motion motion,
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> globalTracks,
        int frameOffset)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number == 2)
            {
                motion.Add(ReadModelKeyframe(field.Payload, globalTracks, frameOffset));
            }
        }
    }

    private static ModelKeyframe ReadModelKeyframe(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> globalTracks,
        int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var visible = true;
        var ikStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        var effectParameters = new List<MotionEffectParameter>();
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    visible = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
                case 3:
                    var state = ReadConstraintState(field.Payload);
                    ikStates[ResolveTrackName(globalTracks, state.TrackIndex, "Constraint")] = state.Enabled;
                    break;
                case 4:
                    effectParameters.Add(ReadEffectParameter(field.Payload, globalTracks));
                    break;
            }
        }

        return new ModelKeyframe(common.FrameIndex, visible, ikStates)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations,
            EffectParameters = effectParameters
        };
    }

    private static void ReadMorphBundle(Motion motion, ReadOnlyMemory<byte> data, int frameOffset)
    {
        var localTracks = new Dictionary<ulong, string>();
        var keyframes = new List<ReadOnlyMemory<byte>>();
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 2:
                    var track = ReadTrack(field.Payload);
                    localTracks[track.Index] = track.Name;
                    break;
                case 3:
                    keyframes.Add(field.Payload);
                    break;
            }
        }

        foreach (var dataItem in keyframes)
        {
            motion.Add(ReadMorphKeyframe(dataItem, localTracks, frameOffset));
        }
    }

    private static MorphKeyframe ReadMorphKeyframe(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> localTracks,
        int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var trackIndex = 0UL;
        var weight = 0f;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 2:
                    trackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 3:
                    weight = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
            }
        }

        return new MorphKeyframe(ResolveTrackName(localTracks, trackIndex, "Morph"), common.FrameIndex, weight)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations
        };
    }

    private static void ReadSelfShadowBundle(Motion motion, ReadOnlyMemory<byte> data, int frameOffset)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number == 2)
            {
                motion.Add(ReadSelfShadowKeyframe(field.Payload, frameOffset));
            }
        }
    }

    private static SelfShadowKeyframe ReadSelfShadowKeyframe(ReadOnlyMemory<byte> data, int frameOffset)
    {
        var common = NmdKeyframeCommon.Empty;
        var mode = 0;
        var distance = 0f;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    common = ReadKeyframeCommon(field.Payload, frameOffset);
                    break;
                case 3:
                    mode = checked((int)ProtoReader.ReadVarint(field.Payload.Span));
                    break;
                case 4:
                    distance = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
            }
        }

        return new SelfShadowKeyframe(common.FrameIndex, mode, distance)
        {
            IsSelected = common.IsSelected,
            Annotations = common.Annotations
        };
    }

    private static NmdKeyframeCommon ReadKeyframeCommon(ReadOnlyMemory<byte> data, int frameOffset)
    {
        var frameIndex = frameOffset;
        var selected = false;
        var annotations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    var annotation = ReadAnnotation(field.Payload);
                    annotations[annotation.Key] = annotation.Value;
                    break;
                case 2:
                    frameIndex = checked((int)ProtoReader.ReadVarint(field.Payload.Span)) + frameOffset;
                    break;
                case 4:
                    selected = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
            }
        }

        return new NmdKeyframeCommon(Math.Max(0, frameIndex), selected, annotations);
    }

    private static KeyValuePair<string, string> ReadAnnotation(ReadOnlyMemory<byte> data)
    {
        var name = string.Empty;
        var value = string.Empty;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    name = ProtoReader.ReadUtf8(field.Payload.Span);
                    break;
                case 2:
                    value = ProtoReader.ReadUtf8(field.Payload.Span);
                    break;
            }
        }

        return new KeyValuePair<string, string>(name, value);
    }

    private static MotionEffectParameter ReadEffectParameter(
        ReadOnlyMemory<byte> data,
        IReadOnlyDictionary<ulong, string> globalTracks)
    {
        var trackIndex = 0UL;
        MotionEffectParameterValue? value = null;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    trackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 2:
                    value = new MotionEffectParameterValue.Bool(ProtoReader.ReadVarint(field.Payload.Span) != 0);
                    break;
                case 3:
                    value = new MotionEffectParameterValue.Int(unchecked((int)ProtoReader.ReadVarint(field.Payload.Span)));
                    break;
                case 4:
                    value = new MotionEffectParameterValue.Float(ProtoReader.ReadSingle(field.Payload.Span));
                    break;
                case 5:
                    value = new MotionEffectParameterValue.Vector4(ReadVector4(field.Payload));
                    break;
            }
        }

        return new MotionEffectParameter(
            ResolveTrackName(globalTracks, trackIndex, "EffectParameter"),
            value ?? new MotionEffectParameterValue.Float(0));
    }

    private static NmdTrack ReadTrack(ReadOnlyMemory<byte> data)
    {
        var index = 0UL;
        var name = string.Empty;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    index = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 2:
                    name = ProtoReader.ReadUtf8(field.Payload.Span);
                    break;
            }
        }

        return new NmdTrack(index, name);
    }

    private static bool TryReadBundle(ReadOnlyMemory<byte> data, out NmdBundle bundle)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    bundle = new NmdBundle(NmdBundleKind.Accessory, field.Payload);
                    return true;
                case 2:
                    bundle = new NmdBundle(NmdBundleKind.Bone, field.Payload);
                    return true;
                case 3:
                    bundle = new NmdBundle(NmdBundleKind.Camera, field.Payload);
                    return true;
                case 5:
                    bundle = new NmdBundle(NmdBundleKind.Light, field.Payload);
                    return true;
                case 6:
                    bundle = new NmdBundle(NmdBundleKind.Model, field.Payload);
                    return true;
                case 7:
                    bundle = new NmdBundle(NmdBundleKind.Morph, field.Payload);
                    return true;
                case 9:
                    bundle = new NmdBundle(NmdBundleKind.SelfShadow, field.Payload);
                    return true;
            }
        }

        bundle = default;
        return false;
    }

    private static NmdModelBinding ReadModelBinding(ReadOnlyMemory<byte> data)
    {
        var localBoneTrackIndex = 0UL;
        var globalObjectTrackIndex = 0UL;
        var globalBoneTrackIndex = 0UL;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    localBoneTrackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 2:
                    globalObjectTrackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 3:
                    globalBoneTrackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
            }
        }

        return new NmdModelBinding(localBoneTrackIndex, globalObjectTrackIndex, globalBoneTrackIndex);
    }

    private static NmdConstraintState ReadConstraintState(ReadOnlyMemory<byte> data)
    {
        var trackIndex = 0UL;
        var enabled = true;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    trackIndex = ProtoReader.ReadVarint(field.Payload.Span);
                    break;
                case 2:
                    enabled = ProtoReader.ReadVarint(field.Payload.Span) != 0;
                    break;
            }
        }

        return new NmdConstraintState(trackIndex, enabled);
    }

    private static Vector3 ReadVector3(ReadOnlyMemory<byte> data)
    {
        var result = Vector3.Zero;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    result.X = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 2:
                    result.Y = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 3:
                    result.Z = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
            }
        }

        return result;
    }

    private static Vector4 ReadVector4(ReadOnlyMemory<byte> data)
    {
        var result = Vector4.Zero;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    result.X = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 2:
                    result.Y = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 3:
                    result.Z = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 4:
                    result.W = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
            }
        }

        return result;
    }

    private static Quaternion ReadQuaternion(ReadOnlyMemory<byte> data)
    {
        var result = Quaternion.Identity;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    result.X = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 2:
                    result.Y = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 3:
                    result.Z = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
                case 4:
                    result.W = ProtoReader.ReadSingle(field.Payload.Span);
                    break;
            }
        }

        return result;
    }

    private static BoneInterpolation ReadBoneInterpolation(ReadOnlyMemory<byte> data)
    {
        var x = BezierCurve.Linear;
        var y = BezierCurve.Linear;
        var z = BezierCurve.Linear;
        var orientation = BezierCurve.Linear;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    x = ReadInterpolationUnit(field.Payload);
                    break;
                case 2:
                    y = ReadInterpolationUnit(field.Payload);
                    break;
                case 3:
                    z = ReadInterpolationUnit(field.Payload);
                    break;
                case 4:
                    orientation = ReadInterpolationUnit(field.Payload);
                    break;
            }
        }

        return new BoneInterpolation(x, y, z, orientation);
    }

    private static CameraInterpolation ReadCameraInterpolation(ReadOnlyMemory<byte> data)
    {
        var x = BezierCurve.Linear;
        var y = BezierCurve.Linear;
        var z = BezierCurve.Linear;
        var angle = BezierCurve.Linear;
        var fov = BezierCurve.Linear;
        var distance = BezierCurve.Linear;
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            switch (field.Number)
            {
                case 1:
                    x = ReadInterpolationUnit(field.Payload);
                    break;
                case 2:
                    y = ReadInterpolationUnit(field.Payload);
                    break;
                case 3:
                    z = ReadInterpolationUnit(field.Payload);
                    break;
                case 4:
                    angle = ReadInterpolationUnit(field.Payload);
                    break;
                case 5:
                    fov = ReadInterpolationUnit(field.Payload);
                    break;
                case 6:
                    distance = ReadInterpolationUnit(field.Payload);
                    break;
            }
        }

        return new CameraInterpolation(x, y, z, angle, distance, fov);
    }

    private static BezierCurve ReadInterpolationUnit(ReadOnlyMemory<byte> data)
    {
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number == 1)
            {
                return ReadIntegerInterpolation(field.Payload);
            }

            if (field.Number == 2)
            {
                return ReadFloatInterpolation(field.Payload);
            }
        }

        return BezierCurve.Linear;
    }

    private static BezierCurve ReadIntegerInterpolation(ReadOnlyMemory<byte> data)
    {
        var values = new byte[] { 20, 20, 107, 107 };
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number is >= 1 and <= 4)
            {
                values[field.Number - 1] = ClampToInterpolationByte(ProtoReader.ReadVarint(field.Payload.Span));
            }
        }

        return new BezierCurve(
            new BezierControlPoint(values[0], values[1]),
            new BezierControlPoint(values[2], values[3]));
    }

    private static BezierCurve ReadFloatInterpolation(ReadOnlyMemory<byte> data)
    {
        var values = new float[] { 20f / 127f, 20f / 127f, 107f / 127f, 107f / 127f };
        foreach (var field in new ProtoReader(data).ReadFields())
        {
            if (field.Number is >= 1 and <= 4)
            {
                values[field.Number - 1] = ProtoReader.ReadSingle(field.Payload.Span);
            }
        }

        return new BezierCurve(
            new BezierControlPoint(FloatToInterpolationByte(values[0]), FloatToInterpolationByte(values[1])),
            new BezierControlPoint(FloatToInterpolationByte(values[2]), FloatToInterpolationByte(values[3])));
    }

    private static byte ClampToInterpolationByte(ulong value)
    {
        return (byte)Math.Clamp(value, 0, 127);
    }

    private static byte FloatToInterpolationByte(float value)
    {
        return (byte)Math.Clamp(MathF.Round(value * 127f), 0, 127);
    }

    private static string ResolveTrackName(IReadOnlyDictionary<ulong, string> tracks, ulong index, string fallbackPrefix)
    {
        return tracks.TryGetValue(index, out var name) && !string.IsNullOrEmpty(name)
            ? name
            : $"{fallbackPrefix}{index}";
    }

    private static Vector3 QuaternionToEuler(Quaternion rotation)
    {
        rotation = Quaternion.Normalize(rotation);
        var sinPitch = 2f * ((rotation.W * rotation.X) - (rotation.Y * rotation.Z));
        var pitch = MathF.Asin(Math.Clamp(sinPitch, -1f, 1f));
        var sinYaw = 2f * ((rotation.W * rotation.Y) + (rotation.Z * rotation.X));
        var cosYaw = 1f - (2f * ((rotation.X * rotation.X) + (rotation.Y * rotation.Y)));
        var yaw = MathF.Atan2(sinYaw, cosYaw);
        var sinRoll = 2f * ((rotation.W * rotation.Z) + (rotation.X * rotation.Y));
        var cosRoll = 1f - (2f * ((rotation.X * rotation.X) + (rotation.Z * rotation.Z)));
        var roll = MathF.Atan2(sinRoll, cosRoll);
        return new Vector3(pitch, yaw, roll);
    }

    private sealed record NmdMotionDocument(
        string Name,
        IReadOnlyDictionary<string, string> Annotations,
        IReadOnlyDictionary<ulong, string> GlobalTracks,
        IReadOnlyList<NmdBundle> Bundles);

    private readonly record struct NmdTrack(ulong Index, string Name);

    private readonly record struct NmdKeyframeCommon(
        int FrameIndex,
        bool IsSelected,
        IReadOnlyDictionary<string, string> Annotations)
    {
        public static NmdKeyframeCommon Empty { get; } = new(
            0,
            false,
            new Dictionary<string, string>(0, StringComparer.Ordinal));
    }

    private readonly record struct NmdBundle(NmdBundleKind Kind, ReadOnlyMemory<byte> Payload);

    private readonly record struct NmdModelBinding(
        ulong LocalBoneTrackIndex,
        ulong GlobalObjectTrackIndex,
        ulong GlobalBoneTrackIndex);

    private readonly record struct NmdConstraintState(ulong TrackIndex, bool Enabled);

    private enum NmdBundleKind
    {
        Accessory,
        Bone,
        Camera,
        Light,
        Model,
        Morph,
        SelfShadow
    }
}
