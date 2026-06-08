using System.IO.Compression;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class ProjectArchiveReader
{
    public MmdProject Read(ReadOnlyMemory<byte> data, bool loadResources = true)
    {
        using var stream = new MemoryStream(data.ToArray());
        return Read(stream, loadResources);
    }

    public MmdProject Read(Stream stream, bool loadResources = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ProjectArchiveWriter.ManifestEntryName)
            ?? archive.GetEntry("project.zmm")
            ?? throw new MmdFormatException("Project archive manifest was not found.");
        using var manifestStream = manifestEntry.Open();
        using var manifest = new MemoryStream();
        manifestStream.CopyTo(manifest);
        return new ProjectJsonReader().Read(
            manifest.ToArray(),
            projectPath: null,
            loadResources,
            source => OpenEntry(archive, source));
    }

    public MmdProject ReadFile(string path, bool loadResources = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Read(stream, loadResources);
    }

    private static Stream? OpenEntry(ZipArchive archive, string source)
    {
        var normalized = source.Replace('\\', '/');
        var entry = archive.GetEntry(normalized);
        if (entry is null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        var copy = new MemoryStream();
        entryStream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }
}
