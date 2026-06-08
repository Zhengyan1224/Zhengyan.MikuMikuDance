namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed record SceneParentBinding(string? ModelName, string? BoneName);

public static class SceneParentBindingResolver
{
    public static bool TryResolve(MmdProject project, string? modelName, string? boneName, out SceneParentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            binding = new SceneParentBinding(null, null);
            return true;
        }

        var model = project.ModelInstances.FirstOrDefault(instance =>
            string.Equals(instance.Name, modelName, StringComparison.Ordinal));
        if (model is null)
        {
            binding = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(boneName))
        {
            binding = new SceneParentBinding(model.Name, null);
            return true;
        }

        var bone = model.Model.Bones.FirstOrDefault(item =>
            string.Equals(item.Name, boneName, StringComparison.Ordinal));
        if (bone is null)
        {
            binding = default!;
            return false;
        }

        binding = new SceneParentBinding(model.Name, bone.Name);
        return true;
    }
}
