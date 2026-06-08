using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public enum VertexSkinningType
{
    Bdef1,
    Bdef2,
    Bdef4,
    Sdef,
    Qdef
}

public enum MorphCategory
{
    System,
    Eyebrow,
    Eye,
    Lip,
    Other
}

public enum MorphType
{
    Group,
    Vertex,
    Bone,
    Uv,
    Uv1,
    Uv2,
    Uv3,
    Uv4,
    Material,
    Flip,
    Impulse
}

public enum LabelItemType
{
    Bone,
    Morph
}

public enum RigidBodyShapeType
{
    Sphere,
    Box,
    Capsule
}

public enum RigidBodyTransformType
{
    Static,
    Dynamic,
    DynamicWithBone
}

public enum JointType
{
    Generic6DofSpring,
    Generic6Dof,
    Point2Point,
    ConeTwist,
    Slider,
    Hinge
}

public enum SphereTextureMode
{
    Disabled,
    Multiply,
    Add,
    SubTexture
}

public sealed record Vertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    IReadOnlyList<Vector4> AdditionalUvs,
    SkinningWeights Skinning,
    float EdgeSize);

public sealed record SkinningWeights(
    VertexSkinningType Type,
    IReadOnlyList<int> BoneIndices,
    IReadOnlyList<float> Weights,
    SdefParameters? Sdef = null);

public sealed record SdefParameters(Vector3 C, Vector3 R0, Vector3 R1);

public sealed record Material(
    string Name,
    string EnglishName,
    Vector4 Diffuse,
    Vector3 Specular,
    float SpecularPower,
    Vector3 Ambient,
    bool IsDoubleSided,
    bool GroundShadowEnabled,
    bool SelfShadowMapEnabled,
    bool SelfShadowEnabled,
    bool EdgeEnabled,
    Vector4 EdgeColor,
    float EdgeSize,
    int TextureIndex,
    int SphereTextureIndex,
    SphereTextureMode SphereTextureMode,
    int ToonTextureIndex,
    bool ToonTextureShared,
    int VertexCount,
    string Memo);

public sealed record Bone(
    string Name,
    string EnglishName,
    Vector3 Origin,
    int ParentBoneIndex,
    int LayerIndex,
    BoneFlags Flags,
    int ConnectionIndex,
    Vector3 ConnectionOffset,
    int InherentParentIndex,
    float InherentCoefficient,
    AxisConstraint? AxisConstraint,
    LocalAxes? LocalAxes,
    int Key,
    IkConstraint? Ik);

[Flags]
public enum BoneFlags
{
    None = 0,
    IndexedTailPosition = 1 << 0,
    Rotatable = 1 << 1,
    Movable = 1 << 2,
    Visible = 1 << 3,
    Enabled = 1 << 4,
    Ik = 1 << 5,
    InherentLocal = 1 << 7,
    InherentOrientation = 1 << 8,
    InherentTranslation = 1 << 9,
    FixedAxis = 1 << 10,
    LocalAxis = 1 << 11,
    OutsideParent = 1 << 13
}

public sealed record AxisConstraint(Vector3 Axis);

public sealed record LocalAxes(Vector3 X, Vector3 Z);

public sealed record IkConstraint(int EffectorBoneIndex, int IterationCount, float AngleLimit, IReadOnlyList<IkLink> Links);

public sealed record IkLink(int BoneIndex, bool AngleLimitEnabled, Vector3 LowerLimit, Vector3 UpperLimit);

public sealed record Morph(
    string Name,
    string EnglishName,
    MorphCategory Category,
    MorphType Type,
    IReadOnlyList<MorphOffset> Offsets);

public abstract record MorphOffset;

public sealed record GroupMorphOffset(int MorphIndex, float Weight) : MorphOffset;

public sealed record VertexMorphOffset(int VertexIndex, Vector3 Translation) : MorphOffset;

public sealed record BoneMorphOffset(int BoneIndex, Vector3 Translation, Quaternion Orientation) : MorphOffset;

public sealed record UvMorphOffset(int VertexIndex, Vector4 Offset) : MorphOffset;

public sealed record MaterialMorphOffset(
    int MaterialIndex,
    bool IsAdditive,
    Vector4 Diffuse,
    Vector3 Specular,
    float SpecularPower,
    Vector3 Ambient,
    Vector4 EdgeColor,
    float EdgeSize,
    Vector4 TextureCoefficient,
    Vector4 SphereTextureCoefficient,
    Vector4 ToonTextureCoefficient) : MorphOffset;

public sealed record FlipMorphOffset(int MorphIndex, float Weight) : MorphOffset;

public sealed record ImpulseMorphOffset(int RigidBodyIndex, bool Local, Vector3 Velocity, Vector3 Torque) : MorphOffset;

public sealed record ModelLabel(string Name, string EnglishName, bool Special, IReadOnlyList<ModelLabelItem> Items);

public sealed record ModelLabelItem(LabelItemType Type, int Index);

public sealed record RigidBody(
    string Name,
    string EnglishName,
    int BoneIndex,
    byte CollisionGroupId,
    ushort CollisionMask,
    RigidBodyShapeType ShapeType,
    Vector3 ShapeSize,
    Vector3 Translation,
    Vector3 Orientation,
    float Mass,
    float LinearDamping,
    float AngularDamping,
    float Restitution,
    float Friction,
    RigidBodyTransformType TransformType);

public sealed record Joint(
    string Name,
    string EnglishName,
    JointType Type,
    int RigidBodyAIndex,
    int RigidBodyBIndex,
    Vector3 Translation,
    Vector3 Orientation,
    Vector3 LinearLowerLimit,
    Vector3 LinearUpperLimit,
    Vector3 AngularLowerLimit,
    Vector3 AngularUpperLimit,
    Vector3 LinearStiffness,
    Vector3 AngularStiffness);

public sealed record SoftBody(
    string Name,
    string EnglishName,
    int MaterialIndex,
    int GroupId,
    int CollisionMask,
    int VertexCount,
    int AnchorCount);
