namespace Zhengyan.MikuMikuDance.Formats.Pmx;

public enum PmxTextEncoding
{
    Utf16Le = 0,
    Utf8 = 1
}

public sealed record PmxHeader(
    float Version,
    PmxTextEncoding TextEncoding,
    int AdditionalUvCount,
    int VertexIndexSize,
    int TextureIndexSize,
    int MaterialIndexSize,
    int BoneIndexSize,
    int MorphIndexSize,
    int RigidBodyIndexSize);
