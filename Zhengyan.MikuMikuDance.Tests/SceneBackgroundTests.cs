using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class SceneBackgroundTests
{
    [Fact]
    public void NormalizeDisablesImageWithoutSourceAndRepairsScale()
    {
        var background = new SceneBackground
        {
            VideoEnabled = true,
            VideoScale = float.NegativeInfinity,
            VideoFrameTime = TimeSpan.FromSeconds(-1),
            ImageEnabled = true,
            ImageScale = float.NaN
        };

        background.Normalize();

        Assert.False(background.VideoEnabled);
        Assert.Equal(1f, background.VideoScale);
        Assert.Equal(TimeSpan.Zero, background.VideoFrameTime);
        Assert.False(background.ImageEnabled);
        Assert.Equal(1f, background.ImageScale);
    }

    [Fact]
    public void ClearVideoResetsVideoState()
    {
        var background = new SceneBackground
        {
            VideoSource = new Uri("background.avi", UriKind.Relative),
            VideoEnabled = true,
            VideoOffsetX = -8,
            VideoOffsetY = 16,
            VideoScale = 0.5f,
            VideoFrameTime = TimeSpan.FromSeconds(3)
        };

        background.ClearVideo();

        Assert.Null(background.VideoSource);
        Assert.False(background.VideoEnabled);
        Assert.Equal(0, background.VideoOffsetX);
        Assert.Equal(0, background.VideoOffsetY);
        Assert.Equal(1f, background.VideoScale);
        Assert.Equal(TimeSpan.Zero, background.VideoFrameTime);
    }

    [Fact]
    public void ClearImageResetsImageState()
    {
        var background = new SceneBackground
        {
            ImageSource = new Uri("background.png", UriKind.Relative),
            ImageEnabled = true,
            ImageOffsetX = 12,
            ImageOffsetY = -4,
            ImageScale = 2f
        };

        background.ClearImage();

        Assert.Null(background.ImageSource);
        Assert.False(background.ImageEnabled);
        Assert.Equal(0, background.ImageOffsetX);
        Assert.Equal(0, background.ImageOffsetY);
        Assert.Equal(1f, background.ImageScale);
    }
}
