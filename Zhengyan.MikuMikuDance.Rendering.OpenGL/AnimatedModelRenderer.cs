using Silk.NET.OpenGL;
using Zhengyan.MikuMikuDance.Core.Animation;
using Zhengyan.MikuMikuDance.Core.Modeling;
using Zhengyan.MikuMikuDance.Core.Scene;
using Zhengyan.MikuMikuDance.Rendering;

namespace Zhengyan.MikuMikuDance.Rendering.OpenGL;

public sealed class AnimatedModelRenderer : IRenderer, IOpenGlRenderer
{
    private readonly ModelInstance _instance;
    private readonly Motion _motion;
    private readonly AnimationPlaybackClock _clock;
    private readonly string? _textureBaseDirectory;
    private OpenGlMeshProgram? _program;
    private OpenGlBackgroundImagePass? _backgroundImagePass;
    private OpenGlMeshBuffer? _mesh;
    private int _lastFrameIndex = -1;

    public AnimatedModelRenderer(
        ModelInstance instance,
        Motion motion,
        float framesPerSecond = 30f,
        string? textureBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(motion);
        _instance = instance;
        _motion = motion;
        _clock = new AnimationPlaybackClock(framesPerSecond);
        _textureBaseDirectory = textureBaseDirectory;
    }

    public int CurrentFrameIndex { get; private set; }

    public void Load(RenderDeviceInfo deviceInfo)
    {
        throw new InvalidOperationException("AnimatedModelRenderer requires an OpenGL render host.");
    }

    public void Load(GL gl, RenderDeviceInfo deviceInfo)
    {
        _program = new OpenGlMeshProgram(gl, _textureBaseDirectory);
        _backgroundImagePass = new OpenGlBackgroundImagePass(gl, _textureBaseDirectory);
        var mesh = CreateMesh(0);
        if (mesh.Vertices.Count > 0 && mesh.Indices.Count > 0)
        {
            _mesh = new OpenGlMeshBuffer(gl, mesh, BufferUsageARB.DynamicDraw);
        }
    }

    public void Resize(int width, int height)
    {
    }

    public void Render(RenderFrameContext context)
    {
        if (_program is null || _mesh is null)
        {
            return;
        }

        CurrentFrameIndex = _clock.Advance(context.DeltaTimeSeconds, _motion.MaxFrameIndex);
        if (CurrentFrameIndex != _lastFrameIndex)
        {
            var sample = MotionSampler.Sample(_motion, CurrentFrameIndex);
            SceneMotionApplier.Apply(context.Project, sample);
            _mesh.UpdateVertices(CreateMesh(sample));
            _lastFrameIndex = CurrentFrameIndex;
        }

        if (!_instance.Visible)
        {
            return;
        }

        _backgroundImagePass?.Draw(context);
        _program.Use(context);
        _program.DrawScene([_mesh]);
        _program.End();
    }

    public void Dispose()
    {
        _mesh?.Dispose();
        _mesh = null;
        _backgroundImagePass?.Dispose();
        _backgroundImagePass = null;
        _program?.Dispose();
        _program = null;
    }

    private RenderMesh CreateMesh(int frameIndex)
    {
        return CreateMesh(MotionSampler.Sample(_motion, frameIndex));
    }

    private RenderMesh CreateMesh(MotionSample sample)
    {
        var state = AnimationPoseEvaluator.Evaluate(_instance.Model, sample);
        return RenderMeshBuilder.FromModel(_instance, state.Pose, state.Morphs);
    }
}
