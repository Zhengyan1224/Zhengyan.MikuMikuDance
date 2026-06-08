using System.Text;
using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Effects;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Formats.DirectX;
using Zhengyan.MikuMikuDance.Formats.Mme;
using Zhengyan.MikuMikuDance.Formats.Nmd;
using Zhengyan.MikuMikuDance.Formats.Pmd;
using Zhengyan.MikuMikuDance.Formats.Pmx;
using Zhengyan.MikuMikuDance.Formats.Vmd;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class FormatReaderTests
{
    [Fact]
    public void ReadsMinimalPmxModel()
    {
        var data = CreateMinimalPmx();

        var model = new PmxModelReader().Read(data);

        Assert.Equal("初音ミク", model.Name);
        Assert.Equal("Miku Hatsune", model.EnglishName);
        Assert.Empty(model.Vertices);
        Assert.Empty(model.Bones);
    }

    [Fact]
    public void WritesAndReadsPmxModel()
    {
        var model = new MmdModel(ModelFormat.Pmx)
        {
            Name = "PmxRoundTrip",
            EnglishName = "PmxRoundTripEn",
            Comment = "Comment",
            EnglishComment = "CommentEn"
        };
        var textureIndex = model.AddTexture("diffuse.png");
        var sphereIndex = model.AddTexture("sphere.spa");
        model.AddVertex(new Vertex(
            Vector3.Zero,
            Vector3.UnitZ,
            Vector2.Zero,
            [new Vector4(1, 2, 3, 4)],
            new SkinningWeights(VertexSkinningType.Bdef1, [0], [1]),
            1));
        model.AddVertex(new Vertex(
            Vector3.UnitX,
            Vector3.UnitZ,
            Vector2.One,
            [Vector4.One],
            new SkinningWeights(
                VertexSkinningType.Sdef,
                [0, 1],
                [0.25f, 0.75f],
                new SdefParameters(new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1))),
            0.5f));
        model.AddVertex(new Vertex(
            Vector3.UnitY,
            Vector3.UnitZ,
            new Vector2(0.5f),
            [Vector4.Zero],
            new SkinningWeights(VertexSkinningType.Bdef2, [1, 0], [0.4f, 0.6f]),
            1));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            "matEn",
            new Vector4(0.8f, 0.7f, 0.6f, 0.5f),
            new Vector3(0.1f, 0.2f, 0.3f),
            32,
            new Vector3(0.4f, 0.5f, 0.6f),
            true,
            true,
            false,
            true,
            true,
            new Vector4(0, 0, 0, 1),
            1.25f,
            textureIndex,
            sphereIndex,
            SphereTextureMode.Add,
            2,
            true,
            3,
            "memo"));
        model.AddBone(new Bone(
            "root",
            "rootEn",
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Rotatable | BoneFlags.IndexedTailPosition,
            1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        model.AddBone(new Bone(
            "ik",
            "ikEn",
            Vector3.UnitX,
            0,
            1,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Rotatable | BoneFlags.Ik,
            -1,
            Vector3.UnitY,
            -1,
            0,
            null,
            null,
            0,
            new IkConstraint(0, 8, 0.5f, [new IkLink(0, true, new Vector3(-0.1f), new Vector3(0.1f))])));
        model.AddMorph(new Morph("group", "groupEn", MorphCategory.System, MorphType.Group, [
            new GroupMorphOffset(1, 0.5f)
        ]));
        model.AddMorph(new Morph("vertex", "vertexEn", MorphCategory.Lip, MorphType.Vertex, [
            new VertexMorphOffset(1, new Vector3(0, 0.1f, 0))
        ]));
        model.AddMorph(new Morph("bone", "boneEn", MorphCategory.Other, MorphType.Bone, [
            new BoneMorphOffset(1, Vector3.UnitY, Quaternion.Identity)
        ]));
        model.AddMorph(new Morph("uv", "uvEn", MorphCategory.Other, MorphType.Uv, [
            new UvMorphOffset(0, new Vector4(0.1f, 0.2f, 0.3f, 0.4f))
        ]));
        model.AddMorph(new Morph("material", "materialEn", MorphCategory.Other, MorphType.Material, [
            new MaterialMorphOffset(
                0,
                true,
                Vector4.One,
                Vector3.One,
                1,
                Vector3.One,
                Vector4.One,
                1,
                Vector4.One,
                Vector4.One,
                Vector4.One)
        ]));
        model.AddLabel(new ModelLabel("Root", "RootEn", true, [
            new ModelLabelItem(LabelItemType.Bone, 0),
            new ModelLabelItem(LabelItemType.Morph, 1)
        ]));
        model.AddRigidBody(new RigidBody(
            "body",
            "bodyEn",
            0,
            1,
            0xfffe,
            RigidBodyShapeType.Box,
            Vector3.One,
            Vector3.UnitX,
            Vector3.UnitY,
            1,
            0.1f,
            0.2f,
            0.3f,
            0.4f,
            RigidBodyTransformType.DynamicWithBone));
        model.AddJoint(new Joint(
            "joint",
            "jointEn",
            JointType.Generic6Dof,
            0,
            0,
            Vector3.Zero,
            Vector3.Zero,
            -Vector3.One,
            Vector3.One,
            -Vector3.One,
            Vector3.One,
            Vector3.One,
            Vector3.One));

        var data = new PmxModelWriter().Write(model);
        var decoded = new PmxModelReader().Read(data);

        Assert.Equal("PmxRoundTrip", decoded.Name);
        Assert.Equal("PmxRoundTripEn", decoded.EnglishName);
        Assert.Equal(3, decoded.Vertices.Count);
        Assert.Single(decoded.Vertices[0].AdditionalUvs);
        Assert.Equal(VertexSkinningType.Sdef, decoded.Vertices[1].Skinning.Type);
        Assert.Equal(3, decoded.Indices.Count);
        Assert.Equal(2, decoded.Textures.Count);
        Assert.Single(decoded.Materials);
        Assert.True(decoded.Materials[0].IsDoubleSided);
        Assert.Equal(SphereTextureMode.Add, decoded.Materials[0].SphereTextureMode);
        Assert.Equal(2, decoded.Bones.Count);
        Assert.NotNull(decoded.Bones[1].Ik);
        Assert.True(decoded.Bones[1].Ik!.Links[0].AngleLimitEnabled);
        Assert.Equal(5, decoded.Morphs.Count);
        Assert.Equal(MorphType.Material, decoded.Morphs[4].Type);
        Assert.Single(decoded.Labels);
        Assert.Single(decoded.RigidBodies);
        Assert.Single(decoded.Joints);
    }

    [Fact]
    public void ReadsMinimalPmdModel()
    {
        var data = CreateMinimalPmd();

        var model = new PmdModelReader().Read(data);

        Assert.Equal("PmdModel", model.Name);
        Assert.Single(model.Vertices);
        Assert.Equal(3, model.Indices.Count);
        Assert.Single(model.Materials);
        Assert.Equal("texture.png", model.Textures[0]);
        Assert.Equal(string.Empty, model.SharedToonTextures[0]);
    }

    [Fact]
    public void WritesAndReadsPmdModel()
    {
        var model = new MmdModel(ModelFormat.Pmd)
        {
            Name = "RoundTrip",
            Comment = "Comment",
            EnglishName = "RoundTripEn",
            EnglishComment = "CommentEn"
        };
        var textureIndex = model.AddTexture("body.png");
        model.SetSharedToonTexture(0, "toon01.bmp");
        model.AddVertex(new Vertex(
            Vector3.Zero,
            Vector3.UnitY,
            new Vector2(0.25f, 0.75f),
            [],
            new SkinningWeights(VertexSkinningType.Bdef2, [0, 1], [0.6f, 0.4f]),
            1));
        model.AddVertex(new Vertex(
            Vector3.UnitX,
            Vector3.UnitY,
            Vector2.Zero,
            [],
            new SkinningWeights(VertexSkinningType.Bdef1, [1], [1]),
            0));
        model.AddVertex(new Vertex(
            Vector3.UnitY,
            Vector3.UnitY,
            Vector2.One,
            [],
            new SkinningWeights(VertexSkinningType.Bdef1, [1], [1]),
            1));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            new Vector4(0.8f, 0.7f, 0.6f, 0.5f),
            new Vector3(0.1f, 0.2f, 0.3f),
            16,
            new Vector3(0.4f, 0.5f, 0.6f),
            false,
            true,
            true,
            true,
            true,
            Vector4.UnitW,
            1,
            textureIndex,
            -1,
            SphereTextureMode.Disabled,
            0,
            true,
            3,
            string.Empty));
        model.AddBone(new Bone(
            "root",
            "rootEn",
            Vector3.Zero,
            -1,
            0,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Rotatable | BoneFlags.IndexedTailPosition,
            1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            null));
        model.AddBone(new Bone(
            "knee",
            "kneeEn",
            Vector3.UnitX,
            0,
            0,
            BoneFlags.Enabled | BoneFlags.Visible | BoneFlags.Rotatable | BoneFlags.Ik,
            -1,
            Vector3.Zero,
            -1,
            0,
            null,
            null,
            0,
            new IkConstraint(1, 4, 0.5f, [new IkLink(0, false, Vector3.Zero, Vector3.Zero)])));
        model.AddMorph(new Morph("base", string.Empty, MorphCategory.System, MorphType.Vertex, [
            new VertexMorphOffset(0, Vector3.Zero)
        ]));
        model.AddMorph(new Morph("smile", "smileEn", MorphCategory.Lip, MorphType.Vertex, [
            new VertexMorphOffset(1, new Vector3(0, 0.1f, 0))
        ]));
        model.AddLabel(new ModelLabel("Face", "FaceEn", true, [new ModelLabelItem(LabelItemType.Morph, 1)]));
        model.AddLabel(new ModelLabel("Body", "BodyEn", false, [new ModelLabelItem(LabelItemType.Bone, 0)]));
        model.AddRigidBody(new RigidBody(
            "body",
            string.Empty,
            0,
            1,
            0xfffe,
            RigidBodyShapeType.Sphere,
            Vector3.One,
            Vector3.Zero,
            Vector3.Zero,
            1,
            0.1f,
            0.2f,
            0.3f,
            0.4f,
            RigidBodyTransformType.Dynamic));
        model.AddJoint(new Joint(
            "joint",
            string.Empty,
            JointType.Generic6DofSpring,
            0,
            0,
            Vector3.Zero,
            Vector3.Zero,
            -Vector3.One,
            Vector3.One,
            -Vector3.One,
            Vector3.One,
            Vector3.One,
            Vector3.One));

        var data = new PmdModelWriter().Write(model);
        var decoded = new PmdModelReader().Read(data);

        Assert.Equal("RoundTrip", decoded.Name);
        Assert.Equal("RoundTripEn", decoded.EnglishName);
        Assert.Equal(3, decoded.Vertices.Count);
        Assert.Equal(3, decoded.Indices.Count);
        Assert.Single(decoded.Materials);
        Assert.Equal("body.png", decoded.Textures[0]);
        Assert.Equal("toon01.bmp", decoded.SharedToonTextures[0]);
        Assert.Equal(2, decoded.Bones.Count);
        Assert.Equal("rootEn", decoded.Bones[0].EnglishName);
        Assert.NotNull(decoded.Bones[1].Ik);
        Assert.Equal(2, decoded.Morphs.Count);
        Assert.Equal("smileEn", decoded.Morphs[1].EnglishName);
        Assert.Equal(2, decoded.Labels.Count);
        Assert.Single(decoded.RigidBodies);
        Assert.Single(decoded.Joints);
    }

    [Fact]
    public void ReadsMinimalVmdMotion()
    {
        var data = CreateMinimalVmd();

        var motion = new VmdMotionReader().Read(data);

        Assert.Equal("Model", motion.Name);
        Assert.Empty(motion.BoneKeyframes);
        Assert.Equal(0, motion.MaxFrameIndex);
    }

    [Fact]
    public void WritesAndReadsVmdMotion()
    {
        var motion = new Motion("AsciiModel", MotionFormat.Vmd);
        motion.Add(new BoneKeyframe(
            "center",
            8,
            new Vector3(1, 2, 3),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.25f),
            new BoneInterpolation(
                new BezierCurve(new BezierControlPoint(11, 22), new BezierControlPoint(88, 99)),
                BezierCurve.Linear,
                BezierCurve.Linear,
                BezierCurve.Linear)));
        motion.Add(new MorphKeyframe("smile", 9, 0.5f));
        motion.Add(new CameraKeyframe(
            10,
            new Vector3(4, 5, 6),
            new Vector3(0.1f, 0.2f, 0.3f),
            -42,
            35,
            new CameraInterpolation(
                BezierCurve.Linear,
                new BezierCurve(new BezierControlPoint(12, 24), new BezierControlPoint(96, 108)),
                BezierCurve.Linear,
                BezierCurve.Linear,
                BezierCurve.Linear,
                BezierCurve.Linear),
            false));
        motion.Add(new LightKeyframe(11, new Vector3(0.1f, 0.2f, 0.3f), new Vector3(-1, -2, -3)));
        motion.Add(new SelfShadowKeyframe(12, 1, 99.5f));
        motion.Add(new ModelKeyframe(13, false, new Dictionary<string, bool>(StringComparer.Ordinal) { ["legIK"] = false }));
        var data = new VmdMotionWriter().Write(motion);

        var decoded = new VmdMotionReader().Read(data);

        Assert.Equal("AsciiModel", decoded.Name);
        Assert.Single(decoded.BoneKeyframes);
        Assert.Equal("center", decoded.BoneKeyframes[0].BoneName);
        Assert.Equal(8, decoded.BoneKeyframes[0].FrameIndex);
        Assert.Equal(1, decoded.BoneKeyframes[0].Translation.X, precision: 3);
        Assert.Equal(11, decoded.BoneKeyframes[0].Interpolation.TranslationX.P1.X);
        Assert.Single(decoded.MorphKeyframes);
        Assert.Equal("smile", decoded.MorphKeyframes[0].MorphName);
        Assert.Equal(0.5f, decoded.MorphKeyframes[0].Weight, precision: 3);
        Assert.Single(decoded.CameraKeyframes);
        Assert.Equal(-42, decoded.CameraKeyframes[0].Distance, precision: 3);
        Assert.False(decoded.CameraKeyframes[0].PerspectiveEnabled);
        Assert.Equal(12, decoded.CameraKeyframes[0].Interpolation.LookAtY.P1.X);
        Assert.Single(decoded.LightKeyframes);
        Assert.Equal(-2, decoded.LightKeyframes[0].Direction.Y, precision: 3);
        Assert.Single(decoded.SelfShadowKeyframes);
        Assert.Equal(99.5f, decoded.SelfShadowKeyframes[0].Distance, precision: 3);
        Assert.Single(decoded.ModelKeyframes);
        Assert.False(decoded.ModelKeyframes[0].Visible);
        Assert.False(decoded.ModelKeyframes[0].IkStates["legIK"]);
        Assert.Equal(13, decoded.MaxFrameIndex);
    }

    [Fact]
    public void WritesAndReadsNmdMotion()
    {
        var motion = new Motion("NmdModel", MotionFormat.Nmd);
        motion.Add(new BoneKeyframe(
            "センター",
            12,
            new Vector3(1, 2, 3),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
            new BoneInterpolation(
                new BezierCurve(new BezierControlPoint(10, 20), new BezierControlPoint(80, 90)),
                BezierCurve.Linear,
                BezierCurve.Linear,
                BezierCurve.Linear),
            false));
        motion.Add(new MorphKeyframe("笑い", 13, 0.75f));
        motion.Add(new CameraKeyframe(
            14,
            new Vector3(4, 5, 6),
            new Vector3(0.1f, 0.2f, 0.3f),
            -30,
            40,
            CameraInterpolation.Linear,
            false));
        motion.Add(new LightKeyframe(15, new Vector3(0.7f, 0.8f, 0.9f), new Vector3(-1, -2, -3)));
        motion.Add(new ModelKeyframe(16, false, new Dictionary<string, bool>(StringComparer.Ordinal) { ["右足IK"] = false }));
        motion.Add(new AccessoryKeyframe(
            "stage",
            17,
            true,
            new Vector3(7, 8, 9),
            new Vector3(0.2f, 0.3f, 0.4f),
            1.5f,
            0.6f,
            "model",
            "頭"));
        motion.Add(new SelfShadowKeyframe(18, 2, 55.5f));
        var data = new NmdMotionWriter().Write(motion);

        var decoded = new NmdMotionReader().Read(data);

        Assert.Equal("NmdModel", decoded.Name);
        Assert.Single(decoded.BoneKeyframes);
        Assert.Equal("センター", decoded.BoneKeyframes[0].BoneName);
        Assert.Equal(12, decoded.BoneKeyframes[0].FrameIndex);
        Assert.False(decoded.BoneKeyframes[0].PhysicsSimulationEnabled);
        Assert.Equal(10, decoded.BoneKeyframes[0].Interpolation.TranslationX.P1.X);
        Assert.Single(decoded.MorphKeyframes);
        Assert.Equal("笑い", decoded.MorphKeyframes[0].MorphName);
        Assert.Equal(0.75f, decoded.MorphKeyframes[0].Weight, precision: 3);
        Assert.Single(decoded.CameraKeyframes);
        Assert.False(decoded.CameraKeyframes[0].PerspectiveEnabled);
        Assert.Single(decoded.LightKeyframes);
        Assert.Equal(0.8f, decoded.LightKeyframes[0].Color.Y, precision: 3);
        Assert.Single(decoded.ModelKeyframes);
        Assert.False(decoded.ModelKeyframes[0].Visible);
        Assert.False(decoded.ModelKeyframes[0].IkStates["右足IK"]);
        Assert.Single(decoded.AccessoryKeyframes);
        Assert.Equal("stage", decoded.AccessoryKeyframes[0].AccessoryName);
        Assert.Equal("model", decoded.AccessoryKeyframes[0].ParentModelName);
        Assert.Equal("頭", decoded.AccessoryKeyframes[0].ParentBoneName);
        Assert.Single(decoded.SelfShadowKeyframes);
        Assert.Equal(18, decoded.SelfShadowKeyframes[0].FrameIndex);
        Assert.Equal(55.5f, decoded.SelfShadowKeyframes[0].Distance, precision: 3);
        Assert.Equal(18, decoded.MaxFrameIndex);
    }

    [Fact]
    public void WritesAndReadsNmdMetadata()
    {
        var motion = new Motion("MetaMotion", MotionFormat.Nmd);
        motion.Annotations["author"] = "tester";
        motion.Add(new BoneKeyframe("center", 1, Vector3.One, Quaternion.Identity, BoneInterpolation.Linear)
        {
            IsSelected = true,
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["note"] = "bone"
            }
        });
        motion.Add(new ModelKeyframe(2, true, new Dictionary<string, bool>(StringComparer.Ordinal) { ["legIK"] = false })
        {
            Annotations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["note"] = "model"
            },
            EffectParameters =
            [
                new MotionEffectParameter("ModelEnabled", new MotionEffectParameterValue.Bool(true)),
                new MotionEffectParameter("ModelMode", new MotionEffectParameterValue.Int(42))
            ]
        });
        motion.Add(new AccessoryKeyframe("stage", 3, true, Vector3.Zero, Vector3.Zero, 1, 0.5f)
        {
            EffectParameters =
            [
                new MotionEffectParameter("AccessoryOpacity", new MotionEffectParameterValue.Float(0.25f)),
                new MotionEffectParameter("AccessoryColor", new MotionEffectParameterValue.Vector4(new Vector4(1, 2, 3, 4)))
            ]
        });

        var data = new NmdMotionWriter().Write(motion);
        var decoded = new NmdMotionReader().Read(data);

        Assert.Equal("tester", decoded.Annotations["author"]);
        Assert.True(decoded.BoneKeyframes[0].IsSelected);
        Assert.Equal("bone", decoded.BoneKeyframes[0].Annotations["note"]);
        Assert.Equal("model", decoded.ModelKeyframes[0].Annotations["note"]);
        Assert.Contains(decoded.ModelKeyframes[0].EffectParameters, parameter =>
            parameter.Name == "ModelEnabled" && parameter.Value is MotionEffectParameterValue.Bool { Value: true });
        Assert.Contains(decoded.ModelKeyframes[0].EffectParameters, parameter =>
            parameter.Name == "ModelMode" && parameter.Value is MotionEffectParameterValue.Int { Value: 42 });
        Assert.Contains(decoded.AccessoryKeyframes[0].EffectParameters, parameter =>
            parameter.Name == "AccessoryOpacity" && parameter.Value is MotionEffectParameterValue.Float { Value: > 0.24f and < 0.26f });
        Assert.Contains(decoded.AccessoryKeyframes[0].EffectParameters, parameter =>
            parameter.Name == "AccessoryColor" && parameter.Value is MotionEffectParameterValue.Vector4 { Value.W: 4 });
    }

    [Fact]
    public void ReadsMinimalDirectXAccessory()
    {
        var data = Encoding.UTF8.GetBytes("""
            xof 0303txt 0032
            Mesh Cube {
              3;
              0.0;0.0;0.0;,
              1.0;0.0;0.0;,
              0.0;1.0;0.0;;
              1;
              3;0,1,2;;
              MeshTextureCoords {
                3;
                0.0;0.0;,
                1.0;0.0;,
                0.0;1.0;;
              }
              MeshMaterialList {
                1;
                1;
                0;;
                Material Mat {
                  1.0;1.0;1.0;1.0;;
                  8.0;
                  0.2;0.2;0.2;;
                  0.0;0.0;0.0;;
                  TextureFilename { "texture.png"; }
                }
              }
            }
            """);

        var document = new DirectXAccessoryReader().Read(data, "accessory.x");

        Assert.Equal("accessory.x", document.SourceName);
        Assert.Single(document.Meshes);
        Assert.Equal(3, document.VertexCount);
        Assert.Equal(1, document.FaceCount);
        Assert.Equal(1, document.MaterialCount);
        Assert.Equal("texture.png", document.Meshes[0].Materials[0].TextureFilename);
    }

    [Fact]
    public void WritesAndReadsDirectXAccessory()
    {
        var mesh = new AccessoryMesh(
            "Quad.Mesh",
            [Vector3.Zero, Vector3.UnitX, Vector3.One, Vector3.UnitY],
            [new AccessoryFace([0, 1, 2]), new AccessoryFace([0, 2, 3])],
            [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
            [Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY],
            [
                new AccessoryMaterial(
                    "Body Mat",
                    new Vector4(0.8f, 0.7f, 0.6f, 0.5f),
                    new Vector3(0.1f, 0.2f, 0.3f),
                    new Vector3(0.4f, 0.5f, 0.6f),
                    32,
                    "body.png",
                    "body_n.png")
            ],
            [0, 0]);
        var document = new AccessoryMeshDocument("quad.x", [mesh]);

        var data = new DirectXAccessoryWriter().Write(document);
        var decoded = new DirectXAccessoryReader().Read(data, "quad.x");

        Assert.Equal("quad.x", decoded.SourceName);
        Assert.Single(decoded.Meshes);
        Assert.Equal(4, decoded.VertexCount);
        Assert.Equal(2, decoded.FaceCount);
        Assert.Equal(1, decoded.MaterialCount);
        var decodedMesh = decoded.Meshes[0];
        Assert.Equal("Quad.Mesh", decodedMesh.Name);
        Assert.Equal(4, decodedMesh.Normals.Count);
        Assert.Equal(4, decodedMesh.TextureCoordinates.Count);
        Assert.Equal(0.7f, decodedMesh.Materials[0].Diffuse.Y, precision: 3);
        Assert.Equal(0.2f, decodedMesh.Materials[0].Emissive.Y, precision: 3);
        Assert.Equal(0.5f, decodedMesh.Materials[0].Specular.Y, precision: 3);
        Assert.Equal("body.png", decodedMesh.Materials[0].TextureFilename);
        Assert.Equal("body_n.png", decodedMesh.Materials[0].NormalMapFilename);
    }

    [Fact]
    public void ReadsMmeEffectStructure()
    {
        const string text = """
            // MME-style metadata
            float4x4 WorldViewProj : WORLDVIEWPROJECTION;
            texture2D DiffuseTexture <
              string ResourceName = "diffuse.png";
              string Object = "Geometry";
              float4 Color = float4(1, 0.5, 0.25, 1);
            >;
            float Alpha < bool UIWidget = true; int Order = 2; > = 0.75;

            technique Main < string Script = "object"; > {
              pass P0 < string ScriptExternal = "color"; > {
                VertexShader = compile vs_3_0 VSMain();
                PixelShader = compile ps_3_0 PSMain();
                ZEnable = true;
              }
            }
            """;

        var effect = new MmeEffectReader().ReadText(text, "sample.fx");

        Assert.Equal("sample.fx", effect.SourceName);
        Assert.Equal(3, effect.Parameters.Count);
        Assert.Contains(effect.Parameters, parameter => parameter.Name == "WorldViewProj" && parameter.Semantic == "WORLDVIEWPROJECTION");
        var texture = effect.Parameters.Single(parameter => parameter.Name == "DiffuseTexture");
        Assert.Equal("texture2D", texture.Type);
        Assert.Equal(3, texture.Annotations.Count);
        Assert.Contains(texture.Annotations, annotation => annotation.Name == "ResourceName" && annotation.Value is EffectValue.String { Value: "diffuse.png" });
        Assert.Contains(texture.Annotations, annotation => annotation.Name == "Color" && annotation.Value is EffectValue.Vector { ComponentCount: 4 });
        var alpha = effect.Parameters.Single(parameter => parameter.Name == "Alpha");
        Assert.Equal("0.75", alpha.DefaultValue);
        Assert.Contains(alpha.Annotations, annotation => annotation.Name == "UIWidget" && annotation.Value is EffectValue.Bool { Value: true });
        Assert.Single(effect.Techniques);
        Assert.Equal("Main", effect.Techniques[0].Name);
        Assert.Single(effect.Techniques[0].Passes);
        var pass = effect.Techniques[0].Passes[0];
        Assert.Equal("P0", pass.Name);
        Assert.Equal("compile vs_3_0 VSMain()", pass.States["VertexShader"]);
        Assert.Equal("true", pass.States["ZEnable"]);
        Assert.Equal(1, effect.PassCount);
    }

    private static byte[] CreateMinimalPmd()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, shiftJis);
        writer.Write(Encoding.ASCII.GetBytes("Pmd"));
        writer.Write(1.0f);
        WriteFixed(writer, "PmdModel", 20, shiftJis);
        WriteFixed(writer, string.Empty, 256, shiftJis);
        writer.Write(1);
        WriteVector3(writer, 0, 0, 0);
        WriteVector3(writer, 0, 1, 0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((byte)100);
        writer.Write((byte)0);
        writer.Write(3);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(1);
        WriteVector3(writer, 1, 1, 1);
        writer.Write(1f);
        writer.Write(8f);
        WriteVector3(writer, 0.2f, 0.2f, 0.2f);
        WriteVector3(writer, 0.1f, 0.1f, 0.1f);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write(3);
        WriteFixed(writer, "texture.png", 20, shiftJis);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0);
        for (var i = 0; i < 10; i++)
        {
            WriteFixed(writer, string.Empty, 100, shiftJis);
        }

        writer.Write(0);
        writer.Write(0);
        return stream.ToArray();
    }


    private static byte[] CreateMinimalPmx()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(Encoding.ASCII.GetBytes("PMX "));
        writer.Write(2.1f);
        writer.Write((byte)8);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        WritePmxString(writer, "初音ミク");
        WritePmxString(writer, "Miku Hatsune");
        WritePmxString(writer, string.Empty);
        WritePmxString(writer, string.Empty);
        for (var i = 0; i < 9; i++)
        {
            writer.Write(0);
        }

        return stream.ToArray();
    }

    private static byte[] CreateMinimalVmd()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, shiftJis);
        WriteFixed(writer, "Vocaloid Motion Data 0002", 30, shiftJis);
        WriteFixed(writer, "Model", 20, shiftJis);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        return stream.ToArray();
    }

    private static void WritePmxString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteFixed(BinaryWriter writer, string value, int length, Encoding encoding)
    {
        var bytes = encoding.GetBytes(value);
        Array.Resize(ref bytes, length);
        writer.Write(bytes);
    }

    private static void WriteVector3(BinaryWriter writer, float x, float y, float z)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
    }
}
