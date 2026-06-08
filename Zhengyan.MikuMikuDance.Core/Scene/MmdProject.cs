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

    public SceneBackground Background { get; } = new();

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
        instance.TransformOrder = _modelInstances.Count;
        _modelInstances.Add(instance);
        return instance;
    }

    public void AddAccessory(Accessory accessory)
    {
        ArgumentNullException.ThrowIfNull(accessory);
        _accessories.Add(accessory);
    }

    public bool MoveModel(int sourceIndex, int targetIndex)
    {
        if (!CanMove(sourceIndex, targetIndex, _modelInstances.Count))
        {
            return false;
        }

        Move(_modelInstances, sourceIndex, targetIndex);
        Move(_models, sourceIndex, targetIndex);
        NormalizeTransformOrder();
        return true;
    }

    public bool MoveModelTransformOrder(int sourceOrder, int targetOrder)
    {
        if (!CanMove(sourceOrder, targetOrder, _modelInstances.Count))
        {
            return false;
        }

        foreach (var instance in _modelInstances)
        {
            if (instance.TransformOrder == sourceOrder)
            {
                instance.TransformOrder = targetOrder;
            }
            else if (sourceOrder < targetOrder &&
                instance.TransformOrder > sourceOrder &&
                instance.TransformOrder <= targetOrder)
            {
                instance.TransformOrder--;
            }
            else if (sourceOrder > targetOrder &&
                instance.TransformOrder >= targetOrder &&
                instance.TransformOrder < sourceOrder)
            {
                instance.TransformOrder++;
            }
        }

        NormalizeTransformOrder();
        return true;
    }

    public IReadOnlyList<ModelInstance> GetModelsByTransformOrder()
    {
        return _modelInstances
            .OrderBy(instance => instance.TransformOrder)
            .ThenBy(instance => _modelInstances.IndexOf(instance))
            .ToArray();
    }

    public bool MoveAccessory(int sourceIndex, int targetIndex)
    {
        if (!CanMove(sourceIndex, targetIndex, _accessories.Count))
        {
            return false;
        }

        Move(_accessories, sourceIndex, targetIndex);
        return true;
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

    private static bool CanMove(int sourceIndex, int targetIndex, int count)
    {
        return sourceIndex >= 0
            && sourceIndex < count
            && targetIndex >= 0
            && targetIndex < count
            && sourceIndex != targetIndex;
    }

    private static void Move<T>(List<T> items, int sourceIndex, int targetIndex)
    {
        var item = items[sourceIndex];
        items.RemoveAt(sourceIndex);
        items.Insert(targetIndex, item);
    }

    private void NormalizeTransformOrder()
    {
        var ordered = GetModelsByTransformOrder();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].TransformOrder = i;
        }
    }
}
