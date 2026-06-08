using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public readonly record struct RenderPickRay(Vector3 Origin, Vector3 Direction)
{
    public RenderPickRay(Vector3 origin, Vector3 direction, bool normalize)
        : this(origin, normalize ? Vector3.Normalize(direction) : direction)
    {
    }
}

public enum RenderPickTargetKind
{
    Model,
    Accessory
}

public sealed record RenderPickTarget(
    RenderPickTargetKind Kind,
    int ObjectIndex,
    string ObjectName,
    IReadOnlyList<RenderMesh> Meshes);

public sealed record RenderPickHit(
    RenderPickTargetKind Kind,
    int ObjectIndex,
    string ObjectName,
    float Distance,
    Vector3 Position,
    int MeshIndex,
    int TriangleIndex);

public static class RenderPicker
{
    private const float Epsilon = 1e-6f;

    public static RenderPickRay CreateCameraRay(Camera camera, int viewportWidth, int viewportHeight, Vector2 viewportPosition)
    {
        ArgumentNullException.ThrowIfNull(camera);
        viewportWidth = Math.Max(1, viewportWidth);
        viewportHeight = Math.Max(1, viewportHeight);

        var rotation = Matrix4x4.CreateFromYawPitchRoll(camera.Angle.Y, camera.Angle.X, camera.Angle.Z);
        var origin = camera.LookAt + Vector3.Transform(new Vector3(0, 0, camera.Distance), rotation);
        var forward = Vector3.Normalize(camera.LookAt - origin);
        var right = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));

        var normalizedX = (viewportPosition.X / viewportWidth) * 2f - 1f;
        var normalizedY = 1f - (viewportPosition.Y / viewportHeight) * 2f;
        if (camera.PerspectiveEnabled)
        {
            var aspectRatio = viewportWidth / (float)viewportHeight;
            var tangent = MathF.Tan(camera.FieldOfView * MathF.PI / 360f);
            var direction = forward
                + right * (normalizedX * tangent * aspectRatio)
                + up * (normalizedY * tangent);
            return new RenderPickRay(origin, direction, normalize: true);
        }

        var orthographicHeight = 40f;
        var orthographicWidth = orthographicHeight * viewportWidth / viewportHeight;
        origin += right * (normalizedX * orthographicWidth * 0.5f)
            + up * (normalizedY * orthographicHeight * 0.5f);
        return new RenderPickRay(origin, forward, normalize: true);
    }

    public static RenderPickHit? PickProject(MmdProject project, int viewportWidth, int viewportHeight, Vector2 viewportPosition)
    {
        ArgumentNullException.ThrowIfNull(project);
        var ray = CreateCameraRay(project.Camera, viewportWidth, viewportHeight, viewportPosition);
        return Pick(ray, CreateTargets(project));
    }

    public static IReadOnlyList<RenderPickTarget> CreateTargets(MmdProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var targets = new List<RenderPickTarget>();

        for (var i = 0; i < project.ModelInstances.Count; i++)
        {
            var instance = project.ModelInstances[i];
            if (!instance.Visible || !instance.Model.Visible || instance.Model.Vertices.Count == 0 || instance.Model.Indices.Count == 0)
            {
                continue;
            }

            targets.Add(new RenderPickTarget(
                RenderPickTargetKind.Model,
                i,
                instance.Name,
                [RenderMeshBuilder.FromModel(instance)]));
        }

        for (var i = 0; i < project.Accessories.Count; i++)
        {
            var accessory = project.Accessories[i];
            if (!accessory.Visible)
            {
                continue;
            }

            var document = FindAccessoryMeshDocument(project, accessory, i);
            if (document is null || document.Meshes.Count == 0)
            {
                continue;
            }

            targets.Add(new RenderPickTarget(
                RenderPickTargetKind.Accessory,
                i,
                accessory.Name,
                RenderMeshBuilder.FromAccessory(document, accessory)));
        }

        return targets;
    }

    public static RenderPickHit? Pick(RenderPickRay ray, IEnumerable<RenderPickTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var normalizedRay = new RenderPickRay(ray.Origin, ray.Direction, normalize: true);
        RenderPickHit? closest = null;

        foreach (var target in targets)
        {
            for (var meshIndex = 0; meshIndex < target.Meshes.Count; meshIndex++)
            {
                var mesh = target.Meshes[meshIndex];
                for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
                {
                    var a = TransformVertex(mesh, mesh.Indices[index]);
                    var b = TransformVertex(mesh, mesh.Indices[index + 1]);
                    var c = TransformVertex(mesh, mesh.Indices[index + 2]);
                    if (!TryIntersectTriangle(normalizedRay, a, b, c, out var distance))
                    {
                        continue;
                    }

                    if (closest is not null && distance >= closest.Distance)
                    {
                        continue;
                    }

                    closest = new RenderPickHit(
                        target.Kind,
                        target.ObjectIndex,
                        target.ObjectName,
                        distance,
                        normalizedRay.Origin + normalizedRay.Direction * distance,
                        meshIndex,
                        index / 3);
                }
            }
        }

        return closest;
    }

    private static Vector3 TransformVertex(RenderMesh mesh, uint vertexIndex)
    {
        if (vertexIndex >= mesh.Vertices.Count)
        {
            return Vector3.Zero;
        }

        return Vector3.Transform(mesh.Vertices[(int)vertexIndex].Position, mesh.WorldTransform);
    }

    private static bool TryIntersectTriangle(RenderPickRay ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        var edge1 = b - a;
        var edge2 = c - a;
        var p = Vector3.Cross(ray.Direction, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (MathF.Abs(determinant) < Epsilon)
        {
            return false;
        }

        var inverseDeterminant = 1f / determinant;
        var t = ray.Origin - a;
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u is < 0f or > 1f)
        {
            return false;
        }

        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(ray.Direction, q) * inverseDeterminant;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        return distance > Epsilon;
    }

    private static AccessoryMeshDocument? FindAccessoryMeshDocument(MmdProject project, Accessory accessory, int accessoryIndex)
    {
        if (accessory.Source is not null)
        {
            var byUri = project.AccessoryMeshes.FirstOrDefault(document => UriEquals(document.Source, accessory.Source));
            if (byUri is not null)
            {
                return byUri;
            }

            var sourceName = accessory.Source.IsFile
                ? Path.GetFileName(accessory.Source.LocalPath)
                : Path.GetFileName(accessory.Source.ToString());
            var byName = project.AccessoryMeshes.FirstOrDefault(document =>
                string.Equals(document.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        return accessoryIndex >= 0 && accessoryIndex < project.AccessoryMeshes.Count
            ? project.AccessoryMeshes[accessoryIndex]
            : project.AccessoryMeshes.Count == 1 ? project.AccessoryMeshes[0] : null;
    }

    private static bool UriEquals(Uri? left, Uri right)
    {
        if (left is null)
        {
            return false;
        }

        return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
