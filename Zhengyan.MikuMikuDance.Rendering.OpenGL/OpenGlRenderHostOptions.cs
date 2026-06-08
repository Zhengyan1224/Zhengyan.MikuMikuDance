using System.Numerics;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

public sealed record OpenGlRenderHostOptions(
    string Title,
    int Width,
    int Height,
    Vector4 ClearColor)
{
    public static OpenGlRenderHostOptions Default { get; } = new(
        "Zhengyan MikuMikuDance",
        1280,
        800,
        new Vector4(0.08f, 0.09f, 0.1f, 1f));
}
