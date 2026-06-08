namespace Zhengyan.MikuMikuDance.UI.ImGui;

public interface IImGuiEditorPanel
{
    string Title { get; }

    void Draw(ImGuiEditorState state);
}
