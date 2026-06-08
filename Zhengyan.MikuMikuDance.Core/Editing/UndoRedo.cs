namespace Zhengyan.MikuMikuDance.Core.Editing;

public interface IUndoableCommand
{
    string Name { get; }

    void Execute();

    void Undo();
}

public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoName => _undo.TryPeek(out var command) ? command.Name : null;

    public string? RedoName => _redo.TryPeek(out var command) ? command.Name : null;

    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    public bool TryUndo()
    {
        if (!_undo.TryPop(out var command))
        {
            return false;
        }

        command.Undo();
        _redo.Push(command);
        return true;
    }

    public bool TryRedo()
    {
        if (!_redo.TryPop(out var command))
        {
            return false;
        }

        command.Execute();
        _undo.Push(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

public sealed class DelegateUndoableCommand : IUndoableCommand
{
    private readonly Action _execute;
    private readonly Action _undo;

    public DelegateUndoableCommand(string name, Action execute, Action undo)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Command" : name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
    }

    public string Name { get; }

    public void Execute() => _execute();

    public void Undo() => _undo();
}

public sealed class BatchUndoableCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _commands;

    public BatchUndoableCommand(string name, IEnumerable<IUndoableCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        Name = string.IsNullOrWhiteSpace(name) ? "Batch" : name;
        _commands = commands.ToArray();
        if (_commands.Count == 0)
        {
            throw new ArgumentException("Batch command must contain at least one command.", nameof(commands));
        }
    }

    public string Name { get; }

    public IReadOnlyList<IUndoableCommand> Commands => _commands;

    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}

public sealed class SnapshotUndoableCommand<TSnapshot> : IUndoableCommand
{
    private readonly Func<TSnapshot> _capture;
    private readonly Action<TSnapshot> _restore;
    private readonly Action _execute;
    private TSnapshot _before = default!;
    private TSnapshot _after = default!;
    private bool _hasBefore;
    private bool _hasAfter;

    public SnapshotUndoableCommand(
        string name,
        Func<TSnapshot> capture,
        Action<TSnapshot> restore,
        Action execute)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Snapshot" : name;
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public string Name { get; }

    public void Execute()
    {
        if (_hasAfter)
        {
            _restore(_after);
            return;
        }

        _before = _capture();
        _hasBefore = true;
        _execute();
        _after = _capture();
        _hasAfter = true;
    }

    public void Undo()
    {
        if (!_hasBefore)
        {
            throw new InvalidOperationException("Cannot undo a snapshot command before it is executed.");
        }

        _restore(_before);
    }
}
