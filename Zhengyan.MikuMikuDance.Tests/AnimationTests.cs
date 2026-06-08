using Zhengyan.MikuMikuDance.Core.Animation;
using System.Numerics;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class AnimationTests
{
    [Fact]
    public void LinearBezierReturnsInput()
    {
        Assert.Equal(0f, BezierCurve.Linear.Evaluate(0f), precision: 3);
        Assert.Equal(0.5f, BezierCurve.Linear.Evaluate(0.5f), precision: 1);
        Assert.Equal(1f, BezierCurve.Linear.Evaluate(1f), precision: 3);
    }

    [Fact]
    public void TimelineNormalizesSelectionRange()
    {
        var timeline = new Timeline();

        timeline.SetSelectionRange(42, 7);

        Assert.Equal(7, timeline.SelectionRange.Start);
        Assert.Equal(42, timeline.SelectionRange.End);
    }

    [Fact]
    public void MotionSamplerInterpolatesBoneAndMorphKeyframes()
    {
        var motion = new Motion("sample", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe("center", 0, Vector3.Zero, Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new BoneKeyframe("center", 10, new Vector3(10, 20, 30), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), BoneInterpolation.Linear));
        motion.Add(new MorphKeyframe("smile", 0, 0));
        motion.Add(new MorphKeyframe("smile", 10, 1));

        var sample = MotionSampler.Sample(motion, 5);

        AssertVectorClose(new Vector3(5, 10, 15), sample.Bones["center"].Translation);
        Assert.Equal(0.5f, sample.Morphs["smile"].Weight, precision: 3);
    }

    [Fact]
    public void MotionSamplerUsesNextBoneKeyframeInterpolation()
    {
        var motion = new Motion("sample", MotionFormat.Vmd);
        var easeOut = new BezierCurve(new BezierControlPoint(0, 80), new BezierControlPoint(40, 127));
        var interpolation = BoneInterpolation.Linear with { TranslationX = easeOut };
        motion.Add(new BoneKeyframe("center", 0, Vector3.Zero, Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new BoneKeyframe("center", 10, new Vector3(10, 0, 0), Quaternion.Identity, interpolation));

        var sample = MotionSampler.SampleBone(motion, "center", 5);

        Assert.NotNull(sample);
        Assert.True(sample.Translation.X > 5);
    }

    [Fact]
    public void MotionSamplerInterpolatesCamera()
    {
        var motion = new Motion("sample", MotionFormat.Vmd);
        motion.Add(new CameraKeyframe(0, Vector3.Zero, Vector3.Zero, 10, 20, CameraInterpolation.Linear));
        motion.Add(new CameraKeyframe(10, new Vector3(0, 10, 0), new Vector3(1, 2, 3), 30, 40, CameraInterpolation.Linear, false));

        var sample = MotionSampler.Sample(motion, 5);

        Assert.NotNull(sample.Camera);
        AssertVectorClose(new Vector3(0, 5, 0), sample.Camera.LookAt);
        Assert.Equal(20, sample.Camera.Distance);
        Assert.Equal(30, sample.Camera.FieldOfView);
        Assert.False(sample.Camera.PerspectiveEnabled);
    }

    [Fact]
    public void MotionSamplerKeepsDiscreteStateUntilNextKeyframe()
    {
        var motion = new Motion("sample", MotionFormat.Vmd);
        motion.Add(new ModelKeyframe(0, true, new Dictionary<string, bool> { ["legIK"] = true }));
        motion.Add(new ModelKeyframe(10, false, new Dictionary<string, bool> { ["legIK"] = false }));

        var before = MotionSampler.Sample(motion, 5);
        var after = MotionSampler.Sample(motion, 10);

        Assert.NotNull(before.Model);
        Assert.True(before.Model.Visible);
        Assert.True(before.Model.IkStates["legIK"]);
        Assert.NotNull(after.Model);
        Assert.False(after.Model.Visible);
        Assert.False(after.Model.IkStates["legIK"]);
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 2);
        Assert.Equal(expected.Y, actual.Y, precision: 2);
        Assert.Equal(expected.Z, actual.Z, precision: 2);
    }
}
