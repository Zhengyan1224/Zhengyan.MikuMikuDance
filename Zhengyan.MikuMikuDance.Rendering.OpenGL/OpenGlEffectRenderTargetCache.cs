using System.Numerics;
using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Core.Effects;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlEffectRenderTargetCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, OpenGlEffectColorTarget> _colorTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OpenGlEffectDepthTarget> _depthTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OpenGlEffectOffscreenTarget> _offscreenTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly string?[] _currentColorTargets = new string?[4];
    private readonly Dictionary<string, RenderEffectParameter> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _targetAliases = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentDepthTarget;
    private uint _framebuffer;
    private int _viewportWidth = 1;
    private int _viewportHeight = 1;

    public OpenGlEffectRenderTargetCache(GL gl)
    {
        _gl = gl;
    }

    public void BeginEffect(RenderEffect effect, int viewportWidth, int viewportHeight)
    {
        Array.Clear(_currentColorTargets);
        _currentDepthTarget = null;
        _viewportWidth = Math.Max(1, viewportWidth);
        _viewportHeight = Math.Max(1, viewportHeight);
        _parameters.Clear();
        _targetAliases.Clear();
        foreach (var parameter in effect.Parameters)
        {
            _parameters.TryAdd(parameter.Name, parameter);
        }

        ReconcileTargets(effect);
        BindDefaultFramebuffer(_viewportWidth, _viewportHeight);
    }

    public void SetColorTarget(int index, string value)
    {
        if ((uint)index >= _currentColorTargets.Length)
        {
            return;
        }

        _currentColorTargets[index] = ResolveColorTargetName(value);
        BindCurrentFramebuffer();
    }

    public void SetDepthStencilTarget(string value)
    {
        _currentDepthTarget = ResolveDepthTargetName(value);
        BindCurrentFramebuffer();
    }

    public bool TryBindTexture(RenderEffectShaderUniform uniform, int textureUnitIndex)
    {
        var name = FirstNonEmpty(uniform.TextureSourceName, uniform.Name, uniform.ResourceName);
        name = ResolveColorTargetName(name) ?? ResolveDepthTargetName(name);
        if (!string.IsNullOrWhiteSpace(name) && _colorTargets.TryGetValue(name, out var colorTarget))
        {
            BindTexture(textureUnitIndex, colorTarget.Texture);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(name) && _depthTargets.TryGetValue(name, out var depthTarget))
        {
            BindTexture(textureUnitIndex, depthTarget.Texture);
            return true;
        }

        if (uniform.Semantic is RenderEffectSemantic.RenderColorTarget or RenderEffectSemantic.OffscreenRenderTarget)
        {
            var target = _colorTargets.Values.FirstOrDefault();
            if (target is not null)
            {
                BindTexture(textureUnitIndex, target.Texture);
                return true;
            }
        }

        if (uniform.Semantic == RenderEffectSemantic.RenderDepthStencilTarget)
        {
            var target = _depthTargets.Values.FirstOrDefault();
            if (target is not null)
            {
                BindTexture(textureUnitIndex, target.Texture);
                return true;
            }
        }

        return false;
    }

    public void BindDefaultFramebuffer(int viewportWidth, int viewportHeight)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Math.Max(1, viewportWidth), (uint)Math.Max(1, viewportHeight));
    }

    public IReadOnlyList<RenderEffectOffscreenDrawPlan> CreateOffscreenDrawPlans(
        string ownerDrawableName,
        IEnumerable<string> drawableNames)
    {
        return _offscreenTargets.Values
            .Select(target => target.Metadata.CreateDrawPlan(ownerDrawableName, drawableNames))
            .ToArray();
    }

    public void BindOffscreenTarget(RenderEffectOffscreenTarget target)
    {
        UseOffscreenTarget(target);
        _gl.ClearColor(target.ClearColor.X, target.ClearColor.Y, target.ClearColor.Z, target.ClearColor.W);
        _gl.ClearDepth(target.ClearDepth);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void UseOffscreenTarget(RenderEffectOffscreenTarget target)
    {
        SetColorTarget(0, target.Name);
        SetDepthStencilTarget(string.Empty);
    }

    public void Dispose()
    {
        DeleteTargets();
        if (_framebuffer != 0)
        {
            _gl.DeleteFramebuffer(_framebuffer);
            _framebuffer = 0;
        }
    }

    private void ReconcileTargets(RenderEffect effect)
    {
        var requestedColorTargets = effect.Parameters
            .Where(parameter => parameter.Semantic is RenderEffectSemantic.RenderColorTarget or RenderEffectSemantic.OffscreenRenderTarget)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedDepthTargets = effect.Parameters
            .Where(parameter => parameter.Semantic == RenderEffectSemantic.RenderDepthStencilTarget)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedOffscreenTargets = effect.Parameters
            .Where(parameter => parameter.Semantic == RenderEffectSemantic.OffscreenRenderTarget)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DeleteRemovedTargets(_colorTargets, requestedColorTargets);
        DeleteRemovedTargets(_depthTargets, requestedDepthTargets);
        DeleteRemovedTargets(_offscreenTargets, requestedOffscreenTargets);
        foreach (var parameter in effect.Parameters)
        {
            if (parameter.Semantic is RenderEffectSemantic.RenderColorTarget or RenderEffectSemantic.OffscreenRenderTarget)
            {
                RegisterTargetAliases(parameter.Name);
                EnsureColorTarget(parameter.Name);
                if (parameter.OffscreenTarget is not null)
                {
                    EnsureOffscreenTarget(parameter);
                }
            }
            else if (parameter.Semantic == RenderEffectSemantic.RenderDepthStencilTarget)
            {
                RegisterTargetAliases(parameter.Name);
                EnsureDepthTarget(parameter.Name);
            }
        }
    }

    private void BindCurrentFramebuffer()
    {
        var hasColorTarget = _currentColorTargets.Any(name => !string.IsNullOrWhiteSpace(name));
        var hasDepthTarget = !string.IsNullOrWhiteSpace(_currentDepthTarget);
        if (!hasColorTarget && !hasDepthTarget)
        {
            BindDefaultFramebuffer(_viewportWidth, _viewportHeight);
            return;
        }

        if (_framebuffer == 0)
        {
            _framebuffer = _gl.GenFramebuffer();
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        var activeDrawBuffers = new List<DrawBufferMode>(_currentColorTargets.Length);
        for (var i = 0; i < _currentColorTargets.Length; i++)
        {
            var name = _currentColorTargets[i];
            var attachment = ColorAttachment(i);
            if (string.IsNullOrWhiteSpace(name))
            {
                _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, 0, 0);
                continue;
            }

            var target = EnsureColorTarget(name);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, target.Texture, 0);
            activeDrawBuffers.Add(ColorDrawBuffer(i));
        }

        if (string.IsNullOrWhiteSpace(_currentDepthTarget))
        {
            var firstOffscreenTarget = _currentColorTargets
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => _offscreenTargets.TryGetValue(name!, out var offscreen) ? offscreen : null)
                .FirstOrDefault(target => target is not null);
            if (firstOffscreenTarget is null)
            {
                _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, 0, 0);
            }
            else
            {
                _gl.FramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.DepthAttachment,
                    TextureTarget.Texture2D,
                    firstOffscreenTarget.DepthTexture,
                    0);
            }
        }
        else
        {
            var depthTarget = EnsureDepthTarget(_currentDepthTarget);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, depthTarget.Texture, 0);
        }

        if (activeDrawBuffers.Count > 0)
        {
            _gl.DrawBuffers(activeDrawBuffers.ToArray());
            _gl.ReadBuffer(ToReadBuffer(activeDrawBuffers[0]));
        }
        else
        {
            _gl.DrawBuffer(DrawBufferMode.None);
            _gl.ReadBuffer(ReadBufferMode.None);
        }

        _gl.Viewport(0, 0, (uint)_viewportWidth, (uint)_viewportHeight);
    }

    private OpenGlEffectColorTarget EnsureColorTarget(string name)
    {
        if (_colorTargets.TryGetValue(name, out var existing) &&
            existing.Width == TargetWidth(name) &&
            existing.Height == TargetHeight(name))
        {
            return existing;
        }

        if (existing is not null)
        {
            _gl.DeleteTexture(existing.Texture);
        }

        var width = TargetWidth(name);
        var height = TargetHeight(name);
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
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
                null);
        }

        ConfigureRenderTexture();
        var target = new OpenGlEffectColorTarget(name, texture, width, height);
        _colorTargets[name] = target;
        return target;
    }

    private OpenGlEffectOffscreenTarget EnsureOffscreenTarget(RenderEffectParameter parameter)
    {
        var colorTarget = EnsureColorTarget(parameter.Name);
        if (_offscreenTargets.TryGetValue(parameter.Name, out var existing) &&
            existing.Width == colorTarget.Width &&
            existing.Height == colorTarget.Height)
        {
            return existing with { Metadata = parameter.OffscreenTarget! };
        }

        if (existing is not null)
        {
            _gl.DeleteTexture(existing.DepthTexture);
        }

        var depthTexture = CreateDepthTexture(colorTarget.Width, colorTarget.Height);
        var target = new OpenGlEffectOffscreenTarget(
            parameter.Name,
            colorTarget.Texture,
            depthTexture,
            colorTarget.Width,
            colorTarget.Height,
            parameter.OffscreenTarget!);
        _offscreenTargets[parameter.Name] = target;
        return target;
    }

    private OpenGlEffectDepthTarget EnsureDepthTarget(string name)
    {
        if (_depthTargets.TryGetValue(name, out var existing) &&
            existing.Width == _viewportWidth &&
            existing.Height == _viewportHeight)
        {
            return existing;
        }

        if (existing is not null)
        {
            _gl.DeleteTexture(existing.Texture);
        }

        var texture = CreateDepthTexture(_viewportWidth, _viewportHeight);
        var target = new OpenGlEffectDepthTarget(name, texture, _viewportWidth, _viewportHeight);
        _depthTargets[name] = target;
        return target;
    }

    private uint CreateDepthTexture(int width, int height)
    {
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.DepthComponent24,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.DepthComponent,
                PixelType.UnsignedInt,
                null);
        }

        ConfigureRenderTexture();
        return texture;
    }

    private int TargetWidth(string name)
    {
        var ratio = ViewportRatio(name);
        return Math.Max(1, (int)MathF.Round(_viewportWidth * ratio.X));
    }

    private int TargetHeight(string name)
    {
        var ratio = ViewportRatio(name);
        return Math.Max(1, (int)MathF.Round(_viewportHeight * ratio.Y));
    }

    private Vector2 ViewportRatio(string name)
    {
        if (!_parameters.TryGetValue(name, out var parameter))
        {
            return Vector2.One;
        }

        if (!TryGetAnnotation(parameter, "ViewPortRatio", out var value) &&
            !TryGetAnnotation(parameter, "ViewportRatio", out value))
        {
            return Vector2.One;
        }

        return value is EffectValue.Vector vector
            ? new Vector2(
                vector.ComponentCount > 0 ? Math.Max(0.001f, vector.Value.X) : 1,
                vector.ComponentCount > 1 ? Math.Max(0.001f, vector.Value.Y) : 1)
            : Vector2.One;
    }

    private static bool TryGetAnnotation(RenderEffectParameter parameter, string name, out EffectValue value)
    {
        foreach (var annotation in parameter.Annotations)
        {
            if (string.Equals(annotation.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = annotation.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    private void ConfigureRenderTexture()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void BindTexture(int textureUnitIndex, uint texture)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + textureUnitIndex);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
    }

    private void DeleteTargets()
    {
        foreach (var target in _colorTargets.Values)
        {
            _gl.DeleteTexture(target.Texture);
        }

        foreach (var target in _depthTargets.Values)
        {
            _gl.DeleteTexture(target.Texture);
        }

        foreach (var target in _offscreenTargets.Values)
        {
            _gl.DeleteTexture(target.DepthTexture);
        }

        _colorTargets.Clear();
        _depthTargets.Clear();
        _offscreenTargets.Clear();
    }

    private void DeleteRemovedTargets<TTarget>(Dictionary<string, TTarget> targets, HashSet<string> requested)
        where TTarget : IOpenGlEffectTarget
    {
        foreach (var name in targets.Keys.Where(name => !requested.Contains(name)).ToArray())
        {
            _gl.DeleteTexture(targets[name].Texture);
            targets.Remove(name);
        }
    }

    internal string? ResolveColorTargetNameForTesting(string? value)
    {
        return ResolveColorTargetName(value);
    }

    internal string? ResolveDepthTargetNameForTesting(string? value)
    {
        return ResolveDepthTargetName(value);
    }

    private string? ResolveColorTargetName(string? value)
    {
        var normalized = NormalizeTargetName(value);
        if (normalized is null)
        {
            return null;
        }

        if (_colorTargets.ContainsKey(normalized))
        {
            return normalized;
        }

        return OpenGlEffectRenderTargetNameResolver.TryResolve(normalized, _colorTargets.Keys, _targetAliases, out var targetName)
            ? targetName
            : normalized;
    }

    private string? ResolveDepthTargetName(string? value)
    {
        var normalized = NormalizeTargetName(value);
        if (normalized is null)
        {
            return null;
        }

        if (_depthTargets.ContainsKey(normalized))
        {
            return normalized;
        }

        return OpenGlEffectRenderTargetNameResolver.TryResolve(normalized, _depthTargets.Keys, _targetAliases, out var targetName)
            ? targetName
            : normalized;
    }

    private void RegisterTargetAliases(string name)
    {
        foreach (var alias in OpenGlEffectRenderTargetNameResolver.TargetAliases(name))
        {
            _targetAliases.TryAdd(alias, name);
        }
    }

    private static string? NormalizeTargetName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static FramebufferAttachment ColorAttachment(int index)
    {
        return (FramebufferAttachment)((int)FramebufferAttachment.ColorAttachment0 + index);
    }

    private static DrawBufferMode ColorDrawBuffer(int index)
    {
        return (DrawBufferMode)((int)DrawBufferMode.ColorAttachment0 + index);
    }

    private static ReadBufferMode ToReadBuffer(DrawBufferMode mode)
    {
        return (ReadBufferMode)mode;
    }
}

internal interface IOpenGlEffectTarget
{
    uint Texture { get; }
}

internal sealed record OpenGlEffectColorTarget(string Name, uint Texture, int Width, int Height) : IOpenGlEffectTarget;

internal sealed record OpenGlEffectDepthTarget(string Name, uint Texture, int Width, int Height) : IOpenGlEffectTarget;

internal sealed record OpenGlEffectOffscreenTarget(
    string Name,
    uint Texture,
    uint DepthTexture,
    int Width,
    int Height,
    RenderEffectOffscreenTarget Metadata) : IOpenGlEffectTarget;

internal static class OpenGlEffectRenderTargetNameResolver
{
    public static bool TryResolve(
        string requestedName,
        IEnumerable<string> targetNames,
        IReadOnlyDictionary<string, string> explicitAliases,
        out string targetName)
    {
        var targets = targetNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (targets.Contains(requestedName))
        {
            targetName = requestedName;
            return true;
        }

        if (explicitAliases.TryGetValue(requestedName, out var explicitTarget) && targets.Contains(explicitTarget))
        {
            targetName = explicitTarget;
            return true;
        }

        foreach (var name in targets)
        {
            if (TargetAliases(name).Any(alias => string.Equals(alias, requestedName, StringComparison.OrdinalIgnoreCase)))
            {
                targetName = name;
                return true;
            }
        }

        targetName = string.Empty;
        return false;
    }

    public static IEnumerable<string> TargetAliases(string name)
    {
        yield return name;
        foreach (var suffix in new[] { "RenderTarget", "Target", "Texture", "Tex", "Map", "Buffer" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
            {
                var stem = name[..^suffix.Length];
                yield return stem;
                yield return $"{stem}Map";
                yield return $"{stem}Target";
                yield return $"{stem}Texture";
                yield return $"{stem}RenderTarget";
                yield return $"{stem}Buffer";
            }
        }

        yield return $"{name}Map";
        yield return $"{name}Target";
        yield return $"{name}Texture";
        yield return $"{name}RenderTarget";
        yield return $"{name}Buffer";
    }
}
