using System.Numerics;
using System.Text;
using ImGuiNET;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui.Panels;

public sealed class ParametersPanel : IImGuiEditorPanel
{
    public string Title => "Parameters";

    public void Draw(ImGuiEditorState state)
    {
        if (!ImGuiApi.Begin(Title))
        {
            ImGuiApi.End();
            return;
        }

        DrawCamera(state);
        ImGuiApi.Separator();
        DrawLight(state);
        ImGuiApi.Separator();
        DrawBackground(state);
        ImGuiApi.Separator();
        DrawSelection(state);

        ImGuiApi.End();
    }

    private static void DrawCamera(ImGuiEditorState state)
    {
        var camera = state.Project.Camera;
        var lookAt = camera.LookAt;
        if (ImGuiApi.DragFloat3("Camera Look At", ref lookAt, 0.05f))
        {
            camera.LookAt = lookAt;
        }

        var angle = camera.Angle;
        if (ImGuiApi.DragFloat3("Camera Angle", ref angle, 0.01f))
        {
            camera.Angle = angle;
        }

        var distance = camera.Distance;
        if (ImGuiApi.DragFloat("Camera Distance", ref distance, 0.1f, 0f, 1000f))
        {
            camera.Distance = distance;
        }

        var fov = camera.FieldOfView;
        if (ImGuiApi.SliderInt("Field of View", ref fov, 1, 120))
        {
            camera.FieldOfView = fov;
        }

        var perspective = camera.PerspectiveEnabled;
        if (ImGuiApi.Checkbox("Perspective", ref perspective))
        {
            camera.PerspectiveEnabled = perspective;
        }

        DrawCameraParentBinding(state, camera);
    }

    private static void DrawLight(ImGuiEditorState state)
    {
        var light = state.Project.Light;
        var direction = light.Direction;
        if (ImGuiApi.DragFloat3("Light Direction", ref direction, 0.01f))
        {
            light.Direction = direction == Vector3.Zero ? light.Direction : Vector3.Normalize(direction);
        }

        var color = light.Color;
        if (ImGuiApi.ColorEdit3("Light Color", ref color))
        {
            light.Color = color;
        }
    }

    private static void DrawBackground(ImGuiEditorState state)
    {
        var background = state.Project.Background;
        if (!ImGuiApi.TreeNodeEx("Background", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        DrawBackgroundVideo(state, background);
        ImGuiApi.Separator();
        DrawBackgroundImage(state, background);
        ImGuiApi.TreePop();
    }

    private static void DrawBackgroundVideo(ImGuiEditorState state, SceneBackground background)
    {
        var enabled = background.VideoEnabled;
        if (ImGuiApi.Checkbox("Background Video", ref enabled))
        {
            background.VideoEnabled = enabled && background.VideoSource is not null;
            state.StatusText = background.VideoEnabled ? "Background video enabled" : "Background video disabled";
        }

        DrawBackgroundSourceInput(
            "Video Path",
            background.VideoSource,
            value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    background.ClearVideo();
                    state.StatusText = "Background video cleared";
                    return;
                }

                background.VideoSource = new Uri(value, UriKind.RelativeOrAbsolute);
                background.VideoEnabled = true;
                state.StatusText = "Background video updated";
            });

        var offset = new Vector2(background.VideoOffsetX, background.VideoOffsetY);
        if (ImGuiApi.DragFloat2("Video Offset", ref offset, 1f))
        {
            background.VideoOffsetX = (int)MathF.Round(offset.X);
            background.VideoOffsetY = (int)MathF.Round(offset.Y);
            state.StatusText = "Background video offset updated";
        }

        var scale = background.VideoScale;
        if (ImGuiApi.DragFloat("Video Scale", ref scale, 0.01f, 0.01f, 100f))
        {
            background.VideoScale = scale;
            background.Normalize();
            state.StatusText = "Background video scale updated";
        }

        ImGuiApi.TextDisabled($"Video Frame Time: {background.VideoFrameTime.TotalSeconds:0.###}s");
    }

    private static void DrawBackgroundImage(ImGuiEditorState state, SceneBackground background)
    {
        var enabled = background.ImageEnabled;
        if (ImGuiApi.Checkbox("Background Image", ref enabled))
        {
            background.ImageEnabled = enabled && background.ImageSource is not null;
            state.StatusText = background.ImageEnabled ? "Background image enabled" : "Background image disabled";
        }

        DrawBackgroundSourceInput(
            "Image Path",
            background.ImageSource,
            value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    background.ClearImage();
                    state.StatusText = "Background image cleared";
                    return;
                }

                background.ImageSource = new Uri(value, UriKind.RelativeOrAbsolute);
                background.ImageEnabled = true;
                state.StatusText = "Background image updated";
            });

        var offset = new Vector2(background.ImageOffsetX, background.ImageOffsetY);
        if (ImGuiApi.DragFloat2("Image Offset", ref offset, 1f))
        {
            background.ImageOffsetX = (int)MathF.Round(offset.X);
            background.ImageOffsetY = (int)MathF.Round(offset.Y);
            state.StatusText = "Background image offset updated";
        }

        var scale = background.ImageScale;
        if (ImGuiApi.DragFloat("Image Scale", ref scale, 0.01f, 0.01f, 100f))
        {
            background.ImageScale = scale;
            background.Normalize();
            state.StatusText = "Background image scale updated";
        }
    }

    private static void DrawBackgroundSourceInput(string label, Uri? source, Action<string> apply)
    {
        var path = source?.ToString() ?? string.Empty;
        var buffer = new byte[512];
        var bytes = Encoding.UTF8.GetBytes(path);
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length - 1));
        if (ImGuiApi.InputText(label, buffer, (uint)buffer.Length))
        {
            apply(Encoding.UTF8.GetString(buffer).TrimEnd('\0'));
        }
    }

    private static void DrawSelection(ImGuiEditorState state)
    {
        if (!state.Selection.HasSelection)
        {
            ImGuiApi.TextDisabled("No active selection");
            return;
        }

        var model = state.Selection.GetSelectedModel(state.Project);
        if (model is not null)
        {
            ImGuiApi.TextUnformatted($"Model: {model.Name}");
            DrawActiveModelChild(state, model);
            var visible = model.Visible;
            if (ImGuiApi.Checkbox("Model Visible", ref visible))
            {
                model.Visible = visible;
            }

            DrawTransform(model.Transform);
            DrawMorphWeights(state, model);
            DrawModelOutsideParents(state, model);
            return;
        }

        var accessory = state.Selection.GetSelectedAccessory(state.Project);
        if (accessory is not null)
        {
            ImGuiApi.TextUnformatted($"Accessory: {accessory.Name}");
            var visible = accessory.Visible;
            if (ImGuiApi.Checkbox("Visible", ref visible))
            {
                accessory.Visible = visible;
            }

            var opacity = accessory.Opacity;
            if (ImGuiApi.SliderFloat("Opacity", ref opacity, 0f, 1f))
            {
                accessory.Opacity = opacity;
            }

            DrawTransform(accessory.Transform);
            DrawAccessoryParentBinding(state, accessory);
        }
    }

    private static void DrawActiveModelChild(ImGuiEditorState state, ModelInstance model)
    {
        var bone = state.Selection.GetSelectedBone(state.Project);
        if (bone is not null)
        {
            ImGuiApi.Separator();
            DrawSelectedBone(model, state.Selection.ActiveBoneIndex, bone);
            return;
        }

        var morph = state.Selection.GetSelectedMorph(state.Project);
        if (morph is not null)
        {
            ImGuiApi.Separator();
            DrawSelectedMorph(state, model, morph);
        }
    }

    private static void DrawSelectedBone(ModelInstance model, int boneIndex, Bone bone)
    {
        ImGuiApi.TextUnformatted($"Active Bone: {DisplayName(bone)}");
        ImGuiApi.TextDisabled($"Index: {boneIndex}");
        ImGuiApi.TextDisabled($"Parent: {ParentBoneName(model, bone)}");
        ImGuiApi.TextDisabled($"Layer: {bone.LayerIndex}");
        ImGuiApi.TextDisabled($"Origin: {bone.Origin.X:0.###}, {bone.Origin.Y:0.###}, {bone.Origin.Z:0.###}");
        ImGuiApi.TextDisabled($"Flags: {bone.Flags}");
        if (bone.Ik is not null)
        {
            ImGuiApi.TextDisabled($"IK effector: {bone.Ik.EffectorBoneIndex}, links: {bone.Ik.Links.Count}");
        }
    }

    private static void DrawSelectedMorph(ImGuiEditorState state, ModelInstance model, Morph morph)
    {
        var label = DisplayName(morph);
        ImGuiApi.TextUnformatted($"Active Morph: {label}");
        ImGuiApi.TextDisabled($"Category: {morph.Category}");
        ImGuiApi.TextDisabled($"Type: {morph.Type}");
        ImGuiApi.TextDisabled($"Offsets: {morph.Offsets.Count}");

        var weight = model.GetMorphWeight(morph.Name);
        if (ImGuiApi.SliderFloat($"Weight##active-morph-{state.Selection.ActiveMorphIndex}", ref weight, 0f, 1f))
        {
            model.SetMorphWeight(morph.Name, weight);
            state.StatusText = $"Morph {label}: {weight:0.###}";
        }
    }

    private static void DrawAccessoryParentBinding(ImGuiEditorState state, Accessory accessory)
    {
        ImGuiApi.Separator();
        DrawParentBinding(
            state,
            "Parent Model",
            "Parent Bone",
            accessory.ParentModelName,
            accessory.ParentBoneName,
            (modelName, boneName) =>
            {
                if (!AccessoryBinding.TrySetParent(accessory, state.Project, modelName, boneName))
                {
                    return false;
                }

                state.StatusText = modelName is null
                    ? $"Detached accessory: {accessory.Name}"
                    : boneName is null
                        ? $"Bound accessory {accessory.Name} to {modelName}"
                        : $"Bound accessory {accessory.Name} to {modelName}/{boneName}";
                return true;
            });
    }

    private static void DrawCameraParentBinding(ImGuiEditorState state, Camera camera)
    {
        DrawParentBinding(
            state,
            "Camera Parent Model",
            "Camera Parent Bone",
            camera.ParentModelName,
            camera.ParentBoneName,
            (modelName, boneName) =>
            {
                if (!CameraBinding.TrySetParent(camera, state.Project, modelName, boneName))
                {
                    return false;
                }

                state.StatusText = modelName is null
                    ? "Camera parent cleared"
                    : boneName is null
                        ? $"Camera bound to {modelName}"
                        : $"Camera bound to {modelName}/{boneName}";
                return true;
            });
    }

    private static void DrawParentBinding(
        ImGuiEditorState state,
        string modelLabel,
        string boneLabel,
        string? currentModelName,
        string? currentBoneName,
        Func<string?, string?, bool> apply)
    {
        var modelItems = new[] { "(none)" }
            .Concat(state.Project.ModelInstances.Select(model => model.Name))
            .ToArray();
        var modelIndex = 0;
        for (var i = 0; i < state.Project.ModelInstances.Count; i++)
        {
            if (string.Equals(state.Project.ModelInstances[i].Name, currentModelName, StringComparison.Ordinal))
            {
                modelIndex = i + 1;
                break;
            }
        }

        var modelCombo = string.Join('\0', modelItems) + '\0';
        if (ImGuiApi.Combo(modelLabel, ref modelIndex, modelCombo, modelItems.Length))
        {
            var modelName = modelIndex == 0 ? null : modelItems[modelIndex];
            apply(modelName, null);
        }

        if (modelIndex == 0)
        {
            return;
        }

        var model = state.Project.ModelInstances[modelIndex - 1];
        var boneItems = new[] { "(none)" }
            .Concat(model.Model.Bones.Select(bone => bone.Name))
            .ToArray();
        var boneIndex = 0;
        for (var i = 0; i < model.Model.Bones.Count; i++)
        {
            if (string.Equals(model.Model.Bones[i].Name, currentBoneName, StringComparison.Ordinal))
            {
                boneIndex = i + 1;
                break;
            }
        }

        var boneCombo = string.Join('\0', boneItems) + '\0';
        if (ImGuiApi.Combo(boneLabel, ref boneIndex, boneCombo, boneItems.Length))
        {
            var boneName = boneIndex == 0 ? null : boneItems[boneIndex];
            apply(model.Name, boneName);
        }
    }

    private static void DrawMorphWeights(ImGuiEditorState state, ModelInstance model)
    {
        if (model.Model.Morphs.Count == 0)
        {
            return;
        }

        ImGuiApi.Separator();
        if (!ImGuiApi.TreeNodeEx("Morphs", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (ImGuiApi.Button("Reset Morphs"))
        {
            model.ClearMorphWeights();
            state.StatusText = $"Reset morphs: {model.Name}";
        }

        foreach (var morph in model.Model.Morphs)
        {
            var weight = model.GetMorphWeight(morph.Name);
            var label = string.IsNullOrWhiteSpace(morph.Name) ? morph.EnglishName : morph.Name;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = morph.Type.ToString();
            }

            var sliderLabel = $"{label}##morph-{morph.Name}";
            if (ImGuiApi.SliderFloat(sliderLabel, ref weight, 0f, 1f))
            {
                model.SetMorphWeight(morph.Name, weight);
                state.StatusText = $"Morph {label}: {weight:0.###}";
            }
        }

        ImGuiApi.TreePop();
    }

    private static void DrawModelOutsideParents(ImGuiEditorState state, ModelInstance model)
    {
        var bones = model.Model.Bones
            .Where(bone => bone.Flags.HasFlag(BoneFlags.OutsideParent))
            .ToArray();
        if (bones.Length == 0)
        {
            return;
        }

        ImGuiApi.Separator();
        if (!ImGuiApi.TreeNodeEx("Outside Parents", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        foreach (var bone in bones)
        {
            if (!ImGuiApi.TreeNodeEx(bone.Name))
            {
                continue;
            }

            var binding = model.GetOutsideParentBinding(bone.Name);
            DrawParentBinding(
                state,
                $"Parent Model##outside-{bone.Name}",
                $"Parent Bone##outside-{bone.Name}",
                binding?.ParentModelName,
                binding?.ParentBoneName,
                (modelName, boneName) =>
                {
                    if (!ModelOutsideParentBindingEditor.TrySetParent(model, state.Project, bone.Name, modelName, boneName))
                    {
                        return false;
                    }

                    state.StatusText = modelName is null
                        ? $"Outside parent cleared: {bone.Name}"
                        : boneName is null
                            ? $"Outside parent {bone.Name} -> {modelName}"
                            : $"Outside parent {bone.Name} -> {modelName}/{boneName}";
                    return true;
                });
            ImGuiApi.TreePop();
        }

        ImGuiApi.TreePop();
    }

    private static void DrawTransform(SceneTransform transform)
    {
        var translation = transform.Translation;
        if (ImGuiApi.DragFloat3("Translation", ref translation, 0.05f))
        {
            transform.Translation = translation;
        }

        var rotation = transform.Rotation;
        if (ImGuiApi.DragFloat3("Rotation", ref rotation, 0.01f))
        {
            transform.Rotation = rotation;
        }

        var scale = transform.Scale;
        if (ImGuiApi.DragFloat3("Scale", ref scale, 0.01f, 0.001f, 100f))
        {
            transform.Scale = scale;
        }
    }

    private static string ParentBoneName(ModelInstance model, Bone bone)
    {
        return bone.ParentBoneIndex >= 0 && bone.ParentBoneIndex < model.Model.Bones.Count
            ? model.Model.Bones[bone.ParentBoneIndex].Name
            : "(none)";
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
}
