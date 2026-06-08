using System.Numerics;
using Zhengyan.MikuMikuDance.Rendering;
using Zhengyan.MikuMikuDance.UI.ImGui;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EditorViewportStateTests
{
    [Fact]
    public void TracksAndClearsPointedObject()
    {
        var state = new EditorViewportState();
        var hit = new RenderPickHit(
            RenderPickTargetKind.Model,
            2,
            "Miku",
            10,
            Vector3.One,
            1,
            7);

        state.SetPointedObject(hit);

        Assert.True(state.HasPointedObject);
        Assert.Equal(RenderPickTargetKind.Model, state.PointedKind);
        Assert.Equal(2, state.PointedObjectIndex);
        Assert.Equal("Miku", state.PointedObjectName);

        state.ClearPointedObject();

        Assert.False(state.HasPointedObject);
        Assert.Null(state.PointedKind);
        Assert.Equal(-1, state.PointedObjectIndex);
        Assert.Equal(string.Empty, state.PointedObjectName);
    }
}
