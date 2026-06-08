using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public abstract record MotionKeyframe(int FrameIndex)
{
    public bool IsSelected { get; init; }

    public IReadOnlyDictionary<string, string> Annotations { get; init; } =
        new Dictionary<string, string>(0, StringComparer.Ordinal);
}

public sealed record BoneKeyframe(
    string BoneName,
    int FrameIndex,
    Vector3 Translation,
    Quaternion Orientation,
    BoneInterpolation Interpolation,
    bool PhysicsSimulationEnabled = true) : MotionKeyframe(FrameIndex);

public sealed record MorphKeyframe(
    string MorphName,
    int FrameIndex,
    float Weight) : MotionKeyframe(FrameIndex);

public sealed record CameraKeyframe(
    int FrameIndex,
    Vector3 LookAt,
    Vector3 Angle,
    float Distance,
    int FieldOfView,
    CameraInterpolation Interpolation,
    bool PerspectiveEnabled = true) : MotionKeyframe(FrameIndex);

public sealed record LightKeyframe(
    int FrameIndex,
    Vector3 Color,
    Vector3 Direction) : MotionKeyframe(FrameIndex);

public sealed record SelfShadowKeyframe(
    int FrameIndex,
    int Mode,
    float Distance) : MotionKeyframe(FrameIndex);

public sealed record ModelKeyframe(
    int FrameIndex,
    bool Visible,
    IReadOnlyDictionary<string, bool> IkStates) : MotionKeyframe(FrameIndex)
{
    public IReadOnlyList<MotionEffectParameter> EffectParameters { get; init; } = [];
}

public sealed record AccessoryKeyframe(
    string AccessoryName,
    int FrameIndex,
    bool Visible,
    Vector3 Translation,
    Vector3 Orientation,
    float Scale,
    float Opacity,
    string? ParentModelName = null,
    string? ParentBoneName = null) : MotionKeyframe(FrameIndex)
{
    public IReadOnlyList<MotionEffectParameter> EffectParameters { get; init; } = [];
}

public sealed record MotionEffectParameter(string Name, MotionEffectParameterValue Value);

public abstract record MotionEffectParameterValue
{
    public sealed record Bool(bool Value) : MotionEffectParameterValue;

    public sealed record Int(int Value) : MotionEffectParameterValue;

    public sealed record Float(float Value) : MotionEffectParameterValue;

    public sealed record Vector4(System.Numerics.Vector4 Value) : MotionEffectParameterValue;
}

public sealed record BoneInterpolation(
    BezierCurve TranslationX,
    BezierCurve TranslationY,
    BezierCurve TranslationZ,
    BezierCurve Orientation)
{
    public static BoneInterpolation Linear { get; } = new(
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear);
}

public sealed record CameraInterpolation(
    BezierCurve LookAtX,
    BezierCurve LookAtY,
    BezierCurve LookAtZ,
    BezierCurve Angle,
    BezierCurve Distance,
    BezierCurve FieldOfView)
{
    public static CameraInterpolation Linear { get; } = new(
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear,
        BezierCurve.Linear);
}
