using System.Numerics;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorColor
{
    public EditorColor()
    {
    }

    public EditorColor(float red, float green, float blue, float alpha)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public float Red { get; set; }

    public float Green { get; set; }

    public float Blue { get; set; }

    public float Alpha { get; set; } = 1f;

    public Vector4 ToVector4() => new(Red, Green, Blue, Alpha);

    public static EditorColor FromVector4(Vector4 color) => new(color.X, color.Y, color.Z, color.W);
}
