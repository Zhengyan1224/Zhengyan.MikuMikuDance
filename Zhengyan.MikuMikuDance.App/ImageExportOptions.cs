using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Zhengyan.MikuMikuDance.Rendering.OpenGL;

namespace Zhengyan.MikuMikuDance.App;

internal sealed record ImageExportOptions(
    string InputPath,
    string OutputPath,
    int Width,
    int Height,
    Vector4 ClearColor,
    bool TransparentFramebuffer)
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        TextWriter error,
        [NotNullWhen(true)] out ImageExportOptions? options)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);
        options = null;

        if (args.Count < 3)
        {
            error.WriteLine("Usage: --export-image <input> <out.png> [width] [height] [--transparent] [--clear-color #RRGGBB[AA]]");
            return false;
        }

        var width = OpenGlRenderHostOptions.Default.Width;
        var height = OpenGlRenderHostOptions.Default.Height;
        var clearColor = OpenGlRenderHostOptions.Default.ClearColor;
        var clearColorSpecified = false;
        var transparent = false;
        var dimensions = new List<string>(2);

        for (var i = 3; i < args.Count; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, "--transparent", StringComparison.OrdinalIgnoreCase))
            {
                transparent = true;
                continue;
            }

            if (string.Equals(argument, "--clear-color", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count)
                {
                    error.WriteLine("Missing value for --clear-color.");
                    return false;
                }

                if (!TryParseClearColor(args[++i], error, out clearColor))
                {
                    return false;
                }

                clearColorSpecified = true;
                continue;
            }

            const string clearColorPrefix = "--clear-color=";
            if (argument.StartsWith(clearColorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseClearColor(argument[clearColorPrefix.Length..], error, out clearColor))
                {
                    return false;
                }

                clearColorSpecified = true;
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"Unknown image export option: {argument}");
                return false;
            }

            dimensions.Add(argument);
        }

        if (dimensions.Count > 2)
        {
            error.WriteLine($"Unexpected image export argument: {dimensions[2]}");
            return false;
        }

        if (dimensions.Count >= 1 && !TryReadPositiveInt(dimensions[0], "width", error, out width))
        {
            return false;
        }

        if (dimensions.Count >= 2 && !TryReadPositiveInt(dimensions[1], "height", error, out height))
        {
            return false;
        }

        if (transparent)
        {
            clearColor = clearColorSpecified
                ? new Vector4(clearColor.X, clearColor.Y, clearColor.Z, 0f)
                : Vector4.Zero;
        }

        options = new ImageExportOptions(
            args[1],
            args[2],
            width,
            height,
            clearColor,
            transparent || clearColor.W < 0.999f);
        return true;
    }

    private static bool TryReadPositiveInt(string text, string name, TextWriter error, out int value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0)
        {
            return true;
        }

        error.WriteLine($"Invalid {name}: {text}");
        return false;
    }

    private static bool TryParseClearColor(string text, TextWriter error, out Vector4 color)
    {
        color = default;
        var hex = text.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = hex[1..];
        }

        if (hex.Length is not (6 or 8) ||
            !TryParseByte(hex, 0, out var r) ||
            !TryParseByte(hex, 2, out var g) ||
            !TryParseByte(hex, 4, out var b))
        {
            error.WriteLine($"Invalid clear color: {text}");
            return false;
        }

        byte a;
        if (hex.Length == 8)
        {
            if (!TryParseByte(hex, 6, out a))
            {
                error.WriteLine($"Invalid clear color: {text}");
                return false;
            }
        }
        else
        {
            a = 255;
        }

        color = new Vector4(
            r / 255f,
            g / 255f,
            b / 255f,
            a / 255f);
        return true;
    }

    private static bool TryParseByte(string hex, int startIndex, out byte value)
    {
        return byte.TryParse(
            hex.AsSpan(startIndex, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }
}
