using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class AccessoryBindingTests
{
    [Fact]
    public void SetsAccessoryParentToModelBone()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "Miku" };
        model.AddBone(Bone("center"));
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";
        var accessory = new Accessory("stage");

        var changed = AccessoryBinding.TrySetParent(accessory, project, "MikuInstance", "center");

        Assert.True(changed);
        Assert.Equal("MikuInstance", accessory.ParentModelName);
        Assert.Equal("center", accessory.ParentBoneName);
    }

    [Fact]
    public void ClearsAccessoryParentWhenModelNameIsEmpty()
    {
        var project = new MmdProject();
        var accessory = new Accessory("stage")
        {
            ParentModelName = "Miku",
            ParentBoneName = "center"
        };

        var changed = AccessoryBinding.TrySetParent(accessory, project, null, null);

        Assert.True(changed);
        Assert.Null(accessory.ParentModelName);
        Assert.Null(accessory.ParentBoneName);
    }

    [Fact]
    public void RejectsUnknownModelOrBoneWithoutChangingCurrentParent()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "Miku" };
        model.AddBone(Bone("center"));
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";
        var accessory = new Accessory("stage")
        {
            ParentModelName = "MikuInstance",
            ParentBoneName = "center"
        };

        Assert.False(AccessoryBinding.TrySetParent(accessory, project, "Missing", "center"));
        Assert.Equal("MikuInstance", accessory.ParentModelName);
        Assert.Equal("center", accessory.ParentBoneName);

        Assert.False(AccessoryBinding.TrySetParent(accessory, project, "MikuInstance", "missingBone"));
        Assert.Equal("MikuInstance", accessory.ParentModelName);
        Assert.Equal("center", accessory.ParentBoneName);
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
