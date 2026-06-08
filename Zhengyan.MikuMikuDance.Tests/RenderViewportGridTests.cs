using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderViewportGridTests
{
    [Fact]
    public void CreatesProjectedGridLinesWithAxes()
    {
        var camera = new Camera
        {
            LookAt = Vector3.Zero,
            Angle = new Vector3(-0.45f, 0.35f, 0),
            Distance = 35f,
            FieldOfView = 30
        };

        var lines = RenderViewportGrid.CreateGrid(
            camera,
            800,
            600,
            new RenderViewportGridOptions(Extent: 5f, Step: 1f));

        Assert.NotEmpty(lines);
        Assert.Contains(lines, line => line.Kind == RenderViewportGridLineKind.AxisX);
        Assert.Contains(lines, line => line.Kind == RenderViewportGridLineKind.AxisZ);
        Assert.All(lines, line =>
        {
            Assert.False(float.IsNaN(line.Start.X));
            Assert.False(float.IsNaN(line.End.Y));
        });
    }

    [Fact]
    public void ClampsInvalidGridOptions()
    {
        var camera = new Camera
        {
            LookAt = Vector3.Zero,
            Angle = new Vector3(-0.4f, 0, 0),
            Distance = 25f
        };

        var lines = RenderViewportGrid.CreateGrid(
            camera,
            0,
            0,
            new RenderViewportGridOptions(Extent: -1f, Step: -2f));

        Assert.NotEmpty(lines);
    }
}
