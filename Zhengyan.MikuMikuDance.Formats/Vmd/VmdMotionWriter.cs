using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Formats.Vmd;

public sealed class VmdMotionWriter
{
    private const int HeaderSize = 30;
    private static readonly Encoding ShiftJis = CreateShiftJisEncoding();

    public byte[] Write(Motion motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        using var stream = new MemoryStream();
        Write(motion, stream);
        return stream.ToArray();
    }

    public void Write(Motion motion, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new BinaryWriter(stream, ShiftJis, leaveOpen: true);
        WriteFixed(writer, "Vocaloid Motion Data 0002", HeaderSize);
        WriteFixed(writer, motion.Name, 20);
        WriteBoneKeyframes(writer, motion.BoneKeyframes);
        WriteMorphKeyframes(writer, motion.MorphKeyframes);
        WriteCameraKeyframes(writer, motion.CameraKeyframes);
        WriteLightKeyframes(writer, motion.LightKeyframes);
        WriteSelfShadowKeyframes(writer, motion.SelfShadowKeyframes);
        WriteModelKeyframes(writer, motion.ModelKeyframes);
    }

    private static Encoding CreateShiftJisEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }

    private static void WriteBoneKeyframes(BinaryWriter writer, IReadOnlyList<BoneKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex).ThenBy(frame => frame.BoneName, StringComparer.Ordinal))
        {
            WriteFixed(writer, keyframe.BoneName, 15);
            writer.Write(checked((uint)keyframe.FrameIndex));
            WriteVector3(writer, keyframe.Translation);
            WriteQuaternion(writer, keyframe.Orientation);
            writer.Write(CreateBoneInterpolationBytes(keyframe.Interpolation));
        }
    }

    private static void WriteMorphKeyframes(BinaryWriter writer, IReadOnlyList<MorphKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex).ThenBy(frame => frame.MorphName, StringComparer.Ordinal))
        {
            WriteFixed(writer, keyframe.MorphName, 15);
            writer.Write(checked((uint)keyframe.FrameIndex));
            writer.Write(keyframe.Weight);
        }
    }

    private static void WriteCameraKeyframes(BinaryWriter writer, IReadOnlyList<CameraKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
        {
            writer.Write(checked((uint)keyframe.FrameIndex));
            writer.Write(keyframe.Distance);
            WriteVector3(writer, keyframe.LookAt);
            WriteVector3(writer, keyframe.Angle);
            writer.Write(CreateCameraInterpolationBytes(keyframe.Interpolation));
            writer.Write(keyframe.FieldOfView);
            writer.Write((byte)(keyframe.PerspectiveEnabled ? 0 : 1));
        }
    }

    private static void WriteLightKeyframes(BinaryWriter writer, IReadOnlyList<LightKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
        {
            writer.Write(checked((uint)keyframe.FrameIndex));
            WriteVector3(writer, keyframe.Color);
            WriteVector3(writer, keyframe.Direction);
        }
    }

    private static void WriteSelfShadowKeyframes(BinaryWriter writer, IReadOnlyList<SelfShadowKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
        {
            writer.Write(checked((uint)keyframe.FrameIndex));
            writer.Write((byte)Math.Clamp(keyframe.Mode, 0, byte.MaxValue));
            writer.Write(keyframe.Distance);
        }
    }

    private static void WriteModelKeyframes(BinaryWriter writer, IReadOnlyList<ModelKeyframe> keyframes)
    {
        writer.Write(checked((uint)keyframes.Count));
        foreach (var keyframe in keyframes.OrderBy(frame => frame.FrameIndex))
        {
            writer.Write(checked((uint)keyframe.FrameIndex));
            writer.Write((byte)(keyframe.Visible ? 1 : 0));
            writer.Write(checked((uint)keyframe.IkStates.Count));
            foreach (var state in keyframe.IkStates.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                WriteFixed(writer, state.Key, 20);
                writer.Write((byte)(state.Value ? 1 : 0));
            }
        }
    }

    private static byte[] CreateBoneInterpolationBytes(BoneInterpolation interpolation)
    {
        var bytes = new byte[64];
        WriteCurve(bytes, 0, interpolation.TranslationX);
        WriteCurve(bytes, 1, interpolation.TranslationY);
        WriteCurve(bytes, 2, interpolation.TranslationZ);
        WriteCurve(bytes, 3, interpolation.Orientation);
        return bytes;
    }

    private static byte[] CreateCameraInterpolationBytes(CameraInterpolation interpolation)
    {
        var bytes = new byte[24];
        WriteCurve(bytes, 0, interpolation.LookAtX);
        WriteCurve(bytes, 1, interpolation.LookAtY);
        WriteCurve(bytes, 2, interpolation.LookAtZ);
        WriteCurve(bytes, 3, interpolation.Angle);
        WriteCurve(bytes, 4, interpolation.FieldOfView);
        WriteCurve(bytes, 5, interpolation.Distance);
        return bytes;
    }

    private static void WriteCurve(byte[] bytes, int offset, BezierCurve curve)
    {
        bytes[offset] = curve.P1.X;
        bytes[offset + 4] = curve.P1.Y;
        bytes[offset + 8] = curve.P2.X;
        bytes[offset + 12] = curve.P2.Y;
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteQuaternion(BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    private static void WriteFixed(BinaryWriter writer, string value, int length)
    {
        var bytes = ShiftJis.GetBytes(value);
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
