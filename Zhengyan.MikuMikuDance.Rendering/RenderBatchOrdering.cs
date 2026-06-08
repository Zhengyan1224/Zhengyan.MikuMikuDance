using System.Numerics;

namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderBatchOrdering
{
    public static IReadOnlyList<RenderMeshBatch> OrderTransparentBackToFront(RenderMesh mesh, Matrix4x4 viewMatrix)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh.Batches
            .Where(batch => batch.Material.IsTransparent)
            .OrderBy(batch => EstimateViewDepth(mesh, batch, viewMatrix))
            .ToArray();
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
}
