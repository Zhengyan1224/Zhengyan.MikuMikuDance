namespace Zhengyan.MikuMikuDance.Core.Scene;

public static class CameraBinding
{
    public static void ClearParent(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.ParentModelName = null;
        camera.ParentBoneName = null;
    }

    public static bool TrySetParent(Camera camera, MmdProject project, string? modelName, string? boneName)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(project);
        if (!SceneParentBindingResolver.TryResolve(project, modelName, boneName, out var binding))
        {
            return false;
        }

        camera.ParentModelName = binding.ModelName;
        camera.ParentBoneName = binding.BoneName;
        return true;
    }
}
