using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class ProjectJsonWriter
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public byte[] Write(MmdProject project, string? projectPath = null, ProjectJsonResourceMap? resourceMap = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var baseDirectory = GetProjectDirectory(projectPath);
        var document = new ProjectDocumentDto
        {
            Version = CurrentVersion,
            Name = project.Name,
            Timeline = TimelineDto.From(project.Timeline),
            Camera = CameraDto.From(project.Camera),
            Light = LightDto.From(project.Light),
            Background = BackgroundDto.From(project.Background, baseDirectory),
            Models = project.ModelInstances
                .Select((instance, index) => ModelDto.From(instance, index, baseDirectory, resourceMap))
                .ToList(),
            Accessories = project.Accessories
                .Select((accessory, index) => AccessoryDto.From(accessory, index, baseDirectory, resourceMap))
                .ToList(),
            AccessoryMeshes = project.AccessoryMeshes
                .Select(mesh => AccessoryMeshDto.From(mesh, baseDirectory, resourceMap))
                .ToList(),
            Motions = project.Motions
                .Select((motion, index) => MotionDto.From(motion, index, baseDirectory, resourceMap))
                .ToList()
        };

        return JsonSerializer.SerializeToUtf8Bytes(document, Options);
    }

    public void Write(MmdProject project, Stream stream, string? projectPath = null, ProjectJsonResourceMap? resourceMap = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Write(project, projectPath, resourceMap));
    }

    public void WriteFile(MmdProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        Write(project, stream, path);
    }

    internal static string? ToStoredPath(Uri? source, string? baseDirectory)
    {
        if (source is null)
        {
            return null;
        }

        var value = source.IsAbsoluteUri && source.IsFile ? source.LocalPath : source.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(baseDirectory) && Path.IsPathFullyQualified(value))
        {
            return Path.GetRelativePath(baseDirectory, value);
        }

        return value;
    }

    private static string? GetProjectDirectory(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(projectPath);
        return Path.GetDirectoryName(fullPath);
    }

    private sealed class ProjectDocumentDto
    {
        public int Version { get; set; }

        public string Name { get; set; } = "Untitled";

        public TimelineDto Timeline { get; set; } = new();

        public CameraDto Camera { get; set; } = new();

        public LightDto Light { get; set; } = new();

        public BackgroundDto Background { get; set; } = new();

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

        public static TimelineDto From(Timeline timeline)
        {
            return new TimelineDto
            {
                CurrentFrame = timeline.CurrentFrameIndex,
                Selection = FrameRangeDto.From(timeline.SelectionRange),
                Playback = FrameRangeDto.From(timeline.PlaybackRange),
                Loop = timeline.LoopEnabled
            };
        }
    }

    private sealed class FrameRangeDto
    {
        public int Start { get; set; }

        public int End { get; set; }

        public static FrameRangeDto From(FrameRange range)
        {
            return new FrameRangeDto
            {
                Start = range.Start,
                End = range.End
            };
        }
    }

    private sealed class CameraDto
    {
        public Vector3Dto LookAt { get; set; } = new();

        public Vector3Dto Angle { get; set; } = new();

        public float Distance { get; set; }

        public int FieldOfView { get; set; }

        public bool Perspective { get; set; }

        public string? ParentModel { get; set; }

        public string? ParentBone { get; set; }

        public static CameraDto From(Camera camera)
        {
            return new CameraDto
            {
                LookAt = Vector3Dto.From(camera.LookAt),
                Angle = Vector3Dto.From(camera.Angle),
                Distance = camera.Distance,
                FieldOfView = camera.FieldOfView,
                Perspective = camera.PerspectiveEnabled,
                ParentModel = camera.ParentModelName,
                ParentBone = camera.ParentBoneName
            };
        }
    }

    private sealed class LightDto
    {
        public Vector3Dto Color { get; set; } = new();

        public Vector3Dto Direction { get; set; } = new();

        public static LightDto From(DirectionalLight light)
        {
            return new LightDto
            {
                Color = Vector3Dto.From(light.Color),
                Direction = Vector3Dto.From(light.Direction)
            };
        }
    }

    private sealed class BackgroundDto
    {
        public string? VideoSource { get; set; }

        public bool VideoEnabled { get; set; }

        public int VideoOffsetX { get; set; }

        public int VideoOffsetY { get; set; }

        public float VideoScale { get; set; } = 1f;

        public string? ImageSource { get; set; }

        public bool ImageEnabled { get; set; }

        public int ImageOffsetX { get; set; }

        public int ImageOffsetY { get; set; }

        public float ImageScale { get; set; } = 1f;

        public static BackgroundDto From(SceneBackground background, string? baseDirectory)
        {
            return new BackgroundDto
            {
                VideoSource = ToStoredPath(background.VideoSource, baseDirectory),
                VideoEnabled = background.VideoEnabled,
                VideoOffsetX = background.VideoOffsetX,
                VideoOffsetY = background.VideoOffsetY,
                VideoScale = background.VideoScale,
                ImageSource = ToStoredPath(background.ImageSource, baseDirectory),
                ImageEnabled = background.ImageEnabled,
                ImageOffsetX = background.ImageOffsetX,
                ImageOffsetY = background.ImageOffsetY,
                ImageScale = background.ImageScale
            };
        }
    }

    private sealed class ModelDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public string Format { get; set; } = nameof(ModelFormat.Unknown);

        public bool Visible { get; set; }

        public int DrawOrder { get; set; }

        public int TransformOrder { get; set; }

        public TransformDto Transform { get; set; } = new();

        public Dictionary<string, float>? MorphWeights { get; set; }

        public List<ModelOutsideParentDto>? OutsideParents { get; set; }

        public static ModelDto From(
            ModelInstance instance,
            int order,
            string? baseDirectory,
            ProjectJsonResourceMap? resourceMap)
        {
            var morphWeights = instance.MorphWeights
                .Where(pair => pair.Value.Weight != 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Weight, StringComparer.Ordinal);
            return new ModelDto
            {
                Name = instance.Name,
                Source = resourceMap?.GetModelPath(instance.Model) ?? ToStoredPath(instance.Model.Source, baseDirectory),
                Format = instance.Model.Format.ToString(),
                Visible = instance.Visible,
                DrawOrder = order,
                TransformOrder = instance.TransformOrder,
                Transform = TransformDto.From(instance.Transform),
                MorphWeights = morphWeights.Count == 0 ? null : morphWeights,
                OutsideParents = instance.OutsideParentBindings.Count == 0
                    ? null
                    : instance.OutsideParentBindings.Values.Select(ModelOutsideParentDto.From).ToList()
            };
        }
    }

    private sealed class ModelOutsideParentDto
    {
        public string Bone { get; set; } = string.Empty;

        public string? ParentModel { get; set; }

        public string? ParentBone { get; set; }

        public static ModelOutsideParentDto From(ModelOutsideParentBinding binding)
        {
            return new ModelOutsideParentDto
            {
                Bone = binding.BoneName,
                ParentModel = binding.ParentModelName,
                ParentBone = binding.ParentBoneName
            };
        }
    }

    private sealed class AccessoryDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public bool Visible { get; set; }

        public int DrawOrder { get; set; }

        public TransformDto Transform { get; set; } = new();

        public float Opacity { get; set; }

        public string? ParentModel { get; set; }

        public string? ParentBone { get; set; }

        public static AccessoryDto From(
            Accessory accessory,
            int order,
            string? baseDirectory,
            ProjectJsonResourceMap? resourceMap)
        {
            return new AccessoryDto
            {
                Name = accessory.Name,
                Source = resourceMap?.GetAccessoryPath(accessory) ?? ToStoredPath(accessory.Source, baseDirectory),
                Visible = accessory.Visible,
                DrawOrder = order,
                Transform = TransformDto.From(accessory.Transform),
                Opacity = accessory.Opacity,
                ParentModel = accessory.ParentModelName,
                ParentBone = accessory.ParentBoneName
            };
        }
    }

    private sealed class AccessoryMeshDto
    {
        public string SourceName { get; set; } = string.Empty;

        public string? Source { get; set; }

        public int Meshes { get; set; }

        public int Vertices { get; set; }

        public int Faces { get; set; }

        public static AccessoryMeshDto From(
            AccessoryMeshDocument mesh,
            string? baseDirectory,
            ProjectJsonResourceMap? resourceMap)
        {
            var source = mesh.Source
                ?? (string.IsNullOrWhiteSpace(mesh.SourceName) ? null : new Uri(mesh.SourceName, UriKind.RelativeOrAbsolute));
            return new AccessoryMeshDto
            {
                SourceName = mesh.SourceName,
                Source = resourceMap?.GetAccessoryMeshPath(mesh) ?? ToStoredPath(source, baseDirectory),
                Meshes = mesh.Meshes.Count,
                Vertices = mesh.VertexCount,
                Faces = mesh.FaceCount
            };
        }
    }

    private sealed class MotionDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Source { get; set; }

        public string Format { get; set; } = nameof(MotionFormat.Unknown);

        public int Order { get; set; }

        public int MaxFrame { get; set; }

        public string? Target { get; set; }

        public static MotionDto From(
            Motion motion,
            int order,
            string? baseDirectory,
            ProjectJsonResourceMap? resourceMap)
        {
            return new MotionDto
            {
                Name = motion.Name,
                Source = resourceMap?.GetMotionPath(motion) ?? ToStoredPath(motion.Source, baseDirectory),
                Format = motion.Format.ToString(),
                Order = order,
                MaxFrame = motion.MaxFrameIndex,
                Target = GuessTarget(motion)
            };
        }

        private static string? GuessTarget(Motion motion)
        {
            if (motion.ModelKeyframes.Count > 0 || motion.BoneKeyframes.Count > 0 || motion.MorphKeyframes.Count > 0)
            {
                return "model";
            }

            if (motion.AccessoryKeyframes.Count > 0)
            {
                return "accessory";
            }

            if (motion.CameraKeyframes.Count > 0)
            {
                return "camera";
            }

            if (motion.LightKeyframes.Count > 0)
            {
                return "light";
            }

            if (motion.SelfShadowKeyframes.Count > 0)
            {
                return "selfShadow";
            }

            return null;
        }
    }

    private sealed class TransformDto
    {
        public Vector3Dto Translation { get; set; } = new();

        public Vector3Dto Rotation { get; set; } = new();

        public Vector3Dto Scale { get; set; } = new() { X = 1, Y = 1, Z = 1 };

        public static TransformDto From(SceneTransform transform)
        {
            return new TransformDto
            {
                Translation = Vector3Dto.From(transform.Translation),
                Rotation = Vector3Dto.From(transform.Rotation),
                Scale = Vector3Dto.From(transform.Scale)
            };
        }
    }

    private sealed class Vector3Dto
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public static Vector3Dto From(Vector3 value)
        {
            return new Vector3Dto
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z
            };
        }
    }
}
