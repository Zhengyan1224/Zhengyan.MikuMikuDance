namespace Zhengyan.MikuMikuDance.Core.Animation;

public sealed class Timeline
{
    public int CurrentFrameIndex { get; private set; }

    public FrameRange SelectionRange { get; private set; } = FrameRange.Empty;

    public FrameRange PlaybackRange { get; private set; } = FrameRange.Empty;

    public bool LoopEnabled { get; set; }

    public void Seek(int frameIndex)
    {
        CurrentFrameIndex = Math.Max(0, frameIndex);
    }

    public void MoveBy(int delta)
    {
        Seek(CurrentFrameIndex + delta);
    }

    public void SetSelectionRange(int start, int end)
    {
        SelectionRange = new FrameRange(start, end).Normalize();
    }

    public void SetPlaybackRange(int start, int end)
    {
        PlaybackRange = new FrameRange(start, end).Normalize();
    }
}
