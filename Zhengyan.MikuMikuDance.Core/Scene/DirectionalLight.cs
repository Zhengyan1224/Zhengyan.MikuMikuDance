using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class DirectionalLight
{
    public Vector3 Color { get; set; } = new(0.6f, 0.6f, 0.6f);

    public Vector3 Direction { get; set; } = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.5f));
}
