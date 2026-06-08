namespace Zhengyan.MikuMikuDance.Rendering;

public sealed class AnimationPlaybackClock
{
    private readonly float _framesPerSecond;
    private double _frameCursor;

    public AnimationPlaybackClock(float framesPerSecond = 30f)
    {
        _framesPerSecond = framesPerSecond <= 0 ? 30f : framesPerSecond;
    }

    public int CurrentFrameIndex { get; private set; }

    public int Advance(double deltaTimeSeconds, int maxFrameIndex)
    {
        maxFrameIndex = Math.Max(0, maxFrameIndex);
        if (maxFrameIndex == 0)
        {
            CurrentFrameIndex = 0;
            _frameCursor = 0;
            return CurrentFrameIndex;
        }

        _frameCursor += Math.Max(0, deltaTimeSeconds) * _framesPerSecond;
        if (_frameCursor > maxFrameIndex)
        {
            _frameCursor %= maxFrameIndex + 1;
        }

        CurrentFrameIndex = Math.Clamp((int)Math.Floor(_frameCursor), 0, maxFrameIndex);
        return CurrentFrameIndex;
    }

    public void Seek(int frameIndex)
    {
        _frameCursor = Math.Max(0, frameIndex);
        CurrentFrameIndex = (int)Math.Floor(_frameCursor);
    }
}
