using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Editing;

public enum MotionKeyframeKind
{
    Bone,
    Morph,
    Camera,
    Light,
    SelfShadow,
    Model,
    Accessory
}

public readonly record struct MotionKeyframeKey(MotionKeyframeKind Kind, string? TrackName, int FrameIndex)
{
    public static MotionKeyframeKey From(MotionKeyframe keyframe)
    {
        ArgumentNullException.ThrowIfNull(keyframe);
        return keyframe switch
        {
            BoneKeyframe bone => new MotionKeyframeKey(MotionKeyframeKind.Bone, bone.BoneName, bone.FrameIndex),
            MorphKeyframe morph => new MotionKeyframeKey(MotionKeyframeKind.Morph, morph.MorphName, morph.FrameIndex),
            CameraKeyframe camera => new MotionKeyframeKey(MotionKeyframeKind.Camera, null, camera.FrameIndex),
            LightKeyframe light => new MotionKeyframeKey(MotionKeyframeKind.Light, null, light.FrameIndex),
            SelfShadowKeyframe selfShadow => new MotionKeyframeKey(MotionKeyframeKind.SelfShadow, null, selfShadow.FrameIndex),
            ModelKeyframe model => new MotionKeyframeKey(MotionKeyframeKind.Model, null, model.FrameIndex),
            AccessoryKeyframe accessory => new MotionKeyframeKey(MotionKeyframeKind.Accessory, accessory.AccessoryName, accessory.FrameIndex),
            _ => throw new ArgumentException($"Unsupported keyframe type {keyframe.GetType().FullName}.", nameof(keyframe))
        };
    }

    public bool Matches(MotionKeyframe keyframe)
    {
        return From(keyframe) == this;
    }
}

public sealed class AddMotionKeyframeCommand : IUndoableCommand
{
    private readonly SnapshotUndoableCommand<MotionKeyframe[]> _inner;

    public AddMotionKeyframeCommand(Motion motion, MotionKeyframe keyframe, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(keyframe);
        _inner = MotionSnapshotCommands.Create(
            name ?? $"Add {MotionKeyframeKey.From(keyframe).Kind} keyframe",
            motion,
            () => motion.AddKeyframe(keyframe));
    }

    public string Name => _inner.Name;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}

public sealed class UpdateMotionKeyframeCommand : IUndoableCommand
{
    private readonly SnapshotUndoableCommand<MotionKeyframe[]> _inner;

    public UpdateMotionKeyframeCommand(Motion motion, MotionKeyframe keyframe, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(keyframe);
        _inner = MotionSnapshotCommands.Create(
            name ?? $"Update {MotionKeyframeKey.From(keyframe).Kind} keyframe",
            motion,
            () => motion.AddKeyframe(keyframe));
    }

    public string Name => _inner.Name;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}

public sealed class RemoveMotionKeyframeCommand : IUndoableCommand
{
    private readonly SnapshotUndoableCommand<MotionKeyframe[]> _inner;

    public RemoveMotionKeyframeCommand(Motion motion, MotionKeyframe keyframe, string? name = null)
        : this(motion, MotionKeyframeKey.From(keyframe), name)
    {
    }

    public RemoveMotionKeyframeCommand(Motion motion, MotionKeyframeKey key, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        _inner = MotionSnapshotCommands.Create(
            name ?? $"Remove {key.Kind} keyframe",
            motion,
            () => motion.RemoveKeyframes(key.Matches));
    }

    public string Name => _inner.Name;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}

public sealed class MotionSnapshotCommand : IUndoableCommand
{
    private readonly SnapshotUndoableCommand<MotionKeyframe[]> _inner;

    public MotionSnapshotCommand(string name, Motion motion, Action execute)
        : this(name, motion, () =>
        {
            execute();
            return -1;
        })
    {
    }

    public MotionSnapshotCommand(string name, Motion motion, Func<int> execute)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(execute);
        _inner = MotionSnapshotCommands.Create(name, motion, () => AffectedCount = execute());
    }

    public string Name => _inner.Name;

    public int AffectedCount { get; private set; }

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}

internal static class MotionSnapshotCommands
{
    public static SnapshotUndoableCommand<MotionKeyframe[]> Create(string name, Motion motion, Action execute)
    {
        return new SnapshotUndoableCommand<MotionKeyframe[]>(
            name,
            () => motion.EnumerateAllKeyframes().ToArray(),
            motion.ReplaceAllKeyframes,
            execute);
    }
}
