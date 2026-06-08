using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public sealed record ModelAnimationState(ModelPose Pose, MorphApplication Morphs);

public static class AnimationPoseEvaluator
{
    public static ModelAnimationState Evaluate(MmdModel model, MotionSample sample)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(sample);
        var morphs = MorphEvaluator.Evaluate(model, sample.Morphs);
        var bones = MergeBoneSamples(sample.Bones, morphs.BoneOffsets);
        var pose = ModelPoseEvaluator.Evaluate(model, bones);
        return new ModelAnimationState(pose, morphs);
    }

    private static IReadOnlyDictionary<string, BonePoseSample> MergeBoneSamples(
        IReadOnlyDictionary<string, BonePoseSample> motionBones,
        IReadOnlyDictionary<string, BonePoseSample> morphBones)
    {
        if (morphBones.Count == 0)
        {
            return motionBones;
        }

        var result = new Dictionary<string, BonePoseSample>(motionBones, StringComparer.Ordinal);
        foreach (var (name, morph) in morphBones)
        {
            if (result.TryGetValue(name, out var motion))
            {
                result[name] = new BonePoseSample(
                    motion.Translation + morph.Translation,
                    Quaternion.Normalize(morph.Orientation * motion.Orientation),
                    motion.PhysicsSimulationEnabled);
            }
            else
            {
                result[name] = morph;
            }
        }

        return result;
    }
}
