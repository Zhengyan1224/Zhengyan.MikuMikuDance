using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Editing;

public sealed class InsertTimelineFrameCommand : IUndoableCommand
{
    private readonly MotionSnapshotCommand _inner;

    public InsertTimelineFrameCommand(Motion motion, int frameIndex, int frameCount = 1, string? name = null)
    {
        _inner = new MotionSnapshotCommand(
            name ?? "Insert timeline frame",
            motion,
            () => MotionEditor.InsertTimelineFrames(motion, frameIndex, frameCount));
    }

    public string Name => _inner.Name;

    public int AffectedCount => _inner.AffectedCount;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}

public sealed class RemoveTimelineFrameCommand : IUndoableCommand
{
    private readonly MotionSnapshotCommand _inner;

    public RemoveTimelineFrameCommand(Motion motion, int frameIndex, int frameCount = 1, string? name = null)
    {
        _inner = new MotionSnapshotCommand(
            name ?? "Remove timeline frame",
            motion,
            () => MotionEditor.DeleteTimelineFrames(motion, frameIndex, frameCount));
    }

    public string Name => _inner.Name;

    public int AffectedCount => _inner.AffectedCount;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}
