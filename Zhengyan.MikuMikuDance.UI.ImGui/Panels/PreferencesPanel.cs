using System.Numerics;
using ImGuiNET;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui.Panels;

public sealed class PreferencesPanel : IImGuiEditorPanel
{
    public string Title => "Preferences";

    public void Draw(ImGuiEditorState state)
    {
        if (!state.Preferences.Panels.Preferences)
        {
            return;
        }

        var isOpen = true;
        if (!ImGuiApi.Begin(Title, ref isOpen))
        {
            state.Preferences.Panels.Preferences = isOpen;
            ImGuiApi.End();
            return;
        }

        var preferences = state.Preferences;
        DrawAppearance(preferences, state);
        ImGuiApi.Separator();
        DrawWindow(preferences, state);
        ImGuiApi.Separator();
        DrawViewport(preferences, state);
        ImGuiApi.Separator();
        DrawPanels(preferences, state);

        state.Preferences.Panels.Preferences = isOpen;
        ImGuiApi.End();
    }

    private static void DrawAppearance(EditorPreferences preferences, ImGuiEditorState state)
    {
        var themeIndex = 0;
        for (var i = 0; i < EditorPreferences.AvailableThemes.Count; i++)
        {
            if (string.Equals(EditorPreferences.AvailableThemes[i], preferences.Theme, StringComparison.OrdinalIgnoreCase))
            {
                themeIndex = i;
                break;
            }
        }

        var themes = string.Join('\0', EditorPreferences.AvailableThemes) + '\0';
        if (ImGuiApi.Combo("Theme", ref themeIndex, themes, EditorPreferences.AvailableThemes.Count))
        {
            preferences.Theme = EditorPreferences.AvailableThemes[themeIndex];
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }

        var clearColor = preferences.ClearColor.ToVector4();
        if (ImGuiApi.ColorEdit4("Clear Color", ref clearColor))
        {
            preferences.ClearColor = EditorColor.FromVector4(clearColor);
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }
    }

    private static void DrawWindow(EditorPreferences preferences, ImGuiEditorState state)
    {
        var vsync = preferences.VSync;
        if (ImGuiApi.Checkbox("VSync", ref vsync))
        {
            preferences.VSync = vsync;
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }

        ImGuiApi.TextUnformatted($"Current Window: {preferences.WindowWidth}x{preferences.WindowHeight}");
        ImGuiApi.TextDisabled("Window size is saved on close");
    }

    private static void DrawViewport(EditorPreferences preferences, ImGuiEditorState state)
    {
        var showGrid = preferences.ShowViewportGrid;
        if (ImGuiApi.Checkbox("Viewport Grid", ref showGrid))
        {
            preferences.ShowViewportGrid = showGrid;
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }

        var showPointedDebug = preferences.ShowPointedDebug;
        if (ImGuiApi.Checkbox("Pointed Debug", ref showPointedDebug))
        {
            preferences.ShowPointedDebug = showPointedDebug;
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }
    }

    private static void DrawPanels(EditorPreferences preferences, ImGuiEditorState state)
    {
        var panels = preferences.Panels;
        panels.Scene = DrawPanelToggle("Scene Panel", panels.Scene, state);
        panels.Timeline = DrawPanelToggle("Timeline Panel", panels.Timeline, state);
        panels.Playback = DrawPanelToggle("Playback Panel", panels.Playback, state);
        panels.Parameters = DrawPanelToggle("Parameters Panel", panels.Parameters, state);
    }

    private static bool DrawPanelToggle(string label, bool value, ImGuiEditorState state)
    {
        var selected = value;
        if (ImGuiApi.Checkbox(label, ref selected))
        {
            state.PreferencesDirty = true;
            state.StatusText = "Preferences updated";
        }

        return selected;
    }
}
