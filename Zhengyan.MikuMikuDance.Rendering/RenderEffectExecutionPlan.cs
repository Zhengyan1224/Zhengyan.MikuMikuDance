using System.Text;

namespace Zhengyan.MikuMikuDance.Rendering;

public sealed record RenderEffectExecutionPlan(IReadOnlyList<RenderEffectTechniqueStep> Steps)
{
    public static RenderEffectExecutionPlan Empty { get; } = new([]);

    public IReadOnlyList<RenderEffectExecutePassStep> ExecutePasses =>
        Steps.OfType<RenderEffectExecutePassStep>().ToArray();
}

public abstract record RenderEffectTechniqueStep(RenderEffectScriptCommand Command);

public sealed record RenderEffectTechniqueCommandStep(RenderEffectScriptCommand Command) :
    RenderEffectTechniqueStep(Command);

public sealed record RenderEffectExecutePassStep(
    RenderEffectScriptCommand Command,
    int PassIndex,
    RenderEffectPass Pass,
    RenderEffectPassExecutionPlan PassPlan) : RenderEffectTechniqueStep(Command)
{
    public string PassName => Pass.Name;
}

public sealed record RenderEffectPassExecutionPlan(IReadOnlyList<RenderEffectPassStep> Steps)
{
    public bool DrawsGeometry =>
        Steps.OfType<RenderEffectPassDrawStep>().Any(step => step.Target == RenderEffectDrawTarget.Geometry);

    public bool DrawsBuffer =>
        Steps.OfType<RenderEffectPassDrawStep>().Any(step => step.Target == RenderEffectDrawTarget.Buffer);

    public static RenderEffectPassExecutionPlan Create(IReadOnlyList<RenderEffectScriptCommand> script)
    {
        var steps = new List<RenderEffectPassStep>(script.Count);
        foreach (var command in script)
        {
            steps.Add(command.Type == RenderEffectScriptCommandType.Draw
                ? new RenderEffectPassDrawStep(command, ToDrawTarget(command.Value))
                : new RenderEffectPassCommandStep(command));
        }

        return new RenderEffectPassExecutionPlan(steps);
    }

    private static RenderEffectDrawTarget ToDrawTarget(string value)
    {
        return NormalizeIdentifier(value) switch
        {
            "GEOMETRY" => RenderEffectDrawTarget.Geometry,
            "BUFFER" => RenderEffectDrawTarget.Buffer,
            _ => RenderEffectDrawTarget.Unknown
        };
    }

    private static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }
}

public abstract record RenderEffectPassStep(RenderEffectScriptCommand Command);

public sealed record RenderEffectPassCommandStep(RenderEffectScriptCommand Command) :
    RenderEffectPassStep(Command);

public sealed record RenderEffectPassDrawStep(RenderEffectScriptCommand Command, RenderEffectDrawTarget Target) :
    RenderEffectPassStep(Command);

public enum RenderEffectDrawTarget
{
    Unknown,
    Geometry,
    Buffer
}

public static class RenderEffectExecutionPlanner
{
    public static RenderEffectExecutionPlan Create(
        IReadOnlyList<RenderEffectScriptCommand> techniqueScript,
        IReadOnlyList<RenderEffectPass> passes)
    {
        var steps = new List<RenderEffectTechniqueStep>();
        var passPlans = passes.Select(pass => RenderEffectPassExecutionPlan.Create(pass.Script)).ToArray();
        if (techniqueScript.Count == 0)
        {
            AddAllPasses(steps, passes, passPlans);
            return new RenderEffectExecutionPlan(steps);
        }

        var hasExplicitPassCommand = false;
        foreach (var command in techniqueScript)
        {
            if (command.Type != RenderEffectScriptCommandType.ExecutePass)
            {
                steps.Add(new RenderEffectTechniqueCommandStep(command));
                continue;
            }

            hasExplicitPassCommand = true;
            if (TryFindPass(command.Value, passes, out var passIndex))
            {
                steps.Add(new RenderEffectExecutePassStep(command, passIndex, passes[passIndex], passPlans[passIndex]));
            }
            else
            {
                steps.Add(new RenderEffectTechniqueCommandStep(command));
            }
        }

        if (!hasExplicitPassCommand)
        {
            AddAllPasses(steps, passes, passPlans);
        }

        return new RenderEffectExecutionPlan(steps);
    }

    private static void AddAllPasses(
        List<RenderEffectTechniqueStep> steps,
        IReadOnlyList<RenderEffectPass> passes,
        IReadOnlyList<RenderEffectPassExecutionPlan> passPlans)
    {
        for (var i = 0; i < passes.Count; i++)
        {
            var command = new RenderEffectScriptCommand(RenderEffectScriptCommandType.ExecutePass, passes[i].Name);
            steps.Add(new RenderEffectExecutePassStep(command, i, passes[i], passPlans[i]));
        }
    }

    private static bool TryFindPass(string passName, IReadOnlyList<RenderEffectPass> passes, out int passIndex)
    {
        for (var i = 0; i < passes.Count; i++)
        {
            if (string.Equals(passes[i].Name, passName, StringComparison.Ordinal))
            {
                passIndex = i;
                return true;
            }
        }

        for (var i = 0; i < passes.Count; i++)
        {
            if (string.Equals(passes[i].Name, passName, StringComparison.OrdinalIgnoreCase))
            {
                passIndex = i;
                return true;
            }
        }

        passIndex = -1;
        return false;
    }
}
