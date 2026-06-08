using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public readonly record struct BezierControlPoint(byte X, byte Y);

public sealed record BezierCurve(
    BezierControlPoint P1,
    BezierControlPoint P2)
{
    public static BezierCurve Linear { get; } = new(
        new BezierControlPoint(20, 20),
        new BezierControlPoint(107, 107));

    public float Evaluate(float t)
    {
        t = Math.Clamp(t, 0, 1);
        if (P1.X == P1.Y && P2.X == P2.Y)
        {
            return t;
        }

        var p1 = new Vector2(P1.X / 127f, P1.Y / 127f);
        var p2 = new Vector2(P2.X / 127f, P2.Y / 127f);
        var lo = 0f;
        var hi = 1f;
        var u = t;

        for (var i = 0; i < 12; i++)
        {
            u = (lo + hi) * 0.5f;
            var x = Cubic(0, p1.X, p2.X, 1, u);
            if (x < t)
            {
                lo = u;
            }
            else
            {
                hi = u;
            }
        }

        return Cubic(0, p1.Y, p2.Y, 1, u);
    }

    private static float Cubic(float p0, float p1, float p2, float p3, float t)
    {
        var inv = 1 - t;
        return (inv * inv * inv * p0) +
            (3 * inv * inv * t * p1) +
            (3 * inv * t * t * p2) +
            (t * t * t * p3);
    }
}
