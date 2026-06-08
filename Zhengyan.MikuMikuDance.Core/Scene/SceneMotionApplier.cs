using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public static class SceneMotionApplier
{
    public static void Apply(MmdProject project, MotionSample sample)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sample);
        ApplyCamera(project.Camera, sample.Camera);
        ApplyLight(project.Light, sample.Light);
        ApplyModelVisibility(project, sample.Model);
        ApplyAccessories(project, sample.Accessories);
    }

    private static void ApplyCamera(Camera camera, CameraSample? sample)
    {
        if (sample is null)
        {
            return;
        }

        camera.LookAt = sample.LookAt;
        camera.Angle = sample.Angle;
        camera.Distance = sample.Distance;
        camera.FieldOfView = sample.FieldOfView;
        camera.PerspectiveEnabled = sample.PerspectiveEnabled;
    }

    private static void ApplyLight(DirectionalLight light, LightSample? sample)
    {
        if (sample is null)
        {
            return;
        }

        light.Color = sample.Color;
        light.Direction = sample.Direction;
    }

    private static void ApplyModelVisibility(MmdProject project, ModelSample? sample)
    {
        if (sample is null)
        {
            return;
        }

        foreach (var model in project.ModelInstances)
        {
            model.Visible = sample.Visible;
        }
    }

    private static void ApplyAccessories(
        MmdProject project,
        IReadOnlyDictionary<string, AccessorySample> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        foreach (var accessory in project.Accessories)
        {
            if (!samples.TryGetValue(accessory.Name, out var sample))
            {
                continue;
            }

            accessory.Visible = sample.Visible;
            accessory.Translation = sample.Translation;
            accessory.Orientation = sample.Orientation;
            accessory.Scale = sample.Scale;
            accessory.Opacity = sample.Opacity;
            accessory.ParentModelName = sample.ParentModelName;
            accessory.ParentBoneName = sample.ParentBoneName;
        }
    }
}
