using Zhengyan.MikuMikuDance.Formats.Mme;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlEffectSourceCache
{
    private readonly string? _baseDirectory;
    private readonly Dictionary<string, OpenGlCachedEffectSource> _effects = new(StringComparer.OrdinalIgnoreCase);

    public OpenGlEffectSourceCache(string? baseDirectory = null)
    {
        _baseDirectory = TryGetFullPath(NormalizeSeparators(baseDirectory));
    }

    public bool TryGetEffect(string? effectPath, out RenderEffect effect)
    {
        return TryGetEffect(effectPath, null, out effect);
    }

    public bool TryGetEffect(string? effectPath, RenderEffect? ownerEffect, out RenderEffect effect)
    {
        effect = default!;
        var resolvedPath = ResolvePath(effectPath, ownerEffect);
        if (resolvedPath is null)
        {
            return false;
        }

        if (_effects.TryGetValue(resolvedPath, out var cached))
        {
            effect = cached.Effect ?? default!;
            return cached.Effect is not null;
        }

        var loaded = LoadEffect(resolvedPath);
        _effects[resolvedPath] = loaded;
        effect = loaded.Effect ?? default!;
        return loaded.Effect is not null;
    }

    public bool ReloadEffect(string? effectPath, out RenderEffect effect)
    {
        return ReloadEffect(effectPath, null, out effect);
    }

    public bool ReloadEffect(string? effectPath, RenderEffect? ownerEffect, out RenderEffect effect)
    {
        effect = default!;
        var resolvedPath = ResolvePath(effectPath, ownerEffect);
        if (resolvedPath is null)
        {
            return false;
        }

        var loaded = LoadEffect(resolvedPath);
        _effects[resolvedPath] = loaded;
        effect = loaded.Effect ?? default!;
        return loaded.Effect is not null;
    }

    public bool Invalidate(string? effectPath, RenderEffect? ownerEffect = null)
    {
        var resolvedPath = ResolvePath(effectPath, ownerEffect);
        return resolvedPath is not null && _effects.Remove(resolvedPath);
    }

    public void Clear()
    {
        _effects.Clear();
    }

    public IReadOnlyList<RenderEffectDiagnostic> GetDiagnostics(string? effectPath, RenderEffect? ownerEffect = null)
    {
        var resolvedPath = ResolvePath(effectPath, ownerEffect);
        if (resolvedPath is null)
        {
            return [
                RenderEffectDiagnostics.Error(
                    "MME1000",
                    "Effect path is empty or points to a hidden effect.")
            ];
        }

        return _effects.TryGetValue(resolvedPath, out var cached)
            ? cached.Diagnostics
            : [];
    }

    internal string? ResolvePathForTesting(string? effectPath, RenderEffect? ownerEffect = null)
    {
        return ResolvePath(effectPath, ownerEffect);
    }

    internal IReadOnlyList<string> CachedPathsForTesting => _effects.Keys.ToArray();

    private static OpenGlCachedEffectSource LoadEffect(string resolvedPath)
    {
        try
        {
            if (!File.Exists(resolvedPath))
            {
                return new OpenGlCachedEffectSource(
                    null,
                    [
                        RenderEffectDiagnostics.Error(
                            "MME1001",
                            $"Effect file was not found: {resolvedPath}",
                            resolvedPath)
                    ]);
            }

            using var stream = File.OpenRead(resolvedPath);
            var document = new MmeEffectReader().Read(stream, resolvedPath);
            var effect = RenderEffectCompiler.Compile(document);
            return new OpenGlCachedEffectSource(effect, RenderEffectDiagnostics.Analyze(effect));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new OpenGlCachedEffectSource(
                null,
                [
                    RenderEffectDiagnostics.Error(
                        "MME1002",
                        $"Failed to load effect: {exception.Message}",
                        resolvedPath)
                ]);
        }
    }

    private string? ResolvePath(string? effectPath, RenderEffect? ownerEffect)
    {
        var normalizedPath = NormalizeEffectPath(effectPath);
        if (normalizedPath is null)
        {
            return null;
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            return TryGetFullPath(normalizedPath);
        }

        var candidateDirectories = CandidateDirectories(ownerEffect).ToArray();
        foreach (var directory in candidateDirectories)
        {
            var candidate = TryGetFullPath(Path.Combine(directory, normalizedPath));
            if (candidate is not null && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var fallbackDirectory = candidateDirectories.FirstOrDefault() ?? Directory.GetCurrentDirectory();
        return TryGetFullPath(Path.Combine(fallbackDirectory, normalizedPath));
    }

    private IEnumerable<string> CandidateDirectories(RenderEffect? ownerEffect)
    {
        if (TryGetOwnerDirectory(ownerEffect?.SourceName) is { } ownerDirectory)
        {
            yield return ownerDirectory;
        }

        if (_baseDirectory is not null)
        {
            yield return _baseDirectory;
        }
    }

    private static string? TryGetOwnerDirectory(string? sourceName)
    {
        var normalizedSourceName = NormalizeSeparators(sourceName);
        if (string.IsNullOrWhiteSpace(normalizedSourceName))
        {
            return null;
        }

        var hasDirectoryHint =
            Path.IsPathRooted(normalizedSourceName) ||
            normalizedSourceName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            File.Exists(normalizedSourceName);
        if (!hasDirectoryHint)
        {
            return null;
        }

        var fullPath = TryGetFullPath(normalizedSourceName);
        return string.IsNullOrWhiteSpace(fullPath)
            ? null
            : Path.GetDirectoryName(fullPath);
    }

    private static string? NormalizeEffectPath(string? effectPath)
    {
        var normalized = NormalizeSeparators(effectPath)?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "hide", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static string? NormalizeSeparators(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

internal sealed record OpenGlCachedEffectSource(
    RenderEffect? Effect,
    IReadOnlyList<RenderEffectDiagnostic> Diagnostics);
