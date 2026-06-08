namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderEffectOffscreenDrawPlan(
    RenderEffectOffscreenTarget Target,
    IReadOnlyList<RenderEffectOffscreenDrawableDecision> Decisions);

public sealed record RenderEffectOffscreenDrawableDecision(
    string DrawableName,
    RenderEffectOffscreenAction Action,
    string? EffectPath = null);

public enum RenderEffectOffscreenAction
{
    DrawWithOwnerEffect,
    DrawWithExternalEffect,
    Hide
}

public static class RenderEffectOffscreenScheduler
{
    public static RenderEffectOffscreenDrawPlan CreatePlan(
        RenderEffectOffscreenTarget target,
        string ownerDrawableName,
        IEnumerable<string> drawableNames)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(drawableNames);

        var decisions = drawableNames
            .Select(name => CreateDecision(target, ownerDrawableName, name))
            .ToArray();
        return new RenderEffectOffscreenDrawPlan(target, decisions);
    }

    public static RenderEffectOffscreenDrawableDecision CreateDecision(
        RenderEffectOffscreenTarget target,
        string ownerDrawableName,
        string drawableName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(drawableName);

        foreach (var condition in target.DefaultEffects)
        {
            if (!Matches(condition.Target, ownerDrawableName, drawableName))
            {
                continue;
            }

            return ToDecision(drawableName, condition.EffectPath);
        }

        return new RenderEffectOffscreenDrawableDecision(drawableName, RenderEffectOffscreenAction.DrawWithOwnerEffect);
    }

    private static bool Matches(string target, string ownerDrawableName, string drawableName)
    {
        if (string.Equals(target, "*", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(target, "self", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(ownerDrawableName, drawableName, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(target, drawableName, StringComparison.OrdinalIgnoreCase);
    }

    private static RenderEffectOffscreenDrawableDecision ToDecision(string drawableName, string effectPath)
    {
        return string.Equals(effectPath, "hide", StringComparison.OrdinalIgnoreCase)
            ? new RenderEffectOffscreenDrawableDecision(drawableName, RenderEffectOffscreenAction.Hide)
            : new RenderEffectOffscreenDrawableDecision(drawableName, RenderEffectOffscreenAction.DrawWithExternalEffect, effectPath);
    }
}
