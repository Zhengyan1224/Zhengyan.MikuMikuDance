using System.Numerics;

namespace Zhengyan.MikuMikuDance.Core.Animation;

public readonly record struct MotionEditFilter(FrameRange? Range = null, bool SelectedOnly = false)
{
    public bool Matches(MotionKeyframe keyframe)
    {
        if (SelectedOnly && !keyframe.IsSelected)
        {
            return false;
        }

        return Range is not { } range || range.Normalize().Contains(keyframe.FrameIndex);
    }
}

public sealed record MotionKeyframeClipboard(IReadOnlyList<MotionKeyframe> Keyframes, int SourceStartFrameIndex)
{
    public static MotionKeyframeClipboard Empty { get; } = new([], 0);

    public bool IsEmpty => Keyframes.Count == 0;
}

public static class MotionEditor
{
    public static int MoveKeyframes(Motion motion, int frameDelta, MotionEditFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (frameDelta == 0)
        {
            return 0;
        }

        return RewriteMatching(motion, filter, keyframe => WithFrameIndex(keyframe, keyframe.FrameIndex + frameDelta));
    }

    public static int ScaleKeyframes(
        Motion motion,
        float scale,
        int pivotFrameIndex = 0,
        MotionEditFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (!float.IsFinite(scale) || scale < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Keyframe scale must be a finite non-negative value.");
        }

        return RewriteMatching(motion, filter, keyframe =>
        {
            var scaled = pivotFrameIndex + (keyframe.FrameIndex - pivotFrameIndex) * scale;
            return WithFrameIndex(keyframe, (int)MathF.Round(scaled, MidpointRounding.AwayFromZero));
        });
    }

    public static int CopyKeyframes(
        Motion motion,
        int targetStartFrameIndex,
        MotionEditFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        return PasteKeyframes(motion, CopyKeyframesToClipboard(motion, filter), targetStartFrameIndex);
    }

    public static MotionKeyframeClipboard CopyKeyframesToClipboard(Motion motion, MotionEditFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        var keyframes = motion
            .EnumerateAllKeyframes()
            .Where(filter.Matches)
            .OrderBy(frame => frame.FrameIndex)
            .ThenBy(frame => frame.GetType().Name, StringComparer.Ordinal)
            .ToArray();
        return keyframes.Length == 0
            ? MotionKeyframeClipboard.Empty
            : new MotionKeyframeClipboard(keyframes, keyframes.Min(frame => frame.FrameIndex));
    }

    public static int PasteKeyframes(
        Motion motion,
        MotionKeyframeClipboard clipboard,
        int targetStartFrameIndex,
        bool mirrored = false)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (clipboard.IsEmpty)
        {
            return 0;
        }

        foreach (var keyframe in clipboard.Keyframes)
        {
            var pasted = WithFrameIndex(keyframe, targetStartFrameIndex + keyframe.FrameIndex - clipboard.SourceStartFrameIndex);
            motion.AddKeyframe(mirrored ? MirrorKeyframe(pasted) : pasted);
        }

        return clipboard.Keyframes.Count;
    }

    public static int DeleteKeyframes(Motion motion, MotionEditFilter filter = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        return motion.RemoveKeyframes(filter.Matches);
    }

    public static int InsertTimelineFrames(Motion motion, int frameIndex, int frameCount = 1)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (frameCount <= 0)
        {
            return 0;
        }

        return RewriteMatching(
            motion,
            new MotionEditFilter(new FrameRange(Math.Max(0, frameIndex), int.MaxValue)),
            keyframe => WithFrameIndex(keyframe, keyframe.FrameIndex + frameCount));
    }

    public static int DeleteTimelineFrames(Motion motion, int frameIndex, int frameCount = 1)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (frameCount <= 0)
        {
            return 0;
        }

        frameIndex = Math.Max(0, frameIndex);
        var deleteEndExclusive = frameIndex + frameCount;
        var rewritten = new List<MotionKeyframe>();
        var affected = 0;
        foreach (var keyframe in motion.EnumerateAllKeyframes())
        {
            if (keyframe.FrameIndex >= frameIndex && keyframe.FrameIndex < deleteEndExclusive)
            {
                affected++;
                continue;
            }

            if (keyframe.FrameIndex >= deleteEndExclusive)
            {
                affected++;
                rewritten.Add(WithFrameIndex(keyframe, keyframe.FrameIndex - frameCount));
            }
            else
            {
                rewritten.Add(keyframe);
            }
        }

        if (affected > 0)
        {
            motion.ReplaceAllKeyframes(rewritten);
        }

        return affected;
    }

    private static int RewriteMatching(
        Motion motion,
        MotionEditFilter filter,
        Func<MotionKeyframe, MotionKeyframe> rewrite)
    {
        var affected = 0;
        var rewritten = motion.EnumerateAllKeyframes()
            .Select(keyframe =>
            {
                if (!filter.Matches(keyframe))
                {
                    return keyframe;
                }

                affected++;
                return rewrite(keyframe);
            })
            .ToArray();

        if (affected > 0)
        {
            motion.ReplaceAllKeyframes(rewritten);
        }

        return affected;
    }

    private static MotionKeyframe WithFrameIndex(MotionKeyframe keyframe, int frameIndex)
    {
        var targetFrameIndex = Math.Max(0, frameIndex);
        return keyframe switch
        {
            BoneKeyframe value => value with { FrameIndex = targetFrameIndex },
            MorphKeyframe value => value with { FrameIndex = targetFrameIndex },
            CameraKeyframe value => value with { FrameIndex = targetFrameIndex },
            LightKeyframe value => value with { FrameIndex = targetFrameIndex },
            SelfShadowKeyframe value => value with { FrameIndex = targetFrameIndex },
            ModelKeyframe value => value with { FrameIndex = targetFrameIndex },
            AccessoryKeyframe value => value with { FrameIndex = targetFrameIndex },
            _ => throw new ArgumentException($"Unsupported keyframe type {keyframe.GetType().FullName}.", nameof(keyframe))
        };
    }

    private static MotionKeyframe MirrorKeyframe(MotionKeyframe keyframe)
    {
        return keyframe switch
        {
            BoneKeyframe value => value with
            {
                BoneName = MirrorTrackName(value.BoneName),
                Translation = MirrorTranslation(value.Translation),
                Orientation = MirrorOrientation(value.Orientation)
            },
            AccessoryKeyframe value => value with
            {
                Translation = MirrorTranslation(value.Translation),
                Orientation = MirrorEuler(value.Orientation)
            },
            _ => keyframe
        };
    }

    private static string MirrorTrackName(string name)
    {
        if (name.StartsWith("左", StringComparison.Ordinal))
        {
            return $"右{name[1..]}";
        }

        if (name.StartsWith("右", StringComparison.Ordinal))
        {
            return $"左{name[1..]}";
        }

        foreach (var (left, right) in new[] { ("Left", "Right"), ("left", "right"), ("L_", "R_"), ("l_", "r_") })
        {
            if (name.StartsWith(left, StringComparison.Ordinal))
            {
                return $"{right}{name[left.Length..]}";
            }

            if (name.StartsWith(right, StringComparison.Ordinal))
            {
                return $"{left}{name[right.Length..]}";
            }
        }

        return name;
    }

    private static Vector3 MirrorTranslation(Vector3 translation)
    {
        return new Vector3(-translation.X, translation.Y, translation.Z);
    }

    private static Quaternion MirrorOrientation(Quaternion orientation)
    {
        return Quaternion.Normalize(new Quaternion(orientation.X, -orientation.Y, -orientation.Z, orientation.W));
    }

    private static Vector3 MirrorEuler(Vector3 euler)
    {
        return new Vector3(euler.X, -euler.Y, -euler.Z);
    }
}
