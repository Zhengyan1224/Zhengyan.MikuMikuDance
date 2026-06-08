namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class SceneBackground
{
    public Uri? VideoSource { get; set; }

    public bool VideoEnabled { get; set; }

    public int VideoOffsetX { get; set; }

    public int VideoOffsetY { get; set; }

    public float VideoScale { get; set; } = 1f;

    public TimeSpan VideoFrameTime { get; set; }

    public Uri? ImageSource { get; set; }

    public bool ImageEnabled { get; set; }

    public int ImageOffsetX { get; set; }

    public int ImageOffsetY { get; set; }

    public float ImageScale { get; set; } = 1f;

    public void ClearVideo()
    {
        VideoSource = null;
        VideoEnabled = false;
        VideoOffsetX = 0;
        VideoOffsetY = 0;
        VideoScale = 1f;
        VideoFrameTime = TimeSpan.Zero;
    }

    public void ClearImage()
    {
        ImageSource = null;
        ImageEnabled = false;
        ImageOffsetX = 0;
        ImageOffsetY = 0;
        ImageScale = 1f;
    }

    public void Normalize()
    {
        if (VideoScale <= 0 || float.IsNaN(VideoScale) || float.IsInfinity(VideoScale))
        {
            VideoScale = 1f;
        }

        if (VideoFrameTime < TimeSpan.Zero)
        {
            VideoFrameTime = TimeSpan.Zero;
        }

        if (VideoSource is null)
        {
            VideoEnabled = false;
        }

        if (ImageScale <= 0 || float.IsNaN(ImageScale) || float.IsInfinity(ImageScale))
        {
            ImageScale = 1f;
        }

        if (ImageSource is null)
        {
            ImageEnabled = false;
        }
    }
}
