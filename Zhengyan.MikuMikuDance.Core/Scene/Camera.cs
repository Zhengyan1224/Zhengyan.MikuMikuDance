using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class Camera
{
    public Vector3 LookAt { get; set; } = Vector3.Zero;

    public Vector3 Angle { get; set; } = new(0, 0, 0);

    public float Distance { get; set; } = 45f;

    public int FieldOfView { get; set; } = 30;

    public bool PerspectiveEnabled { get; set; } = true;

    public Matrix4x4 CreateViewMatrix()
    {
        var rotation = Matrix4x4.CreateFromYawPitchRoll(Angle.Y, Angle.X, Angle.Z);
        var offset = Vector3.Transform(new Vector3(0, 0, Distance), rotation);
        var position = LookAt + offset;
        return Matrix4x4.CreateLookAt(position, LookAt, Vector3.UnitY);
    }

    public Matrix4x4 CreateProjectionMatrix(float aspectRatio, float nearPlane = 0.5f, float farPlane = 10000f)
    {
        return PerspectiveEnabled
            ? Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView * MathF.PI / 180f, aspectRatio, nearPlane, farPlane)
            : Matrix4x4.CreateOrthographic(40f * aspectRatio, 40f, nearPlane, farPlane);
    }
}
