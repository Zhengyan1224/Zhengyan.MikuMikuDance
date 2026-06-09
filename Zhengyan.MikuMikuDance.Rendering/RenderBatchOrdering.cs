using System.Numerics;

namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderBatchOrdering
{
    public static RenderBatchOrderingPlan CreatePlan(
        RenderMesh mesh,
        Matrix4x4 viewMatrix,
        Func<RenderMaterial, bool>? requiresTransparentPass = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var shouldDrawTransparent = requiresTransparentPass ?? RequiresDefaultTransparentPass;

        var indexedBatches = mesh.Batches
            .Select((batch, index) => new IndexedRenderMeshBatch(batch, index))
            .ToArray();
        var opaque = indexedBatches
            .Where(item => !shouldDrawTransparent(item.Batch.Material))
            .OrderBy(item => item.Index)
            .Select(item => item.Batch)
            .ToArray();
        var transparent = indexedBatches
            .Where(item => shouldDrawTransparent(item.Batch.Material))
            .Select(item => new
            {
                item.Batch,
                item.Index,
                Depth = EstimateViewDepth(mesh, item.Batch, viewMatrix)
            })
            .OrderBy(item => item.Depth)
            .ThenBy(item => item.Index)
            .Select(item => item.Batch)
            .ToArray();
        return new RenderBatchOrderingPlan(opaque, transparent);
    }

    public static IReadOnlyList<RenderMeshBatch> OrderTransparentBackToFront(RenderMesh mesh, Matrix4x4 viewMatrix)
    {
        return CreatePlan(mesh, viewMatrix).TransparentBatches;
    }

    public static float EstimateViewDepth(RenderMesh mesh, RenderMeshBatch batch, Matrix4x4 viewMatrix)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (batch.IndexCount <= 0 || mesh.Indices.Count == 0 || mesh.Vertices.Count == 0)
        {
            return 0;
        }

        var end = Math.Min(mesh.Indices.Count, batch.StartIndex + batch.IndexCount);
        var sum = 0f;
        var count = 0;
        for (var i = Math.Max(0, batch.StartIndex); i < end; i++)
        {
            var vertexIndex = mesh.Indices[i];
            if (vertexIndex >= mesh.Vertices.Count)
            {
                continue;
            }

            var worldPosition = Vector3.Transform(mesh.Vertices[(int)vertexIndex].Position, mesh.WorldTransform);
            var viewPosition = Vector3.Transform(worldPosition, viewMatrix);
            sum += viewPosition.Z;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private sealed record IndexedRenderMeshBatch(RenderMeshBatch Batch, int Index);

    private static bool RequiresDefaultTransparentPass(RenderMaterial material)
    {
        return material.RequiresTransparentPass;
    }
}

public sealed record RenderBatchOrderingPlan(
    IReadOnlyList<RenderMeshBatch> OpaqueBatches,
    IReadOnlyList<RenderMeshBatch> TransparentBatches);
