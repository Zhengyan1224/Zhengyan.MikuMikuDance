using ImGuiNET;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui.Panels;

public sealed class PlaybackPanel : IImGuiEditorPanel
{
    public string Title => "Playback";

    public void Draw(ImGuiEditorState state)
    {
        if (!ImGuiApi.Begin(Title))
        {
            ImGuiApi.End();
            return;
        }

        if (ImGuiApi.Button(state.IsPlaying ? "Pause" : "Play"))
        {
            state.IsPlaying = !state.IsPlaying;
            state.StatusText = state.IsPlaying ? "Playing" : "Paused";
        }

        ImGuiApi.SameLine();
        if (ImGuiApi.Button("Previous"))
        {
            state.Project.Timeline.MoveBy(-1);
        }

        ImGuiApi.SameLine();
        if (ImGuiApi.Button("Next"))
        {
            state.Project.Timeline.MoveBy(1);
        }

        var loop = state.Project.Timeline.LoopEnabled;
        if (ImGuiApi.Checkbox("Loop", ref loop))
        {
            state.Project.Timeline.LoopEnabled = loop;
        }

        ImGuiApi.End();
    }
}
