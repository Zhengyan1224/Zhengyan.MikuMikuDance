using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class SceneMotionApplierTests
{
    [Fact]
    public void AppliesCameraAndLightSamples()
    {
        var project = new MmdProject();
        var sample = EmptySample() with
        {
            Camera = new CameraSample(new Vector3(1, 2, 3), new Vector3(0.1f, 0.2f, 0.3f), 25, 45, false),
            Light = new LightSample(new Vector3(0.2f, 0.4f, 0.6f), new Vector3(1, -1, 0))
        };

        SceneMotionApplier.Apply(project, sample);

        Assert.Equal(new Vector3(1, 2, 3), project.Camera.LookAt);
        Assert.Equal(25, project.Camera.Distance);
        Assert.Equal(45, project.Camera.FieldOfView);
        Assert.False(project.Camera.PerspectiveEnabled);
        Assert.Equal(new Vector3(0.2f, 0.4f, 0.6f), project.Light.Color);
        Assert.Equal(new Vector3(1, -1, 0), project.Light.Direction);
    }

    [Fact]
    public void AppliesModelVisibility()
    {
        var project = new MmdProject();
        project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "model" });
        var sample = EmptySample() with
        {
            Model = new ModelSample(false, new Dictionary<string, bool>())
        };

        SceneMotionApplier.Apply(project, sample);

        Assert.False(project.ModelInstances[0].Visible);
    }

    [Fact]
    public void AppliesAccessorySamplesByName()
    {
        var project = new MmdProject();
        project.AddAccessory(new Accessory("stage"));
        var sample = EmptySample() with
        {
            Accessories = new Dictionary<string, AccessorySample>(StringComparer.Ordinal)
            {
                ["stage"] = new AccessorySample(
                    false,
                    new Vector3(1, 2, 3),
                    new Vector3(0.1f, 0.2f, 0.3f),
                    2,
                    0.5f,
                    "model",
                    "bone")
            }
        };

        SceneMotionApplier.Apply(project, sample);
        var accessory = project.Accessories[0];

        Assert.False(accessory.Visible);
        Assert.Equal(new Vector3(1, 2, 3), accessory.Translation);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), accessory.Orientation);
        Assert.Equal(2, accessory.Scale);
        Assert.Equal(0.5f, accessory.Opacity);
        Assert.Equal("model", accessory.ParentModelName);
        Assert.Equal("bone", accessory.ParentBoneName);
    }

    private static MotionSample EmptySample()
    {
        return new MotionSample(
            0,
            new Dictionary<string, BonePoseSample>(),
            new Dictionary<string, MorphWeightSample>(),
            null,
            null,
            null,
            null,
            new Dictionary<string, AccessorySample>());
    }
}
