using System.Collections.ObjectModel;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public enum ModelFormat
{
    Unknown,
    Pmd,
    Pmx
}

public sealed class MmdModel
{
    private readonly List<Vertex> _vertices = [];
    private readonly List<int> _indices = [];
    private readonly List<Material> _materials = [];
    private readonly List<Bone> _bones = [];
    private readonly List<Morph> _morphs = [];
    private readonly List<ModelLabel> _labels = [];
    private readonly List<RigidBody> _rigidBodies = [];
    private readonly List<Joint> _joints = [];
    private readonly List<SoftBody> _softBodies = [];
    private readonly List<string> _textures = [];
    private readonly string[] _sharedToonTextures = new string[10];

    public MmdModel(ModelFormat format)
    {
        Format = format;
    }

    public ModelFormat Format { get; set; }

    public Uri? Source { get; set; }

    public string Name { get; set; } = string.Empty;

    public string EnglishName { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public string EnglishComment { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public IReadOnlyList<Vertex> Vertices => new ReadOnlyCollection<Vertex>(_vertices);

    public IReadOnlyList<int> Indices => new ReadOnlyCollection<int>(_indices);

    public IReadOnlyList<Material> Materials => new ReadOnlyCollection<Material>(_materials);

    public IReadOnlyList<Bone> Bones => new ReadOnlyCollection<Bone>(_bones);

    public IReadOnlyList<Morph> Morphs => new ReadOnlyCollection<Morph>(_morphs);

    public IReadOnlyList<ModelLabel> Labels => new ReadOnlyCollection<ModelLabel>(_labels);

    public IReadOnlyList<RigidBody> RigidBodies => new ReadOnlyCollection<RigidBody>(_rigidBodies);

    public IReadOnlyList<Joint> Joints => new ReadOnlyCollection<Joint>(_joints);

    public IReadOnlyList<SoftBody> SoftBodies => new ReadOnlyCollection<SoftBody>(_softBodies);

    public IReadOnlyList<string> Textures => new ReadOnlyCollection<string>(_textures);

    public IReadOnlyList<string> SharedToonTextures => new ReadOnlyCollection<string>(_sharedToonTextures);

    public void AddVertex(Vertex vertex) => _vertices.Add(vertex);

    public void AddIndex(int index) => _indices.Add(index);

    public int AddTexture(string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return -1;
        }

        var existingIndex = _textures.FindIndex(item => string.Equals(item, texturePath, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            return existingIndex;
        }

        _textures.Add(texturePath);
        return _textures.Count - 1;
    }

    public void AddMaterial(Material material) => _materials.Add(material);

    public void SetSharedToonTexture(int index, string texturePath)
    {
        if (index < 0 || index >= _sharedToonTextures.Length)
        {
            return;
        }

        _sharedToonTextures[index] = texturePath;
    }

    public void AddBone(Bone bone) => _bones.Add(bone);

    public void AddMorph(Morph morph) => _morphs.Add(morph);

    public void AddLabel(ModelLabel label) => _labels.Add(label);

    public void AddRigidBody(RigidBody rigidBody) => _rigidBodies.Add(rigidBody);

    public void AddJoint(Joint joint) => _joints.Add(joint);

    public void AddSoftBody(SoftBody softBody) => _softBodies.Add(softBody);

    public void ReplaceVertices(IEnumerable<Vertex> vertices)
    {
        _vertices.Clear();
        _vertices.AddRange(vertices);
    }

    public void ReplaceIndices(IEnumerable<int> indices)
    {
        _indices.Clear();
        _indices.AddRange(indices);
    }

    public void ReplaceMaterials(IEnumerable<Material> materials)
    {
        _materials.Clear();
        _materials.AddRange(materials);
    }

    public void ReplaceBones(IEnumerable<Bone> bones)
    {
        _bones.Clear();
        _bones.AddRange(bones);
    }

    public void ReplaceMorphs(IEnumerable<Morph> morphs)
    {
        _morphs.Clear();
        _morphs.AddRange(morphs);
    }

    public void ReplaceLabels(IEnumerable<ModelLabel> labels)
    {
        _labels.Clear();
        _labels.AddRange(labels);
    }

    public void ReplaceRigidBodies(IEnumerable<RigidBody> rigidBodies)
    {
        _rigidBodies.Clear();
        _rigidBodies.AddRange(rigidBodies);
    }

    public void ReplaceJoints(IEnumerable<Joint> joints)
    {
        _joints.Clear();
        _joints.AddRange(joints);
    }

    public void ReplaceSoftBodies(IEnumerable<SoftBody> softBodies)
    {
        _softBodies.Clear();
        _softBodies.AddRange(softBodies);
    }

    public void ReplaceTextures(IEnumerable<string> textures)
    {
        _textures.Clear();
        _textures.AddRange(textures);
    }

    public void ReplaceSharedToonTextures(IEnumerable<string> texturePaths)
    {
        Array.Clear(_sharedToonTextures);
        var index = 0;
        foreach (var texturePath in texturePaths.Take(_sharedToonTextures.Length))
        {
            _sharedToonTextures[index++] = texturePath;
        }
    }
}
