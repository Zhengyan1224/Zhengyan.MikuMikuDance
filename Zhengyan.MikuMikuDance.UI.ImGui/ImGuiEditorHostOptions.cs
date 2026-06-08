using System.Numerics;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed record ImGuiEditorHostOptions(
    string Title,
    int Width,
    int Height,
    Vector4 ClearColor,
    bool VSync)
{
    public static ImGuiEditorHostOptions Default { get; } = new(
        "Zhengyan MikuMikuDance Editor",
        1440,
        900,
        new Vector4(0.07f, 0.075f, 0.08f, 1f),
        true);

    public static ImGuiEditorHostOptions FromPreferences(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Normalize();
        return Default with
        {
            Width = preferences.WindowWidth,
            Height = preferences.WindowHeight,
            ClearColor = preferences.ClearColor.ToVector4(),
            VSync = preferences.VSync
        };
    }
}
