using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

public interface IOpenGlRenderer
{
    void Load(GL gl, RenderDeviceInfo deviceInfo);
}
