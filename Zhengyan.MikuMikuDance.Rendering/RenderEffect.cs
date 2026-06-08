using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Zhengyan.MikuMikuDance.Core.Effects;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderEffect(
    string SourceName,
    IReadOnlyList<RenderEffectParameter> Parameters,
    IReadOnlyList<RenderEffectTechnique> Techniques,
    RenderEffectScriptMetadata Script,
    string SourceText = "")
{
    public RenderEffectTechnique? DefaultTechnique => Techniques.FirstOrDefault();

    public bool HasScriptExternal =>
        Script.Commands.Any(command => command.Type == RenderEffectScriptCommandType.SetScriptExternal) ||
        Techniques.Any(technique => technique.HasScriptExternal);
}

public sealed record RenderEffectParameter(
    string Name,
    string Type,
    RenderEffectParameterKind Kind,
    RenderEffectSemantic Semantic,
    string? RawSemantic,
    EffectValue? DefaultValue,
    IReadOnlyDictionary<string, EffectValue> Annotations,
    string? ResourceName = null,
    string? ResourceType = null,
    RenderEffectOffscreenTarget? OffscreenTarget = null);

public sealed record RenderEffectOffscreenTarget(
    string Name,
    string Description,
    Vector4 ClearColor,
    float ClearDepth,
    IReadOnlyList<RenderEffectOffscreenDefaultEffect> DefaultEffects)
{
    public RenderEffectOffscreenDrawPlan CreateDrawPlan(string ownerDrawableName, IEnumerable<string> drawableNames)
    {
        return RenderEffectOffscreenScheduler.CreatePlan(this, ownerDrawableName, drawableNames);
    }
}

public sealed record RenderEffectOffscreenDefaultEffect(string Target, string EffectPath);

public sealed record RenderEffectTechnique(
    string Name,
    IReadOnlyDictionary<string, EffectValue> Annotations,
    IReadOnlyList<RenderEffectScriptCommand> Script,
    bool HasScriptExternal,
    IReadOnlyList<RenderEffectPass> Passes)
{
    public RenderEffectExecutionPlan ExecutionPlan { get; init; } = RenderEffectExecutionPlan.Empty;
}

public sealed record RenderEffectPass(
    string Name,
    IReadOnlyDictionary<string, EffectValue> Annotations,
    RenderPassState State,
    string? VertexShaderState,
    string? PixelShaderState,
    IReadOnlyList<RenderEffectScriptCommand> Script,
    RenderEffectShaderProgram? Shader = null);

public sealed record RenderEffectShaderProgram(
    RenderEffectShaderStageSource VertexShader,
    RenderEffectShaderStageSource PixelShader,
    IReadOnlyList<RenderEffectShaderUniform> Uniforms);

public sealed record RenderEffectShaderStageSource(
    RenderShaderStage Stage,
    string Profile,
    string EntryPoint,
    string Source);

public sealed record RenderEffectShaderUniform(
    string Name,
    string Type,
    RenderEffectParameterKind Kind,
    RenderEffectSemantic Semantic,
    string? TextureSourceName = null,
    string? ResourceName = null);

public sealed record RenderPassState(
    IReadOnlyDictionary<string, string> RawStates,
    bool? DepthTestEnabled = null,
    bool? DepthWriteEnabled = null,
    bool? BlendEnabled = null,
    RenderBlendFactor? SourceBlend = null,
    RenderBlendFactor? DestinationBlend = null,
    RenderBlendOperation? BlendOperation = null,
    RenderCullMode? CullMode = null,
    RenderCompareFunction? DepthFunction = null,
    bool? ColorWriteEnabled = null);

public sealed record RenderEffectScriptMetadata(
    RenderEffectScriptClass Class,
    RenderEffectScriptOrder Order,
    string Output,
    IReadOnlyList<RenderEffectScriptCommand> Commands);

public sealed record RenderEffectScriptCommand(RenderEffectScriptCommandType Type, string Value);

public enum RenderEffectParameterKind
{
    Unknown,
    Bool,
    Int,
    Float,
    Vector,
    Matrix,
    Texture,
    Sampler,
    String
}

public enum RenderEffectSemantic
{
    Unknown,
    World,
    View,
    Projection,
    WorldView,
    ViewProjection,
    WorldViewProjection,
    WorldInverse,
    ViewInverse,
    ProjectionInverse,
    WorldViewInverse,
    ViewProjectionInverse,
    WorldViewProjectionInverse,
    WorldTranspose,
    ViewTranspose,
    ProjectionTranspose,
    WorldViewTranspose,
    ViewProjectionTranspose,
    WorldViewProjectionTranspose,
    WorldInverseTranspose,
    ViewInverseTranspose,
    ProjectionInverseTranspose,
    WorldViewInverseTranspose,
    ViewProjectionInverseTranspose,
    WorldViewProjectionInverseTranspose,
    Diffuse,
    Ambient,
    Emissive,
    Specular,
    SpecularPower,
    ToonColor,
    EdgeColor,
    GroundShadowColor,
    Position,
    Direction,
    MaterialTexture,
    MaterialSphereMap,
    MaterialToonTexture,
    AddingTexture,
    MultiplyingTexture,
    AddingSphereTexture,
    MultiplyingSphereTexture,
    ViewportPixelSize,
    Time,
    ElapsedTime,
    MousePosition,
    LeftMouseDown,
    MiddleMouseDown,
    RightMouseDown,
    ControlObject,
    RenderColorTarget,
    RenderDepthStencilTarget,
    AnimatedTexture,
    OffscreenRenderTarget,
    TextureValue,
    StandardsGlobal
}

public enum RenderBlendFactor
{
    Zero,
    One,
    SourceColor,
    InverseSourceColor,
    SourceAlpha,
    InverseSourceAlpha,
    DestinationAlpha,
    InverseDestinationAlpha,
    DestinationColor,
    InverseDestinationColor,
    SourceAlphaSaturate,
    BlendColor,
    InverseBlendColor
}

public enum RenderBlendOperation
{
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max
}

public enum RenderCullMode
{
    None,
    Front,
    Back
}

public enum RenderCompareFunction
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always
}

public enum RenderEffectScriptClass
{
    Object,
    Scene,
    SceneObject
}

public enum RenderEffectScriptOrder
{
    DependsOnScriptExternal,
    PreProcess,
    Standard,
    PostProcess
}

public enum RenderEffectScriptCommandType
{
    Unknown,
    SetRenderColorTarget0,
    SetRenderColorTarget1,
    SetRenderColorTarget2,
    SetRenderColorTarget3,
    SetRenderDepthStencilTarget,
    ClearSetColor,
    ClearSetDepth,
    Clear,
    SetScriptExternal,
    ExecutePass,
    PushLoopCounter,
    GetLoopIndex,
    PopLoopCounter,
    Draw
}

public enum RenderShaderStage
{
    Vertex,
    Fragment
}

public static partial class RenderEffectCompiler
{
    public static RenderEffect Compile(EffectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var parameters = document.Parameters.Select(ToParameter).ToArray();
        var script = BuildScriptMetadata(parameters);
        var techniques = document.Techniques.Select(technique => ToTechnique(technique, document.SourceText, parameters)).ToArray();
        return new RenderEffect(document.SourceName, parameters, techniques, script, document.SourceText);
    }

    private static RenderEffectParameter ToParameter(EffectParameter parameter)
    {
        var annotations = ToAnnotationDictionary(parameter.Annotations);
        var semantic = ToSemantic(parameter.Semantic);
        return new RenderEffectParameter(
            parameter.Name,
            parameter.Type,
            ToKind(parameter.Type),
            semantic,
            string.IsNullOrWhiteSpace(parameter.Semantic) ? null : parameter.Semantic,
            ParseValue(parameter.DefaultValue),
            annotations,
            AnnotationString(annotations, "ResourceName"),
            AnnotationString(annotations, "ResourceType"),
            BuildOffscreenTarget(parameter.Name, semantic, annotations));
    }

    private static RenderEffectOffscreenTarget? BuildOffscreenTarget(
        string name,
        RenderEffectSemantic semantic,
        IReadOnlyDictionary<string, EffectValue> annotations)
    {
        if (semantic != RenderEffectSemantic.OffscreenRenderTarget)
        {
            return null;
        }

        return new RenderEffectOffscreenTarget(
            name,
            AnnotationString(annotations, "Description") ?? string.Empty,
            AnnotationVector(annotations, "ClearColor") ?? new Vector4(0, 0, 0, 1),
            Math.Clamp(AnnotationFloat(annotations, "ClearDepth") ?? 1f, 0f, 1f),
            ParseOffscreenDefaultEffects(AnnotationString(annotations, "DefaultEffect")));
    }

    private static RenderEffectTechnique ToTechnique(
        EffectTechnique technique,
        string sourceText,
        IReadOnlyList<RenderEffectParameter> parameters)
    {
        var annotations = ToAnnotationDictionary(technique.Annotations);
        var script = ParseScript(AnnotationString(annotations, "Script"));
        var passes = technique.Passes.Select(pass => ToPass(pass, sourceText, parameters)).ToArray();
        var hasScriptExternal =
            script.Any(command => command.Type == RenderEffectScriptCommandType.SetScriptExternal) ||
            passes.Any(pass => pass.Script.Any(command => command.Type == RenderEffectScriptCommandType.SetScriptExternal));
        return new RenderEffectTechnique(technique.Name, annotations, script, hasScriptExternal, passes)
        {
            ExecutionPlan = RenderEffectExecutionPlanner.Create(script, passes)
        };
    }

    private static RenderEffectPass ToPass(
        EffectPass pass,
        string sourceText,
        IReadOnlyList<RenderEffectParameter> parameters)
    {
        var annotations = ToAnnotationDictionary(pass.Annotations);
        var script = ParseScript(AnnotationString(annotations, "Script") ?? "Draw=Geometry;");
        var vertexShaderState = FindState(pass.States, "VertexShader");
        var pixelShaderState = FindState(pass.States, "PixelShader");
        return new RenderEffectPass(
            pass.Name,
            annotations,
            ToRenderPassState(pass.States),
            vertexShaderState,
            pixelShaderState,
            script,
            RenderEffectShaderTranslator.TryTranslate(sourceText, vertexShaderState, pixelShaderState, parameters));
    }

    private static RenderPassState ToRenderPassState(IReadOnlyDictionary<string, string> states)
    {
        return new RenderPassState(
            new Dictionary<string, string>(states, StringComparer.OrdinalIgnoreCase),
            DepthTestEnabled: ParseBool(FindState(states, "ZEnable", "DepthEnable")),
            DepthWriteEnabled: ParseBool(FindState(states, "ZWriteEnable", "DepthWriteEnable")),
            BlendEnabled: ParseBool(FindState(states, "AlphaBlendEnable", "BlendEnable")),
            SourceBlend: ParseBlendFactor(FindState(states, "SrcBlend", "SourceBlend")),
            DestinationBlend: ParseBlendFactor(FindState(states, "DestBlend", "DstBlend", "DestinationBlend")),
            BlendOperation: ParseBlendOperation(FindState(states, "BlendOp", "BlendOperation")),
            CullMode: ParseCullMode(FindState(states, "CullMode")),
            DepthFunction: ParseCompareFunction(FindState(states, "ZFunc", "DepthFunc", "DepthFunction")),
            ColorWriteEnabled: ParseBool(FindState(states, "ColorWriteEnable")));
    }

    private static RenderEffectScriptMetadata BuildScriptMetadata(IReadOnlyList<RenderEffectParameter> parameters)
    {
        var parameter = parameters.FirstOrDefault(parameter => parameter.Semantic == RenderEffectSemantic.StandardsGlobal);
        if (parameter is null)
        {
            return new RenderEffectScriptMetadata(
                RenderEffectScriptClass.SceneObject,
                RenderEffectScriptOrder.Standard,
                "color",
                []);
        }

        var scriptClass = AnnotationString(parameter.Annotations, "ScriptClass")?.Trim().ToUpperInvariant() switch
        {
            "OBJECT" => RenderEffectScriptClass.Object,
            "SCENE" => RenderEffectScriptClass.Scene,
            _ => RenderEffectScriptClass.SceneObject
        };
        var scriptOrder = AnnotationString(parameter.Annotations, "ScriptOrder")?.Trim().ToUpperInvariant() switch
        {
            "DEPENDSONSCRIPTEXTERNAL" => RenderEffectScriptOrder.DependsOnScriptExternal,
            "PREPROCESS" => RenderEffectScriptOrder.PreProcess,
            "POSTPROCESS" => RenderEffectScriptOrder.PostProcess,
            _ => RenderEffectScriptOrder.Standard
        };
        var scriptOutput = AnnotationString(parameter.Annotations, "ScriptOutput") ?? "color";
        var commands = ParseScript(AnnotationString(parameter.Annotations, "Script"));
        return new RenderEffectScriptMetadata(scriptClass, scriptOrder, scriptOutput, commands);
    }

    private static IReadOnlyDictionary<string, EffectValue> ToAnnotationDictionary(IReadOnlyList<EffectAnnotation> annotations)
    {
        var result = new Dictionary<string, EffectValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var annotation in annotations)
        {
            result[annotation.Name] = annotation.Value;
        }

        return result;
    }

    private static RenderEffectParameterKind ToKind(string type)
    {
        var normalized = NormalizeIdentifier(type)
            .Replace("UNIFORM", string.Empty, StringComparison.Ordinal)
            .Replace("STATIC", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (normalized.Contains("SAMPLER", StringComparison.Ordinal))
        {
            return RenderEffectParameterKind.Sampler;
        }

        if (normalized.Contains("TEXTURE", StringComparison.Ordinal))
        {
            return RenderEffectParameterKind.Texture;
        }

        if (normalized == "BOOL" || normalized.StartsWith("BOOL", StringComparison.Ordinal))
        {
            return RenderEffectParameterKind.Bool;
        }

        if (normalized == "STRING")
        {
            return RenderEffectParameterKind.String;
        }

        if (normalized.Contains("MATRIX", StringComparison.Ordinal) || MatrixTypeRegex().IsMatch(normalized))
        {
            return RenderEffectParameterKind.Matrix;
        }

        if (normalized is "FLOAT" or "HALF" or "DOUBLE")
        {
            return RenderEffectParameterKind.Float;
        }

        if (normalized.StartsWith("FLOAT", StringComparison.Ordinal) ||
            normalized.StartsWith("HALF", StringComparison.Ordinal) ||
            normalized.StartsWith("DOUBLE", StringComparison.Ordinal))
        {
            return RenderEffectParameterKind.Vector;
        }

        if (normalized == "INT")
        {
            return RenderEffectParameterKind.Int;
        }

        return normalized.StartsWith("INT", StringComparison.Ordinal)
            ? RenderEffectParameterKind.Vector
            : RenderEffectParameterKind.Unknown;
    }

    private static RenderEffectSemantic ToSemantic(string? semantic)
    {
        return NormalizeIdentifier(semantic) switch
        {
            "WORLD" => RenderEffectSemantic.World,
            "VIEW" => RenderEffectSemantic.View,
            "PROJECTION" => RenderEffectSemantic.Projection,
            "WORLDVIEW" => RenderEffectSemantic.WorldView,
            "VIEWPROJECTION" => RenderEffectSemantic.ViewProjection,
            "WORLDVIEWPROJECTION" => RenderEffectSemantic.WorldViewProjection,
            "WORLDINVERSE" => RenderEffectSemantic.WorldInverse,
            "VIEWINVERSE" => RenderEffectSemantic.ViewInverse,
            "PROJECTIONINVERSE" => RenderEffectSemantic.ProjectionInverse,
            "WORLDVIEWINVERSE" => RenderEffectSemantic.WorldViewInverse,
            "VIEWPROJECTIONINVERSE" => RenderEffectSemantic.ViewProjectionInverse,
            "WORLDVIEWPROJECTIONINVERSE" => RenderEffectSemantic.WorldViewProjectionInverse,
            "WORLDTRANSPOSE" => RenderEffectSemantic.WorldTranspose,
            "VIEWTRANSPOSE" => RenderEffectSemantic.ViewTranspose,
            "PROJECTIONTRANSPOSE" => RenderEffectSemantic.ProjectionTranspose,
            "WORLDVIEWTRANSPOSE" => RenderEffectSemantic.WorldViewTranspose,
            "VIEWPROJECTIONTRANSPOSE" => RenderEffectSemantic.ViewProjectionTranspose,
            "WORLDVIEWPROJECTIONTRANSPOSE" => RenderEffectSemantic.WorldViewProjectionTranspose,
            "WORLDINVERSETRANSPOSE" => RenderEffectSemantic.WorldInverseTranspose,
            "VIEWINVERSETRANSPOSE" => RenderEffectSemantic.ViewInverseTranspose,
            "PROJECTIONINVERSETRANSPOSE" => RenderEffectSemantic.ProjectionInverseTranspose,
            "WORLDVIEWINVERSETRANSPOSE" => RenderEffectSemantic.WorldViewInverseTranspose,
            "VIEWPROJECTIONINVERSETRANSPOSE" => RenderEffectSemantic.ViewProjectionInverseTranspose,
            "WORLDVIEWPROJECTIONINVERSETRANSPOSE" => RenderEffectSemantic.WorldViewProjectionInverseTranspose,
            "DIFFUSE" => RenderEffectSemantic.Diffuse,
            "AMBIENT" => RenderEffectSemantic.Ambient,
            "EMISSIVE" => RenderEffectSemantic.Emissive,
            "SPECULAR" => RenderEffectSemantic.Specular,
            "SPECULARPOWER" => RenderEffectSemantic.SpecularPower,
            "TOONCOLOR" => RenderEffectSemantic.ToonColor,
            "EDGECOLOR" => RenderEffectSemantic.EdgeColor,
            "GROUNDSHADOWCOLOR" => RenderEffectSemantic.GroundShadowColor,
            "POSITION" => RenderEffectSemantic.Position,
            "DIRECTION" => RenderEffectSemantic.Direction,
            "MATERIALTEXTURE" => RenderEffectSemantic.MaterialTexture,
            "MATERIALSPHEREMAP" => RenderEffectSemantic.MaterialSphereMap,
            "MATERIALTOONTEXTURE" => RenderEffectSemantic.MaterialToonTexture,
            "ADDINGTEXTURE" => RenderEffectSemantic.AddingTexture,
            "MULTIPLYINGTEXTURE" => RenderEffectSemantic.MultiplyingTexture,
            "ADDINGSPHERETEXTURE" => RenderEffectSemantic.AddingSphereTexture,
            "MULTIPLYINGSPHERETEXTURE" => RenderEffectSemantic.MultiplyingSphereTexture,
            "VIEWPORTPIXELSIZE" => RenderEffectSemantic.ViewportPixelSize,
            "TIME" => RenderEffectSemantic.Time,
            "ELAPSEDTIME" => RenderEffectSemantic.ElapsedTime,
            "MOUSEPOSITION" => RenderEffectSemantic.MousePosition,
            "LEFTMOUSEDOWN" => RenderEffectSemantic.LeftMouseDown,
            "MIDDLEMOUSEDOWN" => RenderEffectSemantic.MiddleMouseDown,
            "RIGHTMOUSEDOWN" => RenderEffectSemantic.RightMouseDown,
            "CONTROLOBJECT" => RenderEffectSemantic.ControlObject,
            "RENDERCOLORTARGET" => RenderEffectSemantic.RenderColorTarget,
            "RENDERDEPTHSTENCILTARGET" => RenderEffectSemantic.RenderDepthStencilTarget,
            "ANIMATEDTEXTURE" => RenderEffectSemantic.AnimatedTexture,
            "OFFSCREENRENDERTARGET" => RenderEffectSemantic.OffscreenRenderTarget,
            "TEXTUREVALUE" => RenderEffectSemantic.TextureValue,
            "STANDARDSGLOBAL" => RenderEffectSemantic.StandardsGlobal,
            _ => RenderEffectSemantic.Unknown
        };
    }

    private static IReadOnlyList<RenderEffectScriptCommand> ParseScript(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var commands = new List<RenderEffectScriptCommand>();
        foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = statement.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                commands.Add(new RenderEffectScriptCommand(RenderEffectScriptCommandType.Unknown, statement.Trim()));
                continue;
            }

            var key = statement[..separator].Trim();
            var value = statement[(separator + 1)..].Trim();
            commands.Add(new RenderEffectScriptCommand(ToScriptCommandType(key), value));
        }

        return commands;
    }

    private static RenderEffectScriptCommandType ToScriptCommandType(string key)
    {
        return NormalizeIdentifier(key) switch
        {
            "RENDERCOLORTARGET" or "RENDERCOLORTARGET0" => RenderEffectScriptCommandType.SetRenderColorTarget0,
            "RENDERCOLORTARGET1" => RenderEffectScriptCommandType.SetRenderColorTarget1,
            "RENDERCOLORTARGET2" => RenderEffectScriptCommandType.SetRenderColorTarget2,
            "RENDERCOLORTARGET3" => RenderEffectScriptCommandType.SetRenderColorTarget3,
            "RENDERDEPTHSTENCILTARGET" => RenderEffectScriptCommandType.SetRenderDepthStencilTarget,
            "CLEARSETCOLOR" => RenderEffectScriptCommandType.ClearSetColor,
            "CLEARSETDEPTH" => RenderEffectScriptCommandType.ClearSetDepth,
            "CLEAR" => RenderEffectScriptCommandType.Clear,
            "SCRIPTEXTERNAL" => RenderEffectScriptCommandType.SetScriptExternal,
            "PASS" => RenderEffectScriptCommandType.ExecutePass,
            "LOOPBYCOUNT" => RenderEffectScriptCommandType.PushLoopCounter,
            "LOOPGETINDEX" => RenderEffectScriptCommandType.GetLoopIndex,
            "LOOPEND" => RenderEffectScriptCommandType.PopLoopCounter,
            "DRAW" => RenderEffectScriptCommandType.Draw,
            _ => RenderEffectScriptCommandType.Unknown
        };
    }

    private static string? FindState(IReadOnlyDictionary<string, string> states, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var state in states)
            {
                if (string.Equals(state.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return state.Value;
                }
            }
        }

        return null;
    }

    private static string? AnnotationString(IReadOnlyDictionary<string, EffectValue> annotations, string name)
    {
        return annotations.TryGetValue(name, out var value)
            ? ValueToString(value)
            : null;
    }

    private static Vector4? AnnotationVector(IReadOnlyDictionary<string, EffectValue> annotations, string name)
    {
        return annotations.TryGetValue(name, out var value) && value is EffectValue.Vector vector
            ? Vector4.Clamp(vector.Value, Vector4.Zero, Vector4.One)
            : null;
    }

    private static float? AnnotationFloat(IReadOnlyDictionary<string, EffectValue> annotations, string name)
    {
        return annotations.TryGetValue(name, out var value)
            ? value switch
            {
                EffectValue.Float floatValue => floatValue.Value,
                EffectValue.Int intValue => intValue.Value,
                EffectValue.Raw rawValue when float.TryParse(
                    rawValue.Value.TrimEnd('f', 'F'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => null
            }
            : null;
    }

    private static IReadOnlyList<RenderEffectOffscreenDefaultEffect> ParseOffscreenDefaultEffects(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var effects = new List<RenderEffectOffscreenDefaultEffect>();
        foreach (var statement in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = statement.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var target = statement[..separator].Trim();
            var effectPath = statement[(separator + 1)..].Trim();
            if (target.Length > 0 && effectPath.Length > 0)
            {
                effects.Add(new RenderEffectOffscreenDefaultEffect(target, effectPath));
            }
        }

        return effects;
    }

    private static string? ValueToString(EffectValue value)
    {
        return value switch
        {
            EffectValue.String stringValue => stringValue.Value,
            EffectValue.Raw rawValue => rawValue.Value,
            EffectValue.Bool boolValue => boolValue.Value ? "true" : "false",
            EffectValue.Int intValue => intValue.Value.ToString(CultureInfo.InvariantCulture),
            EffectValue.Float floatValue => floatValue.Value.ToString(CultureInfo.InvariantCulture),
            EffectValue.Vector vectorValue => VectorToString(vectorValue.Value, vectorValue.ComponentCount),
            _ => null
        };
    }

    private static string VectorToString(Vector4 value, int componentCount)
    {
        var components = new[] { value.X, value.Y, value.Z, value.W };
        return string.Join(
            ",",
            components.Take(Math.Clamp(componentCount, 0, 4)).Select(component => component.ToString(CultureInfo.InvariantCulture)));
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = NormalizeIdentifier(value);
        if (normalized is "TRUE" or "ENABLE" or "ENABLED" or "YES")
        {
            return true;
        }

        if (normalized is "FALSE" or "DISABLE" or "DISABLED" or "NO")
        {
            return false;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
            ? intValue != 0
            : null;
    }

    private static RenderBlendFactor? ParseBlendFactor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue switch
            {
                1 => RenderBlendFactor.Zero,
                2 => RenderBlendFactor.One,
                3 => RenderBlendFactor.SourceColor,
                4 => RenderBlendFactor.InverseSourceColor,
                5 => RenderBlendFactor.SourceAlpha,
                6 => RenderBlendFactor.InverseSourceAlpha,
                7 => RenderBlendFactor.DestinationAlpha,
                8 => RenderBlendFactor.InverseDestinationAlpha,
                9 => RenderBlendFactor.DestinationColor,
                10 => RenderBlendFactor.InverseDestinationColor,
                11 => RenderBlendFactor.SourceAlphaSaturate,
                14 => RenderBlendFactor.BlendColor,
                15 => RenderBlendFactor.InverseBlendColor,
                _ => null
            };
        }

        return NormalizeIdentifier(value) switch
        {
            "ZERO" => RenderBlendFactor.Zero,
            "ONE" => RenderBlendFactor.One,
            "SRCCOLOR" or "SOURCECOLOR" => RenderBlendFactor.SourceColor,
            "INVSRCCOLOR" or "INVERSESOURCECOLOR" => RenderBlendFactor.InverseSourceColor,
            "SRCALPHA" or "SOURCEALPHA" => RenderBlendFactor.SourceAlpha,
            "INVSRCALPHA" or "INVERSESOURCEALPHA" => RenderBlendFactor.InverseSourceAlpha,
            "DESTALPHA" or "DSTALPHA" or "DESTINATIONALPHA" => RenderBlendFactor.DestinationAlpha,
            "INVDESTALPHA" or "INVDSTALPHA" or "INVERSEDESTINATIONALPHA" => RenderBlendFactor.InverseDestinationAlpha,
            "DESTCOLOR" or "DSTCOLOR" or "DESTINATIONCOLOR" => RenderBlendFactor.DestinationColor,
            "INVDESTCOLOR" or "INVDSTCOLOR" or "INVERSEDESTINATIONCOLOR" => RenderBlendFactor.InverseDestinationColor,
            "SRCALPHASAT" or "SOURCEALPHASATURATE" => RenderBlendFactor.SourceAlphaSaturate,
            "BLENDFACTOR" or "CONSTANTCOLOR" => RenderBlendFactor.BlendColor,
            "INVBLENDFACTOR" or "INVERSECONSTANTCOLOR" => RenderBlendFactor.InverseBlendColor,
            _ => null
        };
    }

    private static RenderBlendOperation? ParseBlendOperation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue switch
            {
                1 => RenderBlendOperation.Add,
                2 => RenderBlendOperation.Subtract,
                3 => RenderBlendOperation.ReverseSubtract,
                4 => RenderBlendOperation.Min,
                5 => RenderBlendOperation.Max,
                _ => null
            };
        }

        return NormalizeIdentifier(value) switch
        {
            "ADD" => RenderBlendOperation.Add,
            "SUBTRACT" or "SUB" => RenderBlendOperation.Subtract,
            "REVSUBTRACT" or "REVERSESUBTRACT" => RenderBlendOperation.ReverseSubtract,
            "MIN" => RenderBlendOperation.Min,
            "MAX" => RenderBlendOperation.Max,
            _ => null
        };
    }

    private static RenderCullMode? ParseCullMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue switch
            {
                1 => RenderCullMode.None,
                2 => RenderCullMode.Front,
                3 => RenderCullMode.Back,
                _ => null
            };
        }

        return NormalizeIdentifier(value) switch
        {
            "NONE" or "DISABLE" or "DISABLED" => RenderCullMode.None,
            "CW" or "CLOCKWISE" or "FRONT" => RenderCullMode.Front,
            "CCW" or "COUNTERCLOCKWISE" or "BACK" => RenderCullMode.Back,
            _ => null
        };
    }

    private static RenderCompareFunction? ParseCompareFunction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue switch
            {
                1 => RenderCompareFunction.Never,
                2 => RenderCompareFunction.Less,
                3 => RenderCompareFunction.Equal,
                4 => RenderCompareFunction.LessEqual,
                5 => RenderCompareFunction.Greater,
                6 => RenderCompareFunction.NotEqual,
                7 => RenderCompareFunction.GreaterEqual,
                8 => RenderCompareFunction.Always,
                _ => null
            };
        }

        return NormalizeIdentifier(value) switch
        {
            "NEVER" => RenderCompareFunction.Never,
            "LESS" => RenderCompareFunction.Less,
            "EQUAL" => RenderCompareFunction.Equal,
            "LESSEQUAL" or "LEQUAL" => RenderCompareFunction.LessEqual,
            "GREATER" => RenderCompareFunction.Greater,
            "NOTEQUAL" or "NEVERQUAL" => RenderCompareFunction.NotEqual,
            "GREATEREQUAL" or "GEQUAL" => RenderCompareFunction.GreaterEqual,
            "ALWAYS" => RenderCompareFunction.Always,
            _ => null
        };
    }

    private static EffectValue? ParseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().TrimEnd(';').Trim();
        if (TryParseStringExpression(value, out var stringValue))
        {
            return new EffectValue.String(stringValue);
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return new EffectValue.Bool(boolValue);
        }

        var vectorMatch = VectorValueRegex().Match(value);
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

    private static bool TryParseStringExpression(string value, out string result)
    {
        var builder = new StringBuilder();
        var cursor = 0;
        while (cursor < value.Length)
        {
            while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
            {
                cursor++;
            }

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

    private static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^FLOAT[0-9]X[0-9]$", RegexOptions.Compiled)]
    private static partial Regex MatrixTypeRegex();

    [GeneratedRegex(@"^(?:float|float[234]|int[234])?\s*\((?<values>[^)]*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VectorValueRegex();
}
