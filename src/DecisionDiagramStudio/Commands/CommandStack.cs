using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<CommandStack> _logger;
    private readonly List<IUndoableCommand> _undoCommands = [];
    private readonly List<IUndoableCommand> _redoCommands = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStack"/> class.
    /// </summary>
    public CommandStack()
        : this(DefaultHistoryLimit, NullLogger<CommandStack>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStack"/> class.
    /// </summary>
    /// <param name="logger">The logger used for command-stack diagnostics.</param>
    public CommandStack(ILogger<CommandStack> logger)
        : this(DefaultHistoryLimit, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStack"/> class.
    /// </summary>
    /// <param name="historyLimit">The maximum number of undo entries to retain.</param>
    public CommandStack(int historyLimit)
        : this(historyLimit, NullLogger<CommandStack>.Instance)
    {
    }

    private CommandStack(int historyLimit, ILogger<CommandStack> logger)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(historyLimit, 1);
        _historyLimit = historyLimit;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        try
        {
            command.Execute();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Undoable command execution failed. CommandType={CommandType} ExceptionType={ExceptionType}",
                command.GetType().Name,
                ex.GetType().Name);
            throw;
        }

        _undoCommands.Add(command);
        if (_undoCommands.Count > _historyLimit)
        {
            _undoCommands.RemoveAt(0);
            _logger.LogDebug("Undo history limit exceeded. HistoryLimit={HistoryLimit}", _historyLimit);
        }

        _redoCommands.Clear();
        _logger.LogInformation(
            "Undoable command pushed. CommandType={CommandType} UndoCount={UndoCount} RedoCount={RedoCount}",
            command.GetType().Name,
            _undoCommands.Count,
            _redoCommands.Count);
    }

    /// <summary>
    /// Undoes the most recent command when one exists.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
        {
            _logger.LogTrace("Undo requested with an empty undo history.");
            return;
        }

        var lastIndex = _undoCommands.Count - 1;
        var command = _undoCommands[lastIndex];
        command.Undo();
        _undoCommands.RemoveAt(lastIndex);
        _redoCommands.Add(command);
        _logger.LogInformation(
            "Undoable command undone. CommandType={CommandType} UndoCount={UndoCount} RedoCount={RedoCount}",
            command.GetType().Name,
            _undoCommands.Count,
            _redoCommands.Count);
    }

    /// <summary>
    /// Redoes the most recently undone command when one exists.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
        {
            _logger.LogTrace("Redo requested with an empty redo history.");
            return;
        }

        var lastIndex = _redoCommands.Count - 1;
        var command = _redoCommands[lastIndex];
        command.Execute();
        _redoCommands.RemoveAt(lastIndex);
        _undoCommands.Add(command);
        _logger.LogInformation(
            "Undoable command redone. CommandType={CommandType} UndoCount={UndoCount} RedoCount={RedoCount}",
            command.GetType().Name,
            _undoCommands.Count,
            _redoCommands.Count);
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        _undoCommands.Clear();
        _redoCommands.Clear();
        _logger.LogInformation("Undo and redo history cleared.");
    }
}
