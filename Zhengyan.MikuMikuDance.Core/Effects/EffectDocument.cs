using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Effects;

public sealed record EffectDocument(
    string SourceName,
    string SourceText,
    IReadOnlyList<EffectParameter> Parameters,
    IReadOnlyList<EffectTechnique> Techniques)
{
    public int PassCount => Techniques.Sum(technique => technique.Passes.Count);
}

public sealed record EffectParameter(
    string Type,
    string Name,
    IReadOnlyList<EffectAnnotation> Annotations,
    string? Semantic = null,
    string? DefaultValue = null);

public sealed record EffectAnnotation(string Type, string Name, EffectValue Value);

public sealed record EffectTechnique(
    string Name,
    IReadOnlyList<EffectAnnotation> Annotations,
    IReadOnlyList<EffectPass> Passes);

public sealed record EffectPass(
    string Name,
    IReadOnlyList<EffectAnnotation> Annotations,
    IReadOnlyDictionary<string, string> States);

public abstract record EffectValue
{
    public sealed record Bool(bool Value) : EffectValue;

    public sealed record Int(int Value) : EffectValue;

    public sealed record Float(float Value) : EffectValue;

    public sealed record String(string Value) : EffectValue;

    public sealed record Vector(Vector4 Value, int ComponentCount) : EffectValue;

    public sealed record Raw(string Value) : EffectValue;
}
