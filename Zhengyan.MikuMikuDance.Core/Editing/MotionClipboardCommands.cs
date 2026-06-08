using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Editing;

public sealed class CopyMotionKeyframesCommand : IUndoableCommand
{
    private readonly Motion _motion;
    private readonly MotionEditFilter _filter;

    public CopyMotionKeyframesCommand(Motion motion, MotionEditFilter filter = default, string? name = null)
    {
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _filter = filter;
        Name = name ?? "Copy keyframes";
    }

    public string Name { get; }

    public MotionKeyframeClipboard Clipboard { get; private set; } = MotionKeyframeClipboard.Empty;

    public void Execute()
    {
        Clipboard = MotionEditor.CopyKeyframesToClipboard(_motion, _filter);
    }

    public void Undo()
    {
    }
}

public sealed class PasteMotionKeyframesCommand : IUndoableCommand
{
    private readonly MotionSnapshotCommand _inner;

    public PasteMotionKeyframesCommand(
        Motion motion,
        MotionKeyframeClipboard clipboard,
        int targetStartFrameIndex,
        bool mirrored = false,
        string? name = null)
    {
        _inner = new MotionSnapshotCommand(
            name ?? (mirrored ? "Mirror paste keyframes" : "Paste keyframes"),
            motion,
            () => MotionEditor.PasteKeyframes(motion, clipboard, targetStartFrameIndex, mirrored));
    }

    public string Name => _inner.Name;

    public int AffectedCount => _inner.AffectedCount;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}
