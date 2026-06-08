using System.Globalization;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Formats.DirectX;

public sealed class DirectXAccessoryWriter
{
    public byte[] Write(AccessoryMeshDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Encoding.UTF8.GetBytes(WriteText(document));
    }

    public void Write(AccessoryMeshDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Write(document));
    }

    public string WriteText(AccessoryMeshDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        builder.AppendLine("xof 0303txt 0032");
        builder.AppendLine();
        foreach (var mesh in document.Meshes)
        {
            WriteMesh(builder, mesh);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void WriteMesh(StringBuilder builder, AccessoryMesh mesh)
    {
        builder.Append("Mesh ");
        builder.Append(SanitizeIdentifier(mesh.Name, "Mesh"));
        builder.AppendLine(" {");
        builder.Append("  ");
        builder.Append(Invariant(mesh.Vertices.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var vertex = mesh.Vertices[i];
            builder.Append("  ");
            AppendVector3(builder, vertex);
            builder.Append(i == mesh.Vertices.Count - 1 ? ";" : ",");
            builder.AppendLine();
        }

        builder.Append("  ");
        builder.Append(Invariant(mesh.Faces.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];
            builder.Append("  ");
            builder.Append(Invariant(face.Indices.Count));
            builder.Append(";");
            for (var j = 0; j < face.Indices.Count; j++)
            {
                if (j > 0)
                {
                    builder.Append(",");
                }

                builder.Append(Invariant(face.Indices[j]));
            }

            builder.Append(i == mesh.Faces.Count - 1 ? ";;" : ";,");
            builder.AppendLine();
        }

        if (mesh.Normals.Count > 0)
        {
            WriteNormals(builder, mesh);
        }

        if (mesh.TextureCoordinates.Count > 0)
        {
            WriteTextureCoordinates(builder, mesh);
        }

        if (mesh.Materials.Count > 0)
        {
            WriteMaterialList(builder, mesh);
        }

        builder.AppendLine("}");
    }

    private static void WriteNormals(StringBuilder builder, AccessoryMesh mesh)
    {
        builder.AppendLine("  MeshNormals {");
        builder.Append("    ");
        builder.Append(Invariant(mesh.Normals.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.Normals.Count; i++)
        {
            builder.Append("    ");
            AppendVector3(builder, mesh.Normals[i]);
            builder.Append(i == mesh.Normals.Count - 1 ? ";" : ",");
            builder.AppendLine();
        }

        builder.Append("    ");
        builder.Append(Invariant(mesh.Faces.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];
            builder.Append("    ");
            builder.Append(Invariant(face.Indices.Count));
            builder.Append(";");
            for (var j = 0; j < face.Indices.Count; j++)
            {
                if (j > 0)
                {
                    builder.Append(",");
                }

                builder.Append(Invariant(Math.Clamp(face.Indices[j], 0, Math.Max(0, mesh.Normals.Count - 1))));
            }

            builder.Append(i == mesh.Faces.Count - 1 ? ";;" : ";,");
            builder.AppendLine();
        }

        builder.AppendLine("  }");
    }

    private static void WriteTextureCoordinates(StringBuilder builder, AccessoryMesh mesh)
    {
        builder.AppendLine("  MeshTextureCoords {");
        builder.Append("    ");
        builder.Append(Invariant(mesh.TextureCoordinates.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.TextureCoordinates.Count; i++)
        {
            var uv = mesh.TextureCoordinates[i];
            builder.Append("    ");
            builder.Append(Invariant(uv.X));
            builder.Append(";");
            builder.Append(Invariant(uv.Y));
            builder.Append(i == mesh.TextureCoordinates.Count - 1 ? ";;" : ";,");
            builder.AppendLine();
        }

        builder.AppendLine("  }");
    }

    private static void WriteMaterialList(StringBuilder builder, AccessoryMesh mesh)
    {
        builder.AppendLine("  MeshMaterialList {");
        builder.Append("    ");
        builder.Append(Invariant(mesh.Materials.Count));
        builder.AppendLine(";");
        builder.Append("    ");
        builder.Append(Invariant(mesh.Faces.Count));
        builder.AppendLine(";");
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            var materialIndex = i < mesh.FaceMaterialIndices.Count ? mesh.FaceMaterialIndices[i] : 0;
            builder.Append("    ");
            builder.Append(Invariant(Math.Clamp(materialIndex, 0, Math.Max(0, mesh.Materials.Count - 1))));
            builder.Append(i == mesh.Faces.Count - 1 ? ";;" : ",");
            builder.AppendLine();
        }

        foreach (var material in mesh.Materials)
        {
            WriteMaterial(builder, material);
        }

        builder.AppendLine("  }");
    }

    private static void WriteMaterial(StringBuilder builder, AccessoryMaterial material)
    {
        builder.Append("    Material ");
        builder.Append(SanitizeIdentifier(material.Name, "Material"));
        builder.AppendLine(" {");
        builder.Append("      ");
        AppendColor(builder, material.Diffuse);
        builder.AppendLine(";");
        builder.Append("      ");
        builder.Append(Invariant(material.Shininess));
        builder.AppendLine(";");
        builder.Append("      ");
        AppendVector3(builder, material.Specular);
        builder.AppendLine(";");
        builder.Append("      ");
        AppendVector3(builder, material.Emissive);
        builder.AppendLine(";");
        if (!string.IsNullOrEmpty(material.TextureFilename))
        {
            WriteFilename(builder, "TextureFilename", material.TextureFilename);
        }

        if (!string.IsNullOrEmpty(material.NormalMapFilename))
        {
            WriteFilename(builder, "NormalmapFilename", material.NormalMapFilename);
        }

        builder.AppendLine("    }");
    }

    private static void WriteFilename(StringBuilder builder, string chunkName, string filename)
    {
        builder.Append("      ");
        builder.Append(chunkName);
        builder.Append(" { \"");
        builder.Append(filename.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal));
        builder.AppendLine("\"; }");
    }

    private static void AppendVector3(StringBuilder builder, System.Numerics.Vector3 value)
    {
        builder.Append(Invariant(value.X));
        builder.Append(";");
        builder.Append(Invariant(value.Y));
        builder.Append(";");
        builder.Append(Invariant(value.Z));
        builder.Append(";");
    }

    private static void AppendColor(StringBuilder builder, System.Numerics.Vector4 value)
    {
        builder.Append(Invariant(value.X));
        builder.Append(";");
        builder.Append(Invariant(value.Y));
        builder.Append(";");
        builder.Append(Invariant(value.Z));
        builder.Append(";");
        builder.Append(Invariant(value.W));
        builder.Append(";");
    }

    private static string SanitizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '_' or '.' ? c : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static string Invariant(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Invariant(float value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }
}
