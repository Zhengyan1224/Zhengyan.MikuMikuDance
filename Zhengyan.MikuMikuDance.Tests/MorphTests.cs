using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class MorphTests
{
    [Fact]
    public void EvaluatesVertexMorphOffsets()
    {
        var model = ModelWithVertices(2);
        model.AddMorph(new Morph(
            "smile",
            string.Empty,
            MorphCategory.Lip,
            MorphType.Vertex,
            [new VertexMorphOffset(1, new Vector3(2, 0, 0))]));

        var morphs = MorphEvaluator.Evaluate(model, Weights(("smile", 0.5f)));

        AssertVectorClose(Vector3.Zero, morphs.VertexOffsets[0]);
        AssertVectorClose(new Vector3(1, 0, 0), morphs.VertexOffsets[1]);
    }

    [Fact]
    public void ExpandsGroupMorphWeights()
    {
        var model = ModelWithVertices(1);
        model.AddMorph(new Morph(
            "target",
            string.Empty,
            MorphCategory.Other,
            MorphType.Vertex,
            [new VertexMorphOffset(0, new Vector3(0, 4, 0))]));
        model.AddMorph(new Morph(
            "group",
            string.Empty,
            MorphCategory.Other,
            MorphType.Group,
            [new GroupMorphOffset(0, 0.5f)]));

        var morphs = MorphEvaluator.Evaluate(model, Weights(("group", 0.5f)));

        AssertVectorClose(new Vector3(0, 1, 0), morphs.VertexOffsets[0]);
    }

    [Fact]
    public void EvaluatesUvMorphOffsets()
    {
        var model = ModelWithVertices(1);
        model.AddMorph(new Morph(
            "uv",
            string.Empty,
            MorphCategory.Other,
            MorphType.Uv,
            [new UvMorphOffset(0, new Vector4(0.2f, 0.4f, 0, 0))]));

        var morphs = MorphEvaluator.Evaluate(model, Weights(("uv", 0.5f)));
        var pose = ModelPose.BindPose(model);
        var skinned = CpuSkinningProcessor.SkinVertices(model, pose, morphs);

        Assert.Equal(new Vector2(0.1f, 0.2f), skinned[0].Uv);
    }

    [Fact]
    public void CombinesBoneMorphsWithMotionPose()
    {
        var model = ModelWithVertices(1);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        model.AddMorph(new Morph(
            "boneMorph",
            string.Empty,
            MorphCategory.Other,
            MorphType.Bone,
            [new BoneMorphOffset(0, new Vector3(0, 2, 0), Quaternion.Identity)]));
        var motion = new Motion("motion", MotionFormat.Vmd);
        motion.Add(new MorphKeyframe("boneMorph", 0, 0));
        motion.Add(new MorphKeyframe("boneMorph", 10, 0.5f));
        motion.Add(new BoneKeyframe("root", 0, new Vector3(1, 0, 0), Quaternion.Identity, BoneInterpolation.Linear));

        var sample = MotionSampler.Sample(motion, 10);
        var state = AnimationPoseEvaluator.Evaluate(model, sample);

        Assert.True(state.Pose.TryGetBone("root", out var root));
        AssertVectorClose(new Vector3(1, 1, 0), root.WorldTransform.Translation);
    }

    [Fact]
    public void BuildsRenderMeshWithMorphOffsets()
    {
        var model = ModelWithVertices(3);
        model.Name = "morph";
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMorph(new Morph(
            "move",
            string.Empty,
            MorphCategory.Other,
            MorphType.Vertex,
            [new VertexMorphOffset(0, new Vector3(0, 0, 3))]));
        var morphs = MorphEvaluator.Evaluate(model, Weights(("move", 1)));
        var pose = ModelPose.BindPose(model);

        var mesh = RenderMeshBuilder.FromModel(model, pose, morphs);

        Assert.Equal("morph", mesh.Name);
        AssertVectorClose(new Vector3(0, 0, 3), mesh.Vertices[0].Position);
    }

    [Fact]
    public void BuildsInstanceRenderMeshWithPreviewMorphWeights()
    {
        var model = ModelWithVertices(3);
        model.Name = "instanceMorph";
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMorph(new Morph(
            "move",
            string.Empty,
            MorphCategory.Other,
            MorphType.Vertex,
            [new VertexMorphOffset(0, new Vector3(0, 0, 4))]));
        var project = new Core.Scene.MmdProject();
        var instance = project.AddModel(model);

        instance.SetMorphWeight("move", 0.25f);
        var mesh = RenderMeshBuilder.FromModel(instance);

        AssertVectorClose(new Vector3(0, 0, 1), mesh.Vertices[0].Position);

        instance.SetMorphWeight("move", 0);
        Assert.Empty(instance.MorphWeights);
    }

    private static MmdModel ModelWithVertices(int vertexCount)
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        for (var i = 0; i < vertexCount; i++)
        {
            model.AddVertex(new Vertex(
                new Vector3(i, 0, 0),
                Vector3.UnitZ,
                Vector2.Zero,
                [],
                new SkinningWeights(VertexSkinningType.Bdef1, [0], [1]),
                1));
        }

        return model;
    }

    private static Dictionary<string, MorphWeightSample> Weights(params (string Name, float Weight)[] weights)
    {
        return weights.ToDictionary(
            item => item.Name,
            item => new MorphWeightSample(item.Weight),
            StringComparer.Ordinal);
    }

    private static Bone Bone(string name, Vector3 origin, int parentBoneIndex)
    {
        return new Bone(
            name,
            string.Empty,
            origin,
            parentBoneIndex,
            0,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Movable | BoneFlags.Rotatable,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null);
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 3);
        Assert.Equal(expected.Y, actual.Y, precision: 3);
        Assert.Equal(expected.Z, actual.Z, precision: 3);
    }
}
