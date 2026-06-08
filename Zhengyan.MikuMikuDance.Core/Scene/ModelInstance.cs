using System.Collections.ObjectModel;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class ModelInstance
{
    private readonly Dictionary<string, MorphWeightSample> _morphWeights = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModelOutsideParentBinding> _outsideParentBindings = new(StringComparer.Ordinal);

    public ModelInstance(MmdModel model, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        Name = string.IsNullOrWhiteSpace(name) ? model.Name : name;
    }

    public string Name { get; set; }

    public MmdModel Model { get; }

    public bool Visible { get; set; } = true;

    public int TransformOrder { get; set; }

    public SceneTransform Transform { get; } = new();

    public IReadOnlyDictionary<string, MorphWeightSample> MorphWeights => new ReadOnlyDictionary<string, MorphWeightSample>(_morphWeights);

    public IReadOnlyDictionary<string, ModelOutsideParentBinding> OutsideParentBindings =>
        new ReadOnlyDictionary<string, ModelOutsideParentBinding>(_outsideParentBindings);

    public float GetMorphWeight(string morphName)
    {
        return _morphWeights.TryGetValue(morphName, out var sample) ? sample.Weight : 0f;
    }

    public void SetMorphWeight(string morphName, float weight)
    {
        if (string.IsNullOrWhiteSpace(morphName))
        {
            return;
        }

        weight = Math.Clamp(weight, 0f, 1f);
        if (weight == 0)
        {
            _morphWeights.Remove(morphName);
            return;
        }

        _morphWeights[morphName] = new MorphWeightSample(weight);
    }

    public void ClearMorphWeights()
    {
        _morphWeights.Clear();
    }

    public ModelOutsideParentBinding? GetOutsideParentBinding(string boneName)
    {
        return _outsideParentBindings.TryGetValue(boneName, out var binding) ? binding : null;
    }

    public void SetOutsideParentBinding(ModelOutsideParentBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.BoneName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(binding.ParentModelName))
        {
            _outsideParentBindings.Remove(binding.BoneName);
            return;
        }

        _outsideParentBindings[binding.BoneName] = binding;
    }

    public void ClearOutsideParentBindings()
    {
        _outsideParentBindings.Clear();
    }
}
