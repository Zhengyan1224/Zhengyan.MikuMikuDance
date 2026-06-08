using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Editing;
using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class UndoRedoCommandTests
{
    [Fact]
    public void BatchAddsAllMotionKeyframeKindsAndUndoRedoRestoresThem()
    {
        var motion = new Motion("commands", MotionFormat.Vmd);
        var stack = new UndoRedoStack();
        var keyframes = new MotionKeyframe[]
        {
            new BoneKeyframe("center", 1, Vector3.One, Quaternion.Identity, BoneInterpolation.Linear),
            new MorphKeyframe("smile", 2, 0.5f),
            new CameraKeyframe(3, Vector3.Zero, Vector3.Zero, 10, 30, CameraInterpolation.Linear),
            new LightKeyframe(4, Vector3.One, -Vector3.UnitY),
            new SelfShadowKeyframe(5, 1, 0.4f),
            new ModelKeyframe(6, true, new Dictionary<string, bool> { ["legIK"] = true }),
            new AccessoryKeyframe("stage", 7, true, Vector3.Zero, Vector3.Zero, 1, 1)
        };

        stack.Execute(new BatchUndoableCommand(
            "Add all keyframes",
            keyframes.Select(keyframe => new AddMotionKeyframeCommand(motion, keyframe))));

        Assert.Single(motion.BoneKeyframes);
        Assert.Single(motion.MorphKeyframes);
        Assert.Single(motion.CameraKeyframes);
        Assert.Single(motion.LightKeyframes);
        Assert.Single(motion.SelfShadowKeyframes);
        Assert.Single(motion.ModelKeyframes);
        Assert.Single(motion.AccessoryKeyframes);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal("Add all keyframes", stack.UndoName);

        Assert.True(stack.TryUndo());
        Assert.Empty(motion.EnumerateAllKeyframes());
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);

        Assert.True(stack.TryRedo());
        Assert.Equal(7, motion.EnumerateAllKeyframes().Count());
    }

    [Fact]
    public void UpdatesAndRemovesMotionKeyframesWithUndoRedo()
    {
        var motion = new Motion("update", MotionFormat.Vmd);
        var stack = new UndoRedoStack();
        motion.Add(new MorphKeyframe("smile", 10, 0.25f));

        stack.Execute(new UpdateMotionKeyframeCommand(motion, new MorphKeyframe("smile", 10, 0.75f)));

        Assert.Equal(0.75f, motion.MorphKeyframes.Single().Weight);
        Assert.True(stack.TryUndo());
        Assert.Equal(0.25f, motion.MorphKeyframes.Single().Weight);
        Assert.True(stack.TryRedo());
        Assert.Equal(0.75f, motion.MorphKeyframes.Single().Weight);

        stack.Execute(new RemoveMotionKeyframeCommand(
            motion,
            new MotionKeyframeKey(MotionKeyframeKind.Morph, "smile", 10)));

        Assert.Empty(motion.MorphKeyframes);
        Assert.True(stack.TryUndo());
        Assert.Equal(0.75f, motion.MorphKeyframes.Single().Weight);
        Assert.True(stack.TryRedo());
        Assert.Empty(motion.MorphKeyframes);
    }

    [Fact]
    public void MotionSnapshotCommandRestoresBulkEditorMutations()
    {
        var motion = new Motion("snapshot", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe("center", 1, Vector3.Zero, Quaternion.Identity, BoneInterpolation.Linear)
        {
            IsSelected = true
        });
        motion.Add(new BoneKeyframe("center", 5, Vector3.One, Quaternion.Identity, BoneInterpolation.Linear));
        var command = new MotionSnapshotCommand(
            "Move selected keyframes",
            motion,
            () => MotionEditor.MoveKeyframes(motion, 10, new MotionEditFilter(SelectedOnly: true)));
        var stack = new UndoRedoStack();

        stack.Execute(command);

        Assert.Equal(1, command.AffectedCount);
        Assert.Contains(motion.BoneKeyframes, keyframe => keyframe.FrameIndex == 11 && keyframe.IsSelected);
        Assert.True(stack.TryUndo());
        Assert.Contains(motion.BoneKeyframes, keyframe => keyframe.FrameIndex == 1 && keyframe.IsSelected);
        Assert.True(stack.TryRedo());
        Assert.Contains(motion.BoneKeyframes, keyframe => keyframe.FrameIndex == 11 && keyframe.IsSelected);
    }

    [Fact]
    public void TimelineFrameCommandsUndoAndRedo()
    {
        var motion = new Motion("timeline", MotionFormat.Vmd);
        motion.Add(new MorphKeyframe("smile", 5, 0.25f));
        motion.Add(new MorphKeyframe("smile", 10, 0.5f));
        motion.Add(new MorphKeyframe("smile", 20, 0.75f));
        var stack = new UndoRedoStack();
        var insert = new InsertTimelineFrameCommand(motion, 10, 5);

        stack.Execute(insert);

        Assert.Equal(2, insert.AffectedCount);
        Assert.Equal([5, 15, 25], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
        Assert.True(stack.TryUndo());
        Assert.Equal([5, 10, 20], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
        Assert.True(stack.TryRedo());
        Assert.Equal([5, 15, 25], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());

        var remove = new RemoveTimelineFrameCommand(motion, 10, 10);
        stack.Execute(remove);

        Assert.Equal(2, remove.AffectedCount);
        Assert.Equal([5, 15], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
        Assert.True(stack.TryUndo());
        Assert.Equal([5, 15, 25], motion.MorphKeyframes.Select(frame => frame.FrameIndex).Order().ToArray());
    }

    [Fact]
    public void CopyPasteCommandsUndoAndRedo()
    {
        var motion = new Motion("paste", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe("左腕", 10, Vector3.One, Quaternion.Identity, BoneInterpolation.Linear)
        {
            IsSelected = true
        });
        motion.Add(new BoneKeyframe("左腕", 20, new Vector3(99, 0, 0), Quaternion.Identity, BoneInterpolation.Linear));
        var stack = new UndoRedoStack();
        var copy = new CopyMotionKeyframesCommand(motion, new MotionEditFilter(SelectedOnly: true));

        copy.Execute();
        stack.Execute(new PasteMotionKeyframesCommand(motion, copy.Clipboard, 20));

        Assert.Equal(1, motion.BoneKeyframes.Single(frame => frame.FrameIndex == 20).Translation.X);
        Assert.True(stack.TryUndo());
        Assert.Equal(99, motion.BoneKeyframes.Single(frame => frame.FrameIndex == 20).Translation.X);
        Assert.True(stack.TryRedo());
        Assert.Equal(1, motion.BoneKeyframes.Single(frame => frame.FrameIndex == 20).Translation.X);

        stack.Execute(new PasteMotionKeyframesCommand(motion, copy.Clipboard, 30, mirrored: true));

        var mirrored = motion.BoneKeyframes.Single(frame => frame.BoneName == "右腕" && frame.FrameIndex == 30);
        Assert.Equal(-1, mirrored.Translation.X);
        Assert.True(stack.TryUndo());
        Assert.DoesNotContain(motion.BoneKeyframes, frame => frame.BoneName == "右腕");
    }

    [Fact]
    public void ModelSnapshotCommandRestoresModelEdits()
    {
        var model = new MmdModel(ModelFormat.Pmx)
        {
            Name = "before",
            EnglishName = "Before",
            Visible = true
        };
        model.AddTexture("before.png");
        model.SetSharedToonTexture(0, "toon01.png");
        model.AddBone(CreateBone("center"));
        var stack = new UndoRedoStack();

        stack.Execute(new ModelSnapshotCommand("Edit model", model, () =>
        {
            model.Name = "after";
            model.Visible = false;
            model.ReplaceTextures(["after.png"]);
            model.ReplaceSharedToonTextures(["toon02.png"]);
            model.ReplaceBones([CreateBone("head")]);
        }));

        Assert.Equal("after", model.Name);
        Assert.False(model.Visible);
        Assert.Equal("after.png", model.Textures.Single());
        Assert.Equal("head", model.Bones.Single().Name);

        Assert.True(stack.TryUndo());
        Assert.Equal("before", model.Name);
        Assert.True(model.Visible);
        Assert.Equal("before.png", model.Textures.Single());
        Assert.Equal("toon01.png", model.SharedToonTextures[0]);
        Assert.Equal("center", model.Bones.Single().Name);

        Assert.True(stack.TryRedo());
        Assert.Equal("after", model.Name);
        Assert.False(model.Visible);
        Assert.Equal("toon02.png", model.SharedToonTextures[0]);
        Assert.Equal("head", model.Bones.Single().Name);
    }

    private static Bone CreateBone(string name)
    {
        return new Bone(
            name,
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Visible | BoneFlags.Enabled,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null);
    }
}
