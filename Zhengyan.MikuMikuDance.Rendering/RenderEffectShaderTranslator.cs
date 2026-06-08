using System.Text;
using System.Text.RegularExpressions;

namespace Zhengyan.MikuMikuDance.Rendering;

public static partial class RenderEffectShaderTranslator
{
    public static RenderEffectShaderProgram? TryTranslate(
        string sourceText,
        string? vertexShaderState,
        string? pixelShaderState,
        IReadOnlyList<RenderEffectParameter>? effectParameters = null)
    {
        if (!TryParseCompileState(vertexShaderState, RenderShaderStage.Vertex, out var vertexCompile) ||
            !TryParseCompileState(pixelShaderState, RenderShaderStage.Fragment, out var pixelCompile))
        {
            return null;
        }

        var structs = ParseStructs(sourceText);
        if (!TryParseFunction(sourceText, vertexCompile.EntryPoint, out var vertexFunction) ||
            !TryParseFunction(sourceText, pixelCompile.EntryPoint, out var pixelFunction))
        {
            return null;
        }

        var uniforms = ParseUniforms(sourceText);
        var shaderUniforms = BuildShaderUniforms(uniforms, effectParameters ?? []);
        var varyingMap = BuildVaryingMap(vertexFunction, pixelFunction, structs);
        var vertexSource = TranslateVertexShader(vertexFunction, structs, uniforms, varyingMap);
        var fragmentSource = TranslateFragmentShader(pixelFunction, structs, uniforms, varyingMap);
        return new RenderEffectShaderProgram(
            new RenderEffectShaderStageSource(RenderShaderStage.Vertex, vertexCompile.Profile, vertexCompile.EntryPoint, vertexSource),
            new RenderEffectShaderStageSource(RenderShaderStage.Fragment, pixelCompile.Profile, pixelCompile.EntryPoint, fragmentSource),
            shaderUniforms);
    }

    private static string TranslateVertexShader(
        HlslFunction function,
        IReadOnlyDictionary<string, HlslStruct> structs,
        IReadOnlyList<HlslUniform> uniforms,
        IReadOnlyDictionary<string, string> varyingMap)
    {
        var builder = new StringBuilder();
        AppendHeader(builder);
        builder.AppendLine("layout (location = 0) in vec3 aPosition;");
        builder.AppendLine("layout (location = 1) in vec3 aNormal;");
        builder.AppendLine("layout (location = 2) in vec2 aUv;");
        builder.AppendLine();
        AppendUniforms(builder, uniforms);
        foreach (var varying in VertexVaryings(function, structs, varyingMap))
        {
            builder.AppendLine($"out {ToGlslType(varying.Type)} {varying.Name};");
        }

        builder.AppendLine();
        builder.AppendLine("void main()");
        builder.AppendLine("{");
        foreach (var parameter in function.Parameters)
        {
            builder.AppendLine($"    {ToGlslType(parameter.Type)} {parameter.Name} = {VertexInputExpression(parameter)};");
        }

        var assignedPosition = false;
        foreach (var assignment in ParseFieldAssignments(function.Body))
        {
            if (!structs.TryGetValue(function.ReturnType, out var outputStruct))
            {
                continue;
            }

            var field = outputStruct.Fields.FirstOrDefault(field => string.Equals(field.Name, assignment.FieldName, StringComparison.Ordinal));
            if (field is null)
            {
                continue;
            }

            var expression = TranslateExpression(assignment.Expression, varyingMap);
            if (IsPositionSemantic(field.Semantic))
            {
                builder.AppendLine($"    gl_Position = {expression};");
                assignedPosition = true;
            }
            else if (varyingMap.TryGetValue(field.Name, out var varyingName))
            {
                builder.AppendLine($"    {varyingName} = {expression};");
            }
        }

        if (!assignedPosition && TryParseReturnExpression(function.Body, out var returnExpression))
        {
            var expression = TranslateExpression(returnExpression, varyingMap);
            if (IsPositionSemantic(function.Semantic) || ToGlslType(function.ReturnType) == "vec4")
            {
                builder.AppendLine($"    gl_Position = {expression};");
                assignedPosition = true;
            }
        }

        if (!assignedPosition)
        {
            builder.AppendLine("    gl_Position = vec4(aPosition, 1.0);");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string TranslateFragmentShader(
        HlslFunction function,
        IReadOnlyDictionary<string, HlslStruct> structs,
        IReadOnlyList<HlslUniform> uniforms,
        IReadOnlyDictionary<string, string> varyingMap)
    {
        var builder = new StringBuilder();
        AppendHeader(builder);
        AppendUniforms(builder, uniforms);
        foreach (var varying in FragmentVaryings(function, structs, varyingMap))
        {
            builder.AppendLine($"in {ToGlslType(varying.Type)} {varying.Name};");
        }

        builder.AppendLine("out vec4 FragColor;");
        builder.AppendLine();
        builder.AppendLine("void main()");
        builder.AppendLine("{");
        foreach (var parameter in function.Parameters.Where(parameter => !structs.ContainsKey(parameter.Type)))
        {
            builder.AppendLine($"    {ToGlslType(parameter.Type)} {parameter.Name} = {FragmentInputExpression(parameter, varyingMap)};");
        }

        if (TryParseReturnExpression(function.Body, out var returnExpression))
        {
            builder.AppendLine($"    FragColor = {TranslateExpression(returnExpression, varyingMap)};");
        }
        else
        {
            builder.AppendLine("    FragColor = vec4(1.0);");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static IReadOnlyList<HlslVarying> VertexVaryings(
        HlslFunction function,
        IReadOnlyDictionary<string, HlslStruct> structs,
        IReadOnlyDictionary<string, string> varyingMap)
    {
        if (!structs.TryGetValue(function.ReturnType, out var outputStruct))
        {
            return [];
        }

        return outputStruct.Fields
            .Where(field => !IsPositionSemantic(field.Semantic) && varyingMap.ContainsKey(field.Name))
            .Select(field => new HlslVarying(varyingMap[field.Name], field.Type))
            .ToArray();
    }

    private static IReadOnlyList<HlslVarying> FragmentVaryings(
        HlslFunction function,
        IReadOnlyDictionary<string, HlslStruct> structs,
        IReadOnlyDictionary<string, string> varyingMap)
    {
        var varyings = new List<HlslVarying>();
        foreach (var parameter in function.Parameters)
        {
            if (structs.TryGetValue(parameter.Type, out var inputStruct))
            {
                varyings.AddRange(inputStruct.Fields
                    .Where(field => !IsPositionSemantic(field.Semantic) && varyingMap.ContainsKey(field.Name))
                    .Select(field => new HlslVarying(varyingMap[field.Name], field.Type)));
            }
            else if (!string.IsNullOrWhiteSpace(parameter.Semantic))
            {
                var name = VaryingName(parameter.Semantic, parameter.Name);
                varyings.Add(new HlslVarying(name, parameter.Type));
            }
        }

        return varyings
            .GroupBy(varying => varying.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildVaryingMap(
        HlslFunction vertexFunction,
        HlslFunction pixelFunction,
        IReadOnlyDictionary<string, HlslStruct> structs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (structs.TryGetValue(vertexFunction.ReturnType, out var outputStruct))
        {
            foreach (var field in outputStruct.Fields.Where(field => !IsPositionSemantic(field.Semantic)))
            {
                map[field.Name] = VaryingName(field.Semantic, field.Name);
            }
        }

        foreach (var parameter in pixelFunction.Parameters)
        {
            if (!structs.TryGetValue(parameter.Type, out var inputStruct))
            {
                continue;
            }

            foreach (var field in inputStruct.Fields.Where(field => !IsPositionSemantic(field.Semantic)))
            {
                map.TryAdd(field.Name, VaryingName(field.Semantic, field.Name));
            }
        }

        return map;
    }

    private static IReadOnlyList<RenderEffectShaderUniform> BuildShaderUniforms(
        IReadOnlyList<HlslUniform> uniforms,
        IReadOnlyList<RenderEffectParameter> effectParameters)
    {
        var byName = new Dictionary<string, RenderEffectParameter>(StringComparer.Ordinal);
        foreach (var parameter in effectParameters)
        {
            byName.TryAdd(parameter.Name, parameter);
        }
        return uniforms
            .Select(uniform =>
            {
                byName.TryGetValue(uniform.Name, out var parameter);
                var textureSourceName = uniform.TextureSourceName;
                var semantic = parameter?.Semantic ?? RenderEffectSemantic.Unknown;
                var resourceName = parameter?.ResourceName;
                if (!string.IsNullOrWhiteSpace(textureSourceName) &&
                    byName.TryGetValue(textureSourceName, out var textureParameter))
                {
                    semantic = textureParameter.Semantic != RenderEffectSemantic.Unknown
                        ? textureParameter.Semantic
                        : semantic;
                    resourceName = textureParameter.ResourceName ?? resourceName;
                }

                return new RenderEffectShaderUniform(
                    uniform.Name,
                    uniform.Type,
                    ToKind(uniform.Type),
                    semantic,
                    textureSourceName,
                    resourceName);
            })
            .ToArray();
    }

    private static IReadOnlyList<HlslUniform> ParseUniforms(string sourceText)
    {
        var uniforms = new Dictionary<string, HlslUniform>(StringComparer.Ordinal);
        foreach (var statement in EnumerateTopLevelStatements(sourceText))
        {
            var match = UniformStatementRegex().Match(statement);
            if (!match.Success)
            {
                continue;
            }

            var type = match.Groups["type"].Value.Trim();
            var name = match.Groups["name"].Value.Trim();
            if (IsUniformType(type))
            {
                uniforms[name] = new HlslUniform(name, type);
            }
        }

        foreach (Match match in SamplerStateRegex().Matches(sourceText))
        {
            var name = match.Groups["name"].Value.Trim();
            var type = match.Groups["type"].Value.Trim();
            uniforms[name] = new HlslUniform(name, type, FindSamplerTextureName(match.Groups["body"].Value));
        }

        return uniforms.Values.ToArray();
    }

    private static string? FindSamplerTextureName(string samplerBody)
    {
        var match = SamplerTextureRegex().Match(samplerBody);
        return match.Success
            ? match.Groups["name"].Value.Trim()
            : null;
    }

    private static IEnumerable<string> EnumerateTopLevelStatements(string sourceText)
    {
        var start = 0;
        var braceDepth = 0;
        var angleDepth = 0;
        var inString = false;
        var escaped = false;
        for (var i = 0; i < sourceText.Length; i++)
        {
            var c = sourceText[i];
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

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '<' when braceDepth == 0:
                    angleDepth++;
                    break;
                case '>' when angleDepth > 0:
                    angleDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}' when braceDepth > 0:
                    braceDepth--;
                    break;
                case ';' when braceDepth == 0 && angleDepth == 0:
                    var statement = sourceText[start..i].Trim();
                    if (statement.Length > 0)
                    {
                        yield return statement;
                    }

                    start = i + 1;
                    break;
            }
        }
    }

    private static IReadOnlyDictionary<string, HlslStruct> ParseStructs(string sourceText)
    {
        var structs = new Dictionary<string, HlslStruct>(StringComparer.Ordinal);
        foreach (Match match in StructRegex().Matches(sourceText))
        {
            var fields = new List<HlslField>();
            foreach (Match fieldMatch in StructFieldRegex().Matches(match.Groups["body"].Value))
            {
                fields.Add(new HlslField(
                    fieldMatch.Groups["name"].Value.Trim(),
                    fieldMatch.Groups["type"].Value.Trim(),
                    fieldMatch.Groups["semantic"].Value.Trim()));
            }

            structs[match.Groups["name"].Value.Trim()] = new HlslStruct(match.Groups["name"].Value.Trim(), fields);
        }

        return structs;
    }

    private static bool TryParseFunction(string sourceText, string entryPoint, out HlslFunction function)
    {
        var pattern = $@"(?<return>[A-Za-z_][A-Za-z0-9_]*(?:[0-9](?:x[0-9])?)?)\s+{Regex.Escape(entryPoint)}\s*\((?<params>[^)]*)\)\s*(?::\s*(?<semantic>[A-Za-z_][A-Za-z0-9_]*))?\s*\{{";
        var match = Regex.Match(sourceText, pattern, RegexOptions.Multiline);
        if (!match.Success)
        {
            function = default!;
            return false;
        }

        var openBrace = match.Index + match.Length - 1;
        var closeBrace = FindMatchingBrace(sourceText, openBrace);
        if (closeBrace < 0)
        {
            function = default!;
            return false;
        }

        function = new HlslFunction(
            match.Groups["return"].Value.Trim(),
            entryPoint,
            ParseParameters(match.Groups["params"].Value),
            match.Groups["semantic"].Success ? match.Groups["semantic"].Value.Trim() : null,
            sourceText.Substring(openBrace + 1, closeBrace - openBrace - 1));
        return true;
    }

    private static IReadOnlyList<HlslParameter> ParseParameters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var parameters = new List<HlslParameter>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = ParameterRegex().Match(part);
            if (match.Success)
            {
                parameters.Add(new HlslParameter(
                    match.Groups["type"].Value.Trim(),
                    match.Groups["name"].Value.Trim(),
                    match.Groups["semantic"].Success ? match.Groups["semantic"].Value.Trim() : null));
            }
        }

        return parameters;
    }

    private static IReadOnlyList<HlslFieldAssignment> ParseFieldAssignments(string body)
    {
        var assignments = new List<HlslFieldAssignment>();
        foreach (Match match in FieldAssignmentRegex().Matches(body))
        {
            assignments.Add(new HlslFieldAssignment(
                match.Groups["field"].Value.Trim(),
                match.Groups["expression"].Value.Trim()));
        }

        return assignments;
    }

    private static bool TryParseReturnExpression(string body, out string expression)
    {
        var matches = ReturnRegex().Matches(body);
        if (matches.Count == 0)
        {
            expression = string.Empty;
            return false;
        }

        expression = matches[^1].Groups["expression"].Value.Trim();
        return true;
    }

    private static bool TryParseCompileState(string? value, RenderShaderStage expectedStage, out ShaderCompileState state)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            state = default!;
            return false;
        }

        var match = CompileStateRegex().Match(value);
        if (!match.Success)
        {
            state = default!;
            return false;
        }

        var profile = match.Groups["profile"].Value.Trim();
        var stage = profile.StartsWith("vs_", StringComparison.OrdinalIgnoreCase)
            ? RenderShaderStage.Vertex
            : RenderShaderStage.Fragment;
        if (stage != expectedStage)
        {
            state = default!;
            return false;
        }

        state = new ShaderCompileState(stage, profile, match.Groups["entry"].Value.Trim());
        return true;
    }

    private static string TranslateExpression(string expression, IReadOnlyDictionary<string, string> varyingMap)
    {
        var result = expression.Trim();
        foreach (var pair in varyingMap)
        {
            result = Regex.Replace(result, $@"\b[A-Za-z_][A-Za-z0-9_]*\.{Regex.Escape(pair.Key)}\b", pair.Value);
        }

        result = MulRegex().Replace(result, match =>
        {
            var left = TranslateExpression(match.Groups["left"].Value, varyingMap);
            var right = TranslateExpression(match.Groups["right"].Value, varyingMap);
            return $"({right} * {left})";
        });
        result = result.Replace("tex2D", "texture", StringComparison.Ordinal);
        result = result.Replace("float4x4", "mat4", StringComparison.Ordinal);
        result = result.Replace("float4", "vec4", StringComparison.Ordinal);
        result = result.Replace("float3", "vec3", StringComparison.Ordinal);
        result = result.Replace("float2", "vec2", StringComparison.Ordinal);
        result = result.Replace("FX9_MACRO_VALUE_ONE", "1.0", StringComparison.Ordinal);
        return NormalizeConstructorNumbers(result);
    }

    private static string NormalizeConstructorNumbers(string expression)
    {
        return GlslConstructorRegex().Replace(expression, match =>
        {
            var args = match.Groups["args"].Value
                .Split(',', StringSplitOptions.TrimEntries)
                .Select(argument => IntegerLiteralRegex().Replace(argument, "${value}.0"));
            return $"{match.Groups["type"].Value}({string.Join(", ", args)})";
        });
    }

    private static string VertexInputExpression(HlslParameter parameter)
    {
        var type = ToGlslType(parameter.Type);
        var semantic = NormalizeSemantic(parameter.Semantic);
        return semantic switch
        {
            "POSITION" when type == "vec4" => "vec4(aPosition, 1.0)",
            "POSITION" when type == "vec3" => "aPosition",
            "NORMAL" when type == "vec4" => "vec4(aNormal, 0.0)",
            "NORMAL" when type == "vec3" => "aNormal",
            "TEXCOORD0" when type == "vec4" => "vec4(aUv, 0.0, 1.0)",
            "TEXCOORD0" when type == "vec3" => "vec3(aUv, 0.0)",
            "TEXCOORD0" when type == "vec2" => "aUv",
            _ => ZeroValue(type)
        };
    }

    private static string FragmentInputExpression(HlslParameter parameter, IReadOnlyDictionary<string, string> varyingMap)
    {
        var type = ToGlslType(parameter.Type);
        var semantic = NormalizeSemantic(parameter.Semantic);
        var matching = varyingMap.FirstOrDefault(pair =>
            string.Equals(NormalizeSemantic(pair.Value), NormalizeSemantic(VaryingName(semantic, parameter.Name)), StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(matching.Value))
        {
            return matching.Value;
        }

        return semantic.StartsWith("TEXCOORD", StringComparison.Ordinal)
            ? VaryingName(semantic, parameter.Name)
            : ZeroValue(type);
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("#version 330 core");
        builder.AppendLine();
    }

    private static void AppendUniforms(StringBuilder builder, IReadOnlyList<HlslUniform> uniforms)
    {
        foreach (var uniform in uniforms)
        {
            builder.AppendLine($"uniform {ToGlslType(uniform.Type)} {uniform.Name};");
        }

        if (uniforms.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
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

    private static string ToGlslType(string hlslType)
    {
        return NormalizeType(hlslType) switch
        {
            "FLOAT" or "HALF" or "DOUBLE" => "float",
            "FLOAT2" or "HALF2" or "DOUBLE2" => "vec2",
            "FLOAT3" or "HALF3" or "DOUBLE3" => "vec3",
            "FLOAT4" or "HALF4" or "DOUBLE4" => "vec4",
            "FLOAT3X3" or "FLOAT4X3" => "mat3",
            "FLOAT4X4" or "MATRIX" => "mat4",
            "INT" => "int",
            "INT2" => "ivec2",
            "INT3" => "ivec3",
            "INT4" => "ivec4",
            "BOOL" => "bool",
            "BOOL2" => "bvec2",
            "BOOL3" => "bvec3",
            "BOOL4" => "bvec4",
            "TEXTURE" or "TEXTURE2D" or "SAMPLER" or "SAMPLER2D" => "sampler2D",
            _ => hlslType
        };
    }

    private static string ZeroValue(string glslType)
    {
        return glslType switch
        {
            "float" => "0.0",
            "vec2" => "vec2(0.0)",
            "vec3" => "vec3(0.0)",
            "vec4" => "vec4(0.0)",
            "int" => "0",
            "mat3" => "mat3(1.0)",
            "mat4" => "mat4(1.0)",
            _ => $"{glslType}()"
        };
    }

    private static string VaryingName(string? semantic, string fallback)
    {
        var normalized = NormalizeSemantic(semantic);
        return normalized.Length == 0
            ? $"v_{fallback}"
            : $"v{char.ToUpperInvariant(normalized[0])}{normalized[1..].ToLowerInvariant()}";
    }

    private static bool IsPositionSemantic(string? semantic)
    {
        return NormalizeSemantic(semantic) is "POSITION" or "SVPOSITION";
    }

    private static bool IsUniformType(string type)
    {
        return NormalizeType(type) is
            "FLOAT" or "FLOAT2" or "FLOAT3" or "FLOAT4" or "FLOAT3X3" or "FLOAT4X3" or "FLOAT4X4" or
            "HALF" or "HALF2" or "HALF3" or "HALF4" or
            "INT" or "INT2" or "INT3" or "INT4" or
            "BOOL" or "BOOL2" or "BOOL3" or "BOOL4" or
            "TEXTURE" or "TEXTURE2D" or "SAMPLER" or "SAMPLER2D";
    }

    private static RenderEffectParameterKind ToKind(string type)
    {
        return NormalizeType(type) switch
        {
            "FLOAT" or "HALF" or "DOUBLE" => RenderEffectParameterKind.Float,
            "FLOAT2" or "FLOAT3" or "FLOAT4" or "HALF2" or "HALF3" or "HALF4" => RenderEffectParameterKind.Vector,
            "FLOAT3X3" or "FLOAT4X3" or "FLOAT4X4" or "MATRIX" => RenderEffectParameterKind.Matrix,
            "INT" => RenderEffectParameterKind.Int,
            "INT2" or "INT3" or "INT4" => RenderEffectParameterKind.Vector,
            "BOOL" => RenderEffectParameterKind.Bool,
            "BOOL2" or "BOOL3" or "BOOL4" => RenderEffectParameterKind.Vector,
            "TEXTURE" or "TEXTURE2D" => RenderEffectParameterKind.Texture,
            "SAMPLER" or "SAMPLER2D" => RenderEffectParameterKind.Sampler,
            _ => RenderEffectParameterKind.Unknown
        };
    }

    private static string NormalizeSemantic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string NormalizeType(string value)
    {
        return NormalizeSemantic(value);
    }

    private sealed record ShaderCompileState(RenderShaderStage Stage, string Profile, string EntryPoint);

    private sealed record HlslUniform(string Name, string Type, string? TextureSourceName = null);

    private sealed record HlslStruct(string Name, IReadOnlyList<HlslField> Fields);

    private sealed record HlslField(string Name, string Type, string Semantic);

    private sealed record HlslParameter(string Type, string Name, string? Semantic);

    private sealed record HlslFunction(
        string ReturnType,
        string Name,
        IReadOnlyList<HlslParameter> Parameters,
        string? Semantic,
        string Body);

    private sealed record HlslFieldAssignment(string FieldName, string Expression);

    private sealed record HlslVarying(string Name, string Type);

    [GeneratedRegex(@"compile\s+(?<profile>[vp]s_\d_\d)\s+(?<entry>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompileStateRegex();

    [GeneratedRegex(@"struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>[\s\S]*?)\}\s*;", RegexOptions.Compiled)]
    private static partial Regex StructRegex();

    [GeneratedRegex(@"(?<type>[A-Za-z_][A-Za-z0-9_]*(?:[0-9](?:x[0-9])?)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<semantic>[A-Za-z_][A-Za-z0-9_]*)\s*;", RegexOptions.Compiled)]
    private static partial Regex StructFieldRegex();

    [GeneratedRegex(@"^\s*(?:uniform\s+)?(?<type>[A-Za-z_][A-Za-z0-9_]*(?:[0-9](?:x[0-9])?)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled)]
    private static partial Regex UniformStatementRegex();

    [GeneratedRegex(@"\b(?<type>sampler2D|sampler)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*sampler_state\s*\{(?<body>[\s\S]*?)\}\s*;", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SamplerStateRegex();

    [GeneratedRegex(@"\btexture\s*=\s*<(?<name>[A-Za-z_][A-Za-z0-9_]*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SamplerTextureRegex();

    [GeneratedRegex(@"^\s*(?<type>[A-Za-z_][A-Za-z0-9_]*(?:[0-9](?:x[0-9])?)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<semantic>[A-Za-z_][A-Za-z0-9_]*))?\s*$", RegexOptions.Compiled)]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\.(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<expression>[^;]+);", RegexOptions.Compiled)]
    private static partial Regex FieldAssignmentRegex();

    [GeneratedRegex(@"\breturn\s+(?<expression>[^;]+);", RegexOptions.Compiled)]
    private static partial Regex ReturnRegex();

    [GeneratedRegex(@"\bmul\s*\(\s*(?<left>[^,()]+)\s*,\s*(?<right>[^()]+)\)", RegexOptions.Compiled)]
    private static partial Regex MulRegex();

    [GeneratedRegex(@"(?<type>vec[234]|mat[234])\((?<args>[^)]*)\)", RegexOptions.Compiled)]
    private static partial Regex GlslConstructorRegex();

    [GeneratedRegex(@"(?<![\w.])(?<value>[0-9]+)(?![\w.])", RegexOptions.Compiled)]
    private static partial Regex IntegerLiteralRegex();
}
