using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderBackgroundQuad(
    float Left,
    float Top,
    float Width,
    float Height)
{
    public float Right => Left + Width;

    public float Bottom => Top + Height;

    public Vector2 Center => new(Left + Width * 0.5f, Top + Height * 0.5f);
}

public static class RenderBackgroundLayout
{
    public static RenderBackgroundQuad? FitImage(
        int viewportWidth,
        int viewportHeight,
        int imageWidth,
        int imageHeight,
        float scale,
        int offsetX,
        int offsetY,
        BackgroundImageLayoutMode layoutMode = BackgroundImageLayoutMode.Fit)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        var effectiveScale = scale > 0 && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
        var widthScale = viewportWidth / (float)imageWidth;
        var heightScale = viewportHeight / (float)imageHeight;
        var fitScale = layoutMode == BackgroundImageLayoutMode.Fill
            ? Math.Max(widthScale, heightScale)
            : Math.Min(widthScale, heightScale);
        var width = imageWidth * fitScale * effectiveScale;
        var height = imageHeight * fitScale * effectiveScale;
        var centerX = viewportWidth * 0.5f + offsetX;
        var centerY = viewportHeight * 0.5f + offsetY;
        return new RenderBackgroundQuad(
            centerX - width * 0.5f,
            centerY - height * 0.5f,
            width,
            height);
    }
}
