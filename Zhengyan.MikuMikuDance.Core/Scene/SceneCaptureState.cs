namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class SceneCaptureState
{
    public bool IsRequested { get; private set; }

    public RenderCaptureFrame? LastFrame { get; private set; }

    public void Request()
    {
        IsRequested = true;
    }

    public bool ConsumeRequest()
    {
        if (!IsRequested)
        {
            return false;
        }

        IsRequested = false;
        return true;
    }

    public void Complete(RenderCaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        LastFrame = frame;
    }

    public void Clear()
    {
        IsRequested = false;
        LastFrame = null;
    }
}

public sealed record RenderCaptureFrame(
    int Width,
    int Height,
    DateTimeOffset CapturedAt,
    byte[] RgbaPixels)
{
    public int ByteLength => RgbaPixels.Length;
}
