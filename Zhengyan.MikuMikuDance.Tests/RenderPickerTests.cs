using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderPickerTests
{
    [Fact]
    public void PicksModelUnderViewportPoint()
    {
        var project = CreateProjectWithModel("model", z: 0);

        var hit = RenderPicker.PickProject(project, 800, 600, new Vector2(400, 300));

        Assert.NotNull(hit);
        Assert.Equal(RenderPickTargetKind.Model, hit.Kind);
        Assert.Equal("model", hit.ObjectName);
        Assert.Equal(0, hit.ObjectIndex);
    }

    [Fact]
    public void PicksClosestTarget()
    {
        var farProject = CreateProjectWithModel("far", z: -5);
        var nearModel = CreateTriangleModel("near");
        var nearInstance = farProject.AddModel(nearModel);
        nearInstance.Name = "near";
        nearInstance.Transform.Translation = new Vector3(0, 0, 5);

        var hit = RenderPicker.PickProject(farProject, 800, 600, new Vector2(400, 300));

        Assert.NotNull(hit);
        Assert.Equal("near", hit.ObjectName);
        Assert.Equal(1, hit.ObjectIndex);
    }

    [Fact]
    public void ReturnsNullWhenRayMissesAllTargets()
    {
        var project = CreateProjectWithModel("model", z: 0);

        var hit = RenderPicker.PickProject(project, 800, 600, new Vector2(799, 0));

        Assert.Null(hit);
    }

    private static MmdProject CreateProjectWithModel(string name, float z)
    {
        var project = new MmdProject();
        project.Camera.LookAt = Vector3.Zero;
        project.Camera.Angle = Vector3.Zero;
        project.Camera.Distance = 20f;
        project.Camera.FieldOfView = 30;

        var model = CreateTriangleModel(name);
        var instance = project.AddModel(model);
        instance.Name = name;
        instance.Transform.Translation = new Vector3(0, 0, z);
        return project;
    }

    private static MmdModel CreateTriangleModel(string name)
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = name };
        model.AddVertex(Vertex(-5, -5, 0));
        model.AddVertex(Vertex(5, -5, 0));
        model.AddVertex(Vertex(0, 5, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        return model;
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
