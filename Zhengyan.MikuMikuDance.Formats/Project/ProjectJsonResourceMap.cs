using System.Runtime.CompilerServices;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class ProjectJsonResourceMap
{
    private readonly Dictionary<MmdModel, string> _models = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Accessory, string> _accessories = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AccessoryMeshDocument, string> _accessoryMeshes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Motion, string> _motions = new(ReferenceEqualityComparer.Instance);

    public void SetModelPath(MmdModel model, string path)
    {
        ArgumentNullException.ThrowIfNull(model);
        _models[model] = Normalize(path);
    }

    public void SetAccessoryPath(Accessory accessory, string path)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        _accessories[accessory] = Normalize(path);
    }

    public void SetAccessoryMeshPath(AccessoryMeshDocument mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _accessoryMeshes[mesh] = Normalize(path);
    }

    public void SetMotionPath(Motion motion, string path)
    {
        ArgumentNullException.ThrowIfNull(motion);
        _motions[motion] = Normalize(path);
    }

    public string? GetModelPath(MmdModel model) => _models.GetValueOrDefault(model);

    public string? GetAccessoryPath(Accessory accessory) => _accessories.GetValueOrDefault(accessory);

    public string? GetAccessoryMeshPath(AccessoryMeshDocument mesh) => _accessoryMeshes.GetValueOrDefault(mesh);

    public string? GetMotionPath(Motion motion) => _motions.GetValueOrDefault(motion);

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }
}
