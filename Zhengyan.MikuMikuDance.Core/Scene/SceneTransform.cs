using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class SceneTransform
{
    public Vector3 Translation { get; set; }

    public Vector3 Rotation { get; set; }

    public Vector3 Scale { get; set; } = Vector3.One;

    public Matrix4x4 CreateMatrix()
    {
        return Matrix4x4.CreateScale(Scale)
            * Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z)
            * Matrix4x4.CreateTranslation(Translation);
    }
}
