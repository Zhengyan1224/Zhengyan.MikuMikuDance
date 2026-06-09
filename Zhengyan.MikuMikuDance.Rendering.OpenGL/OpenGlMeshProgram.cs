using System.Numerics;
using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

internal sealed class OpenGlMeshProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly uint _edgeProgram;
    private readonly uint _groundShadowProgram;
    private readonly int _modelUniform;
    private readonly int _viewUniform;
    private readonly int _projectionUniform;
    private readonly int _lightDirectionUniform;
    private readonly int _materialDiffuseUniform;
    private readonly int _viewPositionUniform;
    private readonly int _textureUniform;
    private readonly int _textureEnabledUniform;
    private readonly int _sphereTextureUniform;
    private readonly int _sphereTextureModeUniform;
    private readonly int _toonTextureUniform;
    private readonly int _toonTextureEnabledUniform;
    private readonly int _colorTransformUniform;
    private readonly int _edgeModelUniform;
    private readonly int _edgeViewUniform;
    private readonly int _edgeProjectionUniform;
    private readonly int _edgeColorUniform;
    private readonly int _edgeSizeUniform;
    private readonly int _groundShadowModelUniform;
    private readonly int _groundShadowViewUniform;
    private readonly int _groundShadowProjectionUniform;
    private readonly int _groundShadowColorUniform;
    private readonly OpenGlTextureCache _textures;
    private readonly OpenGlEffectProgramCache _effectPrograms;
    private readonly OpenGlEffectRenderTargetCache _effectRenderTargets;
    private readonly OpenGlEffectSourceCache _externalEffects;
    private uint _bufferPassVertexArray;
    private uint _bufferPassVertexBuffer;
    private Matrix4x4 _currentViewMatrix = Matrix4x4.Identity;
    private Matrix4x4 _currentProjectionMatrix = Matrix4x4.Identity;
    private int _currentViewportWidth = 1;
    private int _currentViewportHeight = 1;
    private Vector4 _effectClearColor = Vector4.Zero;
    private float _effectClearDepth = 1f;

    public OpenGlMeshProgram(GL gl, string? textureBaseDirectory = null)
    {
        _gl = gl;
        _textures = new OpenGlTextureCache(gl, textureBaseDirectory);
        _effectPrograms = new OpenGlEffectProgramCache(gl);
        _effectRenderTargets = new OpenGlEffectRenderTargetCache(gl);
        _externalEffects = new OpenGlEffectSourceCache(textureBaseDirectory);
        _program = CreateProgram(VertexShaderSource, FragmentShaderSource);
        _edgeProgram = CreateProgram(EdgeVertexShaderSource, EdgeFragmentShaderSource);
        _groundShadowProgram = CreateProgram(GroundShadowVertexShaderSource, GroundShadowFragmentShaderSource);
        _modelUniform = _gl.GetUniformLocation(_program, "uModel");
        _viewUniform = _gl.GetUniformLocation(_program, "uView");
        _projectionUniform = _gl.GetUniformLocation(_program, "uProjection");
        _lightDirectionUniform = _gl.GetUniformLocation(_program, "uLightDirection");
        _materialDiffuseUniform = _gl.GetUniformLocation(_program, "uMaterialDiffuse");
        _viewPositionUniform = _gl.GetUniformLocation(_program, "uViewPosition");
        _textureUniform = _gl.GetUniformLocation(_program, "uTexture");
        _textureEnabledUniform = _gl.GetUniformLocation(_program, "uTextureEnabled");
        _sphereTextureUniform = _gl.GetUniformLocation(_program, "uSphereTexture");
        _sphereTextureModeUniform = _gl.GetUniformLocation(_program, "uSphereTextureMode");
        _toonTextureUniform = _gl.GetUniformLocation(_program, "uToonTexture");
        _toonTextureEnabledUniform = _gl.GetUniformLocation(_program, "uToonTextureEnabled");
        _colorTransformUniform = _gl.GetUniformLocation(_program, "uColorTransform");
        _edgeModelUniform = _gl.GetUniformLocation(_edgeProgram, "uModel");
        _edgeViewUniform = _gl.GetUniformLocation(_edgeProgram, "uView");
        _edgeProjectionUniform = _gl.GetUniformLocation(_edgeProgram, "uProjection");
        _edgeColorUniform = _gl.GetUniformLocation(_edgeProgram, "uEdgeColor");
        _edgeSizeUniform = _gl.GetUniformLocation(_edgeProgram, "uEdgeSize");
        _groundShadowModelUniform = _gl.GetUniformLocation(_groundShadowProgram, "uModel");
        _groundShadowViewUniform = _gl.GetUniformLocation(_groundShadowProgram, "uView");
        _groundShadowProjectionUniform = _gl.GetUniformLocation(_groundShadowProgram, "uProjection");
        _groundShadowColorUniform = _gl.GetUniformLocation(_groundShadowProgram, "uShadowColor");
    }

    public void Use(RenderFrameContext context)
    {
        _gl.UseProgram(_program);
        SetFrameMatrices(context);
        var lightDirection = Vector3.Normalize(context.Project.Light.Direction);
        _gl.Uniform3(_lightDirectionUniform, lightDirection);
        _gl.Uniform3(_viewPositionUniform, Vector3.Zero);
        _gl.Uniform4(_colorTransformUniform, RenderColorTransform.Parameters(context.Project.ColorTransform));
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.Uniform1(_textureUniform, 0);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.Uniform1(_sphereTextureUniform, 1);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.Uniform1(_toonTextureUniform, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    public void Draw(OpenGlMeshBuffer mesh)
    {
        DrawGroundShadow(mesh);
        DrawMain(mesh);
        DrawEdges(mesh);
    }

    public void DrawScene(IReadOnlyList<OpenGlMeshBuffer> meshes)
    {
        ExecuteOffscreenPrepasses(meshes);
        foreach (var mesh in meshes)
        {
            Draw(mesh);
        }
    }

    private void ExecuteOffscreenPrepasses(IReadOnlyList<OpenGlMeshBuffer> meshes)
    {
        var drawableNames = meshes.Select(mesh => mesh.Mesh.Name).ToArray();
        foreach (var owner in meshes)
        {
            var effect = owner.Mesh.Effect;
            if (effect?.Parameters.Any(parameter => parameter.OffscreenTarget is not null) != true)
            {
                continue;
            }

            _effectRenderTargets.BeginEffect(effect, _currentViewportWidth, _currentViewportHeight);
            try
            {
                foreach (var plan in _effectRenderTargets.CreateOffscreenDrawPlans(owner.Mesh.Name, drawableNames))
                {
                    DrawOffscreenPlan(effect, plan, meshes);
                }
            }
            finally
            {
                _effectRenderTargets.BindDefaultFramebuffer(_currentViewportWidth, _currentViewportHeight);
                RestoreDefaultMainPassState();
                _gl.UseProgram(_program);
            }
        }
    }

    private void DrawOffscreenPlan(RenderEffect ownerEffect, RenderEffectOffscreenDrawPlan plan, IReadOnlyList<OpenGlMeshBuffer> meshes)
    {
        _effectRenderTargets.BindOffscreenTarget(plan.Target);
        RestoreDefaultMainPassState();
        foreach (var decision in plan.Decisions)
        {
            if (decision.Action == RenderEffectOffscreenAction.Hide)
            {
                continue;
            }

            var mesh = meshes.FirstOrDefault(mesh => string.Equals(mesh.Mesh.Name, decision.DrawableName, StringComparison.OrdinalIgnoreCase));
            if (mesh is null)
            {
                continue;
            }

            DrawOffscreenDecision(ownerEffect, plan.Target, decision, mesh);
            _effectRenderTargets.UseOffscreenTarget(plan.Target);
            RestoreDefaultMainPassState();
        }
    }

    private void DrawOffscreenDecision(
        RenderEffect ownerEffect,
        RenderEffectOffscreenTarget target,
        RenderEffectOffscreenDrawableDecision decision,
        OpenGlMeshBuffer mesh)
    {
        if (decision.Action == RenderEffectOffscreenAction.DrawWithExternalEffect &&
            _externalEffects.TryGetEffect(decision.EffectPath, ownerEffect, out var externalEffect) &&
            TryDrawEffectTechnique(mesh, externalEffect))
        {
            return;
        }

        _effectRenderTargets.UseOffscreenTarget(target);
        DrawMainPass(mesh, null);
    }

    private void DrawGroundShadow(OpenGlMeshBuffer mesh)
    {
        var shadowBatches = mesh.Mesh.Batches
            .Where(batch => batch.Material.GroundShadowEnabled)
            .ToArray();
        if (shadowBatches.Length == 0)
        {
            return;
        }

        _gl.UseProgram(_groundShadowProgram);
        SetMatrix(_groundShadowModelUniform, mesh.Mesh.WorldTransform);
        _gl.BindVertexArray(mesh.VertexArray);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Uniform4(_groundShadowColorUniform, new Vector4(0, 0, 0, 0.28f));
        foreach (var batch in shadowBatches)
        {
            unsafe
            {
                _gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    (void*)(batch.StartIndex * sizeof(uint)));
            }
        }

        _gl.Enable(EnableCap.CullFace);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.UseProgram(_program);
    }

    private void DrawMain(OpenGlMeshBuffer mesh)
    {
        var effect = mesh.Mesh.Effect;
        var technique = effect?.DefaultTechnique;
        if (effect is null || technique is null)
        {
            DrawMainPass(mesh, null);
            return;
        }

        if (technique.ExecutionPlan.Steps.Count == 0)
        {
            DrawMainPass(mesh, null);
            return;
        }

        _effectRenderTargets.BeginEffect(effect, _currentViewportWidth, _currentViewportHeight);
        try
        {
            ExecuteTechniquePlan(mesh, technique.ExecutionPlan);
        }
        finally
        {
            _effectRenderTargets.BindDefaultFramebuffer(_currentViewportWidth, _currentViewportHeight);
        }
    }

    private bool TryDrawEffectTechnique(OpenGlMeshBuffer mesh, RenderEffect effect)
    {
        var technique = effect.DefaultTechnique;
        if (technique is null || technique.ExecutionPlan.Steps.Count == 0)
        {
            return false;
        }

        ExecuteTechniquePlan(mesh, technique.ExecutionPlan);
        return true;
    }

    private void ExecuteTechniquePlan(OpenGlMeshBuffer mesh, RenderEffectExecutionPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case RenderEffectExecutePassStep passStep:
                    ExecuteEffectPass(mesh, passStep);
                    break;
                case RenderEffectTechniqueCommandStep commandStep:
                    ExecuteEffectCommand(commandStep.Command);
                    break;
            }
        }
    }

    private void ExecuteEffectPass(OpenGlMeshBuffer mesh, RenderEffectExecutePassStep passStep)
    {
        foreach (var step in passStep.PassPlan.Steps)
        {
            switch (step)
            {
                case RenderEffectPassCommandStep commandStep:
                    ExecuteEffectCommand(commandStep.Command);
                    break;
                case RenderEffectPassDrawStep { Target: RenderEffectDrawTarget.Geometry }:
                    DrawMainPass(mesh, passStep.Pass);
                    break;
                case RenderEffectPassDrawStep { Target: RenderEffectDrawTarget.Buffer }:
                    DrawBufferPass(mesh, passStep.Pass);
                    break;
            }
        }
    }

    private void ExecuteEffectCommand(RenderEffectScriptCommand command)
    {
        switch (command.Type)
        {
            case RenderEffectScriptCommandType.ClearSetColor:
                _effectClearColor = ParseClearColor(command.Value);
                break;
            case RenderEffectScriptCommandType.ClearSetDepth:
                _effectClearDepth = ParseClearDepth(command.Value);
                break;
            case RenderEffectScriptCommandType.Clear:
                ClearEffectTarget(command.Value);
                break;
            case RenderEffectScriptCommandType.SetRenderColorTarget0:
                _effectRenderTargets.SetColorTarget(0, command.Value);
                break;
            case RenderEffectScriptCommandType.SetRenderColorTarget1:
                _effectRenderTargets.SetColorTarget(1, command.Value);
                break;
            case RenderEffectScriptCommandType.SetRenderColorTarget2:
                _effectRenderTargets.SetColorTarget(2, command.Value);
                break;
            case RenderEffectScriptCommandType.SetRenderColorTarget3:
                _effectRenderTargets.SetColorTarget(3, command.Value);
                break;
            case RenderEffectScriptCommandType.SetRenderDepthStencilTarget:
                _effectRenderTargets.SetDepthStencilTarget(command.Value);
                break;
        }
    }

    private void DrawMainPass(OpenGlMeshBuffer mesh, RenderEffectPass? pass)
    {
        var passState = pass?.State;
        ApplyMainPassState(passState);
        var hasEffectProgram = _effectPrograms.TryGetProgram(pass?.Shader, out var effectProgram);
        if (hasEffectProgram)
        {
            _gl.UseProgram(effectProgram.Program);
            SetEffectFrameUniforms(effectProgram, mesh.Mesh);
        }
        else
        {
            _gl.UseProgram(_program);
            SetMatrix(_modelUniform, mesh.Mesh.WorldTransform);
        }

        _gl.BindVertexArray(mesh.VertexArray);
        var batchPlan = RenderBatchOrdering.CreatePlan(mesh.Mesh, _currentViewMatrix, RequiresTransparentPass);
        foreach (var batch in batchPlan.OpaqueBatches)
        {
            DrawBatch(batch, passState, hasEffectProgram ? effectProgram : null, mesh.Mesh.EffectParameterOverrides);
        }

        if (batchPlan.TransparentBatches.Count > 0)
        {
            _gl.Enable(EnableCap.Blend);
            ApplyBlendFunction(passState);
            _gl.DepthMask(false);
            foreach (var batch in batchPlan.TransparentBatches)
            {
                DrawBatch(batch, passState, hasEffectProgram ? effectProgram : null, mesh.Mesh.EffectParameterOverrides);
            }
        }

        RestoreDefaultMainPassState();
        _gl.UseProgram(_program);
    }

    private bool RequiresTransparentPass(RenderMaterial material)
    {
        return material.RequiresTransparentPass || _textures.HasTransparentPixels(material.TexturePath);
    }

    private void DrawBufferPass(OpenGlMeshBuffer mesh, RenderEffectPass pass)
    {
        var passState = pass.State;
        ApplyBufferPassState(passState);
        var hasEffectProgram = _effectPrograms.TryGetProgram(pass.Shader, out var effectProgram);
        if (!hasEffectProgram)
        {
            RestoreDefaultMainPassState();
            _gl.UseProgram(_program);
            return;
        }

        _gl.UseProgram(effectProgram.Program);
        SetEffectFrameUniforms(effectProgram, mesh.Mesh);
        SetEffectMaterialUniforms(
            effectProgram,
            mesh.Mesh.Batches.FirstOrDefault()?.Material ?? DefaultBufferMaterial,
            mesh.Mesh.EffectParameterOverrides);
        _gl.BindVertexArray(GetBufferPassVertexArray());
        _gl.Disable(EnableCap.CullFace);
        unsafe
        {
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        }

        RestoreDefaultMainPassState();
        _gl.UseProgram(_program);
    }

    private void DrawBatch(
        RenderMeshBatch batch,
        RenderPassState? passState,
        OpenGlEffectProgram? effectProgram = null,
        IReadOnlyDictionary<string, MotionEffectParameterValue>? effectParameterOverrides = null)
    {
        ApplyBatchCullState(batch.Material, passState);
        var cullFaceEnabled = IsCullFaceEnabled(batch.Material, passState);
        if (cullFaceEnabled)
        {
            _gl.CullFace(ToTriangleFace(passState?.CullMode ?? RenderCullMode.Back));
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }

        if (effectProgram is { } program)
        {
            SetEffectMaterialUniforms(program, batch.Material, effectParameterOverrides);
        }
        else
        {
            SetBuiltInMaterialUniforms(batch.Material);
        }

        unsafe
        {
            _gl.DrawElements(
                PrimitiveType.Triangles,
                (uint)batch.IndexCount,
                DrawElementsType.UnsignedInt,
                (void*)(batch.StartIndex * sizeof(uint)));
        }

        if (!cullFaceEnabled)
        {
            _gl.Enable(EnableCap.CullFace);
        }
    }

    private void ApplyBufferPassState(RenderPassState? state)
    {
        if (state?.DepthTestEnabled == true)
        {
            _gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Disable(EnableCap.DepthTest);
        }

        _gl.DepthMask(state?.DepthWriteEnabled ?? false);
        if (state?.BlendEnabled == true)
        {
            _gl.Enable(EnableCap.Blend);
            ApplyBlendFunction(state);
        }
        else
        {
            _gl.Disable(EnableCap.Blend);
        }
    }

    private void SetBuiltInMaterialUniforms(RenderMaterial material)
    {
        _gl.Uniform4(_materialDiffuseUniform, material.Diffuse);
        var texture = _textures.GetTexture(material.TexturePath);
        var sphereTexture = _textures.GetTexture(material.SphereTexturePath);
        var toonTexture = _textures.GetTexture(material.ToonTexturePath);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.Uniform1(_textureEnabledUniform, string.IsNullOrWhiteSpace(material.TexturePath) ? 0 : 1);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, sphereTexture);
        _gl.Uniform1(_sphereTextureModeUniform, ToShaderSphereMode(material.SphereTextureMode));
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, toonTexture);
        _gl.Uniform1(_toonTextureEnabledUniform, string.IsNullOrWhiteSpace(material.ToonTexturePath) ? 0 : 1);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    private void SetEffectFrameUniforms(OpenGlEffectProgram program, RenderMesh mesh)
    {
        foreach (var uniform in program.Uniforms.Values)
        {
            if (uniform.Location < 0)
            {
                continue;
            }

            if (TrySetEffectOverrideUniform(uniform, mesh.EffectParameterOverrides))
            {
                continue;
            }

            switch (uniform.Metadata.Semantic)
            {
                case RenderEffectSemantic.World:
                    SetMatrix(uniform.Location, mesh.WorldTransform);
                    break;
                case RenderEffectSemantic.View:
                    SetMatrix(uniform.Location, _currentViewMatrix);
                    break;
                case RenderEffectSemantic.Projection:
                    SetMatrix(uniform.Location, _currentProjectionMatrix);
                    break;
                case RenderEffectSemantic.WorldView:
                    SetMatrix(uniform.Location, mesh.WorldTransform * _currentViewMatrix);
                    break;
                case RenderEffectSemantic.ViewProjection:
                    SetMatrix(uniform.Location, _currentViewMatrix * _currentProjectionMatrix);
                    break;
                case RenderEffectSemantic.WorldViewProjection:
                    SetMatrix(uniform.Location, mesh.WorldTransform * _currentViewMatrix * _currentProjectionMatrix);
                    break;
                case RenderEffectSemantic.WorldInverse:
                    SetMatrix(uniform.Location, InvertOrIdentity(mesh.WorldTransform));
                    break;
                case RenderEffectSemantic.ViewInverse:
                    SetMatrix(uniform.Location, InvertOrIdentity(_currentViewMatrix));
                    break;
                case RenderEffectSemantic.ProjectionInverse:
                    SetMatrix(uniform.Location, InvertOrIdentity(_currentProjectionMatrix));
                    break;
                case RenderEffectSemantic.WorldTranspose:
                    SetMatrix(uniform.Location, Matrix4x4.Transpose(mesh.WorldTransform));
                    break;
                case RenderEffectSemantic.ViewTranspose:
                    SetMatrix(uniform.Location, Matrix4x4.Transpose(_currentViewMatrix));
                    break;
                case RenderEffectSemantic.ProjectionTranspose:
                    SetMatrix(uniform.Location, Matrix4x4.Transpose(_currentProjectionMatrix));
                    break;
                case RenderEffectSemantic.ViewportPixelSize:
                    _gl.Uniform2(uniform.Location, new Vector2(_currentViewportWidth, _currentViewportHeight));
                    break;
            }
        }
    }

    private void SetEffectMaterialUniforms(
        OpenGlEffectProgram program,
        RenderMaterial material,
        IReadOnlyDictionary<string, MotionEffectParameterValue>? effectParameterOverrides)
    {
        var nextTextureUnit = 0;
        foreach (var uniform in program.Uniforms.Values)
        {
            if (uniform.Location < 0)
            {
                continue;
            }

            if (TrySetEffectOverrideUniform(uniform, effectParameterOverrides))
            {
                continue;
            }

            switch (uniform.Metadata.Kind)
            {
                case RenderEffectParameterKind.Sampler:
                case RenderEffectParameterKind.Texture:
                    if (_effectRenderTargets.TryBindTexture(uniform.Metadata, nextTextureUnit))
                    {
                        _gl.Uniform1(uniform.Location, nextTextureUnit++);
                    }
                    else
                    {
                        var texturePath = ResolveEffectTexturePath(uniform.Metadata, material);
                        BindEffectTexture(uniform.Location, nextTextureUnit++, texturePath, uniform.Metadata.MipmapEnabled);
                    }
                    break;
                default:
                    SetEffectScalarMaterialUniform(uniform, material);
                    break;
            }
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    private bool TrySetEffectOverrideUniform(
        OpenGlEffectUniform uniform,
        IReadOnlyDictionary<string, MotionEffectParameterValue>? effectParameterOverrides)
    {
        if (effectParameterOverrides is null ||
            !effectParameterOverrides.TryGetValue(uniform.Metadata.Name, out var value))
        {
            return false;
        }

        switch (value)
        {
            case MotionEffectParameterValue.Bool boolValue:
                _gl.Uniform1(uniform.Location, boolValue.Value ? 1 : 0);
                return true;
            case MotionEffectParameterValue.Int intValue:
                _gl.Uniform1(uniform.Location, intValue.Value);
                return true;
            case MotionEffectParameterValue.Float floatValue:
                _gl.Uniform1(uniform.Location, floatValue.Value);
                return true;
            case MotionEffectParameterValue.Vector4 vectorValue:
                _gl.Uniform4(uniform.Location, vectorValue.Value);
                return true;
            default:
                return false;
        }
    }

    private void SetEffectScalarMaterialUniform(OpenGlEffectUniform uniform, RenderMaterial material)
    {
        switch (uniform.Metadata.Semantic)
        {
            case RenderEffectSemantic.Diffuse:
                _gl.Uniform4(uniform.Location, material.Diffuse);
                break;
            case RenderEffectSemantic.MaterialTexture:
                BindEffectTexture(uniform.Location, 0, material.TexturePath, uniform.Metadata.MipmapEnabled);
                break;
            case RenderEffectSemantic.MaterialSphereMap:
                BindEffectTexture(uniform.Location, 1, material.SphereTexturePath, uniform.Metadata.MipmapEnabled);
                break;
            case RenderEffectSemantic.MaterialToonTexture:
                BindEffectTexture(uniform.Location, 2, material.ToonTexturePath, uniform.Metadata.MipmapEnabled);
                break;
        }
    }

    private void BindEffectTexture(int uniformLocation, int textureUnitIndex, string? texturePath, bool mipmapEnabled)
    {
        var unit = TextureUnit.Texture0 + textureUnitIndex;
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _textures.GetTexture(texturePath, mipmapEnabled));
        _gl.Uniform1(uniformLocation, textureUnitIndex);
    }

    private static string? ResolveEffectTexturePath(RenderEffectShaderUniform uniform, RenderMaterial material)
    {
        return uniform.Semantic switch
        {
            RenderEffectSemantic.MaterialTexture => material.TexturePath,
            RenderEffectSemantic.MaterialSphereMap => material.SphereTexturePath,
            RenderEffectSemantic.MaterialToonTexture => material.ToonTexturePath,
            _ => uniform.ResourceName
        };
    }

    private void ApplyMainPassState(RenderPassState? state)
    {
        if (state?.DepthTestEnabled == false)
        {
            _gl.Disable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Enable(EnableCap.DepthTest);
        }

        _gl.DepthMask(state?.DepthWriteEnabled ?? true);
        if (state?.BlendEnabled == true)
        {
            _gl.Enable(EnableCap.Blend);
            ApplyBlendFunction(state);
        }
        else
        {
            _gl.Disable(EnableCap.Blend);
        }
    }

    private void ApplyBlendFunction(RenderPassState? state)
    {
        var source = state?.SourceBlend is { } sourceBlend
            ? ToBlendFactor(sourceBlend)
            : BlendingFactor.SrcAlpha;
        var destination = state?.DestinationBlend is { } destinationBlend
            ? ToBlendFactor(destinationBlend)
            : BlendingFactor.OneMinusSrcAlpha;
        _gl.BlendFunc(source, destination);
    }

    private void ApplyBatchCullState(RenderMaterial material, RenderPassState? state)
    {
        if (IsCullFaceEnabled(material, state))
        {
            _gl.Enable(EnableCap.CullFace);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }
    }

    private static bool IsCullFaceEnabled(RenderMaterial material, RenderPassState? state)
    {
        return state?.CullMode is { } mode
            ? mode != RenderCullMode.None
            : !material.DoubleSided;
    }

    private void RestoreDefaultMainPassState()
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
    }

    private void ClearEffectTarget(string value)
    {
        var clearColor = ShouldClear(value, "COLOR");
        var clearDepth = ShouldClear(value, "DEPTH") || ShouldClear(value, "ZBUFFER") || ShouldClear(value, "DEPTHSTENCIL");
        var mask = default(ClearBufferMask);
        if (clearColor)
        {
            _gl.ClearColor(_effectClearColor.X, _effectClearColor.Y, _effectClearColor.Z, _effectClearColor.W);
            mask |= ClearBufferMask.ColorBufferBit;
        }

        if (clearDepth)
        {
            _gl.ClearDepth(_effectClearDepth);
            mask |= ClearBufferMask.DepthBufferBit;
        }

        if (mask != 0)
        {
            _gl.Clear(mask);
        }
    }

    private static bool ShouldClear(string value, string target)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value
            .Split(['|', ',', '+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, target, StringComparison.OrdinalIgnoreCase));
    }

    private static Vector4 ParseClearColor(string value)
    {
        var components = ParseFloatComponents(value);
        return new Vector4(
            components.Length > 0 ? components[0] : 0,
            components.Length > 1 ? components[1] : 0,
            components.Length > 2 ? components[2] : 0,
            components.Length > 3 ? components[3] : 1);
    }

    private static float ParseClearDepth(string value)
    {
        var components = ParseFloatComponents(value);
        return components.Length > 0 ? components[0] : 1f;
    }

    private static float[] ParseFloatComponents(string value)
    {
        return value
            .Trim()
            .Trim('{', '}', '(', ')')
            .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => float.TryParse(
                part.TrimEnd('f', 'F'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result)
                ? result
                : (float?)null)
            .Where(result => result.HasValue)
            .Select(result => result!.Value)
            .ToArray();
    }

    private uint GetBufferPassVertexArray()
    {
        if (_bufferPassVertexArray != 0)
        {
            return _bufferPassVertexArray;
        }

        var vertices = new[]
        {
            -1f, -1f, 0f, 0f, 0f,
             1f, -1f, 0f, 1f, 0f,
            -1f,  1f, 0f, 0f, 1f,
             1f,  1f, 0f, 1f, 1f
        };
        _bufferPassVertexArray = _gl.GenVertexArray();
        _bufferPassVertexBuffer = _gl.GenBuffer();
        _gl.BindVertexArray(_bufferPassVertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _bufferPassVertexBuffer);
        unsafe
        {
            fixed (float* ptr = vertices)
            {
                _gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)),
                    ptr,
                    BufferUsageARB.StaticDraw);
            }
        }

        const uint stride = 5 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        return _bufferPassVertexArray;
    }

    private void DrawEdges(OpenGlMeshBuffer mesh)
    {
        var edgeBatches = mesh.Mesh.Batches
            .Where(batch => batch.Material.EdgeEnabled && batch.Material.EdgeSize > 0 && batch.Material.EdgeColor.W > 0)
            .ToArray();
        if (edgeBatches.Length == 0)
        {
            return;
        }

        _gl.UseProgram(_edgeProgram);
        SetMatrix(_edgeModelUniform, mesh.Mesh.WorldTransform);
        _gl.BindVertexArray(mesh.VertexArray);
        _gl.CullFace(TriangleFace.Front);
        foreach (var batch in edgeBatches)
        {
            _gl.Uniform4(_edgeColorUniform, batch.Material.EdgeColor);
            _gl.Uniform1(_edgeSizeUniform, batch.Material.EdgeSize * 0.01f);
            unsafe
            {
                _gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    (void*)(batch.StartIndex * sizeof(uint)));
            }
        }

        _gl.CullFace(TriangleFace.Back);
        _gl.UseProgram(_program);
    }

    public void End()
    {
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        if (_edgeProgram != 0)
        {
            _gl.DeleteProgram(_edgeProgram);
        }

        if (_groundShadowProgram != 0)
        {
            _gl.DeleteProgram(_groundShadowProgram);
        }

        if (_bufferPassVertexBuffer != 0)
        {
            _gl.DeleteBuffer(_bufferPassVertexBuffer);
            _bufferPassVertexBuffer = 0;
        }

        if (_bufferPassVertexArray != 0)
        {
            _gl.DeleteVertexArray(_bufferPassVertexArray);
            _bufferPassVertexArray = 0;
        }

        _textures.Dispose();
        _effectPrograms.Dispose();
        _effectRenderTargets.Dispose();
    }

    private uint CreateProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);
        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetProgramInfoLog(program);
            throw new InvalidOperationException($"Failed to link OpenGL shader program: {log}");
        }

        _gl.DetachShader(program, vertexShader);
        _gl.DetachShader(program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
        return program;
    }

    private static int ToShaderSphereMode(SphereTextureBlendMode mode)
    {
        return mode switch
        {
            SphereTextureBlendMode.Multiply => 1,
            SphereTextureBlendMode.Add => 2,
            SphereTextureBlendMode.SubTexture => 3,
            _ => 0
        };
    }

    private static TriangleFace ToTriangleFace(RenderCullMode mode)
    {
        return mode switch
        {
            RenderCullMode.Front => TriangleFace.Front,
            _ => TriangleFace.Back
        };
    }

    private static BlendingFactor ToBlendFactor(RenderBlendFactor factor)
    {
        return factor switch
        {
            RenderBlendFactor.Zero => BlendingFactor.Zero,
            RenderBlendFactor.One => BlendingFactor.One,
            RenderBlendFactor.SourceColor => BlendingFactor.SrcColor,
            RenderBlendFactor.InverseSourceColor => BlendingFactor.OneMinusSrcColor,
            RenderBlendFactor.SourceAlpha => BlendingFactor.SrcAlpha,
            RenderBlendFactor.InverseSourceAlpha => BlendingFactor.OneMinusSrcAlpha,
            RenderBlendFactor.DestinationAlpha => BlendingFactor.DstAlpha,
            RenderBlendFactor.InverseDestinationAlpha => BlendingFactor.OneMinusDstAlpha,
            RenderBlendFactor.DestinationColor => BlendingFactor.DstColor,
            RenderBlendFactor.InverseDestinationColor => BlendingFactor.OneMinusDstColor,
            RenderBlendFactor.SourceAlphaSaturate => BlendingFactor.SrcAlphaSaturate,
            RenderBlendFactor.BlendColor => BlendingFactor.ConstantColor,
            RenderBlendFactor.InverseBlendColor => BlendingFactor.OneMinusConstantColor,
            _ => BlendingFactor.One
        };
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        var shader = _gl.CreateShader(shaderType);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"Failed to compile {shaderType}: {log}");
        }

        return shader;
    }

    private unsafe void SetMatrix(int location, Matrix4x4 matrix)
    {
        var values = new[]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
        };
        fixed (float* ptr = values)
        {
            _gl.UniformMatrix4(location, 1, false, ptr);
        }
    }

    private void SetFrameMatrices(RenderFrameContext context)
    {
        _currentViewportWidth = Math.Max(1, context.Width);
        _currentViewportHeight = Math.Max(1, context.Height);
        var aspect = Math.Max(1, context.Width) / (float)Math.Max(1, context.Height);
        var view = context.Project.Camera.CreateViewMatrix();
        _currentViewMatrix = view;
        var projection = context.Project.Camera.CreateProjectionMatrix(aspect);
        _currentProjectionMatrix = projection;
        _gl.UseProgram(_program);
        SetMatrix(_viewUniform, view);
        SetMatrix(_projectionUniform, projection);
        _gl.UseProgram(_edgeProgram);
        SetMatrix(_edgeViewUniform, view);
        SetMatrix(_edgeProjectionUniform, projection);
        _gl.UseProgram(_groundShadowProgram);
        SetMatrix(_groundShadowViewUniform, view);
        SetMatrix(_groundShadowProjectionUniform, projection);
        _gl.UseProgram(_program);
    }

    private static RenderMaterial DefaultBufferMaterial { get; } = new("buffer", Vector4.One);

    private static Matrix4x4 InvertOrIdentity(Matrix4x4 matrix)
    {
        return Matrix4x4.Invert(matrix, out var inverted)
            ? inverted
            : Matrix4x4.Identity;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec2 aUv;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vNormal;
        out vec3 vWorldPosition;
        out vec2 vUv;

        void main()
        {
            vec4 worldPosition = uModel * vec4(aPosition, 1.0);
            vWorldPosition = worldPosition.xyz;
            vNormal = mat3(transpose(inverse(uModel))) * aNormal;
            vUv = aUv;
            gl_Position = uProjection * uView * worldPosition;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec3 vNormal;
        in vec3 vWorldPosition;
        in vec2 vUv;

        uniform vec3 uLightDirection;
        uniform vec3 uViewPosition;
        uniform vec4 uMaterialDiffuse;
        uniform sampler2D uTexture;
        uniform int uTextureEnabled;
        uniform sampler2D uSphereTexture;
        uniform int uSphereTextureMode;
        uniform sampler2D uToonTexture;
        uniform int uToonTextureEnabled;
        uniform vec4 uColorTransform;

        out vec4 FragColor;

        vec3 applyColorTransform(vec3 color)
        {
            float brightness = uColorTransform.x;
            float contrast = uColorTransform.y;
            float saturation = uColorTransform.z;
            float gammaValue = max(uColorTransform.w, 0.01);
            color += vec3(brightness);
            color = (color - vec3(0.5)) * contrast + vec3(0.5);
            float luma = dot(color, vec3(0.2126, 0.7152, 0.0722));
            color = mix(vec3(luma), color, saturation);
            color = clamp(color, vec3(0.0), vec3(1.0));
            color = pow(color, vec3(1.0 / gammaValue));
            return clamp(color, vec3(0.0), vec3(1.0));
        }

        void main()
        {
            vec3 normal = normalize(vNormal);
            float lighting = max(dot(normal, normalize(-uLightDirection)), 0.0);
            vec4 texel = uTextureEnabled != 0 ? texture(uTexture, vUv) : vec4(1.0);
            vec4 baseColor = uMaterialDiffuse * texel;
            vec2 sphereUv = normal.xy * 0.5 + vec2(0.5);
            vec4 sphere = texture(uSphereTexture, sphereUv);
            if (uSphereTextureMode == 1) {
                baseColor.rgb *= sphere.rgb;
            }
            else if (uSphereTextureMode == 2) {
                baseColor.rgb += sphere.rgb * sphere.a;
            }
            else if (uSphereTextureMode == 3) {
                baseColor.rgb = mix(baseColor.rgb, sphere.rgb, sphere.a);
            }

            vec3 ambient = baseColor.rgb * 0.35;
            vec3 diffuse = baseColor.rgb * lighting * 0.75;
            vec3 color = ambient + diffuse;
            if (uToonTextureEnabled != 0) {
                vec4 toon = texture(uToonTexture, vec2(clamp(1.0 - lighting, 0.0, 1.0), 0.5));
                color *= toon.rgb;
            }

            color = applyColorTransform(color);
            FragColor = vec4(color, baseColor.a);
        }
        """;

    private const string EdgeVertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform float uEdgeSize;

        void main()
        {
            vec3 expanded = aPosition + normalize(aNormal) * uEdgeSize;
            gl_Position = uProjection * uView * uModel * vec4(expanded, 1.0);
        }
        """;

    private const string EdgeFragmentShaderSource = """
        #version 330 core
        uniform vec4 uEdgeColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = uEdgeColor;
        }
        """;

    private const string GroundShadowVertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            vec4 worldPosition = uModel * vec4(aPosition, 1.0);
            worldPosition.y = 0.01;
            gl_Position = uProjection * uView * worldPosition;
        }
        """;

    private const string GroundShadowFragmentShaderSource = """
        #version 330 core
        uniform vec4 uShadowColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = uShadowColor;
        }
        """;
}
