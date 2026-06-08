using System.Numerics;
using ImGuiNET;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;
using Zhengyan.MikuMikuDance.UI.ImGui.Panels;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class ImGuiEditorShell
{
    private readonly IReadOnlyList<IImGuiEditorPanel> _panels =
    [
        new SceneObjectsPanel(),
        new TimelinePanel(),
        new PlaybackPanel(),
        new ParametersPanel(),
        new PreferencesPanel()
    ];

    public void Draw(ImGuiEditorState state, Action? requestClose = null)
    {
        HandleShortcuts(state);
        DrawDockSpace();
        DrawMainMenu(state, requestClose);
        DrawToolbar(state);
        DrawViewport(state);
        DrawPanels(state);
        DrawStatusBar(state);
    }

    private static void DrawDockSpace()
    {
        var viewport = ImGuiApi.GetMainViewport();
        ImGuiApi.SetNextWindowPos(viewport.WorkPos);
        ImGuiApi.SetNextWindowSize(viewport.WorkSize);
        ImGuiApi.SetNextWindowViewport(viewport.ID);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoBackground;

        ImGuiApi.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGuiApi.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGuiApi.Begin("EditorDockSpace", flags);
        ImGuiApi.PopStyleVar(2);

        var dockSpaceId = ImGuiApi.GetID("MainDockSpace");
        ImGuiApi.DockSpace(dockSpaceId, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
        ImGuiApi.End();
    }

    private void DrawMainMenu(ImGuiEditorState state, Action? requestClose)
    {
        if (!ImGuiApi.BeginMainMenuBar())
        {
            return;
        }

        if (ImGuiApi.BeginMenu("File"))
        {
            if (ImGuiApi.MenuItem("New"))
            {
                state.Commands.Execute(state, EditorCommand.NewProject);
            }

            if (ImGuiApi.MenuItem("Open"))
            {
                state.Commands.Execute(state, EditorCommand.OpenProject);
            }

            if (ImGuiApi.MenuItem("Save"))
            {
                state.Commands.Execute(state, EditorCommand.SaveProject);
            }

            ImGuiApi.Separator();
            if (ImGuiApi.MenuItem("Exit"))
            {
                requestClose?.Invoke();
            }

            ImGuiApi.EndMenu();
        }

        if (ImGuiApi.BeginMenu("Edit"))
        {
            if (ImGuiApi.MenuItem("Undo", "Ctrl+Z"))
            {
                state.Commands.Execute(state, EditorCommand.Undo);
            }

            if (ImGuiApi.MenuItem("Redo", "Ctrl+Y"))
            {
                state.Commands.Execute(state, EditorCommand.Redo);
            }

            ImGuiApi.Separator();
            if (ImGuiApi.MenuItem("Copy", "Ctrl+C"))
            {
                state.Commands.Execute(state, EditorCommand.Copy);
            }

            if (ImGuiApi.MenuItem("Paste", "Ctrl+V"))
            {
                state.Commands.Execute(state, EditorCommand.Paste);
            }

            ImGuiApi.EndMenu();
        }

        if (ImGuiApi.BeginMenu("View"))
        {
            var panels = state.Preferences.Panels;
            DrawPanelMenuItem("Scene", panels.Scene, EditorCommand.ToggleScenePanel, state);
            DrawPanelMenuItem("Timeline", panels.Timeline, EditorCommand.ToggleTimelinePanel, state);
            DrawPanelMenuItem("Playback", panels.Playback, EditorCommand.TogglePlaybackPanel, state);
            DrawPanelMenuItem("Parameters", panels.Parameters, EditorCommand.ToggleParametersPanel, state);
            DrawPanelMenuItem("Preferences", panels.Preferences, EditorCommand.TogglePreferencesPanel, state);
            ImGuiApi.Separator();
            if (ImGuiApi.MenuItem("Reset Camera"))
            {
                state.Commands.Execute(state, EditorCommand.ResetCamera);
            }

            ImGuiApi.EndMenu();
        }

        ImGuiApi.EndMainMenuBar();
    }

    private static void DrawPanelMenuItem(string label, bool value, EditorCommand command, ImGuiEditorState state)
    {
        var selected = value;
        if (ImGuiApi.MenuItem(label, string.Empty, ref selected))
        {
            state.Commands.Execute(state, command);
        }
    }

    private static void DrawToolbar(ImGuiEditorState state)
    {
        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoDocking;

        ImGuiApi.Begin("Toolbar", flags);
        if (ImGuiApi.Button(state.IsPlaying ? "Pause" : "Play"))
        {
            state.Commands.Execute(state, EditorCommand.TogglePlayback);
        }

        ImGuiApi.SameLine();
        if (ImGuiApi.Button("Frame -"))
        {
            state.Commands.Execute(state, EditorCommand.PreviousFrame);
        }

        ImGuiApi.SameLine();
        if (ImGuiApi.Button("Frame +"))
        {
            state.Commands.Execute(state, EditorCommand.NextFrame);
        }

        ImGuiApi.SameLine();
        ImGuiApi.TextUnformatted($"Frame {state.Project.Timeline.CurrentFrameIndex}");
        ImGuiApi.End();
    }

    private static void HandleShortcuts(ImGuiEditorState state)
    {
        var io = ImGuiApi.GetIO();
        if (io.WantTextInput)
        {
            return;
        }

        if (ImGuiApi.IsKeyPressed(ImGuiKey.Space))
        {
            state.Commands.Execute(state, EditorCommand.TogglePlayback);
        }

        if (ImGuiApi.IsKeyPressed(ImGuiKey.LeftArrow))
        {
            state.Commands.Execute(state, EditorCommand.PreviousFrame);
        }

        if (ImGuiApi.IsKeyPressed(ImGuiKey.RightArrow))
        {
            state.Commands.Execute(state, EditorCommand.NextFrame);
        }

        if (!io.KeyCtrl)
        {
            return;
        }

        if (ImGuiApi.IsKeyPressed(ImGuiKey.Z))
        {
            state.Commands.Execute(state, EditorCommand.Undo);
        }
        else if (ImGuiApi.IsKeyPressed(ImGuiKey.Y))
        {
            state.Commands.Execute(state, EditorCommand.Redo);
        }
        else if (ImGuiApi.IsKeyPressed(ImGuiKey.C))
        {
            state.Commands.Execute(state, EditorCommand.Copy);
        }
        else if (ImGuiApi.IsKeyPressed(ImGuiKey.V))
        {
            state.Commands.Execute(state, EditorCommand.Paste);
        }
    }

    private static void DrawViewport(ImGuiEditorState state)
    {
        ImGuiApi.Begin("Viewport");
        ImGuiApi.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.02f, 0.023f, 0.026f, 1f));
        var available = ImGuiApi.GetContentRegionAvail();
        ImGuiApi.BeginChild("ViewportSurface", available, ImGuiChildFlags.Borders);
        var size = ImGuiApi.GetContentRegionAvail();
        state.Viewport.Width = Math.Max(1, (int)size.X);
        state.Viewport.Height = Math.Max(1, (int)size.Y);
        state.Viewport.IsFocused = ImGuiApi.IsWindowFocused();
        state.Viewport.IsHovered = ImGuiApi.IsWindowHovered();
        var viewportOrigin = ImGuiApi.GetCursorScreenPos();
        HandleViewportCameraInput(state);
        UpdatePointedDebug(state, viewportOrigin);
        DrawBackgroundPlaceholder(state, viewportOrigin);
        DrawViewportGrid(state, viewportOrigin);

        ImGuiApi.SetCursorPos(new Vector2(Math.Max(0f, size.X * 0.5f - 64f), Math.Max(0f, size.Y * 0.5f - 8f)));
        ImGuiApi.TextDisabled(state.Selection.HasSelection ? $"Selected: {state.Selection.ObjectName}" : "OpenGL viewport");
        DrawPointedDebug(state, viewportOrigin);
        DrawPointedOverlay(state, viewportOrigin);
        DrawSelectionOverlay(state, viewportOrigin);
        if (state.Viewport.IsHovered && ImGuiApi.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mousePosition = ImGuiApi.GetMousePos();
            var localPosition = mousePosition - viewportOrigin;
            var hit = RenderPicker.PickProject(state.Project, state.Viewport.Width, state.Viewport.Height, localPosition);
            if (hit is null)
            {
                state.Selection.Clear();
                state.StatusText = "Selection cleared";
            }
            else
            {
                state.Selection.Select(hit);
                state.StatusText = $"Selected {hit.Kind.ToString().ToLowerInvariant()}: {hit.ObjectName}";
            }
        }

        ImGuiApi.EndChild();
        ImGuiApi.PopStyleColor();
        ImGuiApi.End();
    }

    private static void DrawBackgroundPlaceholder(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        var background = state.Project.Background;
        if (background.VideoEnabled && background.VideoSource is not null)
        {
            DrawBackgroundPlaceholder(
                state,
                viewportOrigin,
                "Video",
                background.VideoSource,
                background.VideoOffsetX,
                background.VideoOffsetY,
                background.VideoScale,
                new Vector4(0.16f, 0.12f, 0.19f, 0.54f),
                new Vector4(0.66f, 0.48f, 0.76f, 0.78f));
            return;
        }

        if (!background.ImageEnabled || background.ImageSource is null)
        {
            return;
        }

        DrawBackgroundPlaceholder(
            state,
            viewportOrigin,
            "Image",
            background.ImageSource,
            background.ImageOffsetX,
            background.ImageOffsetY,
            background.ImageScale,
            new Vector4(0.12f, 0.16f, 0.19f, 0.52f),
            new Vector4(0.46f, 0.58f, 0.66f, 0.72f));
    }

    private static void DrawBackgroundPlaceholder(
        ImGuiEditorState state,
        Vector2 viewportOrigin,
        string kind,
        Uri source,
        int offsetX,
        int offsetY,
        float scale,
        Vector4 fillColor,
        Vector4 borderColor)
    {
        var size = new Vector2(state.Viewport.Width, state.Viewport.Height);
        var effectiveScale = Math.Max(0.01f, scale);
        var scaledSize = size * Math.Min(effectiveScale, 4f);
        var center = viewportOrigin + size * 0.5f + new Vector2(offsetX, offsetY);
        var min = center - scaledSize * 0.5f;
        var max = center + scaledSize * 0.5f;
        var drawList = ImGuiApi.GetWindowDrawList();
        var fill = ImGuiApi.ColorConvertFloat4ToU32(fillColor);
        var border = ImGuiApi.ColorConvertFloat4ToU32(borderColor);
        var textColor = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0.78f, 0.84f, 0.88f, 0.95f));
        drawList.AddRectFilled(min, max, fill);
        drawList.AddRect(min, max, border, 0f, ImDrawFlags.None, 1.5f);

        var label = $"Background {kind}: {Path.GetFileName(source.ToString())}";
        drawList.AddText(min + new Vector2(8f, 8f), textColor, label);
    }

    private static void DrawViewportGrid(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        if (!state.Preferences.ShowViewportGrid)
        {
            return;
        }

        var lines = RenderViewportGrid.CreateGrid(
            state.Project.Camera,
            state.Viewport.Width,
            state.Viewport.Height);
        if (lines.Count == 0)
        {
            return;
        }

        var drawList = ImGuiApi.GetWindowDrawList();
        var minorColor = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0.36f, 0.39f, 0.42f, 0.42f));
        var xAxisColor = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0.82f, 0.28f, 0.24f, 0.85f));
        var zAxisColor = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0.24f, 0.48f, 0.9f, 0.85f));
        foreach (var line in lines)
        {
            var color = line.Kind switch
            {
                RenderViewportGridLineKind.AxisX => xAxisColor,
                RenderViewportGridLineKind.AxisZ => zAxisColor,
                _ => minorColor
            };
            var thickness = line.Kind == RenderViewportGridLineKind.Minor ? 1f : 2f;
            drawList.AddLine(viewportOrigin + line.Start, viewportOrigin + line.End, color, thickness);
        }
    }

    private static void UpdatePointedDebug(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        if (!state.Preferences.ShowPointedDebug || !state.Viewport.IsHovered)
        {
            state.Viewport.PointedDebugText = string.Empty;
            state.Viewport.ClearPointedObject();
            return;
        }

        var mousePosition = ImGuiApi.GetMousePos();
        var localPosition = mousePosition - viewportOrigin;
        var hit = RenderPicker.PickProject(state.Project, state.Viewport.Width, state.Viewport.Height, localPosition);
        if (hit is null)
        {
            state.Viewport.ClearPointedObject();
            state.Viewport.PointedDebugText = $"Pointed: none ({localPosition.X:0}, {localPosition.Y:0})";
            return;
        }

        state.Viewport.SetPointedObject(hit);
        state.Viewport.PointedDebugText =
            $"Pointed: {hit.Kind} {hit.ObjectName} mesh {hit.MeshIndex} tri {hit.TriangleIndex}";
    }

    private static void DrawPointedDebug(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        if (!state.Preferences.ShowPointedDebug || string.IsNullOrWhiteSpace(state.Viewport.PointedDebugText))
        {
            return;
        }

        var drawList = ImGuiApi.GetWindowDrawList();
        var position = viewportOrigin + new Vector2(8f, 8f);
        var color = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0.86f, 0.9f, 0.94f, 1f));
        var shadow = ImGuiApi.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.65f));
        drawList.AddText(position + Vector2.One, shadow, state.Viewport.PointedDebugText);
        drawList.AddText(position, color, state.Viewport.PointedDebugText);
    }

    private static void DrawPointedOverlay(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        if (!state.Preferences.ShowPointedDebug || !state.Viewport.HasPointedObject || state.Viewport.PointedKind is null)
        {
            return;
        }

        if (state.Selection.Kind == state.Viewport.PointedKind &&
            state.Selection.ObjectIndex == state.Viewport.PointedObjectIndex)
        {
            return;
        }

        var overlay = RenderSelectionOverlay.CreateProjectOverlay(
            state.Project,
            state.Viewport.PointedKind.Value,
            state.Viewport.PointedObjectIndex,
            state.Viewport.Width,
            state.Viewport.Height,
            RenderSelectionOverlayRole.Pointed);
        DrawSelectionOverlayRect(overlay, viewportOrigin);
    }

    private static void DrawSelectionOverlay(ImGuiEditorState state, Vector2 viewportOrigin)
    {
        if (!state.Selection.HasSelection || state.Selection.Kind is null)
        {
            return;
        }

        var overlay = RenderSelectionOverlay.CreateProjectOverlay(
            state.Project,
            state.Selection.Kind.Value,
            state.Selection.ObjectIndex,
            state.Viewport.Width,
            state.Viewport.Height,
            RenderSelectionOverlayRole.Selected);
        DrawSelectionOverlayRect(overlay, viewportOrigin);
    }

    private static void DrawSelectionOverlayRect(RenderSelectionOverlayRect? overlay, Vector2 viewportOrigin)
    {
        if (overlay is null)
        {
            return;
        }

        var style = RenderSelectionOverlayStyle.ForRole(overlay.Role);
        var drawList = ImGuiApi.GetWindowDrawList();
        var min = viewportOrigin + overlay.Min;
        var max = viewportOrigin + overlay.Max;
        var color = ImGuiApi.ColorConvertFloat4ToU32(style.StrokeColor);
        var labelColor = ImGuiApi.ColorConvertFloat4ToU32(style.LabelColor);
        var shadow = ImGuiApi.ColorConvertFloat4ToU32(style.ShadowColor);
        drawList.AddRect(min + Vector2.One, max + Vector2.One, shadow, 0f, ImDrawFlags.None, style.Thickness);
        drawList.AddRect(min, max, color, 0f, ImDrawFlags.None, style.Thickness);
        if (style.DrawLabel)
        {
            drawList.AddText(
                new Vector2(min.X, Math.Max(viewportOrigin.Y, min.Y - ImGuiApi.GetTextLineHeight() - 2f)),
                labelColor,
                overlay.ObjectName);
        }
    }

    private static void HandleViewportCameraInput(ImGuiEditorState state)
    {
        if (!state.Viewport.IsHovered)
        {
            return;
        }

        var io = ImGuiApi.GetIO();
        var mouseDelta = io.MouseDelta;
        if (mouseDelta.LengthSquared() > 0)
        {
            if (ImGuiApi.IsMouseDragging(ImGuiMouseButton.Right))
            {
                CameraNavigation.Orbit(state.Project.Camera, mouseDelta);
                state.StatusText = "Camera orbit";
            }
            else if (ImGuiApi.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                CameraNavigation.Pan(state.Project.Camera, mouseDelta, state.Viewport.Width, state.Viewport.Height);
                state.StatusText = "Camera pan";
            }
        }

        if (io.MouseWheel != 0)
        {
            CameraNavigation.Zoom(state.Project.Camera, io.MouseWheel);
            state.StatusText = "Camera zoom";
        }
    }

    private void DrawPanels(ImGuiEditorState state)
    {
        if (state.Preferences.Panels.Scene)
        {
            _panels[0].Draw(state);
        }

        if (state.Preferences.Panels.Timeline)
        {
            _panels[1].Draw(state);
        }

        if (state.Preferences.Panels.Playback)
        {
            _panels[2].Draw(state);
        }

        if (state.Preferences.Panels.Parameters)
        {
            _panels[3].Draw(state);
        }

        _panels[4].Draw(state);
    }

    private static void DrawStatusBar(ImGuiEditorState state)
    {
        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoSavedSettings;

        var viewport = ImGuiApi.GetMainViewport();
        var height = ImGuiApi.GetFrameHeight() + ImGuiApi.GetStyle().WindowPadding.Y * 2f;
        ImGuiApi.SetNextWindowPos(new Vector2(viewport.WorkPos.X, viewport.WorkPos.Y + viewport.WorkSize.Y - height));
        ImGuiApi.SetNextWindowSize(new Vector2(viewport.WorkSize.X, height));

        ImGuiApi.Begin("StatusBar", flags);
        var path = string.IsNullOrWhiteSpace(state.ProjectPath) ? "Unsaved project" : state.ProjectPath;
        ImGuiApi.TextUnformatted($"{state.StatusText} | {path}");
        ImGuiApi.SameLine();
        ImGuiApi.TextUnformatted($"Viewport {state.Viewport.Width}x{state.Viewport.Height}");
        ImGuiApi.End();
    }
}
