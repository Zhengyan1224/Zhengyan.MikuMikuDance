using System.IO.Compression;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Nmd;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Pmx;
using Zhengyan.MikuMikuDance.Formats.Vmd;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class ProjectArchiveWriter
{
    public const string ManifestEntryName = "manifest.zmm";

    public byte[] Write(MmdProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var stream = new MemoryStream();
        Write(project, stream);
        return stream.ToArray();
    }

    public void Write(MmdProject project, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stream);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var resourceMap = new ProjectJsonResourceMap();
        AddModels(archive, project, resourceMap);
        AddAccessoryMeshes(archive, project, resourceMap);
        AddAccessories(project, resourceMap);
        AddMotions(archive, project, resourceMap);
        AddEntry(archive, ManifestEntryName, new ProjectJsonWriter().Write(project, resourceMap: resourceMap));
    }

    public void WriteFile(MmdProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        Write(project, stream);
    }

    private static void AddModels(
        ZipArchive archive,
        MmdProject project,
        ProjectJsonResourceMap resourceMap)
    {
        foreach (var pair in project.ModelInstances.Select((instance, index) => (instance, index)))
        {
            var model = pair.instance.Model;
            var extension = ModelExtension(model.Format);
            if (extension is null)
            {
                continue;
            }

            var entryName = UniqueEntryName(
                "Model",
                pair.index,
                pair.instance.Name,
                extension);
            var bytes = ReadSourceOrSerialize(model.Source, () => SerializeModel(model));
            if (bytes is null)
            {
                continue;
            }

            AddEntry(archive, entryName, bytes);
            resourceMap.SetModelPath(model, entryName);
        }
    }

    private static void AddAccessoryMeshes(
        ZipArchive archive,
        MmdProject project,
        ProjectJsonResourceMap resourceMap)
    {
        foreach (var pair in project.AccessoryMeshes.Select((mesh, index) => (mesh, index)))
        {
            var entryName = UniqueEntryName("Accessory", pair.index, pair.mesh.SourceName, ".x");
            var bytes = ReadSourceOrSerialize(pair.mesh.Source, () => new DirectXAccessoryWriter().Write(pair.mesh));
            if (bytes is null)
            {
                continue;
            }

            AddEntry(archive, entryName, bytes);
            resourceMap.SetAccessoryMeshPath(pair.mesh, entryName);
        }
    }

    private static void AddAccessories(MmdProject project, ProjectJsonResourceMap resourceMap)
    {
        foreach (var pair in project.Accessories.Select((accessory, index) => (accessory, index)))
        {
            var sourceName = Path.GetFileName(pair.accessory.Source?.ToString());
            var entryName = UniqueEntryName("Accessory", pair.index, string.IsNullOrWhiteSpace(sourceName) ? pair.accessory.Name : sourceName, ".x");
            resourceMap.SetAccessoryPath(pair.accessory, entryName);
        }
    }

    private static void AddMotions(
        ZipArchive archive,
        MmdProject project,
        ProjectJsonResourceMap resourceMap)
    {
        foreach (var pair in project.Motions.Select((motion, index) => (motion, index)))
        {
            var extension = MotionExtension(pair.motion.Format);
            if (extension is null)
            {
                continue;
            }

            var entryName = UniqueEntryName("Motion", pair.index, pair.motion.Name, extension);
            var bytes = ReadSourceOrSerialize(pair.motion.Source, () => SerializeMotion(pair.motion));
            if (bytes is null)
            {
                continue;
            }

            AddEntry(archive, entryName, bytes);
            resourceMap.SetMotionPath(pair.motion, entryName);
        }
    }

    private static byte[]? ReadSourceOrSerialize(Uri? source, Func<byte[]> serialize)
    {
        if (source is not null && source.IsAbsoluteUri && source.IsFile && File.Exists(source.LocalPath))
        {
            return File.ReadAllBytes(source.LocalPath);
        }

        return serialize();
    }

    private static byte[] SerializeModel(MmdModel model)
    {
        return model.Format switch
        {
            ModelFormat.Pmd => new PmdModelWriter().Write(model),
            ModelFormat.Pmx => new PmxModelWriter().Write(model),
            _ => []
        };
    }

    private static byte[] SerializeMotion(Motion motion)
    {
        return motion.Format switch
        {
            MotionFormat.Vmd => new VmdMotionWriter().Write(motion),
            MotionFormat.Nmd => new NmdMotionWriter().Write(motion),
            _ => []
        };
    }

    private static void AddEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string UniqueEntryName(string prefix, int index, string name, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = prefix;
        }

        return $"{prefix}/{index:D4}-{Sanitize(baseName)}{extension}";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static string? ModelExtension(ModelFormat format)
    {
        return format switch
        {
            ModelFormat.Pmd => ".pmd",
            ModelFormat.Pmx => ".pmx",
            _ => null
        };
    }

    private static string? MotionExtension(MotionFormat format)
    {
        return format switch
        {
            MotionFormat.Vmd => ".vmd",
            MotionFormat.Nmd => ".nmd",
            _ => null
        };
    }
}
