using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Formats.Nmd;

public sealed class NmdMotionWriter
{
    public byte[] Write(Motion motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        var tracks = NmdTrackIndex.Create(motion);
        var writer = new ProtoWriter();
        foreach (var annotation in motion.Annotations.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteMessage(1, message => WriteAnnotation(message, annotation.Key, annotation.Value));
        }

        if (!string.IsNullOrEmpty(motion.Name))
        {
            writer.WriteString(2, motion.Name);
        }

        foreach (var track in tracks.GlobalTracks)
        {
            writer.WriteMessage(4, message => WriteTrack(message, track.Key, track.Value));
        }

        if (motion.AccessoryKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteAccessoryBundleUnit(message, motion.AccessoryKeyframes, tracks));
        }

        if (motion.BoneKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteBoneBundleUnit(message, motion.BoneKeyframes, tracks));
        }

        if (motion.CameraKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteCameraBundleUnit(message, motion.CameraKeyframes));
        }

        if (motion.LightKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteLightBundleUnit(message, motion.LightKeyframes));
        }

        if (motion.ModelKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteModelBundleUnit(message, motion.ModelKeyframes, tracks));
        }

        if (motion.MorphKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteMorphBundleUnit(message, motion.MorphKeyframes, tracks));
        }

        if (motion.SelfShadowKeyframes.Count > 0)
        {
            writer.WriteMessage(5, message => WriteSelfShadowBundleUnit(message, motion.SelfShadowKeyframes));
        }

        return writer.ToArray();
    }

    public void Write(Motion motion, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Write(motion));
    }

    private static void WriteAccessoryBundleUnit(
        ProtoWriter writer,
        IReadOnlyList<AccessoryKeyframe> keyframes,
        NmdTrackIndex tracks)
    {
        writer.WriteMessage(1, bundle =>
        {
            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex).ThenBy(frame => frame.AccessoryName, StringComparer.Ordinal))
            {
                bundle.WriteMessage(2, message => WriteAccessoryKeyframe(message, keyframe, tracks));
            }
        });
    }

    private static void WriteAccessoryKeyframe(ProtoWriter writer, AccessoryKeyframe keyframe, NmdTrackIndex tracks)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        writer.WriteVarint(2, tracks.ResolveGlobal(keyframe.AccessoryName));
        WriteVector3(writer, 3, keyframe.Translation);
        WriteVector4(writer, 4, Quaternion.CreateFromYawPitchRoll(keyframe.Orientation.Y, keyframe.Orientation.X, keyframe.Orientation.Z));
        writer.WriteFloat(5, keyframe.Scale);
        writer.WriteFloat(6, keyframe.Opacity);
        foreach (var parameter in keyframe.EffectParameters.OrderBy(parameter => parameter.Name, StringComparer.Ordinal))
        {
            writer.WriteMessage(7, message => WriteEffectParameter(message, parameter, tracks));
        }

        if (!string.IsNullOrEmpty(keyframe.ParentModelName) && !string.IsNullOrEmpty(keyframe.ParentBoneName))
        {
            writer.WriteMessage(8, binding =>
            {
                binding.WriteVarint(2, tracks.ResolveGlobal(keyframe.ParentModelName));
                binding.WriteVarint(3, tracks.ResolveGlobal(keyframe.ParentBoneName));
            });
        }

        writer.WriteBool(9, keyframe.Visible);
    }

    private static void WriteBoneBundleUnit(
        ProtoWriter writer,
        IReadOnlyList<BoneKeyframe> keyframes,
        NmdTrackIndex tracks)
    {
        writer.WriteMessage(2, bundle =>
        {
            foreach (var track in tracks.BoneTracks)
            {
                bundle.WriteMessage(2, message => WriteTrack(message, track.Key, track.Value));
            }

            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex).ThenBy(frame => frame.BoneName, StringComparer.Ordinal))
            {
                bundle.WriteMessage(3, message => WriteBoneKeyframe(message, keyframe, tracks));
            }
        });
    }

    private static void WriteBoneKeyframe(ProtoWriter writer, BoneKeyframe keyframe, NmdTrackIndex tracks)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        writer.WriteVarint(2, tracks.ResolveBone(keyframe.BoneName));
        WriteVector3(writer, 3, keyframe.Translation);
        WriteVector4(writer, 4, keyframe.Orientation);
        writer.WriteMessage(5, message => WriteBoneInterpolation(message, keyframe.Interpolation));
        writer.WriteBool(7, keyframe.PhysicsSimulationEnabled);
    }

    private static void WriteCameraBundleUnit(ProtoWriter writer, IReadOnlyList<CameraKeyframe> keyframes)
    {
        writer.WriteMessage(3, bundle =>
        {
            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
            {
                bundle.WriteMessage(2, message => WriteCameraKeyframe(message, keyframe));
            }
        });
    }

    private static void WriteCameraKeyframe(ProtoWriter writer, CameraKeyframe keyframe)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        WriteVector3(writer, 2, keyframe.LookAt);
        WriteVector3(writer, 3, keyframe.Angle);
        writer.WriteFloat(4, keyframe.FieldOfView);
        writer.WriteFloat(5, keyframe.Distance);
        writer.WriteMessage(6, message => WriteCameraInterpolation(message, keyframe.Interpolation));
        writer.WriteBool(8, keyframe.PerspectiveEnabled);
    }

    private static void WriteLightBundleUnit(ProtoWriter writer, IReadOnlyList<LightKeyframe> keyframes)
    {
        writer.WriteMessage(5, bundle =>
        {
            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
            {
                bundle.WriteMessage(2, message => WriteLightKeyframe(message, keyframe));
            }
        });
    }

    private static void WriteLightKeyframe(ProtoWriter writer, LightKeyframe keyframe)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        WriteVector3(writer, 2, keyframe.Color);
        WriteVector3(writer, 3, keyframe.Direction);
    }

    private static void WriteModelBundleUnit(
        ProtoWriter writer,
        IReadOnlyList<ModelKeyframe> keyframes,
        NmdTrackIndex tracks)
    {
        writer.WriteMessage(6, bundle =>
        {
            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
            {
                bundle.WriteMessage(2, message => WriteModelKeyframe(message, keyframe, tracks));
            }
        });
    }

    private static void WriteModelKeyframe(ProtoWriter writer, ModelKeyframe keyframe, NmdTrackIndex tracks)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        writer.WriteBool(2, keyframe.Visible);
        foreach (var state in keyframe.IkStates.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteMessage(3, message =>
            {
                message.WriteVarint(1, tracks.ResolveGlobal(state.Key));
                message.WriteBool(2, state.Value);
            });
        }

        foreach (var parameter in keyframe.EffectParameters.OrderBy(parameter => parameter.Name, StringComparer.Ordinal))
        {
            writer.WriteMessage(4, message => WriteEffectParameter(message, parameter, tracks));
        }
    }

    private static void WriteMorphBundleUnit(
        ProtoWriter writer,
        IReadOnlyList<MorphKeyframe> keyframes,
        NmdTrackIndex tracks)
    {
        writer.WriteMessage(7, bundle =>
        {
            foreach (var track in tracks.MorphTracks)
            {
                bundle.WriteMessage(2, message => WriteTrack(message, track.Key, track.Value));
            }

            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex).ThenBy(frame => frame.MorphName, StringComparer.Ordinal))
            {
                bundle.WriteMessage(3, message => WriteMorphKeyframe(message, keyframe, tracks));
            }
        });
    }

    private static void WriteMorphKeyframe(ProtoWriter writer, MorphKeyframe keyframe, NmdTrackIndex tracks)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        writer.WriteVarint(2, tracks.ResolveMorph(keyframe.MorphName));
        writer.WriteFloat(3, keyframe.Weight);
    }

    private static void WriteSelfShadowBundleUnit(ProtoWriter writer, IReadOnlyList<SelfShadowKeyframe> keyframes)
    {
        writer.WriteMessage(9, bundle =>
        {
            foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
            {
                bundle.WriteMessage(2, message => WriteSelfShadowKeyframe(message, keyframe));
            }
        });
    }

    private static void WriteSelfShadowKeyframe(ProtoWriter writer, SelfShadowKeyframe keyframe)
    {
        writer.WriteMessage(1, message => WriteCommon(message, keyframe));
        writer.WriteBool(2, true);
        writer.WriteInt32(3, keyframe.Mode);
        writer.WriteFloat(4, keyframe.Distance);
    }

    private static void WriteCommon(ProtoWriter writer, MotionKeyframe keyframe)
    {
        foreach (var annotation in keyframe.Annotations.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteMessage(1, message => WriteAnnotation(message, annotation.Key, annotation.Value));
        }

        writer.WriteVarint(2, checked((ulong)keyframe.FrameIndex));
        if (keyframe.IsSelected)
        {
            writer.WriteBool(4, true);
        }
    }

    private static void WriteTrack(ProtoWriter writer, string name, ulong index)
    {
        writer.WriteVarint(1, index);
        writer.WriteString(2, name);
    }

    private static void WriteAnnotation(ProtoWriter writer, string name, string value)
    {
        writer.WriteString(1, name);
        writer.WriteString(2, value);
    }

    private static void WriteEffectParameter(
        ProtoWriter writer,
        MotionEffectParameter parameter,
        NmdTrackIndex tracks)
    {
        writer.WriteVarint(1, tracks.ResolveGlobal(parameter.Name));
        switch (parameter.Value)
        {
            case MotionEffectParameterValue.Bool value:
                writer.WriteBool(2, value.Value);
                break;
            case MotionEffectParameterValue.Int value:
                writer.WriteInt32(3, value.Value);
                break;
            case MotionEffectParameterValue.Float value:
                writer.WriteFloat(4, value.Value);
                break;
            case MotionEffectParameterValue.Vector4 value:
                WriteVector4(writer, 5, value.Value);
                break;
        }
    }

    private static void WriteVector3(ProtoWriter writer, int fieldNumber, Vector3 value)
    {
        writer.WriteMessage(fieldNumber, message =>
        {
            message.WriteFloat(1, value.X);
            message.WriteFloat(2, value.Y);
            message.WriteFloat(3, value.Z);
        });
    }

    private static void WriteVector4(ProtoWriter writer, int fieldNumber, Quaternion value)
    {
        writer.WriteMessage(fieldNumber, message =>
        {
            message.WriteFloat(1, value.X);
            message.WriteFloat(2, value.Y);
            message.WriteFloat(3, value.Z);
            message.WriteFloat(4, value.W);
        });
    }

    private static void WriteVector4(ProtoWriter writer, int fieldNumber, Vector4 value)
    {
        writer.WriteMessage(fieldNumber, message =>
        {
            message.WriteFloat(1, value.X);
            message.WriteFloat(2, value.Y);
            message.WriteFloat(3, value.Z);
            message.WriteFloat(4, value.W);
        });
    }

    private static void WriteBoneInterpolation(ProtoWriter writer, BoneInterpolation interpolation)
    {
        WriteInterpolationUnit(writer, 1, interpolation.TranslationX);
        WriteInterpolationUnit(writer, 2, interpolation.TranslationY);
        WriteInterpolationUnit(writer, 3, interpolation.TranslationZ);
        WriteInterpolationUnit(writer, 4, interpolation.Orientation);
    }

    private static void WriteCameraInterpolation(ProtoWriter writer, CameraInterpolation interpolation)
    {
        WriteInterpolationUnit(writer, 1, interpolation.LookAtX);
        WriteInterpolationUnit(writer, 2, interpolation.LookAtY);
        WriteInterpolationUnit(writer, 3, interpolation.LookAtZ);
        WriteInterpolationUnit(writer, 4, interpolation.Angle);
        WriteInterpolationUnit(writer, 5, interpolation.FieldOfView);
        WriteInterpolationUnit(writer, 6, interpolation.Distance);
    }

    private static void WriteInterpolationUnit(ProtoWriter writer, int fieldNumber, BezierCurve curve)
    {
        writer.WriteMessage(fieldNumber, unit =>
        {
            unit.WriteMessage(1, integer =>
            {
                integer.WriteVarint(1, curve.P1.X);
                integer.WriteVarint(2, curve.P1.Y);
                integer.WriteVarint(3, curve.P2.X);
                integer.WriteVarint(4, curve.P2.Y);
            });
        });
    }

    private sealed class NmdTrackIndex
    {
        private readonly Dictionary<string, ulong> _globalByName;
        private readonly Dictionary<string, ulong> _boneByName;
        private readonly Dictionary<string, ulong> _morphByName;

        private NmdTrackIndex(
            Dictionary<string, ulong> globalByName,
            Dictionary<string, ulong> boneByName,
            Dictionary<string, ulong> morphByName)
        {
            _globalByName = globalByName;
            _boneByName = boneByName;
            _morphByName = morphByName;
        }

        public IReadOnlyDictionary<string, ulong> GlobalTracks => _globalByName;

        public IReadOnlyDictionary<string, ulong> BoneTracks => _boneByName;

        public IReadOnlyDictionary<string, ulong> MorphTracks => _morphByName;

        public static NmdTrackIndex Create(Motion motion)
        {
            var global = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var bones = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var morphs = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (var name in motion.AccessoryKeyframes.Select(frame => frame.AccessoryName))
            {
                AddTrack(global, name);
            }

            foreach (var name in motion.AccessoryKeyframes.SelectMany(frame => new[] { frame.ParentModelName, frame.ParentBoneName }))
            {
                AddTrack(global, name);
            }

            foreach (var name in motion.ModelKeyframes.SelectMany(frame => frame.IkStates.Keys))
            {
                AddTrack(global, name);
            }

            foreach (var name in motion.ModelKeyframes.SelectMany(frame => frame.EffectParameters.Select(parameter => parameter.Name)))
            {
                AddTrack(global, name);
            }

            foreach (var name in motion.AccessoryKeyframes.SelectMany(frame => frame.EffectParameters.Select(parameter => parameter.Name)))
            {
                AddTrack(global, name);
            }

            foreach (var name in motion.BoneKeyframes.Select(frame => frame.BoneName))
            {
                AddTrack(bones, name);
            }

            foreach (var name in motion.MorphKeyframes.Select(frame => frame.MorphName))
            {
                AddTrack(morphs, name);
            }

            return new NmdTrackIndex(global, bones, morphs);
        }

        public ulong ResolveGlobal(string name)
        {
            return Resolve(_globalByName, name);
        }

        public ulong ResolveBone(string name)
        {
            return Resolve(_boneByName, name);
        }

        public ulong ResolveMorph(string name)
        {
            return Resolve(_morphByName, name);
        }

        private static void AddTrack(Dictionary<string, ulong> tracks, string? name)
        {
            if (string.IsNullOrEmpty(name) || tracks.ContainsKey(name))
            {
                return;
            }

            tracks[name] = checked((ulong)tracks.Count + 1);
        }

        private static ulong Resolve(Dictionary<string, ulong> tracks, string name)
        {
            if (tracks.TryGetValue(name, out var index))
            {
                return index;
            }

            throw new InvalidOperationException($"NMD track '{name}' was not indexed.");
        }
    }
}
