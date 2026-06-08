using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public sealed record MorphApplication(
    IReadOnlyList<Vector3> VertexOffsets,
    IReadOnlyList<Vector4> UvOffsets,
    IReadOnlyDictionary<string, BonePoseSample> BoneOffsets)
{
    public static MorphApplication Empty(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new MorphApplication(
            Enumerable.Repeat(Vector3.Zero, model.Vertices.Count).ToArray(),
            Enumerable.Repeat(Vector4.Zero, model.Vertices.Count).ToArray(),
            new Dictionary<string, BonePoseSample>(0, StringComparer.Ordinal));
    }
}

public static class MorphEvaluator
{
    public static MorphApplication Evaluate(MmdModel model, IReadOnlyDictionary<string, MorphWeightSample> morphWeights)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(morphWeights);
        var weights = ResolveWeights(model, morphWeights);
        return ApplyResolvedWeights(model, weights);
    }

    public static IReadOnlyDictionary<int, float> ResolveWeights(
        MmdModel model,
        IReadOnlyDictionary<string, MorphWeightSample> morphWeights)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(morphWeights);
        var resolved = new Dictionary<int, float>();
        for (var i = 0; i < model.Morphs.Count; i++)
        {
            var morph = model.Morphs[i];
            if (morphWeights.TryGetValue(morph.Name, out var sample) && sample.Weight != 0)
            {
                AccumulateMorphWeight(model, i, sample.Weight, resolved, []);
            }
        }

        return resolved;
    }

    private static MorphApplication ApplyResolvedWeights(MmdModel model, IReadOnlyDictionary<int, float> weights)
    {
        var vertexOffsets = new Vector3[model.Vertices.Count];
        var uvOffsets = new Vector4[model.Vertices.Count];
        var boneOffsets = new Dictionary<string, BonePoseAccumulator>(StringComparer.Ordinal);
        foreach (var (morphIndex, weight) in weights)
        {
            if (morphIndex < 0 || morphIndex >= model.Morphs.Count || weight == 0)
            {
                continue;
            }

            var morph = model.Morphs[morphIndex];
            foreach (var offset in morph.Offsets)
            {
                ApplyOffset(model, offset, weight, vertexOffsets, uvOffsets, boneOffsets);
            }
        }

        return new MorphApplication(
            vertexOffsets,
            uvOffsets,
            boneOffsets.ToDictionary(
                item => item.Key,
                item => item.Value.ToSample(),
                StringComparer.Ordinal));
    }

    private static void AccumulateMorphWeight(
        MmdModel model,
        int morphIndex,
        float weight,
        Dictionary<int, float> resolved,
        HashSet<int> visiting)
    {
        if (morphIndex < 0 || morphIndex >= model.Morphs.Count || weight == 0 || !visiting.Add(morphIndex))
        {
            return;
        }

        var morph = model.Morphs[morphIndex];
        if (morph.Type is MorphType.Group or MorphType.Flip)
        {
            foreach (var offset in morph.Offsets)
            {
                switch (offset)
                {
                    case GroupMorphOffset group:
                        AccumulateMorphWeight(model, group.MorphIndex, weight * group.Weight, resolved, visiting);
                        break;
                    case FlipMorphOffset flip:
                        AccumulateMorphWeight(model, flip.MorphIndex, weight * flip.Weight, resolved, visiting);
                        break;
                }
            }
        }
        else
        {
            resolved[morphIndex] = resolved.GetValueOrDefault(morphIndex) + weight;
        }

        visiting.Remove(morphIndex);
    }

    private static void ApplyOffset(
        MmdModel model,
        MorphOffset offset,
        float weight,
        Vector3[] vertexOffsets,
        Vector4[] uvOffsets,
        Dictionary<string, BonePoseAccumulator> boneOffsets)
    {
        switch (offset)
        {
            case VertexMorphOffset vertex when IsValidIndex(vertex.VertexIndex, vertexOffsets.Length):
                vertexOffsets[vertex.VertexIndex] += vertex.Translation * weight;
                break;
            case UvMorphOffset uv when IsValidIndex(uv.VertexIndex, uvOffsets.Length):
                uvOffsets[uv.VertexIndex] += uv.Offset * weight;
                break;
            case BoneMorphOffset bone when IsValidIndex(bone.BoneIndex, model.Bones.Count):
                var boneName = model.Bones[bone.BoneIndex].Name;
                if (!boneOffsets.TryGetValue(boneName, out var accumulator))
                {
                    accumulator = new BonePoseAccumulator();
                    boneOffsets[boneName] = accumulator;
                }

                accumulator.Add(bone.Translation, bone.Orientation, weight);
                break;
        }
    }

    private static bool IsValidIndex(int index, int count)
    {
        return index >= 0 && index < count;
    }

    private sealed class BonePoseAccumulator
    {
        private Vector3 _translation;
        private Quaternion _orientation = Quaternion.Identity;
        private bool _hasOrientation;

        public void Add(Vector3 translation, Quaternion orientation, float weight)
        {
            _translation += translation * weight;
            if (weight == 0)
            {
                return;
            }

            var weighted = Quaternion.Slerp(Quaternion.Identity, orientation, Math.Clamp(weight, 0f, 1f));
            _orientation = _hasOrientation ? Quaternion.Normalize(weighted * _orientation) : weighted;
            _hasOrientation = true;
        }

        public BonePoseSample ToSample()
        {
            return new BonePoseSample(_translation, Quaternion.Normalize(_orientation), true);
        }
    }
}
