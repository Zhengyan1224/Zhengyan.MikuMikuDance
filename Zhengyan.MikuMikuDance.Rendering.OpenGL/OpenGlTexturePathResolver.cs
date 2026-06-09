namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal static class OpenGlTexturePathResolver
{
    public static string? Resolve(string? texturePath, string? baseDirectory)
    {
        var trimmed = texturePath?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (Path.IsPathRooted(trimmed))
        {
            return TryGetFullPath(NormalizeSeparators(trimmed));
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            return absolute.IsFile ? TryGetFullPath(absolute.LocalPath) : null;
        }

        var normalizedPath = NormalizeSeparators(trimmed);
        var normalizedBaseDirectory = NormalizeSeparators(baseDirectory);
        return string.IsNullOrWhiteSpace(normalizedBaseDirectory)
            ? TryGetFullPath(normalizedPath)
            : TryGetFullPath(Path.Combine(normalizedBaseDirectory, normalizedPath));
    }

    private static string NormalizeSeparators(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
    }
}
