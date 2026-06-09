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

    [Fact]
    public void SplitsOpaqueAndTransparentBatches()
    {
        var opaque = new RenderMaterial("opaque", Vector4.One);
        var textured = new RenderMaterial("textured", Vector4.One, TexturePath: "diffuse.png");
        var transparent = new RenderMaterial("transparent", new Vector4(1, 1, 1, 0.5f));
        var mesh = new RenderMesh(
            "mesh",
            [
                new RenderVertex(new Vector3(0, 0, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 0, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -8), Vector3.UnitZ, Vector2.Zero)
            ],
            [0, 1, 2, 3, 4, 5, 6, 7, 8],
            [
                new RenderMeshBatch(0, 3, opaque),
                new RenderMeshBatch(3, 3, textured),
                new RenderMeshBatch(6, 3, transparent)
            ]);

        var plan = RenderBatchOrdering.CreatePlan(mesh, Matrix4x4.Identity);

        Assert.Equal([0, 3], plan.OpaqueBatches.Select(batch => batch.StartIndex).ToArray());
        Assert.Equal([6], plan.TransparentBatches.Select(batch => batch.StartIndex).ToArray());
    }

    [Fact]
    public void SupportsTextureAlphaTransparencyClassifier()
    {
        var opaque = new RenderMaterial("opaque", Vector4.One);
        var textured = new RenderMaterial("textured", Vector4.One, TexturePath: "diffuse.png");
        var transparent = new RenderMaterial("transparent", new Vector4(1, 1, 1, 0.5f));
        var mesh = new RenderMesh(
            "mesh",
            [
                new RenderVertex(new Vector3(0, 0, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -1), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 0, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -4), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -8), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -8), Vector3.UnitZ, Vector2.Zero)
            ],
            [0, 1, 2, 3, 4, 5, 6, 7, 8],
            [
                new RenderMeshBatch(0, 3, opaque),
                new RenderMeshBatch(3, 3, textured),
                new RenderMeshBatch(6, 3, transparent)
            ]);

        var plan = RenderBatchOrdering.CreatePlan(
            mesh,
            Matrix4x4.Identity,
            material => material.RequiresTransparentPass || !string.IsNullOrWhiteSpace(material.TexturePath));

        Assert.Equal([0], plan.OpaqueBatches.Select(batch => batch.StartIndex).ToArray());
        Assert.Equal([6, 3], plan.TransparentBatches.Select(batch => batch.StartIndex).ToArray());
    }

    [Fact]
    public void KeepsTransparentBatchesStableAtSameDepth()
    {
        var material = new RenderMaterial("transparent", new Vector4(1, 1, 1, 0.5f));
        var mesh = new RenderMesh(
            "mesh",
            [
                new RenderVertex(new Vector3(0, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(1, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(0, 1, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(2, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(3, 0, -2), Vector3.UnitZ, Vector2.Zero),
                new RenderVertex(new Vector3(2, 1, -2), Vector3.UnitZ, Vector2.Zero)
            ],
            [0, 1, 2, 3, 4, 5],
            [
                new RenderMeshBatch(0, 3, material),
                new RenderMeshBatch(3, 3, material)
            ]);

        var plan = RenderBatchOrdering.CreatePlan(mesh, Matrix4x4.Identity);

        Assert.Equal([0, 3], plan.TransparentBatches.Select(batch => batch.StartIndex).ToArray());
    }
}
