using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class CameraBindingTests
{
    [Fact]
    public void SetsCameraParentToModelBone()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "Miku" };
        model.AddBone(Bone("center"));
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";

        var changed = CameraBinding.TrySetParent(project.Camera, project, "MikuInstance", "center");

        Assert.True(changed);
        Assert.Equal("MikuInstance", project.Camera.ParentModelName);
        Assert.Equal("center", project.Camera.ParentBoneName);
    }

    [Fact]
    public void ClearsCameraParentWhenModelNameIsEmpty()
    {
        var project = new MmdProject();
        project.Camera.ParentModelName = "Miku";
        project.Camera.ParentBoneName = "center";

        var changed = CameraBinding.TrySetParent(project.Camera, project, null, null);

        Assert.True(changed);
        Assert.Null(project.Camera.ParentModelName);
        Assert.Null(project.Camera.ParentBoneName);
    }

    [Fact]
    public void RejectsUnknownModelOrBoneWithoutChangingCurrentParent()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "Miku" };
        model.AddBone(Bone("center"));
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";
        project.Camera.ParentModelName = "MikuInstance";
        project.Camera.ParentBoneName = "center";

        Assert.False(CameraBinding.TrySetParent(project.Camera, project, "Missing", "center"));
        Assert.Equal("MikuInstance", project.Camera.ParentModelName);
        Assert.Equal("center", project.Camera.ParentBoneName);

        Assert.False(CameraBinding.TrySetParent(project.Camera, project, "MikuInstance", "missingBone"));
        Assert.Equal("MikuInstance", project.Camera.ParentModelName);
        Assert.Equal("center", project.Camera.ParentBoneName);
    }

    private static Bone Bone(string name)
    {
        return new Bone(
            name,
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null);
    }
}
