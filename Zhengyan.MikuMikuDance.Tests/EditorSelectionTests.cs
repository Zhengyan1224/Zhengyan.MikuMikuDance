using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;
using Zhengyan.MikuMikuDance.UI.ImGui;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EditorSelectionTests
{
    [Fact]
    public void SelectsActiveBoneWithinSelectedModel()
    {
        var project = CreateProject();
        var selection = new EditorSelection();

        Assert.True(selection.SelectBone(project, 0, 1));

        Assert.True(selection.IsSelected(RenderPickTargetKind.Model, 0));
        Assert.True(selection.HasActiveBone);
        Assert.False(selection.HasActiveMorph);
        Assert.Equal(1, selection.ActiveBoneIndex);
        Assert.Equal("head", selection.ActiveBoneName);
        Assert.Equal("head", selection.GetSelectedBone(project)!.Name);
    }

    [Fact]
    public void SelectsActiveMorphWithinSelectedModel()
    {
        var project = CreateProject();
        var selection = new EditorSelection();

        Assert.True(selection.SelectMorph(project, 0, 0));

        Assert.True(selection.IsSelected(RenderPickTargetKind.Model, 0));
        Assert.False(selection.HasActiveBone);
        Assert.True(selection.HasActiveMorph);
        Assert.Equal("smile", selection.ActiveMorphName);
        Assert.Equal("smile", selection.GetSelectedMorph(project)!.Name);
    }

    [Fact]
    public void ClearsActiveChildWhenSelectingWholeObject()
    {
        var project = CreateProject();
        var selection = new EditorSelection();

        Assert.True(selection.SelectBone(project, 0, 1));
        selection.Select(RenderPickTargetKind.Model, 0, "Miku");

        Assert.False(selection.HasActiveBone);
        Assert.False(selection.HasActiveMorph);

        Assert.True(selection.SelectMorph(project, 0, 0));
        selection.Select(RenderPickTargetKind.Accessory, 0, "stage");

        Assert.False(selection.HasActiveMorph);
        Assert.Null(selection.GetSelectedModel(project));
    }

    [Fact]
    public void RejectsInvalidActiveChildIndices()
    {
        var project = CreateProject();
        var selection = new EditorSelection();

        Assert.False(selection.SelectBone(project, 0, 10));
        Assert.False(selection.SelectMorph(project, 5, 0));
        Assert.False(selection.HasSelection);
    }

    private static MmdProject CreateProject()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "Miku" };
        model.AddBone(Bone("center", -1));
        model.AddBone(Bone("head", 0));
        model.AddMorph(new Morph("smile", string.Empty, MorphCategory.Lip, MorphType.Vertex, []));
        project.AddModel(model).Name = "Miku";
        project.AddAccessory(new Accessory("stage"));
        return project;
    }

    private static Bone Bone(string name, int parentBoneIndex)
    {
        return new Bone(
            name,
            string.Empty,
            Vector3.Zero,
            parentBoneIndex,
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
