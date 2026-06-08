using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class Accessory
{
    public Accessory(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public Uri? Source { get; set; }

    public bool Visible { get; set; } = true;

    public SceneTransform Transform { get; } = new();

    public Vector3 Translation
    {
        get => Transform.Translation;
        set => Transform.Translation = value;
    }

    public Vector3 Orientation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }

    public float Scale
    {
        get => Transform.Scale.X;
        set => Transform.Scale = new Vector3(value);
    }

    public float Opacity { get; set; } = 1f;

    public string? ParentModelName { get; set; }

    public string? ParentBoneName { get; set; }

    public Matrix4x4 CreateWorldMatrix() => Transform.CreateMatrix();
}
