using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class SceneColorTransform
{
    public float Brightness { get; set; }

    public float Contrast { get; set; } = 1f;

    public float Saturation { get; set; } = 1f;

    public float Gamma { get; set; } = 1f;

    public bool IsIdentity =>
        Brightness == 0f &&
        Contrast == 1f &&
        Saturation == 1f &&
        Gamma == 1f;

    public Vector4 ToVector4()
    {
        Normalize();
        return new Vector4(Brightness, Contrast, Saturation, Gamma);
    }

    public void Reset()
    {
        Brightness = 0f;
        Contrast = 1f;
        Saturation = 1f;
        Gamma = 1f;
    }

    public void Normalize()
    {
        Brightness = Math.Clamp(Sanitize(Brightness, 0f), -1f, 1f);
        Contrast = Math.Clamp(Sanitize(Contrast, 1f), 0f, 4f);
        Saturation = Math.Clamp(Sanitize(Saturation, 1f), 0f, 4f);
        Gamma = Math.Clamp(Sanitize(Gamma, 1f), 0.01f, 8f);
    }

    private static float Sanitize(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
