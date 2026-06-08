using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderViewportGridLine(
    Vector2 Start,
    Vector2 End,
    RenderViewportGridLineKind Kind);

public enum RenderViewportGridLineKind
{
    Minor,
    AxisX,
    AxisZ
}

public sealed record RenderViewportGridOptions(
    float Extent = 20f,
    float Step = 1f);

public static class RenderViewportGrid
{
    public static IReadOnlyList<RenderViewportGridLine> CreateGrid(
        Camera camera,
        int viewportWidth,
        int viewportHeight,
        RenderViewportGridOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        viewportWidth = Math.Max(1, viewportWidth);
        viewportHeight = Math.Max(1, viewportHeight);
        options ??= new RenderViewportGridOptions();

        var extent = Math.Max(options.Extent, options.Step);
        var step = Math.Max(options.Step, 0.001f);
        var count = Math.Clamp((int)MathF.Floor(extent / step), 1, 512);
        var viewProjection = camera.CreateViewMatrix() * camera.CreateProjectionMatrix(viewportWidth / (float)viewportHeight);
        var lines = new List<RenderViewportGridLine>(count * 4 + 2);

        for (var i = -count; i <= count; i++)
        {
            var coordinate = i * step;
            AddLine(
                lines,
                viewProjection,
                viewportWidth,
                viewportHeight,
                new Vector3(-extent, 0, coordinate),
                new Vector3(extent, 0, coordinate),
                i == 0 ? RenderViewportGridLineKind.AxisX : RenderViewportGridLineKind.Minor);
            AddLine(
                lines,
                viewProjection,
                viewportWidth,
                viewportHeight,
                new Vector3(coordinate, 0, -extent),
                new Vector3(coordinate, 0, extent),
                i == 0 ? RenderViewportGridLineKind.AxisZ : RenderViewportGridLineKind.Minor);
        }

        return lines;
    }

    private static void AddLine(
        ICollection<RenderViewportGridLine> lines,
        Matrix4x4 viewProjection,
        int viewportWidth,
        int viewportHeight,
        Vector3 start,
        Vector3 end,
        RenderViewportGridLineKind kind)
    {
        if (!TryProject(start, viewProjection, viewportWidth, viewportHeight, out var projectedStart) ||
            !TryProject(end, viewProjection, viewportWidth, viewportHeight, out var projectedEnd))
        {
            return;
        }

        if (!IntersectsViewport(projectedStart, projectedEnd, viewportWidth, viewportHeight))
        {
            return;
        }

        lines.Add(new RenderViewportGridLine(projectedStart, projectedEnd, kind));
    }

    private static bool TryProject(
        Vector3 world,
        Matrix4x4 viewProjection,
        int viewportWidth,
        int viewportHeight,
        out Vector2 screen)
    {
        screen = default;
        var clip = Vector4.Transform(new Vector4(world, 1), viewProjection);
        if (clip.W <= 1e-6f)
        {
            return false;
        }

        var normalized = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        if (normalized.Z is < -1f or > 1f)
        {
            return false;
        }

        screen = new Vector2(
            (normalized.X * 0.5f + 0.5f) * viewportWidth,
            (0.5f - normalized.Y * 0.5f) * viewportHeight);
        return true;
    }

    private static bool IntersectsViewport(Vector2 start, Vector2 end, int viewportWidth, int viewportHeight)
    {
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        return max.X >= 0
            && max.Y >= 0
            && min.X <= viewportWidth
            && min.Y <= viewportHeight;
    }
}
