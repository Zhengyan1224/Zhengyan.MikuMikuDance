using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderSelectionOverlayRect(
    RenderPickTargetKind Kind,
    int ObjectIndex,
    string ObjectName,
    Vector2 Min,
    Vector2 Max,
    RenderSelectionOverlayRole Role = RenderSelectionOverlayRole.Selected)
{
    public Vector2 Size => Max - Min;
}

public enum RenderSelectionOverlayRole
{
    Selected,
    Pointed
}

public sealed record RenderSelectionOverlayStyle(
    Vector4 StrokeColor,
    Vector4 LabelColor,
    Vector4 ShadowColor,
    float Thickness,
    bool DrawLabel)
{
    public static RenderSelectionOverlayStyle ForRole(RenderSelectionOverlayRole role)
    {
        return role == RenderSelectionOverlayRole.Pointed
            ? new RenderSelectionOverlayStyle(
                new Vector4(1f, 0.78f, 0.22f, 0.9f),
                new Vector4(1f, 0.82f, 0.35f, 1f),
                new Vector4(0f, 0f, 0f, 0.45f),
                1.5f,
                false)
            : new RenderSelectionOverlayStyle(
                new Vector4(0.15f, 0.72f, 1f, 1f),
                new Vector4(0.15f, 0.72f, 1f, 1f),
                new Vector4(0f, 0f, 0f, 0.55f),
                2f,
                true);
    }
}

public static class RenderSelectionOverlay
{
    public static RenderSelectionOverlayRect? CreateProjectOverlay(
        MmdProject project,
        RenderPickTargetKind kind,
        int objectIndex,
        int viewportWidth,
        int viewportHeight,
        RenderSelectionOverlayRole role = RenderSelectionOverlayRole.Selected)
    {
        ArgumentNullException.ThrowIfNull(project);
        viewportWidth = Math.Max(1, viewportWidth);
        viewportHeight = Math.Max(1, viewportHeight);

        var target = RenderPicker.CreateTargets(project)
            .FirstOrDefault(item => item.Kind == kind && item.ObjectIndex == objectIndex);
        return target is null
            ? null
            : CreateOverlay(project.Camera, target, viewportWidth, viewportHeight, role);
    }

    public static RenderSelectionOverlayRect? CreateOverlay(
        Camera camera,
        RenderPickTarget target,
        int viewportWidth,
        int viewportHeight,
        RenderSelectionOverlayRole role = RenderSelectionOverlayRole.Selected)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(target);
        viewportWidth = Math.Max(1, viewportWidth);
        viewportHeight = Math.Max(1, viewportHeight);

        var aspectRatio = viewportWidth / (float)viewportHeight;
        var view = camera.CreateViewMatrix();
        var projection = camera.CreateProjectionMatrix(aspectRatio);
        var viewProjection = view * projection;
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        var hasProjectedVertex = false;

        foreach (var mesh in target.Meshes)
        {
            foreach (var vertex in mesh.Vertices)
            {
                var world = Vector3.Transform(vertex.Position, mesh.WorldTransform);
                var clip = Vector4.Transform(new Vector4(world, 1), viewProjection);
                if (clip.W <= 1e-6f)
                {
                    continue;
                }

                var normalized = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
                if (normalized.Z is < -1f or > 1f)
                {
                    continue;
                }

                var screen = new Vector2(
                    (normalized.X * 0.5f + 0.5f) * viewportWidth,
                    (0.5f - normalized.Y * 0.5f) * viewportHeight);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
                hasProjectedVertex = true;
            }
        }

        if (!hasProjectedVertex)
        {
            return null;
        }

        min = Vector2.Clamp(min, Vector2.Zero, new Vector2(viewportWidth, viewportHeight));
        max = Vector2.Clamp(max, Vector2.Zero, new Vector2(viewportWidth, viewportHeight));
        if (max.X <= min.X || max.Y <= min.Y)
        {
            return null;
        }

        return new RenderSelectionOverlayRect(target.Kind, target.ObjectIndex, target.ObjectName, min, max, role);
    }
}
