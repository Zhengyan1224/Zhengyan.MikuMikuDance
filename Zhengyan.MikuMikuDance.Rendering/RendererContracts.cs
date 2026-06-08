namespace Zhengyan.MikuMikuDance.Rendering;

public interface IRenderer : IDisposable
{
    void Load(RenderDeviceInfo deviceInfo);

    void Resize(int width, int height);

    void Render(RenderFrameContext context);
}

public interface IRenderHost : IDisposable
{
    void Run(IRenderer renderer);
}

public sealed record RenderDeviceInfo(string ApiName, string Version, string Vendor, string Renderer);
