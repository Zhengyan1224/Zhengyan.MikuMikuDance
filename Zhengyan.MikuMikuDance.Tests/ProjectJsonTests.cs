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
        project.Camera.ParentModelName = "MikuInstance";
        project.Camera.ParentBoneName = "center";
        project.Light.Color = new Vector3(0.3f, 0.4f, 0.5f);
        project.Light.Direction = new Vector3(-1, -2, -3);
        project.ColorTransform.Brightness = 0.1f;
        project.ColorTransform.Contrast = 1.25f;
        project.ColorTransform.Saturation = 0.75f;
        project.ColorTransform.Gamma = 2.2f;
        project.Background.VideoSource = new Uri(@"videos\background.avi", UriKind.Relative);
        project.Background.VideoEnabled = true;
        project.Background.VideoOffsetX = -20;
        project.Background.VideoOffsetY = 6;
        project.Background.VideoScale = 0.75f;
        project.Background.ImageSource = new Uri(@"images\background.png", UriKind.Relative);
        project.Background.ImageEnabled = true;
        project.Background.ImageOffsetX = 12;
        project.Background.ImageOffsetY = -8;
        project.Background.ImageScale = 1.25f;
        project.Background.ImageOpacity = 0.6f;
        project.Background.ImageLayoutMode = BackgroundImageLayoutMode.Fill;

        var model = new MmdModel(ModelFormat.Pmx)
        {
            Name = "Miku",
            Source = new Uri(@"models\miku.pmx", UriKind.Relative)
        };
        model.AddBone(new Bone(
            "center",
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled | BoneFlags.OutsideParent,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        model.AddMorph(new Morph("smile", string.Empty, MorphCategory.Lip, MorphType.Vertex, []));
        var instance = project.AddModel(model);
        instance.Name = "MikuInstance";
        instance.Visible = false;
        instance.Transform.Translation = new Vector3(4, 5, 6);
        instance.Transform.Rotation = new Vector3(0.4f, 0.5f, 0.6f);
        instance.Transform.Scale = new Vector3(1.5f);
        instance.SetMorphWeight("smile", 0.75f);
        instance.EffectParameterOverrides.SetBool("UseShadow", true);
        instance.EffectParameterOverrides.SetFloat("AlphaScale", 0.6f);
        instance.EffectParameterOverrides.SetVector4("TintColor", new Vector4(1, 0.5f, 0.25f, 1));
        Assert.True(ModelOutsideParentBindingEditor.TrySetParent(
            instance,
            project,
            "center",
            "MikuInstance",
            "center"));

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
        accessory.EffectParameterOverrides.SetInt("Mode", 3);
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
        Assert.Equal("MikuInstance", decoded.Camera.ParentModelName);
        Assert.Equal("center", decoded.Camera.ParentBoneName);
        Assert.Equal(new Vector3(0.3f, 0.4f, 0.5f), decoded.Light.Color);
        Assert.Equal(0.1f, decoded.ColorTransform.Brightness, precision: 3);
        Assert.Equal(1.25f, decoded.ColorTransform.Contrast, precision: 3);
        Assert.Equal(0.75f, decoded.ColorTransform.Saturation, precision: 3);
        Assert.Equal(2.2f, decoded.ColorTransform.Gamma, precision: 3);
        Assert.True(decoded.Background.VideoEnabled);
        Assert.Equal(@"videos\background.avi", decoded.Background.VideoSource!.ToString());
        Assert.Equal(-20, decoded.Background.VideoOffsetX);
        Assert.Equal(6, decoded.Background.VideoOffsetY);
        Assert.Equal(0.75f, decoded.Background.VideoScale, precision: 3);
        Assert.True(decoded.Background.ImageEnabled);
        Assert.Equal(@"images\background.png", decoded.Background.ImageSource!.ToString());
        Assert.Equal(12, decoded.Background.ImageOffsetX);
        Assert.Equal(-8, decoded.Background.ImageOffsetY);
        Assert.Equal(1.25f, decoded.Background.ImageScale, precision: 3);
        Assert.Equal(0.6f, decoded.Background.ImageOpacity, precision: 3);
        Assert.Equal(BackgroundImageLayoutMode.Fill, decoded.Background.ImageLayoutMode);
        Assert.Single(decoded.ModelInstances);
        Assert.Equal("MikuInstance", decoded.ModelInstances[0].Name);
        Assert.False(decoded.ModelInstances[0].Visible);
        Assert.Equal(new Vector3(4, 5, 6), decoded.ModelInstances[0].Transform.Translation);
        Assert.Equal(0.75f, decoded.ModelInstances[0].GetMorphWeight("smile"), precision: 3);
        Assert.True(decoded.ModelInstances[0].EffectParameterOverrides.TryGetValue("UseShadow", out var useShadow));
        Assert.True(Assert.IsType<MotionEffectParameterValue.Bool>(useShadow).Value);
        Assert.True(decoded.ModelInstances[0].EffectParameterOverrides.TryGetValue("AlphaScale", out var alphaScale));
        Assert.Equal(0.6f, Assert.IsType<MotionEffectParameterValue.Float>(alphaScale).Value, precision: 3);
        Assert.True(decoded.ModelInstances[0].EffectParameterOverrides.TryGetValue("TintColor", out var tintColor));
        Assert.Equal(new Vector4(1, 0.5f, 0.25f, 1), Assert.IsType<MotionEffectParameterValue.Vector4>(tintColor).Value);
        var outsideParent = decoded.ModelInstances[0].GetOutsideParentBinding("center");
        Assert.NotNull(outsideParent);
        Assert.Equal("MikuInstance", outsideParent.ParentModelName);
        Assert.Equal("center", outsideParent.ParentBoneName);
        Assert.Equal(ModelFormat.Pmx, decoded.Models[0].Format);
        Assert.Single(decoded.Accessories);
        Assert.Equal("stage", decoded.Accessories[0].Name);
        Assert.Equal("center", decoded.Accessories[0].ParentBoneName);
        Assert.Equal(0.25f, decoded.Accessories[0].Opacity, precision: 3);
        Assert.True(decoded.Accessories[0].EffectParameterOverrides.TryGetValue("Mode", out var mode));
        Assert.Equal(3, Assert.IsType<MotionEffectParameterValue.Int>(mode).Value);
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
    public void WritesAndReadsIndependentModelTransformOrder()
    {
        var project = new MmdProject { Name = "Ordering" };
        var first = project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "first" });
        first.Name = "first";
        first.TransformOrder = 1;
        var second = project.AddModel(new MmdModel(ModelFormat.Pmx) { Name = "second" });
        second.Name = "second";
        second.TransformOrder = 0;

        var data = new ProjectJsonWriter().Write(project);
        var decoded = new ProjectJsonReader().Read(data, loadResources: false);

        Assert.Equal(["first", "second"], decoded.ModelInstances.Select(model => model.Name).ToArray());
        Assert.Equal(["second", "first"], decoded.GetModelsByTransformOrder().Select(model => model.Name).ToArray());
    }

    [Fact]
    public void ReadsOutsideParentBindingToLaterModel()
    {
        var project = new MmdProject { Name = "OutsideParent" };
        var first = project.AddModel(CreateOutsideParentModel("subject"));
        first.Name = "subject";
        var parent = project.AddModel(CreateOutsideParentModel("parent"));
        parent.Name = "parent";

        Assert.True(ModelOutsideParentBindingEditor.TrySetParent(
            first,
            project,
            "center",
            "parent",
            "center"));

        var data = new ProjectJsonWriter().Write(project);
        var decoded = new ProjectJsonReader().Read(data, loadResources: false);

        var binding = decoded.ModelInstances[0].GetOutsideParentBinding("center");
        Assert.NotNull(binding);
        Assert.Equal("parent", binding.ParentModelName);
        Assert.Equal("center", binding.ParentBoneName);
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

    private static MmdModel CreateOutsideParentModel(string name)
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = name };
        model.AddBone(new Bone(
            "center",
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled | BoneFlags.OutsideParent,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        return model;
    }
}
