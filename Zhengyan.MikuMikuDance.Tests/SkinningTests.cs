using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class SkinningTests
{
    [Fact]
    public void EvaluatesParentedBonePose()
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        model.AddBone(Bone("child", new Vector3(0, 1, 0), 0));
        var sample = new Dictionary<string, BonePoseSample>(StringComparer.Ordinal)
        {
            ["root"] = new(new Vector3(1, 0, 0), Quaternion.Identity, true),
            ["child"] = new(new Vector3(0, 2, 0), Quaternion.Identity, true)
        };

        var pose = ModelPoseEvaluator.Evaluate(model, sample);

        Assert.True(pose.TryGetBone("child", out var child));
        AssertVectorClose(new Vector3(1, 3, 0), child.WorldTransform.Translation);
    }

    [Fact]
    public void SkinsBdef1VertexWithBoneTranslation()
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        var vertex = Vertex(new Vector3(1, 2, 3), new SkinningWeights(VertexSkinningType.Bdef1, [0], [1]));
        var sample = new Dictionary<string, BonePoseSample>(StringComparer.Ordinal)
        {
            ["root"] = new(new Vector3(5, 0, 0), Quaternion.Identity, true)
        };
        var pose = ModelPoseEvaluator.Evaluate(model, sample);

        var skinned = CpuSkinningProcessor.SkinVertex(vertex, pose);

        AssertVectorClose(new Vector3(6, 2, 3), skinned.Position);
    }

    [Fact]
    public void BlendsBdef2VertexTransforms()
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("a", Vector3.Zero, -1));
        model.AddBone(Bone("b", Vector3.Zero, -1));
        var vertex = Vertex(new Vector3(0, 0, 0), new SkinningWeights(VertexSkinningType.Bdef2, [0, 1], [0.25f, 0.75f]));
        var sample = new Dictionary<string, BonePoseSample>(StringComparer.Ordinal)
        {
            ["a"] = new(new Vector3(4, 0, 0), Quaternion.Identity, true),
            ["b"] = new(new Vector3(0, 8, 0), Quaternion.Identity, true)
        };
        var pose = ModelPoseEvaluator.Evaluate(model, sample);

        var skinned = CpuSkinningProcessor.SkinVertex(vertex, pose);

        AssertVectorClose(new Vector3(1, 6, 0), skinned.Position);
    }

    [Fact]
    public void BuildsSkinnedRenderMesh()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "skinned" };
        model.AddBone(Bone("root", Vector3.Zero, -1));
        model.AddVertex(Vertex(Vector3.Zero, new SkinningWeights(VertexSkinningType.Bdef1, [0], [1])));
        model.AddVertex(Vertex(Vector3.UnitX, new SkinningWeights(VertexSkinningType.Bdef1, [0], [1])));
        model.AddVertex(Vertex(Vector3.UnitY, new SkinningWeights(VertexSkinningType.Bdef1, [0], [1])));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        var pose = ModelPoseEvaluator.Evaluate(
            model,
            new Dictionary<string, BonePoseSample>(StringComparer.Ordinal)
            {
                ["root"] = new(new Vector3(0, 0, 2), Quaternion.Identity, true)
            });

        var mesh = RenderMeshBuilder.FromModel(model, pose);

        Assert.Equal("skinned", mesh.Name);
        AssertVectorClose(new Vector3(0, 0, 2), mesh.Vertices[0].Position);
    }

    [Fact]
    public void SolvesBasicIkChainTowardTarget()
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        model.AddBone(Bone("knee", new Vector3(1, 0, 0), 0));
        model.AddBone(Bone("effector", new Vector3(2, 0, 0), 1));
        model.AddBone(Bone(
            "target",
            new Vector3(1, 1, 0),
            -1,
            new IkConstraint(2, 12, MathF.PI / 4, [new IkLink(1, false, Vector3.Zero, Vector3.Zero), new IkLink(0, false, Vector3.Zero, Vector3.Zero)])));

        var pose = ModelPoseEvaluator.Evaluate(model, new Dictionary<string, BonePoseSample>(StringComparer.Ordinal));

        var target = pose.Bones[3].WorldTransform.Translation;
        var effector = pose.Bones[2].WorldTransform.Translation;
        Assert.True(Vector3.Distance(target, effector) < 0.2f);
    }

    [Fact]
    public void ClampsIkLinkRotationLimits()
    {
        var model = new MmdModel(ModelFormat.Pmx);
        model.AddBone(Bone("root", Vector3.Zero, -1));
        model.AddBone(Bone("effector", new Vector3(1, 0, 0), 0));
        model.AddBone(Bone(
            "target",
            new Vector3(0, 1, 0),
            -1,
            new IkConstraint(1, 8, MathF.PI / 4, [new IkLink(0, true, Vector3.Zero, new Vector3(0, 0, 0.2f))])));

        var pose = ModelPoseEvaluator.Evaluate(model, new Dictionary<string, BonePoseSample>(StringComparer.Ordinal));

        var effector = pose.Bones[1].WorldTransform.Translation;
        Assert.True(effector.Y > 0.05f);
        Assert.True(effector.Y < 0.25f);
        Assert.True(effector.X > 0.95f);
    }

    private static Bone Bone(string name, Vector3 origin, int parentBoneIndex, IkConstraint? ik = null)
    {
        return new Bone(
            name,
            string.Empty,
            origin,
            parentBoneIndex,
            0,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Movable | BoneFlags.Rotatable | (ik is null ? BoneFlags.None : BoneFlags.Ik),
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            ik);
    }

    private static Vertex Vertex(Vector3 position, SkinningWeights skinning)
    {
        return new Vertex(position, Vector3.UnitZ, Vector2.Zero, [], skinning, 1);
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 3);
        Assert.Equal(expected.Y, actual.Y, precision: 3);
        Assert.Equal(expected.Z, actual.Z, precision: 3);
    }
}
