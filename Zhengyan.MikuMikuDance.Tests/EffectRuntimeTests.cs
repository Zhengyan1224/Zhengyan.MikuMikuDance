using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Effects;
using Zhengyan.MikuMikuDance.Formats.Mme;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EffectRuntimeTests
{
    [Fact]
    public void CompilesEffectParametersAndPassStates()
    {
        const string text = """
            float4 VSMain(float4 position : POSITION) : POSITION
            {
                return position;
            }

            float4 PSMain() : COLOR0
            {
                return float4(1, 1, 1, 1);
            }

            float4x4 WorldViewProj : WORLDVIEWPROJECTION;
            texture2D MainTexture : MATERIALTEXTURE <
              string ResourceName = "diffuse.png";
              string ResourceType = "2D";
            >;
            float4 DiffuseColor : DIFFUSE = float4(1, 0.5, 0.25, 1);
            float Standards : STANDARDSGLOBAL <
              string ScriptClass = "object";
              string ScriptOrder = "postprocess";
              string ScriptOutput = "color";
              string Script = "RenderColorTarget0=Scene;Clear=Color;ScriptExternal=color;Pass=P0;";
            > = 0;

            technique Main {
              pass P0 {
                VertexShader = compile vs_3_0 VSMain();
                PixelShader = compile ps_3_0 PSMain();
                ZEnable = true;
                ZWriteEnable = false;
                AlphaBlendEnable = true;
                SrcBlend = SrcAlpha;
                DestBlend = InvSrcAlpha;
                CullMode = None;
                ZFunc = LessEqual;
              }
            }
            """;

        var document = new MmeEffectReader().ReadText(text, "runtime.fx");
        var effect = RenderEffectCompiler.Compile(document);

        Assert.Equal("runtime.fx", effect.SourceName);
        Assert.Equal(RenderEffectScriptClass.Object, effect.Script.Class);
        Assert.Equal(RenderEffectScriptOrder.PostProcess, effect.Script.Order);
        Assert.Contains(effect.Script.Commands, command => command.Type == RenderEffectScriptCommandType.SetScriptExternal && command.Value == "color");
        Assert.True(effect.HasScriptExternal);

        var worldViewProjection = effect.Parameters.Single(parameter => parameter.Name == "WorldViewProj");
        Assert.Equal(RenderEffectParameterKind.Matrix, worldViewProjection.Kind);
        Assert.Equal(RenderEffectSemantic.WorldViewProjection, worldViewProjection.Semantic);

        var mainTexture = effect.Parameters.Single(parameter => parameter.Name == "MainTexture");
        Assert.Equal(RenderEffectParameterKind.Texture, mainTexture.Kind);
        Assert.Equal(RenderEffectSemantic.MaterialTexture, mainTexture.Semantic);
        Assert.Equal("diffuse.png", mainTexture.ResourceName);
        Assert.Equal("2D", mainTexture.ResourceType);

        var diffuseColor = effect.Parameters.Single(parameter => parameter.Name == "DiffuseColor");
        Assert.Equal(RenderEffectSemantic.Diffuse, diffuseColor.Semantic);
        Assert.Equal(new Vector4(1, 0.5f, 0.25f, 1), Assert.IsType<EffectValue.Vector>(diffuseColor.DefaultValue).Value);

        var pass = effect.DefaultTechnique!.Passes.Single();
        Assert.Equal("compile vs_3_0 VSMain()", pass.VertexShaderState);
        Assert.Equal("compile ps_3_0 PSMain()", pass.PixelShaderState);
        Assert.NotNull(pass.Shader);
        Assert.True(pass.State.DepthTestEnabled);
        Assert.False(pass.State.DepthWriteEnabled);
        Assert.True(pass.State.BlendEnabled);
        Assert.Equal(RenderBlendFactor.SourceAlpha, pass.State.SourceBlend);
        Assert.Equal(RenderBlendFactor.InverseSourceAlpha, pass.State.DestinationBlend);
        Assert.Equal(RenderCullMode.None, pass.State.CullMode);
        Assert.Equal(RenderCompareFunction.LessEqual, pass.State.DepthFunction);
    }

    [Fact]
    public void ParsesScriptAnnotationsWithEmbeddedSemicolons()
    {
        const string text = """
            technique Main <
              string Script = "RenderColorTarget0=Scene;Clear=Color;Pass=P0;";
            > {
              pass P0 < string Script = "Draw=Geometry;"; > {
              }
            }
            """;

        var document = new MmeEffectReader().ReadText(text, "script.fx");
        var effect = RenderEffectCompiler.Compile(document);

        var technique = effect.Techniques.Single();
        Assert.Equal(3, technique.Script.Count);
        Assert.Equal(RenderEffectScriptCommandType.SetRenderColorTarget0, technique.Script[0].Type);
        Assert.Equal("Scene", technique.Script[0].Value);
        Assert.Equal(RenderEffectScriptCommandType.Clear, technique.Script[1].Type);
        Assert.Equal(RenderEffectScriptCommandType.ExecutePass, technique.Script[2].Type);
        Assert.Equal(RenderEffectScriptCommandType.Draw, technique.Passes.Single().Script.Single().Type);
    }

    [Fact]
    public void BuildsExplicitMultiPassExecutionPlanFromTechniqueScript()
    {
        const string text = """
            technique Main <
              string Script = "Clear=Color;Pass=First;RenderColorTarget0=Scene;Pass=Second;";
            > {
              pass First < string Script = "Draw=Geometry;"; > {
              }
              pass Second < string Script = "Draw=Buffer;"; > {
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "multipass.fx"));
        var technique = effect.DefaultTechnique!;

        Assert.Equal(4, technique.ExecutionPlan.Steps.Count);
        var clear = Assert.IsType<RenderEffectTechniqueCommandStep>(technique.ExecutionPlan.Steps[0]);
        Assert.Equal(RenderEffectScriptCommandType.Clear, clear.Command.Type);
        var first = Assert.IsType<RenderEffectExecutePassStep>(technique.ExecutionPlan.Steps[1]);
        Assert.Equal(0, first.PassIndex);
        Assert.Equal("First", first.PassName);
        Assert.True(first.PassPlan.DrawsGeometry);
        Assert.False(first.PassPlan.DrawsBuffer);
        var renderTarget = Assert.IsType<RenderEffectTechniqueCommandStep>(technique.ExecutionPlan.Steps[2]);
        Assert.Equal(RenderEffectScriptCommandType.SetRenderColorTarget0, renderTarget.Command.Type);
        var second = Assert.IsType<RenderEffectExecutePassStep>(technique.ExecutionPlan.Steps[3]);
        Assert.Equal(1, second.PassIndex);
        Assert.Equal("Second", second.PassName);
        Assert.False(second.PassPlan.DrawsGeometry);
        Assert.True(second.PassPlan.DrawsBuffer);
    }

    [Fact]
    public void BuildsDefaultExecutionPlanFromDeclaredPassOrder()
    {
        const string text = """
            technique Main {
              pass First {
              }
              pass Second {
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "default-plan.fx"));
        var executePasses = effect.DefaultTechnique!.ExecutionPlan.ExecutePasses;

        Assert.Collection(
            executePasses,
            step =>
            {
                Assert.Equal(0, step.PassIndex);
                Assert.Equal("First", step.PassName);
                Assert.True(step.PassPlan.DrawsGeometry);
            },
            step =>
            {
                Assert.Equal(1, step.PassIndex);
                Assert.Equal("Second", step.PassName);
                Assert.True(step.PassPlan.DrawsGeometry);
            });
    }

    [Fact]
    public void BuildsPassExecutionPlanInScriptOrder()
    {
        const string text = """
            technique Main {
              pass Mixed < string Script = "Clear=Color;Draw=Geometry;Clear=Depth;Draw=Buffer;"; > {
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "pass-plan.fx"));
        var passPlan = effect.DefaultTechnique!.ExecutionPlan.ExecutePasses.Single().PassPlan;

        Assert.Collection(
            passPlan.Steps,
            step =>
            {
                var command = Assert.IsType<RenderEffectPassCommandStep>(step);
                Assert.Equal(RenderEffectScriptCommandType.Clear, command.Command.Type);
                Assert.Equal("Color", command.Command.Value);
            },
            step =>
            {
                var draw = Assert.IsType<RenderEffectPassDrawStep>(step);
                Assert.Equal(RenderEffectDrawTarget.Geometry, draw.Target);
            },
            step =>
            {
                var command = Assert.IsType<RenderEffectPassCommandStep>(step);
                Assert.Equal(RenderEffectScriptCommandType.Clear, command.Command.Type);
                Assert.Equal("Depth", command.Command.Value);
            },
            step =>
            {
                var draw = Assert.IsType<RenderEffectPassDrawStep>(step);
                Assert.Equal(RenderEffectDrawTarget.Buffer, draw.Target);
            });
    }

    [Fact]
    public void TranslatesSimpleHlslEntryPointsToGlsl()
    {
        const string text = """
            float4 MainVS(float4 position : POSITION) : POSITION
            {
                return position;
            }

            float4 MainPS() : COLOR0
            {
                return float4(1, 0, 0, 1);
            }

            technique T {
              pass P {
                VertexShader = compile vs_3_0 MainVS();
                PixelShader = compile ps_3_0 MainPS();
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "simple.fx"));
        var shader = Assert.IsType<RenderEffectShaderProgram>(effect.DefaultTechnique!.Passes.Single().Shader);

        Assert.Equal("MainVS", shader.VertexShader.EntryPoint);
        Assert.Equal("vs_3_0", shader.VertexShader.Profile);
        Assert.Contains("layout (location = 0) in vec3 aPosition;", shader.VertexShader.Source);
        Assert.Contains("gl_Position = position;", shader.VertexShader.Source);
        Assert.Equal("MainPS", shader.PixelShader.EntryPoint);
        Assert.Contains("out vec4 FragColor;", shader.PixelShader.Source);
        Assert.Contains("FragColor = vec4(1.0, 0.0, 0.0, 1.0);", shader.PixelShader.Source);
    }

    [Fact]
    public void TranslatesStructVaryingsAndTextureSampling()
    {
        const string text = """
            float4x4 g_worldViewProjectMatrix : WORLDVIEWPROJECTION;
            texture2D s_texture : MATERIALTEXTURE;
            sampler s_sampler = sampler_state { texture = <s_texture>; };

            struct VS_OUTPUT
            {
                float4 position : POSITION;
                float3 normal : TEXCOORD1;
                float2 texcoord : TEXCOORD2;
            };

            VS_OUTPUT vs_main(float4 position : POSITION, float3 normal : NORMAL, float2 texcoord : TEXCOORD0)
            {
                VS_OUTPUT output = (VS_OUTPUT) 0;
                output.position = mul(position, g_worldViewProjectMatrix);
                output.normal = normalize(normal);
                output.texcoord = texcoord;
                return output;
            }

            float4 ps_main(VS_OUTPUT input) : COLOR
            {
                return float4(input.normal, 1) * tex2D(s_sampler, input.texcoord);
            }

            technique T {
              pass P {
                VertexShader = compile vs_2_0 vs_main();
                PixelShader = compile ps_2_0 ps_main();
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "main.fx"));
        var shader = Assert.IsType<RenderEffectShaderProgram>(effect.DefaultTechnique!.Passes.Single().Shader);

        Assert.Contains("uniform mat4 g_worldViewProjectMatrix;", shader.VertexShader.Source);
        Assert.Contains("out vec3 vTexcoord1;", shader.VertexShader.Source);
        Assert.Contains("out vec2 vTexcoord2;", shader.VertexShader.Source);
        Assert.Contains("gl_Position = (g_worldViewProjectMatrix * position);", shader.VertexShader.Source);
        Assert.Contains("vTexcoord1 = normalize(normal);", shader.VertexShader.Source);
        Assert.Contains("vTexcoord2 = texcoord;", shader.VertexShader.Source);
        Assert.Contains("uniform sampler2D s_sampler;", shader.PixelShader.Source);
        var samplerUniform = shader.Uniforms.Single(uniform => uniform.Name == "s_sampler");
        Assert.Equal(RenderEffectParameterKind.Sampler, samplerUniform.Kind);
        Assert.Equal("s_texture", samplerUniform.TextureSourceName);
        Assert.Equal(RenderEffectSemantic.MaterialTexture, samplerUniform.Semantic);
        Assert.Contains("in vec3 vTexcoord1;", shader.PixelShader.Source);
        Assert.Contains("in vec2 vTexcoord2;", shader.PixelShader.Source);
        Assert.Contains("FragColor = vec4(vTexcoord1, 1.0) * texture(s_sampler, vTexcoord2);", shader.PixelShader.Source);
    }

    [Fact]
    public void MapsRenderTargetSamplerUniformsToTextureSources()
    {
        const string text = """
            texture2D sceneTarget : RENDERCOLORTARGET <
              float2 ViewPortRatio = { 0.5, 0.25 };
            >;
            sampler2D sceneSampler = sampler_state { texture = <sceneTarget>; };

            texture2D normalTarget : OFFSCREENRENDERTARGET;
            sampler normalSampler = sampler_state { texture = <normalTarget>; };

            float4 VS(float4 position : POSITION) : POSITION
            {
                return position;
            }

            float4 PS(float2 uv : TEXCOORD0) : COLOR
            {
                return tex2D(sceneSampler, uv) + tex2D(normalSampler, uv);
            }

            technique T {
              pass P {
                VertexShader = compile vs_3_0 VS();
                PixelShader = compile ps_3_0 PS();
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "targets.fx"));
        var shader = Assert.IsType<RenderEffectShaderProgram>(effect.DefaultTechnique!.Passes.Single().Shader);

        var sceneSampler = shader.Uniforms.Single(uniform => uniform.Name == "sceneSampler");
        Assert.Equal(RenderEffectParameterKind.Sampler, sceneSampler.Kind);
        Assert.Equal("sceneTarget", sceneSampler.TextureSourceName);
        Assert.Equal(RenderEffectSemantic.RenderColorTarget, sceneSampler.Semantic);

        var normalSampler = shader.Uniforms.Single(uniform => uniform.Name == "normalSampler");
        Assert.Equal(RenderEffectParameterKind.Sampler, normalSampler.Kind);
        Assert.Equal("normalTarget", normalSampler.TextureSourceName);
        Assert.Equal(RenderEffectSemantic.OffscreenRenderTarget, normalSampler.Semantic);
    }

    [Fact]
    public void CompilesOffscreenRenderTargetMetadata()
    {
        const string text = """
            texture2D normalTarget : OFFSCREENRENDERTARGET <
              string Description = "Normal map target";
              float4 ClearColor = { 0.1, 0.2, 0.3, 1.0 };
              float ClearDepth = 0.75;
              string DefaultEffect =
                "self = hide;"
                "* = effects/main.fx;";
            >;
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "offscreen.fx"));

        var parameter = effect.Parameters.Single(parameter => parameter.Name == "normalTarget");
        var offscreen = Assert.IsType<RenderEffectOffscreenTarget>(parameter.OffscreenTarget);
        Assert.Equal("normalTarget", offscreen.Name);
        Assert.Equal("Normal map target", offscreen.Description);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 1), offscreen.ClearColor);
        Assert.Equal(0.75f, offscreen.ClearDepth);
        Assert.Collection(
            offscreen.DefaultEffects,
            condition =>
            {
                Assert.Equal("self", condition.Target);
                Assert.Equal("hide", condition.EffectPath);
            },
            condition =>
            {
                Assert.Equal("*", condition.Target);
                Assert.Equal("effects/main.fx", condition.EffectPath);
            });
    }

    [Fact]
    public void CompilesMipmapAnnotationsForTexturesAndRenderTargets()
    {
        const string text = """
            float4 VS(float4 position : POSITION) : POSITION
            {
                return position;
            }

            texture2D mainTexture : MATERIALTEXTURE <
              string ResourceName = "diffuse.png";
              bool MipMap = true;
            >;

            sampler2D mainSampler = sampler_state
            {
                Texture = <mainTexture>;
            };

            texture2D sceneTarget : RENDERCOLORTARGET <
              int Mipmap = 1;
            >;

            sampler2D sceneSampler = sampler_state
            {
                Texture = <sceneTarget>;
            };

            texture2D normalTarget : OFFSCREENRENDERTARGET <
              string Description = "Normal target";
              bool GenerateMipmap = true;
            >;

            sampler2D normalSampler = sampler_state
            {
                Texture = <normalTarget>;
            };

            float4 PS(float2 uv : TEXCOORD0) : COLOR0
            {
                return tex2D(mainSampler, uv) + tex2D(sceneSampler, uv) + tex2D(normalSampler, uv);
            }

            technique Main {
              pass P0 {
                VertexShader = compile vs_3_0 VS();
                PixelShader = compile ps_3_0 PS();
              }
            }
            """;

        var effect = RenderEffectCompiler.Compile(new MmeEffectReader().ReadText(text, "mipmap.fx"));

        var mainTexture = effect.Parameters.Single(parameter => parameter.Name == "mainTexture");
        Assert.True(mainTexture.MipmapEnabled);

        var sceneTarget = effect.Parameters.Single(parameter => parameter.Name == "sceneTarget");
        Assert.True(sceneTarget.MipmapEnabled);

        var normalTarget = effect.Parameters.Single(parameter => parameter.Name == "normalTarget");
        Assert.True(normalTarget.MipmapEnabled);
        Assert.True(Assert.IsType<RenderEffectOffscreenTarget>(normalTarget.OffscreenTarget).MipmapEnabled);

        var shader = Assert.IsType<RenderEffectShaderProgram>(effect.DefaultTechnique!.Passes.Single().Shader);
        Assert.True(shader.Uniforms.Single(uniform => uniform.Name == "mainSampler").MipmapEnabled);
        Assert.True(shader.Uniforms.Single(uniform => uniform.Name == "sceneSampler").MipmapEnabled);
        Assert.True(shader.Uniforms.Single(uniform => uniform.Name == "normalSampler").MipmapEnabled);
    }

    [Fact]
    public void CreatesOffscreenDrawPlanFromDefaultEffectConditions()
    {
        var target = new RenderEffectOffscreenTarget(
            "normalTarget",
            string.Empty,
            new Vector4(0, 0, 0, 1),
            1,
            [
                new RenderEffectOffscreenDefaultEffect("self", "hide"),
                new RenderEffectOffscreenDefaultEffect("AccessoryA", "effects/accessory.fx"),
                new RenderEffectOffscreenDefaultEffect("*", "effects/main.fx")
            ]);

        var plan = target.CreateDrawPlan("ModelA", ["ModelA", "ModelB", "AccessoryA"]);

        Assert.Equal("normalTarget", plan.Target.Name);
        Assert.Collection(
            plan.Decisions,
            decision =>
            {
                Assert.Equal("ModelA", decision.DrawableName);
                Assert.Equal(RenderEffectOffscreenAction.Hide, decision.Action);
                Assert.Null(decision.EffectPath);
            },
            decision =>
            {
                Assert.Equal("ModelB", decision.DrawableName);
                Assert.Equal(RenderEffectOffscreenAction.DrawWithExternalEffect, decision.Action);
                Assert.Equal("effects/main.fx", decision.EffectPath);
            },
            decision =>
            {
                Assert.Equal("AccessoryA", decision.DrawableName);
                Assert.Equal(RenderEffectOffscreenAction.DrawWithExternalEffect, decision.Action);
                Assert.Equal("effects/accessory.fx", decision.EffectPath);
            });
    }
}
