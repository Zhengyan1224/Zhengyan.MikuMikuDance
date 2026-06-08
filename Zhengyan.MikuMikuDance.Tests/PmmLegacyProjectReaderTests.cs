using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Project;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class PmmLegacyProjectReaderTests
{
    [Fact]
    public void ReadsMinimalPmmV2Project()
    {
        var data = CreatePmmV2Project();

        var document = new PmmLegacyProjectReader().Inspect(data);

        Assert.Equal(2, document.Version);
        Assert.Equal(1280, document.OutputWidth);
        Assert.Equal(720, document.OutputHeight);
        Assert.Single(document.Models);
        Assert.Equal("Miku", document.Models[0].Name);
        Assert.Equal("center", Assert.Single(document.Models[0].BoneNames));
        Assert.Single(document.Models[0].BoneKeyframes);
        Assert.Equal("smile", Assert.Single(document.Models[0].MorphNames));
        Assert.Single(document.Accessories);
        Assert.Equal("stage", document.Accessories[0].Name);
        Assert.Equal(24, document.CurrentFrameIndex);
        Assert.Equal("music.wav", document.AudioPath);
    }

    [Fact]
    public void ConvertsPmmV2ProjectToCurrentProject()
    {
        var data = CreatePmmV2Project();

        var project = new PmmLegacyProjectReader().Read(data, loadResources: false);

        Assert.Equal("PMM Project", project.Name);
        Assert.Equal(24, project.Timeline.CurrentFrameIndex);
        Assert.True(project.Timeline.LoopEnabled);
        Assert.Single(project.ModelInstances);
        Assert.Equal("Miku", project.ModelInstances[0].Name);
        Assert.Equal(ModelFormat.Pmx, project.Models[0].Format);
        Assert.Single(project.Models[0].Bones);
        Assert.Single(project.Models[0].Morphs);
        Assert.Single(project.Accessories);
        Assert.Equal("stage", project.Accessories[0].Name);
        Assert.Equal("Miku", project.Accessories[0].ParentModelName);
        Assert.Equal("center", project.Accessories[0].ParentBoneName);
        Assert.Single(project.Motions);
        Assert.Single(project.Motions[0].BoneKeyframes);
        Assert.Single(project.Motions[0].MorphKeyframes);
        Assert.Single(project.Motions[0].ModelKeyframes);
        Assert.Single(project.Motions[0].AccessoryKeyframes);
        Assert.Single(project.Motions[0].CameraKeyframes);
        Assert.Single(project.Motions[0].LightKeyframes);
        Assert.Single(project.Motions[0].SelfShadowKeyframes);
    }

    [Fact]
    public void ReadsPmmV1ProjectUsingReferencedModel()
    {
        var root = Path.Combine(Path.GetTempPath(), "pmm-v1-" + Guid.NewGuid().ToString("N"));
        var modelDirectory = Path.Combine(root, "models");
        Directory.CreateDirectory(modelDirectory);
        try
        {
            var modelPath = Path.Combine(modelDirectory, "miku.pmd");
            File.WriteAllBytes(modelPath, new PmdModelWriter().Write(CreatePmdModelForPmmV1()));
            var pmmPath = Path.Combine(root, "scene.pmm");
            File.WriteAllBytes(pmmPath, CreatePmmV1Project("models/miku.pmd"));

            var document = new PmmLegacyProjectReader().Inspect(File.ReadAllBytes(pmmPath), pmmPath);
            var project = new PmmLegacyProjectReader().ReadFile(pmmPath, loadResources: false);

            Assert.Equal(1, document.Version);
            Assert.Single(document.Models);
            Assert.Equal("center", Assert.Single(document.Models[0].BoneNames));
            Assert.Equal("smile", Assert.Single(document.Models[0].MorphNames));
            Assert.Single(document.Models[0].BoneKeyframes);
            Assert.Single(document.Models[0].MorphKeyframes);
            Assert.Single(project.ModelInstances);
            Assert.Equal(ModelFormat.Pmd, project.Models[0].Format);
            Assert.Single(project.Models[0].Bones);
            Assert.Single(project.Models[0].Morphs);
            Assert.Single(project.Motions[0].BoneKeyframes);
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
    public void RejectsPmmV1RelativeModelPathWithoutProjectPath()
    {
        var data = CreatePmmV1Project("models/miku.pmd");

        var ex = Assert.Throws<MmdFormatException>(() => new PmmLegacyProjectReader().Inspect(data));

        Assert.Contains("project path", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesPmmV2ProjectAndReadsItBack()
    {
        var project = new MmdProject { Name = "Writable" };
        project.Timeline.Seek(18);
        project.Timeline.LoopEnabled = true;
        project.Timeline.SetPlaybackRange(0, 90);
        project.Camera.LookAt = new Vector3(1, 2, 3);
        project.Camera.Angle = new Vector3(0.1f, 0.2f, 0.3f);
        project.Camera.Distance = 42;
        project.Camera.FieldOfView = 38;
        project.Light.Color = new Vector3(0.3f, 0.4f, 0.5f);
        project.Light.Direction = new Vector3(-1, -2, -3);

        var model = CreatePmdModelForPmmV1();
        model.Source = new Uri("models/miku.pmd", UriKind.Relative);
        var instance = project.AddModel(model);
        instance.Name = "Miku";
        var accessory = new Accessory("stage")
        {
            Source = new Uri("stage/stage.x", UriKind.Relative),
            ParentModelName = "Miku",
            ParentBoneName = "center",
            Opacity = 0.5f
        };
        project.AddAccessory(accessory);
        var motion = new Motion("Timeline", MotionFormat.Unknown);
        motion.Add(new BoneKeyframe("center", 10, new Vector3(1, 0, 0), Quaternion.Identity, BoneInterpolation.Linear)
        {
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pmm.model"] = "Miku"
            }
        });
        motion.Add(new MorphKeyframe("smile", 12, 0.8f)
        {
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pmm.model"] = "Miku"
            }
        });
        motion.Add(new CameraKeyframe(20, new Vector3(1, 2, 3), Vector3.Zero, 40, 35, CameraInterpolation.Linear));
        motion.Add(new LightKeyframe(0, new Vector3(0.4f), new Vector3(-0.5f, -1, 0.5f)));
        motion.Add(new AccessoryKeyframe("stage", 24, true, Vector3.One, Vector3.Zero, 10, 0.5f, "Miku", "center"));
        motion.Add(new SelfShadowKeyframe(30, 1, 0.02f));
        project.AddMotion(motion);

        var data = new PmmLegacyProjectWriter().Write(project);
        var document = new PmmLegacyProjectReader().Inspect(data);

        Assert.Equal(2, document.Version);
        Assert.Single(document.Models);
        Assert.Equal("Miku", document.Models[0].Name);
        Assert.Equal("center", Assert.Single(document.Models[0].BoneNames));
        Assert.Single(document.Models[0].BoneKeyframes, item => item.FrameIndex != 0);
        Assert.Single(document.Models[0].MorphKeyframes, item => item.FrameIndex != 0);
        Assert.Single(document.Camera.Keyframes, item => item.FrameIndex != 0);
        Assert.Single(document.Light.Keyframes);
        Assert.Single(document.Accessories);
        Assert.Single(document.Accessories[0].Keyframes, item => item.FrameIndex != 0);
        Assert.Single(document.SelfShadow!.Keyframes, item => item.FrameIndex != 0);
        Assert.Equal(18, document.CurrentFrameIndex);
        Assert.True(document.LoopEnabled);
    }

    private static byte[] CreatePmmV1Project(string modelPath)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteFixedAscii(writer, "Polygon Movie maker 0001", 30);
        writer.Write(640);
        writer.Write(480);
        writer.Write(250);
        writer.Write(30f);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        WriteModelsV1(writer, modelPath);
        WriteCamera(writer, version: 1);
        WriteLight(writer);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write(12);
        writer.Write(0);
        writer.Write(735);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(60);
        writer.Write((byte)0);
        WriteFixedShiftJis(writer, string.Empty, 256);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        WriteFixedShiftJis(writer, string.Empty, 256);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        WriteFixedShiftJis(writer, string.Empty, 256);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(60f);
        writer.Write(2);
        writer.Write(-1);
        writer.Write(1f);
        return stream.ToArray();
    }

    private static byte[] CreatePmmV2Project()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteFixedAscii(writer, "Polygon Movie maker 0002", 30);
        writer.Write(1280);
        writer.Write(720);
        writer.Write(300);
        writer.Write(35f);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        WriteModelsV2(writer);
        WriteCamera(writer, version: 2);
        WriteLight(writer);
        writer.Write((byte)0);
        writer.Write(0);
        WriteAccessories(writer, version: 2);
        writer.Write(24);
        writer.Write(0);
        writer.Write(735);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(120);
        writer.Write((byte)1);
        WriteFixedShiftJis(writer, "music.wav", 256);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        WriteFixedShiftJis(writer, string.Empty, 256);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        WriteFixedShiftJis(writer, "background.png", 256);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(60f);
        writer.Write(2);
        writer.Write(-1);
        writer.Write(1f);
        writer.Write((byte)0);
        writer.Write((byte)3);
        WriteGravity(writer);
        WriteSelfShadow(writer);
        writer.Write(255);
        writer.Write(255);
        writer.Write(255);
        writer.Write((byte)0);
        writer.Write(-1);
        writer.Write(-1);
        for (var i = 0; i < 16; i++)
        {
            writer.Write(i % 5 == 0 ? 1f : 0f);
        }

        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write(24);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(0);
        return stream.ToArray();
    }

    private static MmdModel CreatePmdModelForPmmV1()
    {
        var model = new MmdModel(ModelFormat.Pmd)
        {
            Name = "Miku",
            Comment = "PMM v1 fixture"
        };
        model.AddBone(new Bone(
            "center",
            string.Empty,
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Rotatable | BoneFlags.Movable | BoneFlags.Visible | BoneFlags.Enabled,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        model.AddMorph(new Morph("smile", string.Empty, MorphCategory.Lip, MorphType.Vertex, []));
        return model;
    }

    private static void WriteModelsV1(BinaryWriter writer, string modelPath)
    {
        writer.Write((byte)0);
        writer.Write((byte)1);
        WriteFixedShiftJis(writer, "Miku", 20);
        writer.Write((byte)0);
        WriteFixedShiftJis(writer, "Miku", 20);
        WriteFixedShiftJis(writer, modelPath, 256);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(60);
        WriteBoneKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0, version: 1);
        writer.Write(0);
        WriteMorphKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write(0);
        WriteModelKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0, constraintStates: 0, outsideParentStates: 0);
        writer.Write(0);
        WriteBoneState(writer, version: 1);
        writer.Write(0f);
    }

    private static void WriteModelsV2(BinaryWriter writer)
    {
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)0);
        WriteVariableShiftJis(writer, "Miku");
        WriteVariableShiftJis(writer, "Miku EN");
        WriteFixedShiftJis(writer, "models/miku.pmx", 256);
        writer.Write((byte)0);
        writer.Write(1);
        WriteVariableShiftJis(writer, "center");
        writer.Write(1);
        WriteVariableShiftJis(writer, "smile");
        writer.Write(1);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(120);
        WriteBoneKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0, version: 2);
        writer.Write(0);
        WriteMorphKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write(0);
        WriteModelKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0, constraintStates: 1, outsideParentStates: 0);
        writer.Write(0);
        WriteBoneState(writer, version: 2);
        writer.Write(0f);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(0f);
        writer.Write((byte)1);
        writer.Write((byte)0);
    }

    private static void WriteCamera(BinaryWriter writer, int version)
    {
        WriteBaseKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write(-45f);
        writer.Write(0f);
        writer.Write(10f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        if (version > 1)
        {
            writer.Write(-1);
            writer.Write(-1);
        }
        WriteLinearInterpolations(writer, 6);
        writer.Write((byte)0);
        writer.Write(35);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(10f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(10f);
        writer.Write(45f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write((byte)0);
    }

    private static void WriteLight(BinaryWriter writer)
    {
        WriteBaseKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write(0.6f);
        writer.Write(0.6f);
        writer.Write(0.6f);
        writer.Write(-0.5f);
        writer.Write(-1f);
        writer.Write(0.5f);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(0.6f);
        writer.Write(0.6f);
        writer.Write(0.6f);
        writer.Write(-0.5f);
        writer.Write(-1f);
        writer.Write(0.5f);
    }

    private static void WriteAccessories(BinaryWriter writer, int version)
    {
        writer.Write((byte)1);
        WriteFixedShiftJis(writer, "stage", 100);
        writer.Write((byte)0);
        WriteFixedShiftJis(writer, "stage", 100);
        WriteFixedShiftJis(writer, "accessories/stage.x", 256);
        writer.Write((byte)0);
        WriteAccessoryKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write(0);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        writer.Write(2f);
        writer.Write(3f);
        writer.Write(10f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write((byte)1);
        if (version > 1)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteGravity(BinaryWriter writer)
    {
        writer.Write(9.8f);
        writer.Write(10);
        writer.Write(0f);
        writer.Write(-1f);
        writer.Write(0f);
        writer.Write((byte)0);
        WriteBaseKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write((byte)0);
        writer.Write(10);
        writer.Write(9.8f);
        writer.Write(0f);
        writer.Write(-1f);
        writer.Write(0f);
        writer.Write((byte)0);
        writer.Write(0);
    }

    private static void WriteSelfShadow(BinaryWriter writer)
    {
        writer.Write((byte)1);
        writer.Write(0.01125f);
        WriteBaseKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write((byte)1);
        writer.Write(0.01125f);
        writer.Write((byte)0);
        writer.Write(0);
    }

    private static void WriteBoneKeyframe(
        BinaryWriter writer,
        bool includeIndex,
        int objectIndex,
        int frameIndex,
        int version)
    {
        WriteBaseKeyframe(writer, includeIndex, objectIndex, frameIndex);
        WriteLinearInterpolations(writer, 4);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        writer.Write((byte)0);
        if (version > 1)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteBoneState(BinaryWriter writer, int version)
    {
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        if (version > 1)
        {
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
        }
        else
        {
            writer.Write(0);
            writer.Write((byte)0);
            writer.Write((byte)0);
        }
    }

    private static void WriteMorphKeyframe(
        BinaryWriter writer,
        bool includeIndex,
        int objectIndex,
        int frameIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, objectIndex, frameIndex);
        writer.Write(0.75f);
        writer.Write((byte)0);
    }

    private static void WriteModelKeyframe(
        BinaryWriter writer,
        bool includeIndex,
        int objectIndex,
        int frameIndex,
        int constraintStates,
        int outsideParentStates)
    {
        WriteBaseKeyframe(writer, includeIndex, objectIndex, frameIndex);
        writer.Write((byte)1);
        for (var i = 0; i < constraintStates; i++)
        {
            writer.Write((byte)1);
        }

        for (var i = 0; i < outsideParentStates; i++)
        {
            writer.Write(-1);
            writer.Write(-1);
        }

        writer.Write((byte)0);
    }

    private static void WriteAccessoryKeyframe(
        BinaryWriter writer,
        bool includeIndex,
        int objectIndex,
        int frameIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, objectIndex, frameIndex);
        writer.Write((byte)1);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1f);
        writer.Write(2f);
        writer.Write(3f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(10f);
        writer.Write((byte)1);
        writer.Write((byte)0);
    }

    private static void WriteBaseKeyframe(
        BinaryWriter writer,
        bool includeIndex,
        int objectIndex,
        int frameIndex)
    {
        if (includeIndex)
        {
            writer.Write(objectIndex);
        }

        writer.Write(frameIndex);
        writer.Write(-1);
        writer.Write(-1);
    }

    private static void WriteLinearInterpolations(BinaryWriter writer, int count)
    {
        for (var i = 0; i < count; i++)
        {
            writer.Write((byte)20);
            writer.Write((byte)20);
            writer.Write((byte)107);
            writer.Write((byte)107);
        }
    }

    private static void WriteFixedAscii(BinaryWriter writer, string text, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        writer.Write(bytes.AsSpan(0, Math.Min(bytes.Length, length)));
        for (var i = bytes.Length; i < length; i++)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteFixedShiftJis(BinaryWriter writer, string text, int length)
    {
        var bytes = ShiftJis.GetBytes(text);
        writer.Write(bytes.AsSpan(0, Math.Min(bytes.Length, length)));
        for (var i = bytes.Length; i < length; i++)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteVariableShiftJis(BinaryWriter writer, string text)
    {
        var bytes = ShiftJis.GetBytes(text);
        Assert.True(bytes.Length <= byte.MaxValue);
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private static Encoding ShiftJis
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932);
        }
    }
}
