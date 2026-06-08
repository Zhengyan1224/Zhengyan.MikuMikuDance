using System.Collections.ObjectModel;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class MmdProject
{
    private readonly List<MmdModel> _models = [];
    private readonly List<ModelInstance> _modelInstances = [];
    private readonly List<Accessory> _accessories = [];
    private readonly List<AccessoryMeshDocument> _accessoryMeshes = [];
    private readonly List<Motion> _motions = [];

    public string Name { get; set; } = "Untitled";

    public Timeline Timeline { get; } = new();

    public Camera Camera { get; } = new();

    public DirectionalLight Light { get; } = new();

    public IReadOnlyList<MmdModel> Models => new ReadOnlyCollection<MmdModel>(_models);

    public IReadOnlyList<ModelInstance> ModelInstances => new ReadOnlyCollection<ModelInstance>(_modelInstances);

    public IReadOnlyList<Accessory> Accessories => new ReadOnlyCollection<Accessory>(_accessories);

    public IReadOnlyList<AccessoryMeshDocument> AccessoryMeshes => new ReadOnlyCollection<AccessoryMeshDocument>(_accessoryMeshes);

    public IReadOnlyList<Motion> Motions => new ReadOnlyCollection<Motion>(_motions);

    public int DurationFrames => _motions.Count == 0 ? 0 : _motions.Max(motion => motion.MaxFrameIndex);

    public ModelInstance AddModel(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _models.Add(model);
        var instance = new ModelInstance(model);
        _modelInstances.Add(instance);
        return instance;
    }

    public void AddAccessory(Accessory accessory)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        _accessories.Add(accessory);
    }

    public void AddAccessoryMesh(AccessoryMeshDocument accessoryMesh)
    {
        ArgumentNullException.ThrowIfNull(accessoryMesh);
        _accessoryMeshes.Add(accessoryMesh);
    }

    public void AddMotion(Motion motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        _motions.Add(motion);
    }
}
