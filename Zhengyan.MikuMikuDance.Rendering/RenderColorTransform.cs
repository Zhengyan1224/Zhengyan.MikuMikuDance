using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public static class RenderColorTransform
{
    private static readonly Vector3 LuminanceWeights = new(0.2126f, 0.7152f, 0.0722f);

    public static Vector4 Apply(Vector4 color, SceneColorTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        transform.Normalize();

        var rgb = new Vector3(color.X, color.Y, color.Z);
        rgb += new Vector3(transform.Brightness);
        rgb = (rgb - new Vector3(0.5f)) * transform.Contrast + new Vector3(0.5f);
        var luma = Vector3.Dot(rgb, LuminanceWeights);
        rgb = Vector3.Lerp(new Vector3(luma), rgb, transform.Saturation);
        rgb = Vector3.Clamp(rgb, Vector3.Zero, Vector3.One);
        var inverseGamma = 1f / transform.Gamma;
        rgb = new Vector3(
            MathF.Pow(rgb.X, inverseGamma),
            MathF.Pow(rgb.Y, inverseGamma),
            MathF.Pow(rgb.Z, inverseGamma));
        return new Vector4(Vector3.Clamp(rgb, Vector3.Zero, Vector3.One), color.W);
    }

    public static Vector4 Parameters(SceneColorTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return transform.ToVector4();
    }
}
