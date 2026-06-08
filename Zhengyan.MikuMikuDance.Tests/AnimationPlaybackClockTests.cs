using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class AnimationPlaybackClockTests
{
    [Fact]
    public void AdvancesFramesByElapsedSeconds()
    {
        var clock = new AnimationPlaybackClock(30);

        var frame = clock.Advance(0.5, 100);

        Assert.Equal(15, frame);
    }

    [Fact]
    public void WrapsAtMaxFrame()
    {
        var clock = new AnimationPlaybackClock(30);

        var frame = clock.Advance(1, 10);

        Assert.Equal(8, frame);
    }

    [Fact]
    public void IgnoresNegativeDelta()
    {
        var clock = new AnimationPlaybackClock(30);
        clock.Seek(5);

        var frame = clock.Advance(-1, 100);

        Assert.Equal(5, frame);
    }
}
