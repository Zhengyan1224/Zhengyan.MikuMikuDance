using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderFrameContext(
    MmdProject Project,
    int Width,
    int Height,
    double DeltaTimeSeconds);
