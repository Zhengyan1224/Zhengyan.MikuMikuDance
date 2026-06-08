namespace Zhengyan.MikuMikuDance.Core.Modeling;

public sealed record MmdModelSnapshot(
    ModelFormat Format,
    Uri? Source,
    string Name,
    string EnglishName,
    string Comment,
    string EnglishComment,
    bool Visible,
    IReadOnlyList<Vertex> Vertices,
    IReadOnlyList<int> Indices,
    IReadOnlyList<Material> Materials,
    IReadOnlyList<Bone> Bones,
    IReadOnlyList<Morph> Morphs,
    IReadOnlyList<ModelLabel> Labels,
    IReadOnlyList<RigidBody> RigidBodies,
    IReadOnlyList<Joint> Joints,
    IReadOnlyList<SoftBody> SoftBodies,
    IReadOnlyList<string> Textures,
    IReadOnlyList<string> SharedToonTextures)
{
    public static MmdModelSnapshot Capture(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new MmdModelSnapshot(
            model.Format,
            model.Source,
            model.Name,
            model.EnglishName,
            model.Comment,
            model.EnglishComment,
            model.Visible,
            model.Vertices.ToArray(),
            model.Indices.ToArray(),
            model.Materials.ToArray(),
            model.Bones.ToArray(),
            model.Morphs.ToArray(),
            model.Labels.ToArray(),
            model.RigidBodies.ToArray(),
            model.Joints.ToArray(),
            model.SoftBodies.ToArray(),
            model.Textures.ToArray(),
            model.SharedToonTextures.ToArray());
    }

    public void Restore(MmdModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Format = Format;
        model.Source = Source;
        model.Name = Name;
        model.EnglishName = EnglishName;
        model.Comment = Comment;
        model.EnglishComment = EnglishComment;
        model.Visible = Visible;
        model.ReplaceVertices(Vertices);
        model.ReplaceIndices(Indices);
        model.ReplaceMaterials(Materials);
        model.ReplaceBones(Bones);
        model.ReplaceMorphs(Morphs);
        model.ReplaceLabels(Labels);
        model.ReplaceRigidBodies(RigidBodies);
        model.ReplaceJoints(Joints);
        model.ReplaceSoftBodies(SoftBodies);
        model.ReplaceTextures(Textures);
        model.ReplaceSharedToonTextures(SharedToonTextures);
    }
}
