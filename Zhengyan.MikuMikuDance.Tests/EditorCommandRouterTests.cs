using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.UI.ImGui;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EditorCommandRouterTests
{
    [Fact]
    public void RoutesPlaybackAndFrameCommands()
    {
        var state = CreateState();

        Assert.True(state.Commands.Execute(state, EditorCommand.TogglePlayback));
        Assert.True(state.IsPlaying);
        Assert.True(state.Commands.Execute(state, EditorCommand.NextFrame));
        Assert.Equal(1, state.Project.Timeline.CurrentFrameIndex);
        Assert.True(state.Commands.Execute(state, EditorCommand.PreviousFrame));
        Assert.Equal(0, state.Project.Timeline.CurrentFrameIndex);
    }

    [Fact]
    public void RoutesCameraResetCommand()
    {
        var state = CreateState();
        state.Project.Camera.LookAt = new Vector3(1, 2, 3);
        state.Project.Camera.Distance = 20;
        state.Project.Camera.PerspectiveEnabled = false;

        Assert.True(state.Commands.Execute(state, EditorCommand.ResetCamera));

        Assert.Equal(Vector3.Zero, state.Project.Camera.LookAt);
        Assert.Equal(45f, state.Project.Camera.Distance);
        Assert.True(state.Project.Camera.PerspectiveEnabled);
    }

    [Fact]
    public void RoutesPanelToggleCommands()
    {
        var state = CreateState();

        Assert.True(state.Preferences.Panels.Scene);
        Assert.True(state.Commands.Execute(state, EditorCommand.ToggleScenePanel));

        Assert.False(state.Preferences.Panels.Scene);
        Assert.True(state.PreferencesDirty);
    }

    [Fact]
    public void ReportsPlaceholderCommandsAsNotHandled()
    {
        var state = CreateState();

        Assert.False(state.Commands.Execute(state, EditorCommand.Undo));
        Assert.Equal("Undo command routed", state.StatusText);
    }

    private static ImGuiEditorState CreateState()
    {
        return new ImGuiEditorState(new MmdProject(), new EditorPreferences());
    }
}
