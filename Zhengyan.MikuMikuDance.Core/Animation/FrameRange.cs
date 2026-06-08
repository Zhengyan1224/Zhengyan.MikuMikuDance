namespace Zhengyan.MikuMikuDance.Core.Animation;

public readonly record struct FrameRange(int Start, int End)
{
    public static FrameRange Empty => new(0, 0);

    public int Length => Math.Max(0, End - Start);

    public bool Contains(int frameIndex)
    {
        return frameIndex >= Start && frameIndex <= End;
    }

    public FrameRange Normalize()
    {
        return Start <= End ? this : new FrameRange(End, Start);
    }
}
