using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorCommandRouter
{
    public bool Execute(ImGuiEditorState state, EditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (command)
        {
            case EditorCommand.NewProject:
                state.StatusText = "New project command routed";
                return true;
            case EditorCommand.OpenProject:
                state.StatusText = "Open project command routed";
                return true;
            case EditorCommand.SaveProject:
                state.StatusText = "Save project command routed";
                return true;
            case EditorCommand.Undo:
                state.StatusText = "Undo command routed";
                return false;
            case EditorCommand.Redo:
                state.StatusText = "Redo command routed";
                return false;
            case EditorCommand.Copy:
                state.StatusText = "Copy command routed";
                return false;
            case EditorCommand.Paste:
                state.StatusText = "Paste command routed";
                return false;
            case EditorCommand.TogglePlayback:
                state.IsPlaying = !state.IsPlaying;
                state.StatusText = state.IsPlaying ? "Playback started" : "Playback paused";
                return true;
            case EditorCommand.PreviousFrame:
                state.Project.Timeline.MoveBy(-1);
                state.StatusText = $"Frame {state.Project.Timeline.CurrentFrameIndex}";
                return true;
            case EditorCommand.NextFrame:
                state.Project.Timeline.MoveBy(1);
                state.StatusText = $"Frame {state.Project.Timeline.CurrentFrameIndex}";
                return true;
            case EditorCommand.ResetCamera:
                CameraNavigation.Reset(state.Project.Camera);
                state.StatusText = "Camera reset";
                return true;
            case EditorCommand.ToggleScenePanel:
                TogglePanel(state, panel => panel.Scene, (panel, value) => panel.Scene = value);
                return true;
            case EditorCommand.ToggleTimelinePanel:
                TogglePanel(state, panel => panel.Timeline, (panel, value) => panel.Timeline = value);
                return true;
            case EditorCommand.TogglePlaybackPanel:
                TogglePanel(state, panel => panel.Playback, (panel, value) => panel.Playback = value);
                return true;
            case EditorCommand.ToggleParametersPanel:
                TogglePanel(state, panel => panel.Parameters, (panel, value) => panel.Parameters = value);
                return true;
            case EditorCommand.TogglePreferencesPanel:
                TogglePanel(state, panel => panel.Preferences, (panel, value) => panel.Preferences = value);
                return true;
            default:
                return false;
        }
    }

    private static void TogglePanel(
        ImGuiEditorState state,
        Func<EditorPanelPreferences, bool> get,
        Action<EditorPanelPreferences, bool> set)
    {
        var panels = state.Preferences.Panels;
        var enabled = !get(panels);
        set(panels, enabled);
        state.PreferencesDirty = true;
        state.StatusText = enabled ? "Panel shown" : "Panel hidden";
    }
}
