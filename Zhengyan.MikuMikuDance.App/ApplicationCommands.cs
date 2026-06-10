using Zhengyan.MikuMikuDance.Core.Diagnostics;
using Zhengyan.MikuMikuDance.Core.Effects;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Mme;
using Zhengyan.MikuMikuDance.Formats.Nmd;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Project;
using Zhengyan.MikuMikuDance.Formats.Pmx;
using Zhengyan.MikuMikuDance.Formats.Vmd;
using Zhengyan.MikuMikuDance.Rendering;
using Zhengyan.MikuMikuDance.Rendering.OpenGL;
using Zhengyan.MikuMikuDance.UI.ImGui;

namespace Zhengyan.MikuMikuDance.App;

internal static class ApplicationCommands
{
    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            PrintHelp();
            return 0;
        }

        return args[0] switch
        {
            "--features" => PrintFeatures(),
            "--inspect" when args.Count >= 2 => Inspect(args[1]),
            "--export-pmm" when args.Count >= 3 => ExportPmm(args[1], args[2]),
            "--export-image" when args.Count >= 3 => ExportImage(args),
            "--pose" when args.Count >= 4 => Pose(args[1], args[2], args[3]),
            "--preview" => Preview(args.Count >= 2 ? args[1] : null, args.Count >= 3 ? args[2] : null),
            "--editor" => Editor(args.Count >= 2 ? args[1] : null),
            "--help" or "-h" => Help(),
            _ => Unknown(args[0])
        };
    }

    private static int Help()
    {
        PrintHelp();
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static int PrintFeatures()
    {
        var catalog = FeatureCatalog.FromNanoemReference();
        PrintGroup("Core formats", catalog.CoreFormats);
        PrintGroup("Scene objects", catalog.SceneObjects);
        PrintGroup("Editing surfaces", catalog.EditingSurfaces);
        PrintGroup("Runtime systems", catalog.RuntimeSystems);
        return 0;
    }

    private static int Inspect(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".pmd":
                var pmdModel = LoadModel(path);
                PrintModel("PMD", pmdModel);
                return 0;
            case ".pmx":
                var model = LoadModel(path);
                PrintModel("PMX", model);
                return 0;
            case ".vmd":
            case ".nmd":
                {
                var motion = LoadMotion(path);
                PrintMotion(extension.TrimStart('.').ToUpperInvariant(), motion);
                }

                return 0;
            case ".zmm":
                using (var stream = File.OpenRead(path))
                {
                var project = new ProjectJsonReader().Read(stream, path, loadResources: false);
                PrintProject(project);
                }

                return 0;
            case ".nma":
                using (var stream = File.OpenRead(path))
                {
                var project = new ProjectArchiveReader().Read(stream, loadResources: false);
                PrintProject(project);
                }

                return 0;
            case ".pmm":
                using (var stream = File.OpenRead(path))
                {
                var project = new PmmLegacyProjectReader().Read(stream, path, loadResources: false);
                PrintProject(project);
                }

                return 0;
            case ".x":
                using (var stream = File.OpenRead(path))
                {
                var accessory = new DirectXAccessoryReader().Read(stream, Path.GetFileName(path));
                Console.WriteLine($"DirectX accessory: {accessory.SourceName}");
                Console.WriteLine($"Meshes: {accessory.Meshes.Count}");
                Console.WriteLine($"Vertices: {accessory.VertexCount}");
                Console.WriteLine($"Faces: {accessory.FaceCount}");
                Console.WriteLine($"Materials: {accessory.MaterialCount}");
                }

                return 0;
            case ".fx":
                using (var stream = File.OpenRead(path))
                {
                var effect = new MmeEffectReader().Read(stream, Path.GetFileName(path));
                PrintEffect(effect);
                }

                return 0;
            default:
                Console.Error.WriteLine($"Unsupported inspect extension: {extension}");
                return 1;
        }
    }

    private static int Preview(string? path, string? motionPath)
    {
        var project = new MmdProject { Name = "Preview" };
        var meshes = new List<RenderMesh>();
        ModelInstance? modelInstance = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: {path}");
                return 1;
            }

            modelInstance = LoadPreviewAsset(path, project, meshes);
        }

        if (!string.IsNullOrWhiteSpace(motionPath) && !File.Exists(motionPath))
        {
            Console.Error.WriteLine($"Motion file not found: {motionPath}");
            return 1;
        }

        using var host = new OpenGlRenderHost(project);
        var textureBaseDirectory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(Path.GetFullPath(path));
        using var renderer = CreatePreviewRenderer(modelInstance, meshes, motionPath, textureBaseDirectory);
        host.Run(renderer);
        return 0;
    }

    private static int Editor(string? projectPath)
    {
        var project = string.IsNullOrWhiteSpace(projectPath)
            ? new MmdProject { Name = "Untitled" }
            : LoadProject(projectPath);
        using var host = new ImGuiEditorHost(project, projectPath);
        host.Run();
        return 0;
    }

    private static int ExportPmm(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        var project = LoadProject(inputPath);
        new PmmLegacyProjectWriter().WriteFile(project, outputPath);
        Console.WriteLine($"Wrote PMM: {outputPath}");
        return 0;
    }

    private static int ExportImage(IReadOnlyList<string> args)
    {
        if (!ImageExportOptions.TryParse(args, Console.Error, out var imageOptions))
        {
            return 1;
        }

        var inputPath = imageOptions.InputPath;
        var outputPath = imageOptions.OutputPath;
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        try
        {
            var (project, renderer) = CreateExportScene(inputPath);
            project.Capture.Clear();
            var options = OpenGlRenderHostOptions.Default with
            {
                Title = "Zhengyan MikuMikuDance Image Export",
                Width = imageOptions.Width,
                Height = imageOptions.Height,
                ClearColor = imageOptions.ClearColor,
                CaptureAndClose = true,
                TransparentFramebuffer = imageOptions.TransparentFramebuffer
            };

            using var host = new OpenGlRenderHost(project, options);
            using (renderer)
            {
                host.Run(renderer);
            }

            var frame = project.Capture.LastFrame;
            if (frame is null)
            {
                Console.Error.WriteLine("Image export failed: no frame was captured.");
                return 1;
            }

            RenderPngWriter.WriteFrame(outputPath, frame);
            Console.WriteLine($"Wrote PNG: {outputPath} ({frame.Width}x{frame.Height})");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Image export failed: {exception.Message}");
            return 1;
        }
    }

    private static int Pose(string modelPath, string motionPath, string frameText)
    {
        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine($"Model file not found: {modelPath}");
            return 1;
        }

        if (!File.Exists(motionPath))
        {
            Console.Error.WriteLine($"Motion file not found: {motionPath}");
            return 1;
        }

        if (!int.TryParse(frameText, out var frameIndex) || frameIndex < 0)
        {
            Console.Error.WriteLine($"Invalid frame index: {frameText}");
            return 1;
        }

        var model = LoadModel(modelPath);
        using var motionStream = File.OpenRead(motionPath);
        var motion = LoadMotion(motionStream, Path.GetExtension(motionPath).ToLowerInvariant());
        var sample = MotionSampler.Sample(motion, frameIndex);
        var animationState = AnimationPoseEvaluator.Evaluate(model, sample);
        var vertices = CpuSkinningProcessor.SkinVertices(model, animationState.Pose, animationState.Morphs);
        var morphedVertexCount = animationState.Morphs.VertexOffsets.Count(offset => offset != System.Numerics.Vector3.Zero);
        var morphedBoneCount = animationState.Morphs.BoneOffsets.Count;

        Console.WriteLine($"Model: {model.Name}");
        Console.WriteLine($"Motion: {motion.Name}");
        Console.WriteLine($"Frame: {sample.FrameIndex}");
        Console.WriteLine($"Sampled bones: {sample.Bones.Count}");
        Console.WriteLine($"Sampled morphs: {sample.Morphs.Count}");
        Console.WriteLine($"Morphed vertices: {morphedVertexCount}");
        Console.WriteLine($"Morphed bones: {morphedBoneCount}");
        Console.WriteLine($"Evaluated bones: {animationState.Pose.Bones.Count}");
        Console.WriteLine($"Skinned vertices: {vertices.Count}");
        if (vertices.Count > 0)
        {
            var first = vertices[0];
            Console.WriteLine($"First vertex: {first.Position.X:0.###}, {first.Position.Y:0.###}, {first.Position.Z:0.###}");
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Zhengyan.MikuMikuDance");
        Console.WriteLine("Usage:");
        Console.WriteLine("  --features              Print nanoem-compatible feature catalog");
        Console.WriteLine("  --inspect <file.pmd|file.pmx|file.vmd|file.nmd|file.x|file.fx|file.zmm|file.nma|file.pmm>");
        Console.WriteLine("  --export-pmm <file.zmm|file.nma|file.pmm> <out.pmm>");
        Console.WriteLine("  --export-image <file.pmd|file.pmx|file.x|file.zmm|file.nma|file.pmm> <out.png> [width] [height] [--transparent] [--clear-color #RRGGBB[AA]]");
        Console.WriteLine("  --pose <file.pmd|file.pmx> <file.vmd|file.nmd> <frame>");
        Console.WriteLine("  --preview [file.pmd|file.pmx|file.x] [file.vmd|file.nmd]");
        Console.WriteLine("  --editor [file.zmm|file.nma|file.pmm]");
    }

    private static void PrintModel(string format, MmdModel model)
    {
        Console.WriteLine($"{format} model: {model.Name}");
        Console.WriteLine($"English name: {model.EnglishName}");
        Console.WriteLine($"Vertices: {model.Vertices.Count}");
        Console.WriteLine($"Indices: {model.Indices.Count}");
        Console.WriteLine($"Textures: {model.Textures.Count}");
        Console.WriteLine($"Materials: {model.Materials.Count}");
        Console.WriteLine($"Bones: {model.Bones.Count}");
        Console.WriteLine($"Morphs: {model.Morphs.Count}");
        Console.WriteLine($"Rigid bodies: {model.RigidBodies.Count}");
        Console.WriteLine($"Joints: {model.Joints.Count}");
    }

    private static void PrintProject(MmdProject project)
    {
        Console.WriteLine($"Project: {project.Name}");
        Console.WriteLine($"Models: {project.ModelInstances.Count}");
        Console.WriteLine($"Accessories: {project.Accessories.Count}");
        Console.WriteLine($"Accessory meshes: {project.AccessoryMeshes.Count}");
        Console.WriteLine($"Motions: {project.Motions.Count}");
        Console.WriteLine($"Current frame: {project.Timeline.CurrentFrameIndex}");
        Console.WriteLine($"Playback range: {project.Timeline.PlaybackRange.Start}-{project.Timeline.PlaybackRange.End}");
        Console.WriteLine($"Loop: {project.Timeline.LoopEnabled}");
        Console.WriteLine($"Duration frames: {project.DurationFrames}");
    }

    private static void PrintMotion(string format, Motion motion)
    {
        Console.WriteLine($"{format} motion: {motion.Name}");
        Console.WriteLine($"Bone keyframes: {motion.BoneKeyframes.Count}");
        Console.WriteLine($"Morph keyframes: {motion.MorphKeyframes.Count}");
        Console.WriteLine($"Camera keyframes: {motion.CameraKeyframes.Count}");
        Console.WriteLine($"Light keyframes: {motion.LightKeyframes.Count}");
        Console.WriteLine($"Model keyframes: {motion.ModelKeyframes.Count}");
        Console.WriteLine($"Accessory keyframes: {motion.AccessoryKeyframes.Count}");
        Console.WriteLine($"Self shadow keyframes: {motion.SelfShadowKeyframes.Count}");
        Console.WriteLine($"Max frame: {motion.MaxFrameIndex}");
    }

    private static void PrintEffect(EffectDocument effect)
    {
        Console.WriteLine($"MME effect: {effect.SourceName}");
        Console.WriteLine($"Parameters: {effect.Parameters.Count}");
        Console.WriteLine($"Techniques: {effect.Techniques.Count}");
        Console.WriteLine($"Passes: {effect.PassCount}");
    }

    private static void PrintGroup(string title, IReadOnlyList<string> items)
    {
        Console.WriteLine(title);
        foreach (var item in items)
        {
            Console.WriteLine($"  - {item}");
        }
    }

    private static IRenderer CreatePreviewRenderer(
        ModelInstance? modelInstance,
        List<RenderMesh> meshes,
        string? motionPath,
        string? textureBaseDirectory)
    {
        if (modelInstance is not null && !string.IsNullOrWhiteSpace(motionPath))
        {
            var motion = LoadMotion(motionPath);
            return new AnimatedModelRenderer(modelInstance, motion, textureBaseDirectory: textureBaseDirectory);
        }

        return new BasicMeshRenderer(meshes, textureBaseDirectory);
    }

    private static (MmdProject Project, IRenderer Renderer) CreateExportScene(string inputPath)
    {
        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension is ".zmm" or ".nma" or ".pmm")
        {
            var project = LoadProject(inputPath);
            var meshes = BuildProjectMeshes(project);
            var textureBaseDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPath));
            return (project, new BasicMeshRenderer(meshes, textureBaseDirectory));
        }

        var previewProject = new MmdProject { Name = "Image Export" };
        var previewMeshes = new List<RenderMesh>();
        LoadPreviewAsset(inputPath, previewProject, previewMeshes);
        var previewTextureBaseDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPath));
        return (previewProject, new BasicMeshRenderer(previewMeshes, previewTextureBaseDirectory));
    }

    private static List<RenderMesh> BuildProjectMeshes(MmdProject project)
    {
        var meshes = new List<RenderMesh>();
        foreach (var target in RenderPicker.CreateTargets(project))
        {
            meshes.AddRange(target.Meshes);
        }

        return meshes;
    }

    private static ModelInstance? LoadPreviewAsset(string path, MmdProject project, List<RenderMesh> meshes)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".pmd":
                var pmdModel = LoadModel(path);
                var pmdInstance = project.AddModel(pmdModel);
                meshes.Add(RenderMeshBuilder.FromModel(pmdInstance));
                return pmdInstance;
            case ".pmx":
                var pmxModel = LoadModel(path);
                var pmxInstance = project.AddModel(pmxModel);
                meshes.Add(RenderMeshBuilder.FromModel(pmxInstance));
                return pmxInstance;
            case ".x":
                using (var stream = File.OpenRead(path))
                {
                var accessoryMesh = new DirectXAccessoryReader().Read(stream, Path.GetFileName(path)) with
                {
                    Source = new Uri(Path.GetFullPath(path))
                };
                project.AddAccessoryMesh(accessoryMesh);
                var accessory = new Accessory(Path.GetFileNameWithoutExtension(path))
                {
                    Source = new Uri(Path.GetFullPath(path))
                };
                project.AddAccessory(accessory);
                meshes.AddRange(RenderMeshBuilder.FromAccessory(accessoryMesh, accessory));
                }

                return null;
            default:
                throw new InvalidOperationException($"Unsupported preview extension: {extension}");
        }
    }

    private static MmdModel LoadModel(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        using var stream = File.OpenRead(path);
        var model = extension switch
        {
            ".pmd" => new PmdModelReader().Read(stream),
            ".pmx" => new PmxModelReader().Read(stream),
            _ => throw new InvalidOperationException($"Unsupported model extension: {extension}")
        };
        model.Source = new Uri(Path.GetFullPath(path));
        return model;
    }

    private static Motion LoadMotion(string path)
    {
        using var stream = File.OpenRead(path);
        var motion = LoadMotion(stream, Path.GetExtension(path).ToLowerInvariant());
        motion.Source = new Uri(Path.GetFullPath(path));
        return motion;
    }

    private static Motion LoadMotion(Stream stream, string extension)
    {
        return extension switch
        {
            ".vmd" => new VmdMotionReader().Read(stream),
            ".nmd" => new NmdMotionReader().Read(stream),
            _ => throw new InvalidOperationException($"Unsupported motion extension: {extension}")
        };
    }

    private static MmdProject LoadProject(string path)
    {
        using var stream = File.OpenRead(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".zmm" => new ProjectJsonReader().Read(stream, path, loadResources: true),
            ".nma" => new ProjectArchiveReader().Read(stream, loadResources: true),
            ".pmm" => new PmmLegacyProjectReader().Read(stream, path, loadResources: true),
            _ => throw new InvalidOperationException($"Unsupported project extension: {Path.GetExtension(path)}")
        };
    }
}
