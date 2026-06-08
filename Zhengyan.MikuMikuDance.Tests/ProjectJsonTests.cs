using System.Numerics;
using System.IO.Compression;
using System.Text.Json;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Project;
using Zhengyan.MikuMikuDance.Formats.Vmd;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class ProjectJsonTests
{
    [Fact]
    public void WritesAndReadsProjectSceneState()
    {
        var project = new MmdProject { Name = "SceneState" };
        project.Timeline.Seek(24);
        project.Timeline.SetSelectionRange(40, 10);
        project.Timeline.SetPlaybackRange(0, 120);
        project.Timeline.LoopEnabled = true;
        project.Camera.LookAt = new Vector3(1, 2, 3);
        project.Camera.Angle = new Vector3(0.1f, 0.2f, 0.3f);
        project.Camera.Distance = 35;
        project.Camera.FieldOfView = 42;
        project.Camera.PerspectiveEnabled = false;
        project.Light.Color = new Vector3(0.3f, 0.4f, 0.5f);
        project.Light.Direction = new Vector3(-1, -2, -3);

        var model = new MmdModel(ModelFormat.Pmx)
        {
            Name = "Miku",
            Source = new Uri(@"models\miku.pmx", UriKind.Relative)
        };
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";
        instance.Visible = false;
        instance.Transform.Translation = new Vector3(4, 5, 6);
        instance.Transform.Rotation = new Vector3(0.4f, 0.5f, 0.6f);
        instance.Transform.Scale = new Vector3(1.5f);

        var accessory = new Accessory("stage")
        {
            Source = new Uri(@"stage\stage.x", UriKind.Relative),
            Visible = false,
            Opacity = 0.25f,
            ParentModelName = "MikuInstance",
            ParentBoneName = "center"
        };
        accessory.Transform.Translation = new Vector3(7, 8, 9);
        accessory.Transform.Scale = new Vector3(2);
        project.AddAccessory(accessory);

        var motion = new Motion("Motion", MotionFormat.Vmd)
        {
            Source = new Uri(@"motions\walk.vmd", UriKind.Relative)
        };
        motion.Add(new BoneKeyframe("center", 30, Vector3.One, Quaternion.Identity, BoneInterpolation.Linear));
        project.AddMotion(motion);

        var data = new ProjectJsonWriter().Write(project);
        var decoded = new ProjectJsonReader().Read(data, loadResources: false);

        Assert.Equal("SceneState", decoded.Name);
        Assert.Equal(24, decoded.Timeline.CurrentFrameIndex);
        Assert.Equal(10, decoded.Timeline.SelectionRange.Start);
        Assert.Equal(40, decoded.Timeline.SelectionRange.End);
        Assert.Equal(120, decoded.Timeline.PlaybackRange.End);
        Assert.True(decoded.Timeline.LoopEnabled);
        Assert.Equal(new Vector3(1, 2, 3), decoded.Camera.LookAt);
        Assert.Equal(35, decoded.Camera.Distance);
        Assert.Equal(42, decoded.Camera.FieldOfView);
        Assert.False(decoded.Camera.PerspectiveEnabled);
        Assert.Equal(new Vector3(0.3f, 0.4f, 0.5f), decoded.Light.Color);
        Assert.Single(decoded.ModelInstances);
        Assert.Equal("MikuInstance", decoded.ModelInstances[0].Name);
        Assert.False(decoded.ModelInstances[0].Visible);
        Assert.Equal(new Vector3(4, 5, 6), decoded.ModelInstances[0].Transform.Translation);
        Assert.Equal(ModelFormat.Pmx, decoded.Models[0].Format);
        Assert.Single(decoded.Accessories);
        Assert.Equal("stage", decoded.Accessories[0].Name);
        Assert.Equal("center", decoded.Accessories[0].ParentBoneName);
        Assert.Equal(0.25f, decoded.Accessories[0].Opacity, precision: 3);
        Assert.Single(decoded.Motions);
        Assert.Equal("Motion", decoded.Motions[0].Name);
        Assert.Equal(MotionFormat.Vmd, decoded.Motions[0].Format);
    }

    [Fact]
    public void ReadsProjectResourcesRelativeToProjectFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "zmm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var motion = new Motion("RelativeMotion", MotionFormat.Vmd);
            motion.Add(new MorphKeyframe("smile", 9, 0.75f));
            var motionPath = Path.Combine(root, "walk.vmd");
            File.WriteAllBytes(motionPath, new VmdMotionWriter().Write(motion));

            var project = new MmdProject { Name = "ResourceProject" };
            motion.Source = new Uri(motionPath);
            project.AddMotion(motion);
            var projectPath = Path.Combine(root, "scene.zmm");
            new ProjectJsonWriter().WriteFile(project, projectPath);

            var json = File.ReadAllText(projectPath);
            using var document = JsonDocument.Parse(json);
            var storedSource = document.RootElement
                .GetProperty("Motions")[0]
                .GetProperty("Source")
                .GetString();
            Assert.Equal("walk.vmd", storedSource);

            var decoded = new ProjectJsonReader().ReadFile(projectPath);

            Assert.Single(decoded.Motions);
            Assert.Equal("RelativeMotion", decoded.Motions[0].Name);
            Assert.Single(decoded.Motions[0].MorphKeyframes);
            Assert.Equal("smile", decoded.Motions[0].MorphKeyframes[0].MorphName);
            Assert.Equal(Path.GetFullPath(motionPath), decoded.Motions[0].Source!.LocalPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void WritesAndReadsProjectArchiveResources()
    {
        var accessoryMesh = new AccessoryMesh(
            "StageMesh",
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [new AccessoryFace([0, 1, 2])],
            [Vector3.UnitZ],
            [Vector2.Zero],
            [],
            []);
        var project = new MmdProject { Name = "ArchiveProject" };
        var accessoryDocument = new AccessoryMeshDocument("stage.x", [accessoryMesh]);
        project.AddAccessoryMesh(accessoryDocument);
        project.AddAccessory(new Accessory("stage")
        {
            Source = new Uri("stage.x", UriKind.Relative),
            Opacity = 0.5f
        });
        var motion = new Motion("Walk", MotionFormat.Vmd);
        motion.Add(new MorphKeyframe("smile", 12, 0.8f));
        project.AddMotion(motion);

        var data = new ProjectArchiveWriter().Write(project);
        using (var archive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
        {
            Assert.NotNull(archive.GetEntry(ProjectArchiveWriter.ManifestEntryName));
            Assert.NotNull(archive.GetEntry("Accessory/0000-stage.x"));
            Assert.NotNull(archive.GetEntry("Motion/0000-Walk.vmd"));
        }

        var decoded = new ProjectArchiveReader().Read(data);

        Assert.Equal("ArchiveProject", decoded.Name);
        Assert.Single(decoded.AccessoryMeshes);
        Assert.Equal(3, decoded.AccessoryMeshes[0].VertexCount);
        Assert.Equal("Accessory/0000-stage.x", decoded.AccessoryMeshes[0].Source!.ToString());
        Assert.Single(decoded.Accessories);
        Assert.Equal(0.5f, decoded.Accessories[0].Opacity, precision: 3);
        Assert.Equal("Accessory/0000-stage.x", decoded.Accessories[0].Source!.ToString());
        Assert.Single(decoded.Motions);
        Assert.Equal("Walk", decoded.Motions[0].Name);
        Assert.Single(decoded.Motions[0].MorphKeyframes);
        Assert.Equal("Motion/0000-Walk.vmd", decoded.Motions[0].Source!.ToString());
    }
}
