using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public static class CameraNavigation
{
    public const float MinDistance = 0.1f;
    public const float MaxDistance = 10000f;

    private const float MaxPitch = MathF.PI * 0.5f - 0.01f;
    private const float DefaultOrthographicHeight = 40f;

    public static void Orbit(Camera camera, Vector2 deltaPixels, float radiansPerPixel = 0.01f)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (deltaPixels.LengthSquared() == 0 || radiansPerPixel == 0)
        {
            return;
        }

        camera.Angle = new Vector3(
            Math.Clamp(camera.Angle.X + deltaPixels.Y * radiansPerPixel, -MaxPitch, MaxPitch),
            NormalizeAngle(camera.Angle.Y + deltaPixels.X * radiansPerPixel),
            camera.Angle.Z);
    }

    public static void Pan(Camera camera, Vector2 deltaPixels, int viewportWidth, int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(camera);
        viewportHeight = Math.Max(1, viewportHeight);
        if (deltaPixels.LengthSquared() == 0)
        {
            return;
        }

        var rotation = Matrix4x4.CreateFromYawPitchRoll(camera.Angle.Y, camera.Angle.X, camera.Angle.Z);
        var right = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
        var worldUnitsPerPixel = CalculateWorldUnitsPerPixel(camera, viewportHeight);

        camera.LookAt += (-right * deltaPixels.X + up * deltaPixels.Y) * worldUnitsPerPixel;
    }

    public static void Zoom(Camera camera, float wheelDelta, float sensitivity = 0.12f)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (wheelDelta == 0 || sensitivity == 0)
        {
            return;
        }

        var multiplier = MathF.Exp(-wheelDelta * sensitivity);
        camera.Distance = Math.Clamp(camera.Distance * multiplier, MinDistance, MaxDistance);
    }

    public static void Reset(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.LookAt = Vector3.Zero;
        camera.Angle = Vector3.Zero;
        camera.Distance = 45f;
        camera.FieldOfView = 30;
        camera.PerspectiveEnabled = true;
    }

    private static float CalculateWorldUnitsPerPixel(Camera camera, int viewportHeight)
    {
        if (!camera.PerspectiveEnabled)
        {
            return DefaultOrthographicHeight / viewportHeight;
        }

        var fieldOfViewRadians = camera.FieldOfView * MathF.PI / 180f;
        return 2f * MathF.Max(camera.Distance, MinDistance) * MathF.Tan(fieldOfViewRadians * 0.5f) / viewportHeight;
    }

    private static float NormalizeAngle(float angle)
    {
        const float twoPi = MathF.PI * 2f;
        angle %= twoPi;
        if (angle > MathF.PI)
        {
            angle -= twoPi;
        }
        else if (angle < -MathF.PI)
        {
            angle += twoPi;
        }

        return angle;
    }
}
