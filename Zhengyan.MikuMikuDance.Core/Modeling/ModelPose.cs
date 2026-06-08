using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public sealed record BonePose(
    string Name,
    int ParentBoneIndex,
    Vector3 Origin,
    Vector3 Translation,
    Quaternion Orientation,
    Matrix4x4 LocalTransform,
    Matrix4x4 WorldTransform,
    Matrix4x4 SkinningTransform);

public sealed class ModelPose
{
    private readonly BonePose[] _bones;
    private readonly Dictionary<string, int> _boneIndices;

    public ModelPose(IReadOnlyList<BonePose> bones)
    {
        ArgumentNullException.ThrowIfNull(bones);
        _bones = bones.ToArray();
        _boneIndices = new Dictionary<string, int>(_bones.Length, StringComparer.Ordinal);
        for (var i = 0; i < _bones.Length; i++)
        {
            _boneIndices[_bones[i].Name] = i;
        }
    }

    public IReadOnlyList<BonePose> Bones => _bones;

    public static ModelPose BindPose(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return ModelPoseEvaluator.Evaluate(model, new Dictionary<string, BonePoseSample>(0, StringComparer.Ordinal));
    }

    public bool TryGetBone(int index, out BonePose bone)
    {
        if (index >= 0 && index < _bones.Length)
        {
            bone = _bones[index];
            return true;
        }

        bone = default!;
        return false;
    }

    public bool TryGetBone(string name, out BonePose bone)
    {
        if (_boneIndices.TryGetValue(name, out var index))
        {
            bone = _bones[index];
            return true;
        }

        bone = default!;
        return false;
    }
}
