using System.Numerics;
using System.Text.Json;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Nmd;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Pmx;
using Zhengyan.MikuMikuDance.Formats.Vmd;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class ProjectJsonReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MmdProject Read(
        ReadOnlyMemory<byte> data,
        string? projectPath = null,
        bool loadResources = true,
        ProjectJsonResourceResolver? resourceResolver = null)
    {
        var document = JsonSerializer.Deserialize<ProjectDocumentDto>(data.Span, Options)
            ?? throw new MmdFormatException("Invalid project JSON document.");
        if (document.Version <= 0 || document.Version > ProjectJsonWriter.CurrentVersion)
        {
            throw new MmdFormatException($"Unsupported project JSON version {document.Version}.");
        }

        var baseDirectory = GetProjectDirectory(projectPath);
        var project = new MmdProject
        {
            Name = string.IsNullOrWhiteSpace(document.Name) ? "Untitled" : document.Name
        };
        ApplyTimeline(project.Timeline, document.Timeline);
        ApplyCamera(project.Camera, document.Camera);
        ApplyLight(project.Light, document.Light);
        foreach (var model in document.Models.OrderBy(item => item.TransformOrder).ThenBy(item => item.DrawOrder))
        {
            AddModel(project, model, baseDirectory, loadResources, resourceResolver);
        }

        foreach (var mesh in document.AccessoryMeshes)
        {
            AddAccessoryMesh(project, mesh, baseDirectory, loadResources, resourceResolver);
        }

        foreach (var accessory in document.Accessories.OrderBy(item => item.DrawOrder))
        {
            AddAccessory(project, accessory, baseDirectory, resourceResolver);
        }

        foreach (var motion in document.Motions.OrderBy(item => item.Order))
        {
            AddMotion(project, motion, baseDirectory, loadResources, resourceResolver);
        }

        return project;
    }

    public MmdProject Read(
        Stream stream,
        string? projectPath = null,
        bool loadResources = true,
        ProjectJsonResourceResolver? resourceResolver = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray(), projectPath, loadResources, resourceResolver);
    }

    public MmdProject ReadFile(string path, bool loadResources = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Read(stream, path, loadResources);
    }

    private static void ApplyTimeline(Timeline timeline, TimelineDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        timeline.Seek(dto.CurrentFrame);
        timeline.SetSelectionRange(dto.Selection.Start, dto.Selection.End);
        timeline.SetPlaybackRange(dto.Playback.Start, dto.Playback.End);
        timeline.LoopEnabled = dto.Loop;
    }

    private static void ApplyCamera(Camera camera, CameraDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        camera.LookAt = dto.LookAt.ToVector3();
        camera.Angle = dto.Angle.ToVector3();
        camera.Distance = dto.Distance;
        camera.FieldOfView = dto.FieldOfView;
        camera.PerspectiveEnabled = dto.Perspective;
    }

    private static void ApplyLight(DirectionalLight light, LightDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        light.Color = dto.Color.ToVector3();
        light.Direction = dto.Direction.ToVector3();
    }

    private static void AddModel(
        MmdProject project,
        ModelDto dto,
        string? baseDirectory,
        bool loadResources,
        ProjectJsonResourceResolver? resourceResolver)
    {
        var resolvedPath = resourceResolver is null ? ResolvePath(dto.Source, baseDirectory) : dto.Source;
        var model = TryLoadModel(dto.Source, resolvedPath, loadResources, resourceResolver)
            ?? new MmdModel(ParseEnum(dto.Format, ModelFormat.Unknown))
            {
                Name = dto.Name
            };
        model.Source = CreateSourceUri(resolvedPath, dto.Source);
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = dto.Name;
        }

        var instance = project.AddModel(model);
        instance.Name = string.IsNullOrWhiteSpace(dto.Name) ? model.Name : dto.Name;
        instance.Visible = dto.Visible;
        ApplyTransform(instance.Transform, dto.Transform);
    }

    private static void AddAccessoryMesh(
        MmdProject project,
        AccessoryMeshDto dto,
        string? baseDirectory,
        bool loadResources,
        ProjectJsonResourceResolver? resourceResolver)
    {
        var resolvedPath = resourceResolver is null ? ResolvePath(dto.Source, baseDirectory) : dto.Source;
        AccessoryMeshDocument? document = null;
        if (loadResources && !string.IsNullOrWhiteSpace(dto.Source))
        {
            using var stream = OpenResource(dto.Source, resolvedPath, resourceResolver);
            if (stream is not null)
            {
                document = new DirectXAccessoryReader().Read(stream, Path.GetFileName(dto.Source));
            }
        }

        document ??= new AccessoryMeshDocument(dto.SourceName, []);
        document = document with
        {
            Source = CreateSourceUri(resolvedPath, dto.Source)
        };
        project.AddAccessoryMesh(document);
    }

    private static void AddAccessory(
        MmdProject project,
        AccessoryDto dto,
        string? baseDirectory,
        ProjectJsonResourceResolver? resourceResolver)
    {
        var resolvedPath = resourceResolver is null ? ResolvePath(dto.Source, baseDirectory) : dto.Source;
        var accessory = new Accessory(dto.Name)
        {
            Source = CreateSourceUri(resolvedPath, dto.Source),
            Visible = dto.Visible,
            Opacity = dto.Opacity,
            ParentModelName = dto.ParentModel,
            ParentBoneName = dto.ParentBone
        };
        ApplyTransform(accessory.Transform, dto.Transform);
        project.AddAccessory(accessory);
    }

    private static void AddMotion(
        MmdProject project,
        MotionDto dto,
        string? baseDirectory,
        bool loadResources,
        ProjectJsonResourceResolver? resourceResolver)
    {
        var resolvedPath = resourceResolver is null ? ResolvePath(dto.Source, baseDirectory) : dto.Source;
        var motion = TryLoadMotion(dto.Source, resolvedPath, loadResources, resourceResolver)
            ?? new Motion(dto.Name, ParseEnum(dto.Format, MotionFormat.Unknown));
        motion.Source = CreateSourceUri(resolvedPath, dto.Source);
        if (string.IsNullOrWhiteSpace(motion.Name))
        {
            motion.Name = dto.Name;
        }

        project.AddMotion(motion);
    }

    private static MmdModel? TryLoadModel(
        string? source,
        string? resolvedPath,
        bool loadResources,
        ProjectJsonResourceResolver? resourceResolver)
    {
        if (!loadResources || string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        using var stream = OpenResource(source, resolvedPath, resourceResolver);
        if (stream is null)
        {
            return null;
        }

        return Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".pmd" => new PmdModelReader().Read(stream),
            ".pmx" => new PmxModelReader().Read(stream),
            _ => null
        };
    }

    private static Motion? TryLoadMotion(
        string? source,
        string? resolvedPath,
        bool loadResources,
        ProjectJsonResourceResolver? resourceResolver)
    {
        if (!loadResources || string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        using var stream = OpenResource(source, resolvedPath, resourceResolver);
        if (stream is null)
        {
            return null;
        }

        return Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".vmd" => new VmdMotionReader().Read(stream),
            ".nmd" => new NmdMotionReader().Read(stream),
            _ => null
        };
    }

    private static Stream? OpenResource(
        string source,
        string? resolvedPath,
        ProjectJsonResourceResolver? resourceResolver)
    {
        if (resourceResolver is not null)
        {
            var resolved = resourceResolver(source);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath)
            ? File.OpenRead(resolvedPath)
            : null;
    }

    private static void ApplyTransform(SceneTransform transform, TransformDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        transform.Translation = dto.Translation.ToVector3();
        transform.Rotation = dto.Rotation.ToVector3();
        transform.Scale = dto.Scale.ToVector3();
    }

    private static string? ResolvePath(string? source, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var absolute))
        {
            return absolute.IsFile ? absolute.LocalPath : source;
        }

        if (Path.IsPathFullyQualified(source))
        {
            return Path.GetFullPath(source);
        }

        return string.IsNullOrWhiteSpace(baseDirectory)
            ? source
            : Path.GetFullPath(Path.Combine(baseDirectory, source));
    }

    private static Uri? CreateSourceUri(string? resolvedPath, string? originalSource)
    {
        if (!string.IsNullOrWhiteSpace(resolvedPath) && Path.IsPathFullyQualified(resolvedPath))
        {
            return new Uri(Path.GetFullPath(resolvedPath));
        }

        if (!string.IsNullOrWhiteSpace(originalSource))
        {
            return new Uri(originalSource, UriKind.RelativeOrAbsolute);
        }

        return null;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : fallback;
    }

    private static string? GetProjectDirectory(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        return Path.GetDirectoryName(Path.GetFullPath(projectPath));
    }

    private sealed class ProjectDocumentDto
    {
        public int Version { get; set; }

        public string Name { get; set; } = "Untitled";

        public TimelineDto? Timeline { get; set; }

        public CameraDto? Camera { get; set; }

        public LightDto? Light { get; set; }

        public List<ModelDto> Models { get; set; } = [];

        public List<AccessoryDto> Accessories { get; set; } = [];

        public List<AccessoryMeshDto> AccessoryMeshes { get; set; } = [];

        public List<MotionDto> Motions { get; set; } = [];
    }

    private sealed class TimelineDto
    {
        public int CurrentFrame { get; set; }

        public FrameRangeDto Selection { get; set; } = new();

        public FrameRangeDto Playback { get; set; } = new();

        public bool Loop { get; set; }
    }

    private sealed class FrameRangeDto
    {
        public int Start { get; set; }

        public int End { get; set; }
    }

    private sealed class CameraDto
    {
        public Vector3Dto LookAt { get; set; } = new();

        public Vector3Dto Angle { get; set; } = new();

        public float Distance { get; set; }

        public int FieldOfView { get; set; }

        public bool Perspective { get; set; } = true;
    }

    private sealed class LightDto
    {
        public Vector3Dto Color { get; set; } = new();

        public Vector3Dto Direction { get; set; } = new();
    }

    private sealed class ModelDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public string Format { get; set; } = nameof(ModelFormat.Unknown);

        public bool Visible { get; set; } = true;

        public int DrawOrder { get; set; }

        public int TransformOrder { get; set; }

        public TransformDto? Transform { get; set; }
    }

    private sealed class AccessoryDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public bool Visible { get; set; } = true;

        public int DrawOrder { get; set; }

        public TransformDto? Transform { get; set; }

        public float Opacity { get; set; } = 1;

        public string? ParentModel { get; set; }

        public string? ParentBone { get; set; }
    }

    private sealed class AccessoryMeshDto
    {
        public string SourceName { get; set; } = string.Empty;

        public string? Source { get; set; }
    }

    private sealed class MotionDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public string Format { get; set; } = nameof(MotionFormat.Unknown);

        public int Order { get; set; }
    }

    private sealed class TransformDto
    {
        public Vector3Dto Translation { get; set; } = new();

        public Vector3Dto Rotation { get; set; } = new();

        public Vector3Dto Scale { get; set; } = new() { X = 1, Y = 1, Z = 1 };
    }

    private sealed class Vector3Dto
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }
}
