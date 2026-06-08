namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed record ModelOutsideParentBinding(
    string BoneName,
    string? ParentModelName,
    string? ParentBoneName);

public static class ModelOutsideParentBindingEditor
{
    public static bool TrySetParent(
        ModelInstance instance,
        MmdProject project,
        string boneName,
        string? parentModelName,
        string? parentBoneName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(project);
        if (!HasOutsideParentBone(instance, boneName))
        {
            return false;
        }

        if (!SceneParentBindingResolver.TryResolve(project, parentModelName, parentBoneName, out var binding))
        {
            return false;
        }

        instance.SetOutsideParentBinding(new ModelOutsideParentBinding(
            boneName,
            binding.ModelName,
            binding.BoneName));
        return true;
    }

    private static bool HasOutsideParentBone(ModelInstance instance, string boneName)
    {
        return instance.Model.Bones.Any(bone =>
            bone.Flags.HasFlag(Modeling.BoneFlags.OutsideParent) &&
            string.Equals(bone.Name, boneName, StringComparison.Ordinal));
    }
}
