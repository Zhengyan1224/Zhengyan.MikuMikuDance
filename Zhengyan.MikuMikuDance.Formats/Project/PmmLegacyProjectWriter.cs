using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.Binary;

namespace Zhengyan.MikuMikuDance.Formats.Project;

public sealed class PmmLegacyProjectWriter
{
    private const string PmmV2Signature = "Polygon Movie maker 0002";
    private const int SignatureSize = 30;
    private const int PathSize = 256;
    private const int AccessoryNameSize = 100;

    public byte[] Write(MmdProject project, string? projectPath = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var stream = new MemoryStream();
        Write(project, stream, projectPath);
        return stream.ToArray();
    }

    public void Write(MmdProject project, Stream stream, string? projectPath = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new BinaryWriter(stream, EncodingProvider.ShiftJis, leaveOpen: true);
        var motionView = ProjectMotionView.From(project);

        WriteFixedAscii(writer, PmmV2Signature, SignatureSize);
        writer.Write(1024);
        writer.Write(768);
        writer.Write(250);
        writer.Write((float)project.Camera.FieldOfView);
        writer.Write((byte)1); // editing CLA
        writer.Write((byte)1); // camera panel
        writer.Write((byte)1); // light panel
        writer.Write((byte)1); // accessory panel
        writer.Write((byte)1); // bone panel
        writer.Write((byte)1); // morph panel
        writer.Write((byte)1); // self-shadow panel

        WriteModels(writer, project, motionView, projectPath);
        WriteCamera(writer, project.Camera, motionView);
        WriteLight(writer, project.Light, motionView);
        writer.Write((byte)0); // selected accessory index
        writer.Write(0); // accessory horizontal scroll
        WriteAccessories(writer, project, motionView, projectPath);
        writer.Write(project.Timeline.CurrentFrameIndex);
        writer.Write(0); // horizontal scroll
        writer.Write(735); // horizontal scroll thumb
        writer.Write(0); // editing mode none
        writer.Write((byte)0); // camera look mode
        writer.Write((byte)(project.Timeline.LoopEnabled ? 1 : 0));
        writer.Write((byte)(project.Timeline.PlaybackRange.Length > 0 ? 1 : 0));
        writer.Write((byte)(project.Timeline.PlaybackRange.Length > 0 ? 1 : 0));
        writer.Write(project.Timeline.PlaybackRange.Start);
        writer.Write(project.Timeline.PlaybackRange.End);
        writer.Write((byte)0); // audio enabled
        WriteFixedString(writer, string.Empty, PathSize);
        writer.Write(0); // background video offset x
        writer.Write(0); // background video offset y
        writer.Write(1f); // background video scale
        WriteFixedString(writer, string.Empty, PathSize);
        writer.Write(0); // background video enabled
        writer.Write(0); // background image offset x
        writer.Write(0); // background image offset y
        writer.Write(1f); // background image scale
        WriteFixedString(writer, string.Empty, PathSize);
        writer.Write((byte)0); // background image enabled
        writer.Write((byte)1); // information shown
        writer.Write((byte)1); // grid and axis
        writer.Write((byte)1); // ground shadow
        writer.Write(60f);
        writer.Write(2); // screen capture mode
        writer.Write(-1); // accessory index after models
        writer.Write(1f); // ground shadow brightness
        writer.Write((byte)0); // translucent ground shadow
        writer.Write((byte)3); // physics simulation tracing
        WriteGravity(writer);
        WriteSelfShadow(writer, motionView);
        writer.Write(255);
        writer.Write(255);
        writer.Write(255);
        writer.Write((byte)0); // black background
        writer.Write(-1); // camera look-at model index
        writer.Write(-1); // camera look-at model bone index
        WriteIdentityMatrix(writer);
        writer.Write((byte)0); // following look-at
        writer.Write((byte)0); // unknown boolean
        writer.Write((byte)1); // physics ground
        writer.Write(project.Timeline.CurrentFrameIndex);
        if (project.ModelInstances.Count > 0)
        {
            writer.Write((byte)1);
            for (var i = 0; i < project.ModelInstances.Count; i++)
            {
                writer.Write(checked((byte)i));
                writer.Write(i);
            }
        }
        else
        {
            writer.Write((byte)0);
        }
    }

    public void WriteFile(MmdProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        Write(project, stream, path);
    }

    private static void WriteModels(
        BinaryWriter writer,
        MmdProject project,
        ProjectMotionView motionView,
        string? projectPath)
    {
        writer.Write((byte)0);
        writer.Write(checked((byte)Math.Min(project.ModelInstances.Count, byte.MaxValue)));
        for (var i = 0; i < project.ModelInstances.Count && i < byte.MaxValue; i++)
        {
            WriteModel(writer, project.ModelInstances[i], i, motionView, projectPath);
        }
    }

    private static void WriteModel(
        BinaryWriter writer,
        ModelInstance instance,
        int index,
        ProjectMotionView motionView,
        string? projectPath)
    {
        var model = instance.Model;
        var boneNames = model.Bones.Select(item => item.Name).ToArray();
        var morphNames = model.Morphs.Select(item => item.Name).ToArray();
        var constraintBoneIndices = model.Bones
            .Select((bone, boneIndex) => (bone, boneIndex))
            .Where(item => item.bone.Ik is not null)
            .Select(item => item.boneIndex)
            .ToArray();
        var outsideParentSubjectBoneIndices = model.Bones
            .Select((bone, boneIndex) => (bone, boneIndex))
            .Where(item => item.bone.Flags.HasFlag(BoneFlags.OutsideParent))
            .Select(item => item.boneIndex)
            .ToArray();
        var modelBoneFrames = motionView.BonesForModel(instance.Name).ToArray();
        var modelMorphFrames = motionView.MorphsForModel(instance.Name).ToArray();
        var modelFrames = motionView.ModelsForModel(instance.Name).ToArray();
        var boneFrameIndexByName = modelBoneFrames
            .GroupBy(item => item.BoneName, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.OrderBy(frame => frame.FrameIndex).ToArray(), StringComparer.Ordinal);
        var morphFrameIndexByName = modelMorphFrames
            .GroupBy(item => item.MorphName, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.OrderBy(frame => frame.FrameIndex).ToArray(), StringComparer.Ordinal);

        writer.Write(checked((byte)index));
        WriteVariableString(writer, instance.Name);
        WriteVariableString(writer, model.EnglishName);
        WriteFixedString(writer, ToStoredPath(model.Source, projectPath), PathSize);
        writer.Write((byte)0); // fixed tracks
        writer.Write(boneNames.Length);
        foreach (var name in boneNames)
        {
            WriteVariableString(writer, name);
        }

        writer.Write(morphNames.Length);
        foreach (var name in morphNames)
        {
            WriteVariableString(writer, name);
        }

        writer.Write(constraintBoneIndices.Length);
        foreach (var constraintBoneIndex in constraintBoneIndices)
        {
            writer.Write(constraintBoneIndex);
        }

        writer.Write(outsideParentSubjectBoneIndices.Length);
        foreach (var outsideParentSubjectBoneIndex in outsideParentSubjectBoneIndices)
        {
            writer.Write(outsideParentSubjectBoneIndex);
        }

        writer.Write(checked((byte)Math.Clamp(index + 1, 0, byte.MaxValue)));
        writer.Write((byte)(instance.Visible ? 1 : 0));
        writer.Write(0); // selected bone
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0); // expansion states
        writer.Write(0); // vertical scroll
        writer.Write(Math.Max(modelFrames.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max(), modelBoneFrames.Select(item => item.FrameIndex).DefaultIfEmpty(0).Max()));

        for (var i = 0; i < boneNames.Length; i++)
        {
            var keyframe = boneFrameIndexByName.TryGetValue(boneNames[i], out var frames)
                ? frames.FirstOrDefault(item => item.FrameIndex == 0)
                : null;
            WriteBoneKeyframe(writer, keyframe, includeIndex: false, objectIndex: i);
        }

        var explicitBoneFrames = modelBoneFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ThenBy(item => item.BoneName).ToArray();
        writer.Write(explicitBoneFrames.Length);
        foreach (var keyframe in explicitBoneFrames)
        {
            WriteBoneKeyframe(writer, keyframe, includeIndex: true, Array.IndexOf(boneNames, keyframe.BoneName));
        }

        for (var i = 0; i < morphNames.Length; i++)
        {
            var keyframe = morphFrameIndexByName.TryGetValue(morphNames[i], out var frames)
                ? frames.FirstOrDefault(item => item.FrameIndex == 0)
                : null;
            WriteMorphKeyframe(writer, keyframe, includeIndex: false, objectIndex: i);
        }

        var explicitMorphFrames = modelMorphFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ThenBy(item => item.MorphName).ToArray();
        writer.Write(explicitMorphFrames.Length);
        foreach (var keyframe in explicitMorphFrames)
        {
            WriteMorphKeyframe(writer, keyframe, includeIndex: true, Array.IndexOf(morphNames, keyframe.MorphName));
        }

        var initialModelFrame = modelFrames.FirstOrDefault(item => item.FrameIndex == 0);
        WriteModelKeyframe(writer, initialModelFrame, includeIndex: false, constraintBoneIndices, outsideParentSubjectBoneIndices.Length, boneNames, instance.Visible);
        var explicitModelFrames = modelFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ToArray();
        writer.Write(explicitModelFrames.Length);
        foreach (var keyframe in explicitModelFrames)
        {
            WriteModelKeyframe(writer, keyframe, includeIndex: true, constraintBoneIndices, outsideParentSubjectBoneIndices.Length, boneNames, instance.Visible);
        }

        for (var i = 0; i < boneNames.Length; i++)
        {
            WriteBoneState(writer);
        }

        for (var i = 0; i < morphNames.Length; i++)
        {
            writer.Write(0f);
        }

        for (var i = 0; i < constraintBoneIndices.Length; i++)
        {
            writer.Write((byte)1);
        }

        for (var i = 0; i < outsideParentSubjectBoneIndices.Length; i++)
        {
            writer.Write(0);
            writer.Write(0);
            writer.Write(-1);
            writer.Write(-1);
        }

        writer.Write((byte)0); // blend
        writer.Write(1f); // edge width
        writer.Write((byte)1); // self-shadow
        writer.Write(checked((byte)Math.Clamp(index, 0, byte.MaxValue)));
    }

    private static void WriteCamera(BinaryWriter writer, Camera camera, ProjectMotionView motionView)
    {
        WriteCameraKeyframe(writer, motionView.CameraFrames.FirstOrDefault(item => item.FrameIndex == 0), camera, includeIndex: false);
        var frames = motionView.CameraFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ToArray();
        writer.Write(frames.Length);
        foreach (var frame in frames)
        {
            WriteCameraKeyframe(writer, frame, camera, includeIndex: true);
        }

        WriteVector3(writer, camera.LookAt);
        WriteVector3(writer, CameraPosition(camera));
        WriteVector3(writer, camera.Angle);
        writer.Write((byte)(camera.PerspectiveEnabled ? 0 : 1));
    }

    private static void WriteLight(BinaryWriter writer, DirectionalLight light, ProjectMotionView motionView)
    {
        WriteLightKeyframe(writer, motionView.LightFrames.FirstOrDefault(item => item.FrameIndex == 0), light, includeIndex: false);
        var frames = motionView.LightFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ToArray();
        writer.Write(frames.Length);
        foreach (var frame in frames)
        {
            WriteLightKeyframe(writer, frame, light, includeIndex: true);
        }

        WriteVector3(writer, light.Color);
        WriteVector3(writer, light.Direction);
    }

    private static void WriteAccessories(
        BinaryWriter writer,
        MmdProject project,
        ProjectMotionView motionView,
        string? projectPath)
    {
        writer.Write(checked((byte)Math.Min(project.Accessories.Count, byte.MaxValue)));
        foreach (var accessory in project.Accessories.Take(byte.MaxValue))
        {
            WriteFixedString(writer, accessory.Name, AccessoryNameSize);
        }

        for (var i = 0; i < project.Accessories.Count && i < byte.MaxValue; i++)
        {
            WriteAccessory(writer, project, project.Accessories[i], i, motionView, projectPath);
        }
    }

    private static void WriteAccessory(
        BinaryWriter writer,
        MmdProject project,
        Accessory accessory,
        int index,
        ProjectMotionView motionView,
        string? projectPath)
    {
        var frames = motionView.AccessoriesFor(accessory.Name).OrderBy(item => item.FrameIndex).ToArray();
        writer.Write(checked((byte)index));
        WriteFixedString(writer, accessory.Name, AccessoryNameSize);
        WriteFixedString(writer, ToStoredPath(accessory.Source, projectPath), PathSize);
        writer.Write(checked((byte)Math.Clamp(index, 0, byte.MaxValue)));
        WriteAccessoryKeyframe(writer, project, frames.FirstOrDefault(item => item.FrameIndex == 0), accessory, includeIndex: false);
        var explicitFrames = frames.Where(item => item.FrameIndex != 0).ToArray();
        writer.Write(explicitFrames.Length);
        foreach (var frame in explicitFrames)
        {
            WriteAccessoryKeyframe(writer, project, frame, accessory, includeIndex: true);
        }

        writer.Write(PackOpacityAndVisible(accessory.Opacity, accessory.Visible));
        writer.Write(ResolveModelIndex(project, accessory.ParentModelName));
        writer.Write(ResolveBoneIndex(project, accessory.ParentModelName, accessory.ParentBoneName));
        WriteVector3(writer, accessory.Translation);
        writer.Write(accessory.Scale);
        WriteVector3(writer, accessory.Orientation);
        writer.Write((byte)1); // shadow
        writer.Write((byte)0); // add blend
    }

    private static void WriteGravity(BinaryWriter writer)
    {
        writer.Write(9.8f);
        writer.Write(10);
        WriteVector3(writer, new Vector3(0, -1, 0));
        writer.Write((byte)0);
        WriteBaseKeyframe(writer, includeIndex: false, objectIndex: 0, frameIndex: 0);
        writer.Write((byte)0);
        writer.Write(10);
        writer.Write(9.8f);
        WriteVector3(writer, new Vector3(0, -1, 0));
        writer.Write((byte)0);
        writer.Write(0);
    }

    private static void WriteSelfShadow(BinaryWriter writer, ProjectMotionView motionView)
    {
        writer.Write((byte)1);
        writer.Write(0.01125f);
        WriteSelfShadowKeyframe(writer, motionView.SelfShadowFrames.FirstOrDefault(item => item.FrameIndex == 0), includeIndex: false);
        var frames = motionView.SelfShadowFrames.Where(item => item.FrameIndex != 0).OrderBy(item => item.FrameIndex).ToArray();
        writer.Write(frames.Length);
        foreach (var frame in frames)
        {
            WriteSelfShadowKeyframe(writer, frame, includeIndex: true);
        }
    }

    private static void WriteBoneKeyframe(BinaryWriter writer, BoneKeyframe? keyframe, bool includeIndex, int objectIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, Math.Max(0, objectIndex), keyframe?.FrameIndex ?? 0);
        WriteBoneInterpolation(writer, keyframe?.Interpolation ?? BoneInterpolation.Linear);
        WriteVector3(writer, keyframe?.Translation ?? Vector3.Zero);
        WriteQuaternion(writer, keyframe?.Orientation ?? Quaternion.Identity);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
        writer.Write((byte)(keyframe?.PhysicsSimulationEnabled == false ? 1 : 0));
    }

    private static void WriteMorphKeyframe(BinaryWriter writer, MorphKeyframe? keyframe, bool includeIndex, int objectIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, Math.Max(0, objectIndex), keyframe?.FrameIndex ?? 0);
        writer.Write(keyframe?.Weight ?? 0f);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteModelKeyframe(
        BinaryWriter writer,
        ModelKeyframe? keyframe,
        bool includeIndex,
        IReadOnlyList<int> constraintBoneIndices,
        int outsideParentSubjectBoneCount,
        IReadOnlyList<string> boneNames,
        bool defaultVisible)
    {
        WriteBaseKeyframe(writer, includeIndex, 0, keyframe?.FrameIndex ?? 0);
        writer.Write((byte)(keyframe?.Visible ?? defaultVisible ? 1 : 0));
        for (var i = 0; i < constraintBoneIndices.Count; i++)
        {
            var boneName = constraintBoneIndices[i] >= 0 && constraintBoneIndices[i] < boneNames.Count
                ? boneNames[constraintBoneIndices[i]]
                : string.Empty;
            var enabled = keyframe is null || string.IsNullOrEmpty(boneName) || !keyframe.IkStates.TryGetValue(boneName, out var value) || value;
            writer.Write((byte)(enabled ? 1 : 0));
        }

        for (var i = 0; i < outsideParentSubjectBoneCount; i++)
        {
            writer.Write(-1);
            writer.Write(-1);
        }

        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteAccessoryKeyframe(
        BinaryWriter writer,
        MmdProject project,
        AccessoryKeyframe? keyframe,
        Accessory accessory,
        bool includeIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, 0, keyframe?.FrameIndex ?? 0);
        var visible = keyframe?.Visible ?? accessory.Visible;
        var opacity = keyframe?.Opacity ?? accessory.Opacity;
        var parentModelName = keyframe?.ParentModelName ?? accessory.ParentModelName;
        var parentBoneName = keyframe?.ParentBoneName ?? accessory.ParentBoneName;
        writer.Write(PackOpacityAndVisible(opacity, visible));
        writer.Write(ResolveModelIndex(project, parentModelName));
        writer.Write(ResolveBoneIndex(project, parentModelName, parentBoneName));
        WriteVector3(writer, keyframe?.Translation ?? accessory.Translation);
        WriteVector3(writer, keyframe?.Orientation ?? accessory.Orientation);
        writer.Write(keyframe?.Scale ?? accessory.Scale);
        writer.Write((byte)1);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteCameraKeyframe(
        BinaryWriter writer,
        CameraKeyframe? keyframe,
        Camera camera,
        bool includeIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, 0, keyframe?.FrameIndex ?? 0);
        writer.Write(keyframe?.Distance ?? camera.Distance);
        WriteVector3(writer, keyframe?.LookAt ?? camera.LookAt);
        WriteVector3(writer, keyframe?.Angle ?? camera.Angle);
        writer.Write(-1);
        writer.Write(-1);
        WriteCameraInterpolation(writer, keyframe?.Interpolation ?? CameraInterpolation.Linear);
        writer.Write((byte)(keyframe?.PerspectiveEnabled ?? camera.PerspectiveEnabled ? 0 : 1));
        writer.Write(keyframe?.FieldOfView ?? camera.FieldOfView);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteLightKeyframe(
        BinaryWriter writer,
        LightKeyframe? keyframe,
        DirectionalLight light,
        bool includeIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, 0, keyframe?.FrameIndex ?? 0);
        WriteVector3(writer, keyframe?.Color ?? light.Color);
        WriteVector3(writer, keyframe?.Direction ?? light.Direction);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteSelfShadowKeyframe(BinaryWriter writer, SelfShadowKeyframe? keyframe, bool includeIndex)
    {
        WriteBaseKeyframe(writer, includeIndex, 0, keyframe?.FrameIndex ?? 0);
        writer.Write((byte)(keyframe?.Mode ?? 1));
        writer.Write(keyframe?.Distance ?? 0.01125f);
        writer.Write((byte)(keyframe?.IsSelected == true ? 1 : 0));
    }

    private static void WriteBoneState(BinaryWriter writer)
    {
        WriteVector3(writer, Vector3.Zero);
        WriteQuaternion(writer, Quaternion.Identity);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
    }

    private static void WriteBaseKeyframe(BinaryWriter writer, bool includeIndex, int objectIndex, int frameIndex)
    {
        if (includeIndex)
        {
            writer.Write(objectIndex);
        }

        writer.Write(frameIndex);
        writer.Write(-1);
        writer.Write(-1);
    }

    private static void WriteBoneInterpolation(BinaryWriter writer, BoneInterpolation interpolation)
    {
        WriteCurve(writer, interpolation.TranslationX);
        WriteCurve(writer, interpolation.TranslationY);
        WriteCurve(writer, interpolation.TranslationZ);
        WriteCurve(writer, interpolation.Orientation);
    }

    private static void WriteCameraInterpolation(BinaryWriter writer, CameraInterpolation interpolation)
    {
        WriteCurve(writer, interpolation.LookAtX);
        WriteCurve(writer, interpolation.LookAtY);
        WriteCurve(writer, interpolation.LookAtZ);
        WriteCurve(writer, interpolation.Angle);
        WriteCurve(writer, interpolation.Distance);
        WriteCurve(writer, interpolation.FieldOfView);
    }

    private static void WriteCurve(BinaryWriter writer, BezierCurve curve)
    {
        writer.Write(curve.P1.X);
        writer.Write(curve.P1.Y);
        writer.Write(curve.P2.X);
        writer.Write(curve.P2.Y);
    }

    private static void WriteIdentityMatrix(BinaryWriter writer)
    {
        for (var i = 0; i < 16; i++)
        {
            writer.Write(i % 5 == 0 ? 1f : 0f);
        }
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteQuaternion(BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }

    private static byte PackOpacityAndVisible(float opacity, bool visible)
    {
        var clampedOpacity = Math.Clamp(opacity, 0, 1);
        var opacityBits = (int)Math.Clamp(MathF.Round((1f - clampedOpacity) * 100f), 0, 100);
        return (byte)((visible ? 1 : 0) | (opacityBits << 1));
    }

    private static string ToStoredPath(Uri? source, string? projectPath)
    {
        if (source is null)
        {
            return string.Empty;
        }

        var value = source.IsAbsoluteUri && source.IsFile ? source.LocalPath : source.ToString();
        if (!string.IsNullOrWhiteSpace(projectPath) && Path.IsPathFullyQualified(value))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.GetRelativePath(directory, value);
            }
        }

        return value;
    }

    private static int ResolveModelIndex(MmdProject project, string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return -1;
        }

        for (var i = 0; i < project.ModelInstances.Count; i++)
        {
            if (string.Equals(project.ModelInstances[i].Name, modelName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int ResolveBoneIndex(MmdProject project, string? modelName, string? boneName)
    {
        var modelIndex = ResolveModelIndex(project, modelName);
        if (modelIndex < 0 || string.IsNullOrWhiteSpace(boneName))
        {
            return -1;
        }

        var bones = project.ModelInstances[modelIndex].Model.Bones;
        for (var i = 0; i < bones.Count; i++)
        {
            if (string.Equals(bones[i].Name, boneName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static Vector3 CameraPosition(Camera camera)
    {
        var rotation = Matrix4x4.CreateFromYawPitchRoll(camera.Angle.Y, camera.Angle.X, camera.Angle.Z);
        return camera.LookAt + Vector3.Transform(new Vector3(0, 0, camera.Distance), rotation);
    }

    private static void WriteFixedAscii(BinaryWriter writer, string text, int length)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        writer.Write(bytes.AsSpan(0, Math.Min(bytes.Length, length)));
        for (var i = bytes.Length; i < length; i++)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteFixedString(BinaryWriter writer, string text, int length)
    {
        var bytes = EncodingProvider.ShiftJis.GetBytes(text);
        writer.Write(bytes.AsSpan(0, Math.Min(bytes.Length, length)));
        for (var i = bytes.Length; i < length; i++)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteVariableString(BinaryWriter writer, string text)
    {
        var bytes = EncodingProvider.ShiftJis.GetBytes(text);
        var length = Math.Min(bytes.Length, byte.MaxValue);
        writer.Write((byte)length);
        writer.Write(bytes.AsSpan(0, length));
    }

    private sealed class ProjectMotionView
    {
        private readonly IReadOnlyList<BoneKeyframe> _boneFrames;
        private readonly IReadOnlyList<MorphKeyframe> _morphFrames;
        private readonly IReadOnlyList<ModelKeyframe> _modelFrames;
        private readonly IReadOnlyList<AccessoryKeyframe> _accessoryFrames;

        private ProjectMotionView(
            IReadOnlyList<BoneKeyframe> boneFrames,
            IReadOnlyList<MorphKeyframe> morphFrames,
            IReadOnlyList<ModelKeyframe> modelFrames,
            IReadOnlyList<AccessoryKeyframe> accessoryFrames,
            IReadOnlyList<CameraKeyframe> cameraFrames,
            IReadOnlyList<LightKeyframe> lightFrames,
            IReadOnlyList<SelfShadowKeyframe> selfShadowFrames)
        {
            _boneFrames = boneFrames;
            _morphFrames = morphFrames;
            _modelFrames = modelFrames;
            _accessoryFrames = accessoryFrames;
            CameraFrames = cameraFrames;
            LightFrames = lightFrames;
            SelfShadowFrames = selfShadowFrames;
        }

        public IReadOnlyList<CameraKeyframe> CameraFrames { get; }

        public IReadOnlyList<LightKeyframe> LightFrames { get; }

        public IReadOnlyList<SelfShadowKeyframe> SelfShadowFrames { get; }

        public static ProjectMotionView From(MmdProject project)
        {
            return new ProjectMotionView(
                project.Motions.SelectMany(item => item.BoneKeyframes).ToArray(),
                project.Motions.SelectMany(item => item.MorphKeyframes).ToArray(),
                project.Motions.SelectMany(item => item.ModelKeyframes).ToArray(),
                project.Motions.SelectMany(item => item.AccessoryKeyframes).ToArray(),
                project.Motions.SelectMany(item => item.CameraKeyframes).OrderBy(item => item.FrameIndex).ToArray(),
                project.Motions.SelectMany(item => item.LightKeyframes).OrderBy(item => item.FrameIndex).ToArray(),
                project.Motions.SelectMany(item => item.SelfShadowKeyframes).OrderBy(item => item.FrameIndex).ToArray());
        }

        public IEnumerable<BoneKeyframe> BonesForModel(string modelName)
        {
            return _boneFrames.Where(item => MatchesModel(item, modelName));
        }

        public IEnumerable<MorphKeyframe> MorphsForModel(string modelName)
        {
            return _morphFrames.Where(item => MatchesModel(item, modelName));
        }

        public IEnumerable<ModelKeyframe> ModelsForModel(string modelName)
        {
            return _modelFrames.Where(item => MatchesModel(item, modelName));
        }

        public IEnumerable<AccessoryKeyframe> AccessoriesFor(string accessoryName)
        {
            return _accessoryFrames.Where(item => string.Equals(item.AccessoryName, accessoryName, StringComparison.Ordinal));
        }

        private static bool MatchesModel(MotionKeyframe keyframe, string modelName)
        {
            return !keyframe.Annotations.TryGetValue("pmm.model", out var target) ||
                string.Equals(target, modelName, StringComparison.Ordinal);
        }
    }
}
