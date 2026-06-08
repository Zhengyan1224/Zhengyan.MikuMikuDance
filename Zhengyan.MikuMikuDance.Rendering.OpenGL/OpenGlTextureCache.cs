using Silk.NET.OpenGL;
using StbImageSharp;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlTextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, uint> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _baseDirectory;
    private uint _whiteTexture;

    public OpenGlTextureCache(GL gl, string? baseDirectory = null)
    {
        _gl = gl;
        _baseDirectory = baseDirectory;
        _whiteTexture = CreateSolidTexture(255, 255, 255, 255);
    }

    public uint WhiteTexture => _whiteTexture;

    public uint GetTexture(string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return _whiteTexture;
        }

        var resolved = ResolvePath(texturePath);
        if (resolved is null || !File.Exists(resolved))
        {
            return _whiteTexture;
        }

        if (_textures.TryGetValue(resolved, out var existing))
        {
            return existing;
        }

        var texture = LoadTexture(resolved);
        _textures[resolved] = texture;
        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            _gl.DeleteTexture(texture);
        }

        _textures.Clear();
        if (_whiteTexture != 0)
        {
            _gl.DeleteTexture(_whiteTexture);
            _whiteTexture = 0;
        }
    }

    private string? ResolvePath(string texturePath)
    {
        if (Path.IsPathRooted(texturePath))
        {
            return Path.GetFullPath(texturePath);
        }

        return string.IsNullOrWhiteSpace(_baseDirectory)
            ? Path.GetFullPath(texturePath)
            : Path.GetFullPath(Path.Combine(_baseDirectory, texturePath));
    }

    private uint LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
        {
            fixed (byte* data = image.Data)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    (uint)image.Width,
                    (uint)image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    data);
            }
        }

        ConfigureTexture();
        return texture;
    }

    private uint CreateSolidTexture(byte r, byte g, byte b, byte a)
    {
        var data = new[] { r, g, b, a };
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    1,
                    1,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }
        }

        ConfigureTexture();
        return texture;
    }

    private void ConfigureTexture()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }
}
