using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public sealed record BonePoseSample(Vector3 Translation, Quaternion Orientation, bool PhysicsSimulationEnabled);

public sealed record MorphWeightSample(float Weight);

public sealed record CameraSample(
    Vector3 LookAt,
    Vector3 Angle,
    float Distance,
    int FieldOfView,
    bool PerspectiveEnabled);

public sealed record LightSample(Vector3 Color, Vector3 Direction);

public sealed record SelfShadowSample(int Mode, float Distance);

public sealed record ModelSample(bool Visible, IReadOnlyDictionary<string, bool> IkStates);

public sealed record AccessorySample(
    bool Visible,
    Vector3 Translation,
    Vector3 Orientation,
    float Scale,
    float Opacity,
    string? ParentModelName,
    string? ParentBoneName);

public sealed record MotionSample(
    int FrameIndex,
    IReadOnlyDictionary<string, BonePoseSample> Bones,
    IReadOnlyDictionary<string, MorphWeightSample> Morphs,
    CameraSample? Camera,
    LightSample? Light,
    SelfShadowSample? SelfShadow,
    ModelSample? Model,
    IReadOnlyDictionary<string, AccessorySample> Accessories);
