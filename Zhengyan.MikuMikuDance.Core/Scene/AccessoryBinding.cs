namespace Zhengyan.MikuMikuDance.Core.Scene;

public static class AccessoryBinding
{
    public static void ClearParent(Accessory accessory)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        accessory.ParentModelName = null;
        accessory.ParentBoneName = null;
    }

    public static bool TrySetParent(Accessory accessory, MmdProject project, string? modelName, string? boneName)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            ClearParent(accessory);
            return true;
        }

        if (!SceneParentBindingResolver.TryResolve(project, modelName, boneName, out var binding))
        {
            return false;
        }

        accessory.ParentModelName = binding.ModelName;
        accessory.ParentBoneName = binding.BoneName;
        return true;
    }
}
