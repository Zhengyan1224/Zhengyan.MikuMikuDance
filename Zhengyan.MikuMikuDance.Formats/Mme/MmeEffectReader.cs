using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Zhengyan.MikuMikuDance.Core.Effects;

namespace Zhengyan.MikuMikuDance.Formats.Mme;

public sealed partial class MmeEffectReader
{
    public EffectDocument Read(ReadOnlyMemory<byte> data, string sourceName = "")
    {
        var text = DecodeText(data);
        return ReadText(text, sourceName);
    }

    public EffectDocument Read(Stream stream, string sourceName = "")
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray(), sourceName);
    }

    public EffectDocument ReadText(string text, string sourceName = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        var stripped = StripComments(text);
        var techniques = ParseTechniques(stripped);
        var parameterText = RemoveTechniqueBlocks(stripped);
        var parameters = ParseParameters(parameterText);
        return new EffectDocument(sourceName, text, parameters, techniques);
    }

    private static string DecodeText(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length >= 3 && span[0] == 0xef && span[1] == 0xbb && span[2] == 0xbf)
        {
            return Encoding.UTF8.GetString(span[3..]);
        }

        if (span.Length >= 2 && span[0] == 0xff && span[1] == 0xfe)
        {
            return Encoding.Unicode.GetString(span[2..]);
        }

        if (span.Length >= 2 && span[0] == 0xfe && span[1] == 0xff)
        {
            return Encoding.BigEndianUnicode.GetString(span[2..]);
        }

        return Encoding.UTF8.GetString(span);
    }

    private static IReadOnlyList<EffectParameter> ParseParameters(string text)
    {
        var parameters = new List<EffectParameter>();
        foreach (Match match in ParameterRegex().Matches(text))
        {
            if (IsInsideAngleBlock(text, match.Index))
            {
                continue;
            }

            var type = match.Groups["type"].Value.Trim();
            if (IsReservedTopLevelType(type))
            {
                continue;
            }

            var name = match.Groups["name"].Value.Trim();
            var annotations = ParseAnnotations(match.Groups["annotations"].Value);
            var semantic = match.Groups["semantic"].Success ? match.Groups["semantic"].Value.Trim() : null;
            var value = match.Groups["value"].Success ? NormalizeValue(match.Groups["value"].Value) : null;
            parameters.Add(new EffectParameter(type, name, annotations, semantic, value));
        }

        return parameters;
    }

    private static IReadOnlyList<EffectTechnique> ParseTechniques(string text)
    {
        var techniques = new List<EffectTechnique>();
        var index = 0;
        while (FindWord(text, "technique", index, out var start))
        {
            var cursor = start + "technique".Length;
            SkipWhitespace(text, ref cursor);
            var name = ReadIdentifier(text, ref cursor);
            if (string.IsNullOrEmpty(name))
            {
                index = cursor;
                continue;
            }

            var annotations = ReadAnnotationBlockIfPresent(text, ref cursor);
            SkipWhitespace(text, ref cursor);
            if (cursor >= text.Length || text[cursor] != '{')
            {
                index = cursor;
                continue;
            }

            var bodyStart = cursor;
            var bodyEnd = FindMatchingBrace(text, bodyStart);
            if (bodyEnd < 0)
            {
                break;
            }

            var body = text.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);
            techniques.Add(new EffectTechnique(name, annotations, ParsePasses(body)));
            index = bodyEnd + 1;
        }

        return techniques;
    }

    private static IReadOnlyList<EffectPass> ParsePasses(string text)
    {
        var passes = new List<EffectPass>();
        var index = 0;
        while (FindWord(text, "pass", index, out var start))
        {
            var cursor = start + "pass".Length;
            SkipWhitespace(text, ref cursor);
            var name = ReadIdentifier(text, ref cursor);
            if (string.IsNullOrEmpty(name))
            {
                name = $"Pass{passes.Count}";
            }

            var annotations = ReadAnnotationBlockIfPresent(text, ref cursor);
            SkipWhitespace(text, ref cursor);
            if (cursor >= text.Length || text[cursor] != '{')
            {
                index = cursor;
                continue;
            }

            var bodyStart = cursor;
            var bodyEnd = FindMatchingBrace(text, bodyStart);
            if (bodyEnd < 0)
            {
                break;
            }

            var body = text.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);
            passes.Add(new EffectPass(name, annotations, ParsePassStates(body)));
            index = bodyEnd + 1;
        }

        return passes;
    }

    private static IReadOnlyDictionary<string, string> ParsePassStates(string body)
    {
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in StateRegex().Matches(body))
        {
            states[match.Groups["name"].Value.Trim()] = NormalizeValue(match.Groups["value"].Value);
        }

        return states;
    }

    private static IReadOnlyList<EffectAnnotation> ParseAnnotations(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return [];
        }

        var text = block.Trim();
        if (text.StartsWith("<", StringComparison.Ordinal) && text.EndsWith(">", StringComparison.Ordinal))
        {
            text = text[1..^1];
        }

        var annotations = new List<EffectAnnotation>();
        foreach (var statement in SplitAnnotationStatements(text))
        {
            var match = AnnotationStatementRegex().Match(statement);
            if (!match.Success)
            {
                continue;
            }

            var type = match.Groups["type"].Value.Trim();
            var name = match.Groups["name"].Value.Trim();
            var value = ParseValue(match.Groups["value"].Value);
            annotations.Add(new EffectAnnotation(type, name, value));
        }

        return annotations;
    }

    private static IReadOnlyList<EffectAnnotation> ReadAnnotationBlockIfPresent(string text, ref int cursor)
    {
        SkipWhitespace(text, ref cursor);
        if (cursor >= text.Length || text[cursor] != '<')
        {
            return [];
        }

        var start = cursor;
        var end = FindMatchingAngle(text, start);
        if (end < 0)
        {
            return [];
        }

        cursor = end + 1;
        return ParseAnnotations(text.Substring(start, end - start + 1));
    }

    private static EffectValue ParseValue(string value)
    {
        value = NormalizeValue(value);
        if (TryParseStringExpression(value, out var stringValue))
        {
            return new EffectValue.String(stringValue);
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return new EffectValue.Bool(boolValue);
        }

        var vectorMatch = VectorRegex().Match(value);
        if (vectorMatch.Success)
        {
            var components = vectorMatch.Groups["values"].Value
                .Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseFloatOrZero)
                .ToArray();
            return new EffectValue.Vector(ToVector4(components), components.Length);
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return new EffectValue.Int(intValue);
        }

        if (float.TryParse(value.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return new EffectValue.Float(floatValue);
        }

        return new EffectValue.Raw(value);
    }

    private static Vector4 ToVector4(IReadOnlyList<float> components)
    {
        return new Vector4(
            components.Count > 0 ? components[0] : 0,
            components.Count > 1 ? components[1] : 0,
            components.Count > 2 ? components[2] : 0,
            components.Count > 3 ? components[3] : 0);
    }

    private static float ParseFloatOrZero(string value)
    {
        return float.TryParse(value.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static string RemoveTechniqueBlocks(string text)
    {
        var builder = new StringBuilder(text);
        var index = 0;
        while (FindWord(builder.ToString(), "technique", index, out var start))
        {
            var brace = builder.ToString().IndexOf('{', start);
            if (brace < 0)
            {
                break;
            }

            var end = FindMatchingBrace(builder.ToString(), brace);
            if (end < 0)
            {
                break;
            }

            for (var i = start; i <= end; i++)
            {
                builder[i] = ' ';
            }

            index = end + 1;
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SplitAnnotationStatements(string text)
    {
        var start = 0;
        var inString = false;
        var escaped = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == ';')
            {
                var statement = text[start..i].Trim();
                if (statement.Length > 0)
                {
                    yield return statement;
                }

                start = i + 1;
            }
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }

    private static bool TryParseStringExpression(string value, out string result)
    {
        var builder = new StringBuilder();
        var cursor = 0;
        while (cursor < value.Length)
        {
            SkipWhitespace(value, ref cursor);
            if (cursor >= value.Length)
            {
                result = builder.ToString();
                return builder.Length > 0;
            }

            if (value[cursor] != '"')
            {
                result = string.Empty;
                return false;
            }

            cursor++;
            while (cursor < value.Length)
            {
                var c = value[cursor++];
                if (c == '\\' && cursor < value.Length)
                {
                    builder.Append(value[cursor++]);
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    builder.Append(c);
                }
            }
        }

        result = builder.ToString();
        return builder.Length > 0;
    }

    private static bool IsInsideAngleBlock(string text, int offset)
    {
        var depth = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                depth++;
            }
            else if (text[i] == '>' && depth > 0)
            {
                depth--;
            }
        }

        return depth > 0;
    }

    private static string RemoveAnnotationBlocks(string text)
    {
        var builder = new StringBuilder(text);
        var index = 0;
        while (index < builder.Length)
        {
            if (builder[index] != '<')
            {
                index++;
                continue;
            }

            var end = FindMatchingAngle(builder.ToString(), index);
            if (end < 0)
            {
                break;
            }

            for (var i = index; i <= end; i++)
            {
                builder[i] = ' ';
            }

            index = end + 1;
        }

        return builder.ToString();
    }

    private static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                i += 2;
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(text.Length, i + 2);
            }
            else
            {
                builder.Append(text[i++]);
            }
        }

        return builder.ToString();
    }

    private static bool FindWord(string text, string word, int startIndex, out int index)
    {
        index = text.IndexOf(word, startIndex, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !IsIdentifierChar(text[index - 1]);
            var afterIndex = index + word.Length;
            var after = afterIndex >= text.Length || !IsIdentifierChar(text[afterIndex]);
            if (before && after)
            {
                return true;
            }

            index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal);
        }

        return false;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        return FindMatching(text, openIndex, '{', '}');
    }

    private static int FindMatchingAngle(string text, int openIndex)
    {
        return FindMatching(text, openIndex, '<', '>');
    }

    private static int FindMatching(string text, int openIndex, char open, char close)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static void SkipWhitespace(string text, ref int cursor)
    {
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }
    }

    private static string ReadIdentifier(string text, ref int cursor)
    {
        SkipWhitespace(text, ref cursor);
        var start = cursor;
        while (cursor < text.Length && IsIdentifierChar(text[cursor]))
        {
            cursor++;
        }

        return text[start..cursor];
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c is '_' or '.';
    }

    private static bool IsReservedTopLevelType(string type)
    {
        return type is "technique" or "pass" or "struct" or "sampler_state" or "SamplerState";
    }

    private static string NormalizeValue(string value)
    {
        return value.Trim().TrimEnd(';').Trim();
    }

    [GeneratedRegex(
        @"(?<type>(?:uniform\s+)?(?:static\s+)?[A-Za-z_][A-Za-z0-9_<>]*(?:\s+[A-Za-z_][A-Za-z0-9_<>]*)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<semantic>[A-Za-z_][A-Za-z0-9_]*))?\s*(?<annotations><[\s\S]*?>)?\s*(?:=\s*(?<value>[^;{}]+))?\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(
        @"\A\s*(?<type>[A-Za-z_][A-Za-z0-9_<>]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[\s\S]+?)\s*\z",
        RegexOptions.Compiled)]
    private static partial Regex AnnotationStatementRegex();

    [GeneratedRegex(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^;]+)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex StateRegex();

    [GeneratedRegex(@"^(?:(?:float|float[234]|int[234])?\s*\((?<values>[^)]*)\)|\{\s*(?<values>[^}]*)\s*\})$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VectorRegex();
}
