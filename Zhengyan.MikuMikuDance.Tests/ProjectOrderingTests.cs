using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class ProjectOrderingTests
{
    [Fact]
    public void MovesModelInstancesAndModelsTogether()
    {
        var project = new MmdProject();
        var first = new MmdModel(ModelFormat.Pmx) { Name = "first" };
        var second = new MmdModel(ModelFormat.Pmx) { Name = "second" };
        project.AddModel(first).Name = "firstInstance";
        project.AddModel(second).Name = "secondInstance";

        Assert.True(project.MoveModel(1, 0));

        Assert.Equal("secondInstance", project.ModelInstances[0].Name);
        Assert.Equal("second", project.Models[0].Name);
        Assert.Equal("firstInstance", project.ModelInstances[1].Name);
        Assert.Equal("first", project.Models[1].Name);
    }

    [Fact]
    public void MovesAccessories()
    {
        var project = new MmdProject();
        project.AddAccessory(new Accessory("first"));
        project.AddAccessory(new Accessory("second"));

        Assert.True(project.MoveAccessory(0, 1));

        Assert.Equal("second", project.Accessories[0].Name);
        Assert.Equal("first", project.Accessories[1].Name);
    }

    [Fact]
    public void MovesModelTransformOrderWithoutChangingDrawOrder()
    {
        var project = new MmdProject();
        project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "first" }).Name = "first";
        project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "second" }).Name = "second";
        project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "third" }).Name = "third";

        Assert.True(project.MoveModelTransformOrder(2, 0));

        Assert.Equal(["first", "second", "third"], project.ModelInstances.Select(model => model.Name).ToArray());
        Assert.Equal(["third", "first", "second"], project.GetModelsByTransformOrder().Select(model => model.Name).ToArray());
        Assert.Equal(1, project.ModelInstances[0].TransformOrder);
        Assert.Equal(2, project.ModelInstances[1].TransformOrder);
        Assert.Equal(0, project.ModelInstances[2].TransformOrder);
    }

    [Fact]
    public void RejectsInvalidMoves()
    {
        var project = new MmdProject();
        project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "model" });
        project.AddAccessory(new Accessory("accessory"));

        Assert.False(project.MoveModel(0, 0));
        Assert.False(project.MoveModel(-1, 0));
        Assert.False(project.MoveModel(0, 1));
        Assert.False(project.MoveAccessory(0, 0));
        Assert.False(project.MoveAccessory(1, 0));
        Assert.False(project.MoveModelTransformOrder(0, 0));
        Assert.False(project.MoveModelTransformOrder(1, 0));

        Assert.Equal("model", project.Models[0].Name);
        Assert.Equal("accessory", project.Accessories[0].Name);
    }
}
