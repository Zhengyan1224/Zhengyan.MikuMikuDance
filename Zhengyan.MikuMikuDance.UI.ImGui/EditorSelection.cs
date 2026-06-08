using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorSelection
{
    public RenderPickTargetKind? Kind { get; private set; }

    public int ObjectIndex { get; private set; } = -1;

    public string ObjectName { get; private set; } = string.Empty;

    public int ActiveBoneIndex { get; private set; } = -1;

    public string ActiveBoneName { get; private set; } = string.Empty;

    public int ActiveMorphIndex { get; private set; } = -1;

    public string ActiveMorphName { get; private set; } = string.Empty;

    public bool HasSelection => Kind is not null && ObjectIndex >= 0;

    public bool HasActiveBone => Kind == RenderPickTargetKind.Model && ActiveBoneIndex >= 0;

    public bool HasActiveMorph => Kind == RenderPickTargetKind.Model && ActiveMorphIndex >= 0;

    public void Clear()
    {
        Kind = null;
        ObjectIndex = -1;
        ObjectName = string.Empty;
        ClearActiveBone();
        ClearActiveMorph();
    }

    public void Select(RenderPickTargetKind kind, int objectIndex, string objectName)
    {
        Kind = kind;
        ObjectIndex = objectIndex;
        ObjectName = objectName;
        ClearActiveBone();
        ClearActiveMorph();
    }

    public void Select(RenderPickHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        Select(hit.Kind, hit.ObjectIndex, hit.ObjectName);
    }

    public bool IsSelected(RenderPickTargetKind kind, int objectIndex)
    {
        return Kind == kind && ObjectIndex == objectIndex;
    }

    public bool SelectBone(MmdProject project, int modelIndex, int boneIndex)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (modelIndex < 0 || modelIndex >= project.ModelInstances.Count)
        {
            return false;
        }

        var model = project.ModelInstances[modelIndex];
        if (boneIndex < 0 || boneIndex >= model.Model.Bones.Count)
        {
            return false;
        }

        Select(RenderPickTargetKind.Model, modelIndex, model.Name);
        ActiveBoneIndex = boneIndex;
        ActiveBoneName = model.Model.Bones[boneIndex].Name;
        ClearActiveMorph();
        return true;
    }

    public bool SelectMorph(MmdProject project, int modelIndex, int morphIndex)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (modelIndex < 0 || modelIndex >= project.ModelInstances.Count)
        {
            return false;
        }

        var model = project.ModelInstances[modelIndex];
        if (morphIndex < 0 || morphIndex >= model.Model.Morphs.Count)
        {
            return false;
        }

        Select(RenderPickTargetKind.Model, modelIndex, model.Name);
        ActiveMorphIndex = morphIndex;
        ActiveMorphName = model.Model.Morphs[morphIndex].Name;
        ClearActiveBone();
        return true;
    }

    public Bone? GetSelectedBone(MmdProject project)
    {
        var model = GetSelectedModel(project);
        return model is not null && ActiveBoneIndex >= 0 && ActiveBoneIndex < model.Model.Bones.Count
            ? model.Model.Bones[ActiveBoneIndex]
            : null;
    }

    public Morph? GetSelectedMorph(MmdProject project)
    {
        var model = GetSelectedModel(project);
        return model is not null && ActiveMorphIndex >= 0 && ActiveMorphIndex < model.Model.Morphs.Count
            ? model.Model.Morphs[ActiveMorphIndex]
            : null;
    }

    public ModelInstance? GetSelectedModel(MmdProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Kind == RenderPickTargetKind.Model && ObjectIndex >= 0 && ObjectIndex < project.ModelInstances.Count
            ? project.ModelInstances[ObjectIndex]
            : null;
    }

    public Accessory? GetSelectedAccessory(MmdProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Kind == RenderPickTargetKind.Accessory && ObjectIndex >= 0 && ObjectIndex < project.Accessories.Count
            ? project.Accessories[ObjectIndex]
            : null;
    }

    private void ClearActiveBone()
    {
        ActiveBoneIndex = -1;
        ActiveBoneName = string.Empty;
    }

    private void ClearActiveMorph()
    {
        ActiveMorphIndex = -1;
        ActiveMorphName = string.Empty;
    }
}
