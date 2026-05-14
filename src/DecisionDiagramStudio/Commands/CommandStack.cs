namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Manages undo and redo history for undoable commands.
/// </summary>
public sealed class CommandStack
{
    /// <summary>
    /// The default maximum number of undo entries to retain.
    /// </summary>
    public const int DefaultHistoryLimit = 50;

    private readonly int _historyLimit;
    private readonly List<IUndoableCommand> _undoCommands = [];
    private readonly List<IUndoableCommand> _redoCommands = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStack"/> class.
    /// </summary>
    public CommandStack()
        : this(DefaultHistoryLimit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStack"/> class.
    /// </summary>
    /// <param name="historyLimit">The maximum number of undo entries to retain.</param>
    public CommandStack(int historyLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(historyLimit, 1);
        _historyLimit = historyLimit;
    }

    /// <summary>
    /// Gets a value indicating whether an undo operation is available.
    /// </summary>
    public bool CanUndo => _undoCommands.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a redo operation is available.
    /// </summary>
    public bool CanRedo => _redoCommands.Count > 0;

    /// <summary>
    /// Executes and stores a command.
    /// </summary>
    /// <param name="command">The command to execute and store.</param>
    public void Push(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undoCommands.Add(command);
        if (_undoCommands.Count > _historyLimit)
        {
            _undoCommands.RemoveAt(0);
        }

        _redoCommands.Clear();
    }

    /// <summary>
    /// Undoes the most recent command when one exists.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var lastIndex = _undoCommands.Count - 1;
        var command = _undoCommands[lastIndex];
        command.Undo();
        _undoCommands.RemoveAt(lastIndex);
        _redoCommands.Add(command);
    }

    /// <summary>
    /// Redoes the most recently undone command when one exists.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var lastIndex = _redoCommands.Count - 1;
        var command = _redoCommands[lastIndex];
        command.Execute();
        _redoCommands.RemoveAt(lastIndex);
        _undoCommands.Add(command);
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        _undoCommands.Clear();
        _redoCommands.Clear();
    }
}
