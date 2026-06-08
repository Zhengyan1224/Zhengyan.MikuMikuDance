using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class MotionEditorTests
{
    [Fact]
    public void MovesSelectedKeyframesAndPreservesPayload()
    {
        var motion = new Motion("edit", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe("center", 10, new Vector3(1, 2, 3), Quaternion.Identity, BoneInterpolation.Linear)
        {
            IsSelected = true,
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["note"] = "keep"
            }
        });
        motion.Add(new BoneKeyframe("center", 20, Vector3.Zero, Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new MorphKeyframe("smile", 12, 0.5f)
        {
            IsSelected = true
        });
        motion.Add(new CameraKeyframe(14, Vector3.One, Vector3.Zero, 30, 40, CameraInterpolation.Linear)
        {
            IsSelected = true
        });

        var count = MotionEditor.MoveKeyframes(motion, 5, new MotionEditFilter(SelectedOnly: true));

        Assert.Equal(3, count);
        Assert.Equal(15, motion.BoneKeyframes.Single(frame => frame.IsSelected).FrameIndex);
        Assert.Equal("keep", motion.BoneKeyframes.Single(frame => frame.IsSelected).Annotations["note"]);
        Assert.Contains(motion.BoneKeyframes, frame => frame.FrameIndex == 20 && !frame.IsSelected);
        Assert.Equal(17, motion.MorphKeyframes[0].FrameIndex);
        Assert.Equal(19, motion.CameraKeyframes[0].FrameIndex);
    }

    [Fact]
    public void ScalesKeyframesAroundPivot()
    {
        var motion = new Motion("scale", MotionFormat.Vmd);
        motion.Add(new LightKeyframe(10, Vector3.One, Vector3.UnitY));
        motion.Add(new SelfShadowKeyframe(20, 1, 50));
        motion.Add(new ModelKeyframe(30, true, new Dictionary<string, bool>()));

        var count = MotionEditor.ScaleKeyframes(motion, 0.5f, pivotFrameIndex: 10);

        Assert.Equal(3, count);
        Assert.Equal(10, motion.LightKeyframes[0].FrameIndex);
        Assert.Equal(15, motion.SelfShadowKeyframes[0].FrameIndex);
        Assert.Equal(20, motion.ModelKeyframes[0].FrameIndex);
    }

    [Fact]
    public void CopiesRangeToTargetStartAndReplacesCollidingKeys()
    {
        var motion = new Motion("copy", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe("center", 10, new Vector3(1, 0, 0), Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new BoneKeyframe("center", 12, new Vector3(2, 0, 0), Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new BoneKeyframe("center", 20, new Vector3(99, 0, 0), Quaternion.Identity, BoneInterpolation.Linear));
        motion.Add(new AccessoryKeyframe("stage", 11, true, Vector3.One, Vector3.Zero, 1, 1));

        var count = MotionEditor.CopyKeyframes(motion, 20, new MotionEditFilter(new FrameRange(10, 12)));

        Assert.Equal(3, count);
        Assert.Equal(4, motion.BoneKeyframes.Count);
        Assert.Equal(1, motion.BoneKeyframes.Single(frame => frame.FrameIndex == 20).Translation.X);
        Assert.Equal(2, motion.BoneKeyframes.Single(frame => frame.FrameIndex == 22).Translation.X);
        Assert.Contains(motion.AccessoryKeyframes, frame => frame.FrameIndex == 21);
    }

    [Fact]
    public void DeletesRangeAndClampsMovedFramesToZero()
    {
        var motion = new Motion("delete", MotionFormat.Vmd);
        motion.Add(new MorphKeyframe("smile", 3, 0.25f));
        motion.Add(new MorphKeyframe("smile", 6, 0.5f));
        motion.Add(new MorphKeyframe("smile", 9, 0.75f));

        var deleted = MotionEditor.DeleteKeyframes(motion, new MotionEditFilter(new FrameRange(4, 7)));
        var moved = MotionEditor.MoveKeyframes(motion, -10);

        Assert.Equal(1, deleted);
        Assert.Equal(2, moved);
        Assert.Equal([0], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
    }

    [Fact]
    public void InsertsAndDeletesTimelineFrames()
    {
        var motion = new Motion("frames", MotionFormat.Vmd);
        motion.Add(new MorphKeyframe("smile", 5, 0.25f));
        motion.Add(new MorphKeyframe("smile", 10, 0.5f));
        motion.Add(new MorphKeyframe("smile", 20, 0.75f));

        var inserted = MotionEditor.InsertTimelineFrames(motion, 10, 3);

        Assert.Equal(2, inserted);
        Assert.Equal([5, 13, 23], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());

        var deleted = MotionEditor.DeleteTimelineFrames(motion, 10, 5);

        Assert.Equal(2, deleted);
        Assert.Equal([5, 18], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
    }

    [Fact]
    public void ClipboardPastesAndMirrorPastesKeyframes()
    {
        var motion = new Motion("clipboard", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe(
            "左腕",
            10,
            new Vector3(1, 2, 3),
            new Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
            BoneInterpolation.Linear)
        {
            IsSelected = true
        });
        motion.Add(new AccessoryKeyframe("stage", 12, true, new Vector3(2, 0, 0), new Vector3(0.1f, 0.2f, 0.3f), 1, 1)
        {
            IsSelected = true
        });

        var clipboard = MotionEditor.CopyKeyframesToClipboard(motion, new MotionEditFilter(SelectedOnly: true));
        var pasted = MotionEditor.PasteKeyframes(motion, clipboard, 20);
        var mirrored = MotionEditor.PasteKeyframes(motion, clipboard, 30, mirrored: true);

        Assert.Equal(2, pasted);
        Assert.Equal(2, mirrored);
        Assert.Contains(motion.BoneKeyframes, frame => frame.BoneName == "左腕" && frame.FrameIndex == 20);
        var mirroredBone = motion.BoneKeyframes.Single(frame => frame.BoneName == "右腕" && frame.FrameIndex == 30);
        Assert.Equal(-1, mirroredBone.Translation.X);
        Assert.Equal(2, mirroredBone.Translation.Y);
        Assert.True(mirroredBone.Orientation.Y < 0);
        Assert.True(mirroredBone.Orientation.Z < 0);
        var mirroredAccessory = motion.AccessoryKeyframes.Single(frame => frame.FrameIndex == 32);
        Assert.Equal(-2, mirroredAccessory.Translation.X);
        Assert.Equal(-0.2f, mirroredAccessory.Orientation.Y);
        Assert.Equal(-0.3f, mirroredAccessory.Orientation.Z);
    }
}
