using System.Numerics;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderBatchOrderingTests
{
    [Fact]
    public void OrdersTransparentBatchesBackToFront()
    {
        var material = new RenderMaterial("transparent", new Vector4(1, 1, 1, 0.5f));
        var mesh = new RenderMesh(
            "mesh",
            [
                new RenderVertex(new Vector3(0, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -8), Vector3.UnitZ, Vector2.Zero)
            ],
            [0, 1, 2, 3, 4, 5],
            [
                new RenderMeshBatch(0, 3, material),
                new RenderMeshBatch(3, 3, material)
            ]);

        var ordered = RenderBatchOrdering.OrderTransparentBackToFront(mesh, Matrix4x4.Identity);

        Assert.Equal(3, ordered[0].StartIndex);
        Assert.Equal(0, ordered[1].StartIndex);
    }
}
