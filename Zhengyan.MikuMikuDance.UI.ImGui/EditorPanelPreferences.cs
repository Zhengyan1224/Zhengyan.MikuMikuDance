namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorPanelPreferences
{
    public bool Scene { get; set; } = true;

    public bool Timeline { get; set; } = true;

    public bool Playback { get; set; } = true;

    public bool Parameters { get; set; } = true;

    public bool Preferences { get; set; }

    public void CopyFrom(EditorPanelPreferences source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Scene = source.Scene;
        Timeline = source.Timeline;
        Playback = source.Playback;
        Parameters = source.Parameters;
        Preferences = source.Preferences;
    }
}
