using System.Collections.ObjectModel;
using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class EffectParameterOverrideSet
{
    private readonly Dictionary<string, MotionEffectParameterValue> _values = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MotionEffectParameterValue> Values =>
        new ReadOnlyDictionary<string, MotionEffectParameterValue>(_values);

    public int Count => _values.Count;

    public bool TryGetValue(string name, out MotionEffectParameterValue value)
    {
        return _values.TryGetValue(name, out value!);
    }

    public void SetBool(string name, bool value)
    {
        Set(name, new MotionEffectParameterValue.Bool(value));
    }

    public void SetInt(string name, int value)
    {
        Set(name, new MotionEffectParameterValue.Int(value));
    }

    public void SetFloat(string name, float value)
    {
        Set(name, new MotionEffectParameterValue.Float(value));
    }

    public void SetVector4(string name, Vector4 value)
    {
        Set(name, new MotionEffectParameterValue.Vector4(value));
    }

    public void Set(string name, MotionEffectParameterValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _values[name] = value;
    }

    public bool Remove(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && _values.Remove(name);
    }

    public void Clear()
    {
        _values.Clear();
    }
}
