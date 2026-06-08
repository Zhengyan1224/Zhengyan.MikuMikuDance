using ImGuiNET;
using ImGuiApi = ImGuiNET.ImGui;

namespace Zhengyan.MikuMikuDance.UI.ImGui.Panels;

public sealed class TimelinePanel : IImGuiEditorPanel
{
    public string Title => "Timeline";

    public void Draw(ImGuiEditorState state)
    {
        if (!ImGuiApi.Begin(Title))
        {
            ImGuiApi.End();
            return;
        }

        var project = state.Project;
        var currentFrame = project.Timeline.CurrentFrameIndex;
        var maxFrame = Math.Max(project.DurationFrames, 1);
        if (ImGuiApi.SliderInt("Frame", ref currentFrame, 0, maxFrame))
        {
            project.Timeline.Seek(currentFrame);
        }

        ImGuiApi.TextUnformatted($"Range: {project.Timeline.PlaybackRange.Start}-{project.Timeline.PlaybackRange.End}");
        ImGuiApi.TextUnformatted($"Duration: {project.DurationFrames} frames");

        ImGuiApi.End();
    }
}
