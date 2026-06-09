using Silk.NET.OpenGL;
using StbImageSharp;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlTextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, OpenGlCachedTexture> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _baseDirectory;
    private uint _whiteTexture;

    public OpenGlTextureCache(GL gl, string? baseDirectory = null)
    {
        _gl = gl;
        _baseDirectory = baseDirectory;
        _whiteTexture = CreateSolidTexture(255, 255, 255, 255);
    }

    public uint WhiteTexture => _whiteTexture;

    public uint GetTexture(string? texturePath, bool mipmapEnabled = false)
    {
        return GetTextureInfo(texturePath, mipmapEnabled).Texture;
    }

    public OpenGlTextureInfo GetTextureInfo(string? texturePath, bool mipmapEnabled = false)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return new OpenGlTextureInfo(_whiteTexture, 1, 1, false);
        }

        if (RenderToonResources.TryGetSharedToonIndex(texturePath, out var toonIndex))
        {
            return GetBuiltInToonTexture(toonIndex, mipmapEnabled);
        }

        var resolved = ResolvePath(texturePath);
        if (resolved is null || !File.Exists(resolved))
        {
            return new OpenGlTextureInfo(_whiteTexture, 1, 1, false);
        }

        if (_textures.TryGetValue(resolved, out var existing))
        {
            if (mipmapEnabled && !existing.MipmapEnabled)
            {
                EnableMipmap(existing.Texture);
                existing = existing with { MipmapEnabled = true };
                _textures[resolved] = existing;
            }

            return existing.ToInfo();
        }

        var texture = LoadTexture(resolved, mipmapEnabled);
        _textures[resolved] = texture;
        return texture.ToInfo();
    }

    public bool HasTransparentPixels(string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return false;
        }

        if (RenderToonResources.TryGetSharedToonIndex(texturePath, out _))
        {
            return false;
        }

        var resolved = ResolvePath(texturePath);
        if (resolved is null || !File.Exists(resolved))
        {
            return false;
        }

        if (_textures.TryGetValue(resolved, out var existing))
        {
            return existing.HasTransparentPixels;
        }

        var texture = LoadTexture(resolved, mipmapEnabled: false);
        _textures[resolved] = texture;
        return texture.HasTransparentPixels;
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            _gl.DeleteTexture(texture.Texture);
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
        return OpenGlTexturePathResolver.Resolve(texturePath, _baseDirectory);
    }

    private OpenGlCachedTexture LoadTexture(string path, bool mipmapEnabled)
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

        ConfigureTexture(mipmapEnabled);
        return new OpenGlCachedTexture(
            texture,
            mipmapEnabled,
            RenderTextureAlpha.HasTransparentPixels(image.Data, image.Width, image.Height),
            image.Width,
            image.Height);
    }

    private OpenGlTextureInfo GetBuiltInToonTexture(int index, bool mipmapEnabled)
    {
        var resource = RenderToonResources.GetSharedToon(index);
        if (_textures.TryGetValue(resource.Uri, out var existing))
        {
            if (mipmapEnabled && !existing.MipmapEnabled)
            {
                EnableMipmap(existing.Texture);
                existing = existing with { MipmapEnabled = true };
                _textures[resource.Uri] = existing;
            }

            return existing.ToInfo();
        }

        var texture = LoadRgbaTexture(resource.RgbaPixels, resource.Width, resource.Height, mipmapEnabled);
        _textures[resource.Uri] = new OpenGlCachedTexture(
            texture,
            mipmapEnabled,
            RenderTextureAlpha.HasTransparentPixels(resource.RgbaPixels, resource.Width, resource.Height),
            resource.Width,
            resource.Height);
        return _textures[resource.Uri].ToInfo();
    }

    private uint LoadRgbaTexture(byte[] rgbaPixels, int width, int height, bool mipmapEnabled)
    {
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
        {
            fixed (byte* data = rgbaPixels)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    data);
            }
        }

        ConfigureTexture(mipmapEnabled);
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

        ConfigureTexture(mipmapEnabled: false);
        return texture;
    }

    private void ConfigureTexture(bool mipmapEnabled)
    {
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)(mipmapEnabled ? GLEnum.LinearMipmapLinear : GLEnum.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        if (mipmapEnabled)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void EnableMipmap(uint texture)
    {
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }
}

internal readonly record struct OpenGlTextureInfo(uint Texture, int Width, int Height, bool HasTransparentPixels);

internal readonly record struct OpenGlCachedTexture(
    uint Texture,
    bool MipmapEnabled,
    bool HasTransparentPixels,
    int Width,
    int Height)
{
    public OpenGlTextureInfo ToInfo()
    {
        return new OpenGlTextureInfo(Texture, Width, Height, HasTransparentPixels);
    }
}
