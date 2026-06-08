using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class ModelOutsideParentBindingTests
{
    [Fact]
    public void SetsOutsideParentBindingForOutsideParentBone()
    {
        var project = new MmdProject();
        var child = project.AddModel(Model("child", BoneFlags.OutsideParent));
        child.Name = "child";
        var parent = project.AddModel(Model("parent", BoneFlags.None));
        parent.Name = "parent";

        var changed = ModelOutsideParentBindingEditor.TrySetParent(child, project, "center", "parent", "center");

        Assert.True(changed);
        var binding = child.GetOutsideParentBinding("center");
        Assert.NotNull(binding);
        Assert.Equal("parent", binding.ParentModelName);
        Assert.Equal("center", binding.ParentBoneName);
    }

    [Fact]
    public void RejectsNonOutsideParentBone()
    {
        var project = new MmdProject();
        var child = project.AddModel(Model("child", BoneFlags.None));
        child.Name = "child";
        var parent = project.AddModel(Model("parent", BoneFlags.None));
        parent.Name = "parent";

        Assert.False(ModelOutsideParentBindingEditor.TrySetParent(child, project, "center", "parent", "center"));
        Assert.Empty(child.OutsideParentBindings);
    }

    [Fact]
    public void ClearsOutsideParentBindingWhenParentModelIsEmpty()
    {
        var project = new MmdProject();
        var child = project.AddModel(Model("child", BoneFlags.OutsideParent));
        child.Name = "child";
        var parent = project.AddModel(Model("parent", BoneFlags.None));
        parent.Name = "parent";
        Assert.True(ModelOutsideParentBindingEditor.TrySetParent(child, project, "center", "parent", "center"));

        Assert.True(ModelOutsideParentBindingEditor.TrySetParent(child, project, "center", null, null));

        Assert.Empty(child.OutsideParentBindings);
    }

    private static MmdModel Model(string name, BoneFlags extraFlags)
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = name };
        model.AddBone(new Bone(
            "center",
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled | extraFlags,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        return model;
    }
}
