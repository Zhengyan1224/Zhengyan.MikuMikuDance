using System.Globalization;
using System.Numerics;
using System.Text;
using Zhengyan.MikuMikuDance.Core.Scene;

namespace Zhengyan.MikuMikuDance.Formats.DirectX;

public sealed class DirectXAccessoryReader
{
    public AccessoryMeshDocument Read(ReadOnlyMemory<byte> data, string sourceName = "")
    {
        var text = Encoding.UTF8.GetString(data.Span);
        if (!text.TrimStart().StartsWith("xof", StringComparison.Ordinal))
        {
            throw new MmdFormatException("Invalid DirectX .x signature.");
        }

        var parser = new DirectXTextParser(text);
        return new AccessoryMeshDocument(sourceName, parser.ParseMeshes())
        {
            Source = string.IsNullOrWhiteSpace(sourceName)
                ? null
                : new Uri(sourceName, UriKind.RelativeOrAbsolute)
        };
    }

    public AccessoryMeshDocument Read(Stream stream, string sourceName = "")
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray(), sourceName);
    }

    private enum TokenKind
    {
        Identifier,
        Number,
        String,
        Symbol,
        End
    }

    private readonly record struct Token(TokenKind Kind, string Text, char Symbol, int Offset);

    private sealed class DirectXTextParser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public DirectXTextParser(string text)
        {
            _tokens = Tokenize(text);
        }

        public IReadOnlyList<AccessoryMesh> ParseMeshes()
        {
            var meshes = new List<AccessoryMesh>();
            while (!CurrentIsEnd)
            {
                if (CurrentIsIdentifier("Mesh"))
                {
                    meshes.Add(ParseMesh());
                }
                else
                {
                    Advance();
                }
            }

            return meshes;
        }

        private AccessoryMesh ParseMesh()
        {
            ExpectIdentifier("Mesh");
            var name = Current.Kind == TokenKind.Identifier ? Advance().Text : string.Empty;
            ExpectSymbol('{');

            var vertexCount = ReadInt();
            ExpectSymbol(';');
            var vertices = new List<Vector3>(vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                vertices.Add(ReadVector3());
                ReadListSeparator(i == vertexCount - 1);
            }

            var faceCount = ReadInt();
            ExpectSymbol(';');
            var faces = new List<AccessoryFace>(faceCount);
            for (var i = 0; i < faceCount; i++)
            {
                var indexCount = ReadInt();
                ExpectSymbol(';');
                var indices = new List<int>(indexCount);
                for (var j = 0; j < indexCount; j++)
                {
                    indices.Add(ReadInt());
                    if (j < indexCount - 1)
                    {
                        ExpectSymbol(',');
                    }
                }

                ExpectSymbol(';');
                ReadListSeparator(i == faceCount - 1);
                faces.Add(new AccessoryFace(indices));
            }

            var normals = new List<Vector3>();
            var textureCoordinates = new List<Vector2>();
            var materials = new List<AccessoryMaterial>();
            var faceMaterialIndices = new List<int>();
            while (!CurrentIsEnd && !CurrentIsSymbol('}'))
            {
                if (CurrentIsIdentifier("MeshNormals"))
                {
                    normals = ParseMeshNormals();
                }
                else if (CurrentIsIdentifier("MeshTextureCoords") || CurrentIsIdentifier("MeshTextureCords"))
                {
                    textureCoordinates = ParseMeshTextureCoordinates();
                }
                else if (CurrentIsIdentifier("MeshMaterialList"))
                {
                    (materials, faceMaterialIndices) = ParseMeshMaterialList();
                }
                else
                {
                    SkipObjectOrToken();
                }
            }

            ExpectSymbol('}');
            return new AccessoryMesh(name, vertices, faces, normals, textureCoordinates, materials, faceMaterialIndices);
        }

        private List<Vector3> ParseMeshNormals()
        {
            ExpectIdentifier(Current.Text);
            ExpectSymbol('{');
            var count = ReadInt();
            ExpectSymbol(';');
            var normals = new List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                normals.Add(ReadVector3());
                ReadListSeparator(i == count - 1);
            }

            var faceNormalCount = ReadInt();
            ExpectSymbol(';');
            for (var i = 0; i < faceNormalCount; i++)
            {
                var indexCount = ReadInt();
                ExpectSymbol(';');
                for (var j = 0; j < indexCount; j++)
                {
                    ReadInt();
                    if (j < indexCount - 1)
                    {
                        ExpectSymbol(',');
                    }
                }

                ExpectSymbol(';');
                ReadListSeparator(i == faceNormalCount - 1);
            }

            ExpectSymbol('}');
            return normals;
        }

        private List<Vector2> ParseMeshTextureCoordinates()
        {
            ExpectIdentifier(Current.Text);
            ExpectSymbol('{');
            var count = ReadInt();
            ExpectSymbol(';');
            var textureCoordinates = new List<Vector2>(count);
            for (var i = 0; i < count; i++)
            {
                textureCoordinates.Add(ReadVector2());
                ReadListSeparator(i == count - 1);
            }

            ExpectSymbol('}');
            return textureCoordinates;
        }

        private (List<AccessoryMaterial> Materials, List<int> FaceMaterialIndices) ParseMeshMaterialList()
        {
            ExpectIdentifier("MeshMaterialList");
            ExpectSymbol('{');
            var materialCount = ReadInt();
            ExpectSymbol(';');
            var faceIndexCount = ReadInt();
            ExpectSymbol(';');
            var faceMaterialIndices = new List<int>(faceIndexCount);
            for (var i = 0; i < faceIndexCount; i++)
            {
                faceMaterialIndices.Add(ReadInt());
                ReadListSeparator(i == faceIndexCount - 1);
            }

            var materials = new List<AccessoryMaterial>(materialCount);
            while (!CurrentIsEnd && !CurrentIsSymbol('}'))
            {
                if (CurrentIsIdentifier("Material"))
                {
                    materials.Add(ParseMaterial());
                }
                else
                {
                    SkipObjectOrToken();
                }
            }

            ExpectSymbol('}');
            return (materials, faceMaterialIndices);
        }

        private AccessoryMaterial ParseMaterial()
        {
            ExpectIdentifier("Material");
            var name = Current.Kind == TokenKind.Identifier ? Advance().Text : string.Empty;
            ExpectSymbol('{');
            var diffuse = ReadColor();
            ExpectSymbol(';');
            var shininess = ReadFloat();
            ExpectSymbol(';');
            var specular = ReadVector3();
            ExpectSymbol(';');
            var emissive = ReadVector3();
            ExpectSymbol(';');
            string? textureFilename = null;
            string? normalMapFilename = null;

            while (!CurrentIsEnd && !CurrentIsSymbol('}'))
            {
                if (CurrentIsIdentifier("TextureFilename"))
                {
                    textureFilename = ParseFilenameChunk("TextureFilename");
                }
                else if (CurrentIsIdentifier("NormalmapFilename") || CurrentIsIdentifier("NormalMapFilename"))
                {
                    normalMapFilename = ParseFilenameChunk(Current.Text);
                }
                else
                {
                    SkipObjectOrToken();
                }
            }

            ExpectSymbol('}');
            return new AccessoryMaterial(name, diffuse, emissive, specular, shininess, textureFilename, normalMapFilename);
        }

        private string ParseFilenameChunk(string identifier)
        {
            ExpectIdentifier(identifier);
            ExpectSymbol('{');
            var filename = Expect(TokenKind.String).Text;
            ExpectSymbol(';');
            ExpectSymbol('}');
            return filename;
        }

        private Vector2 ReadVector2()
        {
            var x = ReadFloat();
            ExpectSymbol(';');
            var y = ReadFloat();
            ExpectSymbol(';');
            return new Vector2(x, y);
        }

        private Vector3 ReadVector3()
        {
            var x = ReadFloat();
            ExpectSymbol(';');
            var y = ReadFloat();
            ExpectSymbol(';');
            var z = ReadFloat();
            ExpectSymbol(';');
            return new Vector3(x, y, z);
        }

        private Vector4 ReadColor()
        {
            var r = ReadFloat();
            ExpectSymbol(';');
            var g = ReadFloat();
            ExpectSymbol(';');
            var b = ReadFloat();
            ExpectSymbol(';');
            var a = ReadFloat();
            ExpectSymbol(';');
            return new Vector4(r, g, b, a);
        }

        private int ReadInt()
        {
            var token = Expect(TokenKind.Number);
            return int.Parse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private float ReadFloat()
        {
            var token = Expect(TokenKind.Number);
            return float.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void ReadListSeparator(bool last)
        {
            if (CurrentIsSymbol(','))
            {
                Advance();
            }
            else if (last && CurrentIsSymbol(';'))
            {
                Advance();
            }
        }

        private void SkipObjectOrToken()
        {
            if (Current.Kind == TokenKind.Identifier && Peek().Symbol == '{')
            {
                Advance();
                ExpectSymbol('{');
                var depth = 1;
                while (!CurrentIsEnd && depth > 0)
                {
                    if (CurrentIsSymbol('{'))
                    {
                        depth++;
                    }
                    else if (CurrentIsSymbol('}'))
                    {
                        depth--;
                    }

                    Advance();
                }
            }
            else
            {
                Advance();
            }
        }

        private bool CurrentIsEnd => Current.Kind == TokenKind.End;

        private Token Current => _tokens[_position];

        private Token Peek() => _position + 1 < _tokens.Count ? _tokens[_position + 1] : _tokens[^1];

        private bool CurrentIsIdentifier(string value)
        {
            return Current.Kind == TokenKind.Identifier && string.Equals(Current.Text, value, StringComparison.Ordinal);
        }

        private bool CurrentIsSymbol(char value)
        {
            return Current.Kind == TokenKind.Symbol && Current.Symbol == value;
        }

        private Token Advance()
        {
            return _tokens[_position++];
        }

        private void ExpectIdentifier(string value)
        {
            var token = Expect(TokenKind.Identifier);
            if (!string.Equals(token.Text, value, StringComparison.Ordinal))
            {
                throw new MmdFormatException($"Expected '{value}' at offset {token.Offset}, got '{token.Text}'.");
            }
        }

        private void ExpectSymbol(char symbol)
        {
            var token = Expect(TokenKind.Symbol);
            if (token.Symbol != symbol)
            {
                throw new MmdFormatException($"Expected '{symbol}' at offset {token.Offset}, got '{token.Symbol}'.");
            }
        }

        private Token Expect(TokenKind kind)
        {
            var token = Advance();
            if (token.Kind != kind)
            {
                throw new MmdFormatException($"Expected {kind} at offset {token.Offset}, got {token.Kind}.");
            }

            return token;
        }

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            var offset = 0;
            while (offset < text.Length)
            {
                var c = text[offset];
                if (char.IsWhiteSpace(c))
                {
                    offset++;
                    continue;
                }

                if (c == '/' && offset + 1 < text.Length && text[offset + 1] == '/')
                {
                    offset += 2;
                    while (offset < text.Length && text[offset] != '\n')
                    {
                        offset++;
                    }

                    continue;
                }

                if (c == '/' && offset + 1 < text.Length && text[offset + 1] == '*')
                {
                    offset += 2;
                    while (offset + 1 < text.Length && !(text[offset] == '*' && text[offset + 1] == '/'))
                    {
                        offset++;
                    }

                    offset = Math.Min(text.Length, offset + 2);
                    continue;
                }

                if (c == '"')
                {
                    var start = offset++;
                    var builder = new StringBuilder();
                    while (offset < text.Length && text[offset] != '"')
                    {
                        builder.Append(text[offset++]);
                    }

                    if (offset >= text.Length)
                    {
                        throw new MmdFormatException($"Unterminated string at offset {start}.");
                    }

                    offset++;
                    tokens.Add(new Token(TokenKind.String, builder.ToString(), '\0', start));
                    continue;
                }

                if ("{};,".Contains(c))
                {
                    tokens.Add(new Token(TokenKind.Symbol, c.ToString(), c, offset++));
                    continue;
                }

                if (IsNumberStart(text, offset))
                {
                    var start = offset++;
                    while (offset < text.Length && IsNumberContinuation(text[offset]))
                    {
                        offset++;
                    }

                    tokens.Add(new Token(TokenKind.Number, text[start..offset], '\0', start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    var start = offset++;
                    while (offset < text.Length && (char.IsLetterOrDigit(text[offset]) || text[offset] is '_' or '$' or '.'))
                    {
                        offset++;
                    }

                    tokens.Add(new Token(TokenKind.Identifier, text[start..offset], '\0', start));
                    continue;
                }

                offset++;
            }

            tokens.Add(new Token(TokenKind.End, string.Empty, '\0', text.Length));
            return tokens;
        }

        private static bool IsNumberStart(string text, int offset)
        {
            var c = text[offset];
            return char.IsDigit(c) || c == '.' || ((c == '-' || c == '+') && offset + 1 < text.Length && (char.IsDigit(text[offset + 1]) || text[offset + 1] == '.'));
        }

        private static bool IsNumberContinuation(char c)
        {
            return char.IsDigit(c) || c is '.' or '-' or '+' or 'e' or 'E';
        }
    }
}
