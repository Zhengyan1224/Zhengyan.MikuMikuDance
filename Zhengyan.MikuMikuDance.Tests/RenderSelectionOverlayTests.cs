using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderSelectionOverlayTests
{
    [Fact]
    public void CreatesOverlayForSelectedModel()
    {
        var project = CreateProjectWithModel();

        var overlay = RenderSelectionOverlay.CreateProjectOverlay(project, RenderPickTargetKind.Model, 0, 800, 600);

        Assert.NotNull(overlay);
        Assert.Equal("model", overlay.ObjectName);
        Assert.Equal(RenderSelectionOverlayRole.Selected, overlay.Role);
        Assert.True(overlay.Min.X < 400);
        Assert.True(overlay.Max.X > 400);
        Assert.True(overlay.Min.Y < 300);
        Assert.True(overlay.Max.Y > 300);
    }

    [Fact]
    public void CreatesPointedOverlayRoleAndStyle()
    {
        var project = CreateProjectWithModel();

        var overlay = RenderSelectionOverlay.CreateProjectOverlay(
            project,
            RenderPickTargetKind.Model,
            0,
            800,
            600,
            RenderSelectionOverlayRole.Pointed);
        var selectedStyle = RenderSelectionOverlayStyle.ForRole(RenderSelectionOverlayRole.Selected);
        var pointedStyle = RenderSelectionOverlayStyle.ForRole(RenderSelectionOverlayRole.Pointed);

        Assert.NotNull(overlay);
        Assert.Equal(RenderSelectionOverlayRole.Pointed, overlay.Role);
        Assert.False(pointedStyle.DrawLabel);
        Assert.True(selectedStyle.DrawLabel);
        Assert.NotEqual(selectedStyle.StrokeColor, pointedStyle.StrokeColor);
    }

    [Fact]
    public void ReturnsNullForMissingOrInvisibleTarget()
    {
        var project = CreateProjectWithModel();

        Assert.Null(RenderSelectionOverlay.CreateProjectOverlay(project, RenderPickTargetKind.Accessory, 0, 800, 600));

        project.ModelInstances[0].Visible = false;

        Assert.Null(RenderSelectionOverlay.CreateProjectOverlay(project, RenderPickTargetKind.Model, 0, 800, 600));
    }

    private static MmdProject CreateProjectWithModel()
    {
        var project = new MmdProject();
        project.Camera.LookAt = Vector3.Zero;
        project.Camera.Angle = Vector3.Zero;
        project.Camera.Distance = 20f;
        project.Camera.FieldOfView = 30;

        var model = new MmdModel(ModelFormat.Pmx) { Name = "model" };
        model.AddVertex(Vertex(-5, -5, 0));
        model.AddVertex(Vertex(5, -5, 0));
        model.AddVertex(Vertex(0, 5, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        project.AddModel(model).Name = "model";
        return project;
    }

    private static Vertex Vertex(float x, float y, float z)
    {
        return new Vertex(
            new Vector3(x, y, z),
            Vector3.UnitZ,
            Vector2.Zero,
            [],
            new SkinningWeights(VertexSkinningType.Bdef1, [0], [1f]),
            1);
    }
}
