using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class CameraNavigationTests
{
    [Fact]
    public void OrbitUpdatesYawAndPitch()
    {
        var camera = new Camera();

        CameraNavigation.Orbit(camera, new Vector2(10, -5), radiansPerPixel: 0.1f);

        Assert.Equal(-0.5f, camera.Angle.X, precision: 5);
        Assert.Equal(1f, camera.Angle.Y, precision: 5);
        Assert.Equal(0f, camera.Angle.Z);
    }

    [Fact]
    public void OrbitClampsPitch()
    {
        var camera = new Camera();

        CameraNavigation.Orbit(camera, new Vector2(0, 10000), radiansPerPixel: 0.1f);

        Assert.True(camera.Angle.X < MathF.PI * 0.5f);
        Assert.True(camera.Angle.X > 1.5f);
    }

    [Fact]
    public void PanMovesLookAtInCameraPlane()
    {
        var camera = new Camera
        {
            Distance = 20,
            FieldOfView = 30
        };

        CameraNavigation.Pan(camera, new Vector2(10, -5), viewportWidth: 800, viewportHeight: 600);

        Assert.True(camera.LookAt.X < 0);
        Assert.True(camera.LookAt.Y < 0);
        Assert.Equal(0f, camera.LookAt.Z, precision: 5);
    }

    [Fact]
    public void ZoomKeepsDistanceInValidRange()
    {
        var camera = new Camera
        {
            Distance = 20
        };

        CameraNavigation.Zoom(camera, 1);
        var zoomedInDistance = camera.Distance;
        CameraNavigation.Zoom(camera, -1);

        Assert.True(zoomedInDistance < 20);
        Assert.Equal(20f, camera.Distance, precision: 5);

        camera.Distance = 0.01f;
        CameraNavigation.Zoom(camera, 1);

        Assert.Equal(CameraNavigation.MinDistance, camera.Distance);
    }

    [Fact]
    public void ResetRestoresDefaultCamera()
    {
        var camera = new Camera
        {
            LookAt = new Vector3(1, 2, 3),
            Angle = new Vector3(0.1f, 0.2f, 0.3f),
            Distance = 10,
            FieldOfView = 60,
            PerspectiveEnabled = false
        };

        CameraNavigation.Reset(camera);

        Assert.Equal(Vector3.Zero, camera.LookAt);
        Assert.Equal(Vector3.Zero, camera.Angle);
        Assert.Equal(45f, camera.Distance);
        Assert.Equal(30, camera.FieldOfView);
        Assert.True(camera.PerspectiveEnabled);
    }
}
