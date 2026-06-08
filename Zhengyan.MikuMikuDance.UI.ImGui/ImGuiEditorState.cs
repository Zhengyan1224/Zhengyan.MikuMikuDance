using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class ImGuiEditorState
{
    public ImGuiEditorState(MmdProject project, EditorPreferences preferences, string? projectPath = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(preferences);
        Project = project;
        Preferences = preferences;
        ProjectPath = projectPath;
    }

    public MmdProject Project { get; }

    public EditorPreferences Preferences { get; }

    public string? ProjectPath { get; set; }

    public bool IsPlaying { get; set; }

    public string StatusText { get; set; } = "Ready";

    public EditorViewportState Viewport { get; } = new();

    public EditorSelection Selection { get; } = new();

    public EditorCommandRouter Commands { get; } = new();

    public bool PreferencesDirty { get; set; }
}
