namespace Zhengyan.MikuMikuDance.UI.ImGui;

using Zhengyan.MikuMikuDance.Rendering;

public sealed class EditorViewportState
{
    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsFocused { get; set; }

    public bool IsHovered { get; set; }

    public string PointedDebugText { get; set; } = string.Empty;

    public RenderPickTargetKind? PointedKind { get; private set; }

    public int PointedObjectIndex { get; private set; } = -1;

    public string PointedObjectName { get; private set; } = string.Empty;

    public bool HasPointedObject => PointedKind is not null && PointedObjectIndex >= 0;

    public void SetPointedObject(RenderPickHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        PointedKind = hit.Kind;
        PointedObjectIndex = hit.ObjectIndex;
        PointedObjectName = hit.ObjectName;
    }

    public void ClearPointedObject()
    {
        PointedKind = null;
        PointedObjectIndex = -1;
        PointedObjectName = string.Empty;
    }
}
