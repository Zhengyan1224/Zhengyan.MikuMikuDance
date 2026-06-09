using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class RenderMeshBuilderTests
{
    [Fact]
    public void BuildsModelBatchFromMaterialVertexCount()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "triangle" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            new Vector4(1, 0, 0, 1),
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            -1,
            -1,
            SphereTextureMode.Disabled,
            -1,
            false,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);

        Assert.Equal("triangle", mesh.Name);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(3, mesh.Indices.Count);
        Assert.Single(mesh.Batches);
        Assert.Equal(3, mesh.Batches[0].IndexCount);
    }

    [Fact]
    public void PreservesModelMaterialTexturePath()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "textured" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        var textureIndex = model.AddTexture("textures/body.png");
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            Vector4.One,
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            textureIndex,
            -1,
            SphereTextureMode.Disabled,
            -1,
            false,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);

        Assert.Equal("textures/body.png", mesh.Batches[0].Material.TexturePath);
    }

    [Fact]
    public void PreservesModelSphereAndToonTexturePaths()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "textured" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        var sphereIndex = model.AddTexture("sphere.spa");
        var toonIndex = model.AddTexture("toon/toon.png");
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            Vector4.One,
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            -1,
            sphereIndex,
            SphereTextureMode.Add,
            toonIndex,
            false,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);
        var material = mesh.Batches[0].Material;

        Assert.Equal("sphere.spa", material.SphereTexturePath);
        Assert.Equal(SphereTextureBlendMode.Add, material.SphereTextureMode);
        Assert.Equal("toon/toon.png", material.ToonTexturePath);
    }

    [Fact]
    public void PreservesModelEdgeParameters()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "edge" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            Vector4.One,
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            true,
            new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
            2.5f,
            -1,
            -1,
            SphereTextureMode.Disabled,
            -1,
            false,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);
        var material = mesh.Batches[0].Material;

        Assert.True(material.EdgeEnabled);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), material.EdgeColor);
        Assert.Equal(2.5f, material.EdgeSize);
    }

    [Fact]
    public void PreservesModelTransparencyAndDoubleSidedState()
    {
        var model = new MmdModel(ModelFormat.Pmx) { Name = "transparent" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            new Vector4(1, 1, 1, 0.5f),
            Vector3.Zero,
            0,
            Vector3.Zero,
            true,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            -1,
            -1,
            SphereTextureMode.Disabled,
            -1,
            false,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);
        var material = mesh.Batches[0].Material;

        Assert.True(material.IsTransparent);
        Assert.True(material.DoubleSided);
        Assert.True(material.GroundShadowEnabled);
    }

    [Fact]
    public void ResolvesSharedToonTexturePath()
    {
        var model = new MmdModel(ModelFormat.Pmd) { Name = "toon" };
        model.SetSharedToonTexture(2, "custom_toon.bmp");
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            Vector4.One,
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            -1,
            -1,
            SphereTextureMode.Disabled,
            2,
            true,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);

        Assert.Equal("custom_toon.bmp", mesh.Batches[0].Material.ToonTexturePath);
    }

    [Fact]
    public void ResolvesMissingSharedToonToBuiltInResource()
    {
        var model = new MmdModel(ModelFormat.Pmd) { Name = "toon" };
        model.AddVertex(Vertex(0, 0, 0));
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);
        model.AddMaterial(new Material(
            "mat",
            string.Empty,
            Vector4.One,
            Vector3.Zero,
            0,
            Vector3.Zero,
            false,
            true,
            true,
            true,
            false,
            Vector4.Zero,
            1,
            -1,
            -1,
            SphereTextureMode.Disabled,
            0,
            true,
            3,
            string.Empty));

        var mesh = RenderMeshBuilder.FromModel(model);

        Assert.Equal("@builtin/toon01", mesh.Batches[0].Material.ToonTexturePath);
    }

    [Fact]
    public void ProvidesBuiltInSharedToonResourceData()
    {
        Assert.Equal("@builtin/toon10", RenderToonResources.ResolveSharedToonUri(9));
        Assert.True(RenderToonResources.TryGetSharedToonIndex("toon03.bmp", out var legacyIndex));
        Assert.Equal(2, legacyIndex);
        Assert.True(RenderToonResources.TryGetSharedToonIndex("@builtin/toon04", out var builtInIndex));
        Assert.Equal(3, builtInIndex);

        var resource = RenderToonResources.GetSharedToon(0);

        Assert.Equal("toon01.bmp", resource.Name);
        Assert.Equal(RenderToonResources.Width, resource.Width);
        Assert.Equal(RenderToonResources.Height, resource.Height);
        Assert.Equal(resource.Width * resource.Height * 4, resource.ByteLength);
        Assert.Equal(255, resource.RgbaPixels[3]);
        Assert.NotEqual(resource.RgbaPixels[0], resource.RgbaPixels[^4]);
    }

    [Fact]
    public void BuildsModelMeshWithInstanceTransform()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "model" };
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddVertex(Vertex(0, 0, 1));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);

        var instance = project.AddModel(model);
        instance.Name = "instance";
        instance.Transform.Translation = new Vector3(2, 3, 4);
        instance.Transform.Scale = new Vector3(2, 2, 2);

        var mesh = RenderMeshBuilder.FromModel(instance);
        var transformed = Vector3.Transform(Vector3.One, mesh.WorldTransform);

        Assert.Single(project.ModelInstances);
        Assert.Equal("instance", mesh.Name);
        Assert.Equal(new Vector3(4, 5, 6), transformed);
    }

    [Fact]
    public void CopiesModelEffectParameterOverridesToRenderMesh()
    {
        var project = new MmdProject();
        var model = new MmdModel(ModelFormat.Pmx) { Name = "model" };
        model.AddVertex(Vertex(1, 0, 0));
        model.AddVertex(Vertex(0, 1, 0));
        model.AddVertex(Vertex(0, 0, 1));
        model.AddIndex(0);
        model.AddIndex(1);
        model.AddIndex(2);

        var instance = project.AddModel(model);
        instance.EffectParameterOverrides.SetFloat("AlphaScale", 0.5f);
        var mesh = RenderMeshBuilder.FromModel(instance);

        instance.EffectParameterOverrides.SetFloat("AlphaScale", 1f);

        Assert.True(mesh.EffectParameterOverrides.TryGetValue("AlphaScale", out var value));
        Assert.Equal(0.5f, Assert.IsType<MotionEffectParameterValue.Float>(value).Value, precision: 3);
    }

    [Fact]
    public void TriangulatesAccessoryQuads()
    {
        var accessoryMesh = new AccessoryMesh(
            "quad",
            [
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0)
            ],
            [new AccessoryFace([0, 1, 2, 3])],
            [],
            [],
            [new AccessoryMaterial("mat", Vector4.One, Vector3.Zero, Vector3.Zero, 0, null, null)],
            [0]);
        var document = new AccessoryMeshDocument("quad.x", [accessoryMesh]);

        var mesh = RenderMeshBuilder.FromAccessory(document)[0];

        Assert.Equal(6, mesh.Vertices.Count);
        Assert.Equal(6, mesh.Indices.Count);
        Assert.Single(mesh.Batches);
        Assert.Equal(6, mesh.Batches[0].IndexCount);
    }

    [Fact]
    public void PreservesAccessoryMaterialTexturePath()
    {
        var accessoryMesh = new AccessoryMesh(
            "triangle",
            [
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            ],
            [new AccessoryFace([0, 1, 2])],
            [],
            [],
            [new AccessoryMaterial("mat", Vector4.One, Vector3.Zero, Vector3.Zero, 0, "diffuse.png", null)],
            [0]);
        var document = new AccessoryMeshDocument("accessory.x", [accessoryMesh]);

        var mesh = RenderMeshBuilder.FromAccessory(document)[0];

        Assert.Equal("diffuse.png", mesh.Batches[0].Material.TexturePath);
    }

    [Fact]
    public void BuildsAccessoryMeshWithAccessoryTransform()
    {
        var accessoryMesh = new AccessoryMesh(
            "triangle",
            [
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            ],
            [new AccessoryFace([0, 1, 2])],
            [],
            [],
            [],
            []);
        var document = new AccessoryMeshDocument("accessory.x", [accessoryMesh]);
        var accessory = new Accessory("accessory")
        {
            Translation = new Vector3(1, 2, 3),
            Scale = 3
        };

        var mesh = RenderMeshBuilder.FromAccessory(document, accessory)[0];
        var transformed = Vector3.Transform(Vector3.One, mesh.WorldTransform);

        Assert.Equal(new Vector3(4, 5, 6), transformed);
    }

    [Fact]
    public void CopiesAccessoryEffectParameterOverridesToRenderMesh()
    {
        var accessoryMesh = new AccessoryMesh(
            "triangle",
            [
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            ],
            [new AccessoryFace([0, 1, 2])],
            [],
            [],
            [],
            []);
        var document = new AccessoryMeshDocument("accessory.x", [accessoryMesh]);
        var accessory = new Accessory("accessory");
        accessory.EffectParameterOverrides.SetVector4("TintColor", new Vector4(1, 0.5f, 0.25f, 1));

        var mesh = RenderMeshBuilder.FromAccessory(document, accessory)[0];

        Assert.True(mesh.EffectParameterOverrides.TryGetValue("TintColor", out var value));
        Assert.Equal(new Vector4(1, 0.5f, 0.25f, 1), Assert.IsType<MotionEffectParameterValue.Vector4>(value).Value);
    }

    private static Vertex Vertex(float x, float y, float z)
    {
        return new Vertex(
            new Vector3(x, y, z),
            Vector3.UnitZ,
            Vector2.Zero,
            [],
            new SkinningWeights(VertexSkinningType.Bdef1, [0], [1f]),
            1);
    }
}
