using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Formats.Binary;

namespace Zhengyan.MikuMikuDance.Formats.Vmd;

public sealed class VmdMotionReader
{
    private const int HeaderSize = 30;
    private static readonly Encoding ShiftJis = CreateShiftJisEncoding();

    public Motion Read(ReadOnlyMemory<byte> data)
    {
        var reader = new BinarySpanReader(data);
        var signature = reader.ReadFixedString(HeaderSize, ShiftJis);
        if (!signature.StartsWith("Vocaloid Motion Data", StringComparison.Ordinal))
        {
            throw new MmdFormatException($"Invalid VMD signature '{signature}'.");
        }

        var modelName = reader.ReadFixedString(20, ShiftJis);
        var motion = new Motion(modelName, MotionFormat.Vmd);
        ReadBoneKeyframes(reader, motion);
        ReadMorphKeyframes(reader, motion);
        ReadCameraKeyframes(reader, motion);
        ReadLightKeyframes(reader, motion);
        ReadSelfShadowKeyframes(reader, motion);
        if (!reader.EndOfBuffer)
        {
            ReadModelKeyframes(reader, motion);
        }

        return motion;
    }

    private static Encoding CreateShiftJisEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }

    public Motion Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray());
    }

    private static void ReadBoneKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            var boneName = reader.ReadFixedString(15, ShiftJis);
            var frameIndex = checked((int)reader.ReadUInt32());
            var translation = reader.ReadVector3();
            var orientation = reader.ReadQuaternionXyzw();
            var interpolation = ReadBoneInterpolation(reader.ReadBytes(64));
            motion.Add(new BoneKeyframe(boneName, frameIndex, translation, orientation, interpolation));
        }
    }

    private static void ReadMorphKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            motion.Add(new MorphKeyframe(
                reader.ReadFixedString(15, ShiftJis),
                checked((int)reader.ReadUInt32()),
                reader.ReadSingle()));
        }
    }

    private static void ReadCameraKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            var frameIndex = checked((int)reader.ReadUInt32());
            var distance = reader.ReadSingle();
            var lookAt = reader.ReadVector3();
            var angle = reader.ReadVector3();
            var interpolation = ReadCameraInterpolation(reader.ReadBytes(24));
            var fov = reader.ReadInt32();
            var perspectiveDisabled = reader.ReadByte() != 0;
            motion.Add(new CameraKeyframe(frameIndex, lookAt, angle, distance, fov, interpolation, !perspectiveDisabled));
        }
    }

    private static void ReadLightKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            motion.Add(new LightKeyframe(
                checked((int)reader.ReadUInt32()),
                reader.ReadVector3(),
                reader.ReadVector3()));
        }
    }

    private static void ReadSelfShadowKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            motion.Add(new SelfShadowKeyframe(
                checked((int)reader.ReadUInt32()),
                reader.ReadByte(),
                reader.ReadSingle()));
        }
    }

    private static void ReadModelKeyframes(BinarySpanReader reader, Motion motion)
    {
        var count = checked((int)reader.ReadUInt32());
        for (var i = 0; i < count; i++)
        {
            var frameIndex = checked((int)reader.ReadUInt32());
            var visible = reader.ReadByte() != 0;
            var ikCount = reader.ReadUInt32();
            var states = new Dictionary<string, bool>(checked((int)ikCount), StringComparer.Ordinal);
            for (var j = 0; j < ikCount; j++)
            {
                states[reader.ReadFixedString(20, ShiftJis)] = reader.ReadByte() != 0;
            }

            motion.Add(new ModelKeyframe(frameIndex, visible, states));
        }
    }

    private static BoneInterpolation ReadBoneInterpolation(byte[] bytes)
    {
        if (bytes.Length != 64)
        {
            return BoneInterpolation.Linear;
        }

        return new BoneInterpolation(
            Curve(bytes, 0),
            Curve(bytes, 1),
            Curve(bytes, 2),
            Curve(bytes, 3));
    }

    private static CameraInterpolation ReadCameraInterpolation(byte[] bytes)
    {
        if (bytes.Length != 24)
        {
            return CameraInterpolation.Linear;
        }

        return new CameraInterpolation(
            Curve(bytes, 0),
            Curve(bytes, 1),
            Curve(bytes, 2),
            Curve(bytes, 3),
            Curve(bytes, 4),
            Curve(bytes, 5));
    }

    private static BezierCurve Curve(IReadOnlyList<byte> bytes, int offset)
    {
        var p1 = new BezierControlPoint(bytes[offset], bytes[offset + 4]);
        var p2 = new BezierControlPoint(bytes[offset + 8], bytes[offset + 12]);
        return new BezierCurve(p1, p2);
    }
}
