using System.Numerics;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed record PmmLegacyDocument
{
    public int Version { get; init; }

    public int OutputWidth { get; init; }

    public int OutputHeight { get; init; }

    public int TimelineWidth { get; init; }

    public float CameraFieldOfView { get; init; }

    public int SelectedModelIndex { get; init; }

    public int SelectedAccessoryIndex { get; init; }

    public int CurrentFrameIndex { get; init; }

    public int BeginFrameIndex { get; init; }

    public int EndFrameIndex { get; init; }

    public bool BeginFrameEnabled { get; init; }

    public bool EndFrameEnabled { get; init; }

    public bool LoopEnabled { get; init; }

    public bool AudioEnabled { get; init; }

    public string AudioPath { get; init; } = string.Empty;

    public bool BackgroundVideoEnabled { get; init; }

    public string BackgroundVideoPath { get; init; } = string.Empty;

    public bool BackgroundImageEnabled { get; init; }

    public string BackgroundImagePath { get; init; } = string.Empty;

    public float PreferredFps { get; init; }

    public bool GridAndAxisShown { get; init; }

    public bool GroundShadowShown { get; init; }

    public Vector3 EdgeColor { get; init; }

    public PmmLegacyCamera Camera { get; init; } = new();

    public PmmLegacyLight Light { get; init; } = new();

    public PmmLegacyGravity? Gravity { get; init; }

    public PmmLegacySelfShadow? SelfShadow { get; init; }

    public IReadOnlyList<PmmLegacyModel> Models { get; init; } = [];

    public IReadOnlyList<PmmLegacyAccessory> Accessories { get; init; } = [];
}

public sealed record PmmLegacyModel
{
    public int Index { get; init; }

    public string Name { get; init; } = string.Empty;

    public string EnglishName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public IReadOnlyList<string> BoneNames { get; init; } = [];

    public IReadOnlyList<string> MorphNames { get; init; } = [];

    public IReadOnlyList<int> ConstraintBoneIndices { get; init; } = [];

    public IReadOnlyList<int> OutsideParentSubjectBoneIndices { get; init; } = [];

    public int DrawOrderIndex { get; init; }

    public int TransformOrderIndex { get; init; }

    public bool Visible { get; init; } = true;

    public int LastFrameIndex { get; init; }

    public float EdgeWidth { get; init; }

    public bool BlendEnabled { get; init; }

    public bool SelfShadowEnabled { get; init; }

    public IReadOnlyList<PmmLegacyBoneKeyframe> BoneKeyframes { get; init; } = [];

    public IReadOnlyList<PmmLegacyMorphKeyframe> MorphKeyframes { get; init; } = [];

    public IReadOnlyList<PmmLegacyModelKeyframe> ModelKeyframes { get; init; } = [];
}

public sealed record PmmLegacyAccessory
{
    public int Index { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int DrawOrderIndex { get; init; }

    public bool Visible { get; init; } = true;

    public float Opacity { get; init; } = 1f;

    public Vector3 Translation { get; init; }

    public Vector3 Orientation { get; init; }

    public float Scale { get; init; } = 10f;

    public int ParentModelIndex { get; init; } = -1;

    public int ParentModelBoneIndex { get; init; } = -1;

    public bool ShadowEnabled { get; init; } = true;

    public bool AddBlendEnabled { get; init; }

    public IReadOnlyList<PmmLegacyAccessoryKeyframe> Keyframes { get; init; } = [];
}

public sealed record PmmLegacyCamera
{
    public Vector3 LookAt { get; init; }

    public Vector3 Position { get; init; } = new(0, 10, 45);

    public Vector3 Angle { get; init; }

    public bool PerspectiveEnabled { get; init; } = true;

    public IReadOnlyList<PmmLegacyCameraKeyframe> Keyframes { get; init; } = [];
}

public sealed record PmmLegacyLight
{
    public Vector3 Color { get; init; } = new(0.6f, 0.6f, 0.6f);

    public Vector3 Direction { get; init; } = new(-0.5f, -1f, 0.5f);

    public IReadOnlyList<PmmLegacyLightKeyframe> Keyframes { get; init; } = [];
}

public sealed record PmmLegacyGravity
{
    public float Acceleration { get; init; }

    public int Noise { get; init; }

    public Vector3 Direction { get; init; }

    public bool NoiseEnabled { get; init; }
}

public sealed record PmmLegacySelfShadow
{
    public bool Enabled { get; init; }

    public float Distance { get; init; }

    public IReadOnlyList<PmmLegacySelfShadowKeyframe> Keyframes { get; init; } = [];
}

public sealed record PmmLegacyBoneKeyframe(
    string BoneName,
    int FrameIndex,
    Vector3 Translation,
    Quaternion Orientation,
    PmmLegacyBoneInterpolation Interpolation,
    bool PhysicsSimulationEnabled,
    bool IsSelected);

public sealed record PmmLegacyMorphKeyframe(
    string MorphName,
    int FrameIndex,
    float Weight,
    bool IsSelected);

public sealed record PmmLegacyModelKeyframe(
    int FrameIndex,
    bool Visible,
    IReadOnlyDictionary<string, bool> ConstraintStates,
    bool IsSelected);

public sealed record PmmLegacyAccessoryKeyframe(
    string AccessoryName,
    int FrameIndex,
    bool Visible,
    float Opacity,
    int ParentModelIndex,
    int ParentModelBoneIndex,
    Vector3 Translation,
    Vector3 Orientation,
    float Scale,
    bool ShadowEnabled,
    bool IsSelected);

public sealed record PmmLegacyCameraKeyframe(
    int FrameIndex,
    float Distance,
    Vector3 LookAt,
    Vector3 Angle,
    int ParentModelIndex,
    int ParentModelBoneIndex,
    PmmLegacyCameraInterpolation Interpolation,
    bool PerspectiveEnabled,
    int FieldOfView,
    bool IsSelected);

public sealed record PmmLegacyLightKeyframe(
    int FrameIndex,
    Vector3 Color,
    Vector3 Direction,
    bool IsSelected);

public sealed record PmmLegacySelfShadowKeyframe(
    int FrameIndex,
    int Mode,
    float Distance,
    bool IsSelected);

public sealed record PmmLegacyBoneInterpolation(
    PmmLegacyBezierCurve TranslationX,
    PmmLegacyBezierCurve TranslationY,
    PmmLegacyBezierCurve TranslationZ,
    PmmLegacyBezierCurve Orientation);

public sealed record PmmLegacyCameraInterpolation(
    PmmLegacyBezierCurve LookAtX,
    PmmLegacyBezierCurve LookAtY,
    PmmLegacyBezierCurve LookAtZ,
    PmmLegacyBezierCurve Angle,
    PmmLegacyBezierCurve Distance,
    PmmLegacyBezierCurve FieldOfView);

public readonly record struct PmmLegacyBezierCurve(byte X0, byte Y0, byte X1, byte Y1);
