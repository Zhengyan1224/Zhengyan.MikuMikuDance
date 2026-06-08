namespace Zhengyan.MikuMikuDance.Rendering;

public sealed class NullRenderer : IRenderer
{
    public RenderDeviceInfo? DeviceInfo { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Load(RenderDeviceInfo deviceInfo)
    {
        DeviceInfo = deviceInfo;
    }

    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void Render(RenderFrameContext context)
    {
    }

    public void Dispose()
    {
    }
}
