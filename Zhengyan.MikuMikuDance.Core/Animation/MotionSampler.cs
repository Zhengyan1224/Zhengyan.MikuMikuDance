using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public static class MotionSampler
{
    public static MotionSample Sample(Motion motion, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(motion);
        frameIndex = Math.Max(0, frameIndex);
        return new MotionSample(
            frameIndex,
            SampleBones(motion.BoneKeyframes, frameIndex),
            SampleMorphs(motion.MorphKeyframes, frameIndex),
            SampleCamera(motion.CameraKeyframes, frameIndex),
            SampleDiscrete(motion.LightKeyframes, frameIndex, ToLightSample),
            SampleDiscrete(motion.SelfShadowKeyframes, frameIndex, ToSelfShadowSample),
            SampleDiscrete(motion.ModelKeyframes, frameIndex, ToModelSample),
            SampleAccessories(motion.AccessoryKeyframes, frameIndex));
    }

    public static BonePoseSample? SampleBone(Motion motion, string boneName, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentException.ThrowIfNullOrWhiteSpace(boneName);
        return SampleNamed(motion.BoneKeyframes, boneName, frameIndex, frame => frame.BoneName, InterpolateBone);
    }

    public static MorphWeightSample? SampleMorph(Motion motion, string morphName, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentException.ThrowIfNullOrWhiteSpace(morphName);
        return SampleNamed(motion.MorphKeyframes, morphName, frameIndex, frame => frame.MorphName, InterpolateMorph);
    }

    private static IReadOnlyDictionary<string, BonePoseSample> SampleBones(
        IReadOnlyList<BoneKeyframe> keyframes,
        int frameIndex)
    {
        return SampleNamedGroups(keyframes, frameIndex, frame => frame.BoneName, InterpolateBone);
    }

    private static IReadOnlyDictionary<string, MorphWeightSample> SampleMorphs(
        IReadOnlyList<MorphKeyframe> keyframes,
        int frameIndex)
    {
        return SampleNamedGroups(keyframes, frameIndex, frame => frame.MorphName, InterpolateMorph);
    }

    private static IReadOnlyDictionary<string, AccessorySample> SampleAccessories(
        IReadOnlyList<AccessoryKeyframe> keyframes,
        int frameIndex)
    {
        return SampleNamedGroups(keyframes, frameIndex, frame => frame.AccessoryName, InterpolateAccessory);
    }

    private static CameraSample? SampleCamera(IReadOnlyList<CameraKeyframe> keyframes, int frameIndex)
    {
        if (keyframes.Count == 0)
        {
            return null;
        }

        return SampleOrdered(keyframes, frameIndex, InterpolateCamera);
    }

    private static IReadOnlyDictionary<string, TResult> SampleNamedGroups<TKeyframe, TResult>(
        IReadOnlyList<TKeyframe> keyframes,
        int frameIndex,
        Func<TKeyframe, string> nameSelector,
        Func<TKeyframe, TKeyframe, int, TResult> interpolator)
        where TKeyframe : MotionKeyframe
    {
        if (keyframes.Count == 0)
        {
            return new Dictionary<string, TResult>(0, StringComparer.Ordinal);
        }

        var result = new Dictionary<string, TResult>(StringComparer.Ordinal);
        foreach (var group in keyframes.GroupBy(nameSelector, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(frame => frame.FrameIndex).ToArray();
            if (ordered.Length == 0)
            {
                continue;
            }

            result[group.Key] = SampleOrdered(ordered, frameIndex, interpolator);
        }

        return result;
    }

    private static TResult? SampleNamed<TKeyframe, TResult>(
        IReadOnlyList<TKeyframe> keyframes,
        string name,
        int frameIndex,
        Func<TKeyframe, string> nameSelector,
        Func<TKeyframe, TKeyframe, int, TResult> interpolator)
        where TKeyframe : MotionKeyframe
        where TResult : class
    {
        var ordered = keyframes
            .Where(frame => string.Equals(nameSelector(frame), name, StringComparison.Ordinal))
            .OrderBy(frame => frame.FrameIndex)
            .ToArray();
        return ordered.Length == 0 ? null : SampleOrdered(ordered, frameIndex, interpolator);
    }

    private static TResult? SampleDiscrete<TKeyframe, TResult>(
        IReadOnlyList<TKeyframe> keyframes,
        int frameIndex,
        Func<TKeyframe, TResult> converter)
        where TKeyframe : MotionKeyframe
        where TResult : class
    {
        if (keyframes.Count == 0)
        {
            return null;
        }

        var ordered = keyframes.OrderBy(frame => frame.FrameIndex).ToArray();
        var frame = ordered[0];
        foreach (var candidate in ordered)
        {
            if (candidate.FrameIndex > frameIndex)
            {
                break;
            }

            frame = candidate;
        }

        return converter(frame);
    }

    private static TResult SampleOrdered<TKeyframe, TResult>(
        IReadOnlyList<TKeyframe> orderedKeyframes,
        int frameIndex,
        Func<TKeyframe, TKeyframe, int, TResult> interpolator)
        where TKeyframe : MotionKeyframe
    {
        var previous = orderedKeyframes[0];
        if (frameIndex <= previous.FrameIndex)
        {
            return interpolator(previous, previous, previous.FrameIndex);
        }

        for (var i = 1; i < orderedKeyframes.Count; i++)
        {
            var next = orderedKeyframes[i];
            if (frameIndex <= next.FrameIndex)
            {
                return interpolator(previous, next, frameIndex);
            }

            previous = next;
        }

        return interpolator(previous, previous, previous.FrameIndex);
    }

    private static BonePoseSample InterpolateBone(BoneKeyframe previous, BoneKeyframe next, int frameIndex)
    {
        var tx = Coefficient(previous, next, frameIndex, next.Interpolation.TranslationX);
        var ty = Coefficient(previous, next, frameIndex, next.Interpolation.TranslationY);
        var tz = Coefficient(previous, next, frameIndex, next.Interpolation.TranslationZ);
        var to = Coefficient(previous, next, frameIndex, next.Interpolation.Orientation);
        var translation = new Vector3(
            Lerp(previous.Translation.X, next.Translation.X, tx),
            Lerp(previous.Translation.Y, next.Translation.Y, ty),
            Lerp(previous.Translation.Z, next.Translation.Z, tz));
        var orientation = Quaternion.Normalize(Quaternion.Slerp(previous.Orientation, next.Orientation, to));
        return new BonePoseSample(translation, orientation, next.PhysicsSimulationEnabled);
    }

    private static MorphWeightSample InterpolateMorph(MorphKeyframe previous, MorphKeyframe next, int frameIndex)
    {
        var t = Coefficient(previous.FrameIndex, next.FrameIndex, frameIndex);
        return new MorphWeightSample(Lerp(previous.Weight, next.Weight, t));
    }

    private static CameraSample InterpolateCamera(CameraKeyframe previous, CameraKeyframe next, int frameIndex)
    {
        var tx = Coefficient(previous, next, frameIndex, next.Interpolation.LookAtX);
        var ty = Coefficient(previous, next, frameIndex, next.Interpolation.LookAtY);
        var tz = Coefficient(previous, next, frameIndex, next.Interpolation.LookAtZ);
        var ta = Coefficient(previous, next, frameIndex, next.Interpolation.Angle);
        var td = Coefficient(previous, next, frameIndex, next.Interpolation.Distance);
        var tf = Coefficient(previous, next, frameIndex, next.Interpolation.FieldOfView);
        return new CameraSample(
            new Vector3(
                Lerp(previous.LookAt.X, next.LookAt.X, tx),
                Lerp(previous.LookAt.Y, next.LookAt.Y, ty),
                Lerp(previous.LookAt.Z, next.LookAt.Z, tz)),
            Vector3.Lerp(previous.Angle, next.Angle, ta),
            Lerp(previous.Distance, next.Distance, td),
            (int)MathF.Round(Lerp(previous.FieldOfView, next.FieldOfView, tf)),
            next.PerspectiveEnabled);
    }

    private static AccessorySample InterpolateAccessory(AccessoryKeyframe previous, AccessoryKeyframe next, int frameIndex)
    {
        var t = Coefficient(previous.FrameIndex, next.FrameIndex, frameIndex);
        return new AccessorySample(
            next.Visible,
            Vector3.Lerp(previous.Translation, next.Translation, t),
            Vector3.Lerp(previous.Orientation, next.Orientation, t),
            Lerp(previous.Scale, next.Scale, t),
            Lerp(previous.Opacity, next.Opacity, t),
            next.ParentModelName,
            next.ParentBoneName);
    }

    private static LightSample ToLightSample(LightKeyframe keyframe)
    {
        return new LightSample(keyframe.Color, keyframe.Direction);
    }

    private static SelfShadowSample ToSelfShadowSample(SelfShadowKeyframe keyframe)
    {
        return new SelfShadowSample(keyframe.Mode, keyframe.Distance);
    }

    private static ModelSample ToModelSample(ModelKeyframe keyframe)
    {
        return new ModelSample(keyframe.Visible, new Dictionary<string, bool>(keyframe.IkStates, StringComparer.Ordinal));
    }

    private static float Coefficient(MotionKeyframe previous, MotionKeyframe next, int frameIndex, BezierCurve curve)
    {
        return curve.Evaluate(Coefficient(previous.FrameIndex, next.FrameIndex, frameIndex));
    }

    private static float Coefficient(int previousFrameIndex, int nextFrameIndex, int frameIndex)
    {
        if (previousFrameIndex == nextFrameIndex)
        {
            return 1f;
        }

        return Math.Clamp((frameIndex - previousFrameIndex) / (float)(nextFrameIndex - previousFrameIndex), 0f, 1f);
    }

    private static float Lerp(float previous, float next, float t)
    {
        return previous + ((next - previous) * t);
    }
}
