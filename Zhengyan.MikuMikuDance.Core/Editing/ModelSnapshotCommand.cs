using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Core.Editing;

public sealed class ModelSnapshotCommand : IUndoableCommand
{
    private readonly SnapshotUndoableCommand<MmdModelSnapshot> _inner;

    public ModelSnapshotCommand(string name, MmdModel model, Action execute)
    {
        ArgumentNullException.ThrowIfNull(model);
        _inner = new SnapshotUndoableCommand<MmdModelSnapshot>(
            name,
            () => MmdModelSnapshot.Capture(model),
            snapshot => snapshot.Restore(model),
            execute ?? throw new ArgumentNullException(nameof(execute)));
    }

    public string Name => _inner.Name;

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}
