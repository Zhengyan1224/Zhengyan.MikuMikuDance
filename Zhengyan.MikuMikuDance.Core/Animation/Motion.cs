using System.Collections.ObjectModel;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public enum MotionFormat
{
    Unknown,
    Vmd,
    Nmd
}

public sealed class Motion
{
    private readonly List<BoneKeyframe> _boneKeyframes = [];
    private readonly List<MorphKeyframe> _morphKeyframes = [];
    private readonly List<CameraKeyframe> _cameraKeyframes = [];
    private readonly List<LightKeyframe> _lightKeyframes = [];
    private readonly List<SelfShadowKeyframe> _selfShadowKeyframes = [];
    private readonly List<ModelKeyframe> _modelKeyframes = [];
    private readonly List<AccessoryKeyframe> _accessoryKeyframes = [];

    public Motion(string name, MotionFormat format)
    {
        Name = name;
        Format = format;
    }

    public string Name { get; set; }

    public MotionFormat Format { get; set; }

    public Uri? Source { get; set; }

    public Dictionary<string, string> Annotations { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<BoneKeyframe> BoneKeyframes => new ReadOnlyCollection<BoneKeyframe>(_boneKeyframes);

    public IReadOnlyList<MorphKeyframe> MorphKeyframes => new ReadOnlyCollection<MorphKeyframe>(_morphKeyframes);

    public IReadOnlyList<CameraKeyframe> CameraKeyframes => new ReadOnlyCollection<CameraKeyframe>(_cameraKeyframes);

    public IReadOnlyList<LightKeyframe> LightKeyframes => new ReadOnlyCollection<LightKeyframe>(_lightKeyframes);

    public IReadOnlyList<SelfShadowKeyframe> SelfShadowKeyframes => new ReadOnlyCollection<SelfShadowKeyframe>(_selfShadowKeyframes);

    public IReadOnlyList<ModelKeyframe> ModelKeyframes => new ReadOnlyCollection<ModelKeyframe>(_modelKeyframes);

    public IReadOnlyList<AccessoryKeyframe> AccessoryKeyframes => new ReadOnlyCollection<AccessoryKeyframe>(_accessoryKeyframes);

    public int MaxFrameIndex
    {
        get
        {
            var max = 0;
            foreach (var frame in EnumerateAllKeyframes())
            {
                max = Math.Max(max, frame.FrameIndex);
            }

            return max;
        }
    }

    public void Add(BoneKeyframe keyframe) => InsertOrReplace(_boneKeyframes, keyframe, f => (f.BoneName, f.FrameIndex));

    public void Add(MorphKeyframe keyframe) => InsertOrReplace(_morphKeyframes, keyframe, f => (f.MorphName, f.FrameIndex));

    public void Add(CameraKeyframe keyframe) => InsertOrReplace(_cameraKeyframes, keyframe, f => f.FrameIndex);

    public void Add(LightKeyframe keyframe) => InsertOrReplace(_lightKeyframes, keyframe, f => f.FrameIndex);

    public void Add(SelfShadowKeyframe keyframe) => InsertOrReplace(_selfShadowKeyframes, keyframe, f => f.FrameIndex);

    public void Add(ModelKeyframe keyframe) => InsertOrReplace(_modelKeyframes, keyframe, f => f.FrameIndex);

    public void Add(AccessoryKeyframe keyframe) => InsertOrReplace(_accessoryKeyframes, keyframe, f => (f.AccessoryName, f.FrameIndex));

    public int RemoveKeyframes(Predicate<MotionKeyframe> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var count = 0;
        count += RemoveMatching(_boneKeyframes, predicate);
        count += RemoveMatching(_morphKeyframes, predicate);
        count += RemoveMatching(_cameraKeyframes, predicate);
        count += RemoveMatching(_lightKeyframes, predicate);
        count += RemoveMatching(_selfShadowKeyframes, predicate);
        count += RemoveMatching(_modelKeyframes, predicate);
        count += RemoveMatching(_accessoryKeyframes, predicate);
        return count;
    }

    public void ReplaceAllKeyframes(IEnumerable<MotionKeyframe> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        _boneKeyframes.Clear();
        _morphKeyframes.Clear();
        _cameraKeyframes.Clear();
        _lightKeyframes.Clear();
        _selfShadowKeyframes.Clear();
        _modelKeyframes.Clear();
        _accessoryKeyframes.Clear();
        foreach (var keyframe in keyframes)
        {
            AddKeyframe(keyframe);
        }
    }

    public IEnumerable<MotionKeyframe> EnumerateAllKeyframes()
    {
        return _boneKeyframes
            .Cast<MotionKeyframe>()
            .Concat(_morphKeyframes)
            .Concat(_cameraKeyframes)
            .Concat(_lightKeyframes)
            .Concat(_selfShadowKeyframes)
            .Concat(_modelKeyframes)
            .Concat(_accessoryKeyframes);
    }

    public void AddKeyframe(MotionKeyframe keyframe)
    {
        switch (keyframe)
        {
            case BoneKeyframe bone:
                Add(bone);
                break;
            case MorphKeyframe morph:
                Add(morph);
                break;
            case CameraKeyframe camera:
                Add(camera);
                break;
            case LightKeyframe light:
                Add(light);
                break;
            case SelfShadowKeyframe selfShadow:
                Add(selfShadow);
                break;
            case ModelKeyframe model:
                Add(model);
                break;
            case AccessoryKeyframe accessory:
                Add(accessory);
                break;
            default:
                throw new ArgumentException($"Unsupported keyframe type {keyframe.GetType().FullName}.", nameof(keyframe));
        }
    }

    private static void InsertOrReplace<T, TKey>(List<T> keyframes, T keyframe, Func<T, TKey> keySelector)
        where TKey : notnull
    {
        var key = keySelector(keyframe);
        var existingIndex = keyframes.FindIndex(item => EqualityComparer<TKey>.Default.Equals(keySelector(item), key));
        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = keyframe;
        }
        else
        {
            keyframes.Add(keyframe);
        }
    }

    private static int RemoveMatching<T>(List<T> keyframes, Predicate<MotionKeyframe> predicate)
        where T : MotionKeyframe
    {
        return keyframes.RemoveAll(frame => predicate(frame));
    }
}
