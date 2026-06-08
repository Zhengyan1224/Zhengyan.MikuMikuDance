using ImGuiNET;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Rendering;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui.Panels;

public sealed class SceneObjectsPanel : IImGuiEditorPanel
{
    public string Title => "Scene";

    public void Draw(ImGuiEditorState state)
    {
        if (!ImGuiApi.Begin(Title))
        {
            ImGuiApi.End();
            return;
        }

        var project = state.Project;
        ImGuiApi.TextUnformatted(project.Name);
        ImGuiApi.Separator();

        DrawModels(state, project.ModelInstances);
        DrawModelTransformOrder(state);
        DrawAccessories(state, project.Accessories);
        DrawMotions(project.Motions);

        ImGuiApi.End();
    }

    private static void DrawModels(ImGuiEditorState state, IReadOnlyList<Core.Scene.ModelInstance> models)
    {
        if (!ImGuiApi.TreeNodeEx("Models", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (models.Count == 0)
        {
            ImGuiApi.TextDisabled("No models");
        }
        else
        {
            for (var i = 0; i < models.Count; i++)
            {
                var model = models[i];
                var selected = state.Selection.IsSelected(RenderPickTargetKind.Model, i);
                if (DrawOrderButtons(state, RenderPickTargetKind.Model, i, models.Count, model.Name))
                {
                    break;
                }

                ImGuiApi.SameLine();
                DrawModelNode(state, model, i, selected);
            }
        }

        ImGuiApi.TreePop();
    }

    private static void DrawModelNode(
        ImGuiEditorState state,
        Core.Scene.ModelInstance model,
        int modelIndex,
        bool selected)
    {
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (selected)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        var label = string.IsNullOrWhiteSpace(model.Name) ? $"Model {modelIndex}" : model.Name;
        var open = ImGuiApi.TreeNodeEx($"{label}##model-{modelIndex}", flags);
        if (ImGuiApi.IsItemClicked() && !ImGuiApi.IsItemToggledOpen())
        {
            state.Selection.Select(RenderPickTargetKind.Model, modelIndex, model.Name);
            state.StatusText = $"Selected model: {model.Name}";
        }

        if (!open)
        {
            return;
        }

        DrawModelBones(state, model, modelIndex);
        DrawModelMorphs(state, model, modelIndex);
        ImGuiApi.TreePop();
    }

    private static void DrawModelBones(
        ImGuiEditorState state,
        Core.Scene.ModelInstance model,
        int modelIndex)
    {
        if (!ImGuiApi.TreeNodeEx($"Bones ({model.Model.Bones.Count})##bones-{modelIndex}"))
        {
            return;
        }

        if (model.Model.Bones.Count == 0)
        {
            ImGuiApi.TextDisabled("No bones");
        }
        else
        {
            for (var i = 0; i < model.Model.Bones.Count; i++)
            {
                var bone = model.Model.Bones[i];
                var selected = state.Selection.IsSelected(RenderPickTargetKind.Model, modelIndex)
                    && state.Selection.ActiveBoneIndex == i;
                var label = DisplayName(bone);
                if (ImGuiApi.Selectable($"{label}##bone-{modelIndex}-{i}", selected))
                {
                    state.Selection.SelectBone(state.Project, modelIndex, i);
                    state.StatusText = $"Selected bone: {label}";
                }
            }
        }

        ImGuiApi.TreePop();
    }

    private static void DrawModelMorphs(
        ImGuiEditorState state,
        Core.Scene.ModelInstance model,
        int modelIndex)
    {
        if (!ImGuiApi.TreeNodeEx($"Morphs ({model.Model.Morphs.Count})##morphs-{modelIndex}"))
        {
            return;
        }

        if (model.Model.Morphs.Count == 0)
        {
            ImGuiApi.TextDisabled("No morphs");
        }
        else
        {
            for (var i = 0; i < model.Model.Morphs.Count; i++)
            {
                var morph = model.Model.Morphs[i];
                var selected = state.Selection.IsSelected(RenderPickTargetKind.Model, modelIndex)
                    && state.Selection.ActiveMorphIndex == i;
                var label = DisplayName(morph);
                if (ImGuiApi.Selectable($"{label}##morph-{modelIndex}-{i}", selected))
                {
                    state.Selection.SelectMorph(state.Project, modelIndex, i);
                    state.StatusText = $"Selected morph: {label}";
                }
            }
        }

        ImGuiApi.TreePop();
    }

    private static string DisplayName(Bone bone)
    {
        if (!string.IsNullOrWhiteSpace(bone.Name))
        {
            return bone.Name;
        }

        return string.IsNullOrWhiteSpace(bone.EnglishName) ? "(unnamed bone)" : bone.EnglishName;
    }

    private static string DisplayName(Morph morph)
    {
        if (!string.IsNullOrWhiteSpace(morph.Name))
        {
            return morph.Name;
        }

        return string.IsNullOrWhiteSpace(morph.EnglishName) ? "(unnamed morph)" : morph.EnglishName;
    }

    private static void DrawModelTransformOrder(ImGuiEditorState state)
    {
        if (!ImGuiApi.TreeNodeEx("Transform Order", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        var models = state.Project.GetModelsByTransformOrder();
        if (models.Count == 0)
        {
            ImGuiApi.TextDisabled("No models");
        }
        else
        {
            for (var i = 0; i < models.Count; i++)
            {
                var model = models[i];
                ImGuiApi.BeginDisabled(i == 0);
                var moveUp = ImGuiApi.SmallButton($"Up##transform-{i}");
                ImGuiApi.EndDisabled();
                ImGuiApi.SameLine();
                ImGuiApi.BeginDisabled(i >= models.Count - 1);
                var moveDown = ImGuiApi.SmallButton($"Down##transform-{i}");
                ImGuiApi.EndDisabled();
                ImGuiApi.SameLine();
                ImGuiApi.TextUnformatted(model.Name);

                if (!moveUp && !moveDown)
                {
                    continue;
                }

                var targetOrder = i + (moveUp ? -1 : 1);
                if (state.Project.MoveModelTransformOrder(i, targetOrder))
                {
                    state.StatusText = $"Model transform order moved: {model.Name}";
                    break;
                }
            }
        }

        ImGuiApi.TreePop();
    }

    private static void DrawAccessories(ImGuiEditorState state, IReadOnlyList<Core.Scene.Accessory> accessories)
    {
        if (!ImGuiApi.TreeNodeEx("Accessories", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (accessories.Count == 0)
        {
            ImGuiApi.TextDisabled("No accessories");
        }
        else
        {
            for (var i = 0; i < accessories.Count; i++)
            {
                var accessory = accessories[i];
                var selected = state.Selection.IsSelected(RenderPickTargetKind.Accessory, i);
                if (DrawOrderButtons(state, RenderPickTargetKind.Accessory, i, accessories.Count, accessory.Name))
                {
                    break;
                }

                ImGuiApi.SameLine();
                if (ImGuiApi.Selectable(accessory.Name, selected))
                {
                    state.Selection.Select(RenderPickTargetKind.Accessory, i, accessory.Name);
                    state.StatusText = $"Selected accessory: {accessory.Name}";
                }
            }
        }

        ImGuiApi.TreePop();
    }

    private static bool DrawOrderButtons(
        ImGuiEditorState state,
        RenderPickTargetKind kind,
        int index,
        int count,
        string objectName)
    {
        ImGuiApi.BeginDisabled(index == 0);
        var moveUp = ImGuiApi.SmallButton($"Up##{kind}-{index}");
        ImGuiApi.EndDisabled();
        ImGuiApi.SameLine();
        ImGuiApi.BeginDisabled(index >= count - 1);
        var moveDown = ImGuiApi.SmallButton($"Down##{kind}-{index}");
        ImGuiApi.EndDisabled();

        if (!moveUp && !moveDown)
        {
            return false;
        }

        var targetIndex = index + (moveUp ? -1 : 1);
        var moved = kind == RenderPickTargetKind.Model
            ? state.Project.MoveModel(index, targetIndex)
            : state.Project.MoveAccessory(index, targetIndex);
        if (!moved)
        {
            return false;
        }

        state.Selection.Select(kind, targetIndex, objectName);
        state.StatusText = $"{kind} draw order moved: {objectName}";
        return true;
    }

    private static void DrawMotions(IReadOnlyList<Core.Animation.Motion> motions)
    {
        if (!ImGuiApi.TreeNodeEx("Motions", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (motions.Count == 0)
        {
            ImGuiApi.TextDisabled("No motions");
        }
        else
        {
            foreach (var motion in motions)
            {
                ImGuiApi.BulletText($"{motion.Name} ({motion.MaxFrameIndex}f)");
            }
        }

        ImGuiApi.TreePop();
    }
}
