using DecisionDiagramStudio.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Commands;

/// <summary>
/// Verifies command stack undo and redo behavior.
/// </summary>
[TestClass]
public sealed class CommandStackTests
{
    /// <summary>
    /// Verifies the empty stack state.
    /// </summary>
    [TestMethod]
    public void NewStack_ShouldNotAllowUndoOrRedo()
    {
        // Arrange / Act
        var stack = new CommandStack();

        // Assert
        Assert.IsFalse(stack.CanUndo, "A new stack should not have undo history.");
        Assert.IsFalse(stack.CanRedo, "A new stack should not have redo history.");
    }

    /// <summary>
    /// Verifies that Push executes the command and enables undo.
    /// </summary>
    [TestMethod]
    public void Push_ShouldExecuteCommandAndEnableUndo()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();

        // Act
        stack.Push(new RecordingCommand("one", log));

        // Assert
        CollectionAssert.AreEqual(new[] { "execute:one" }, log, "Push should execute the command.");
        Assert.IsTrue(stack.CanUndo, "Pushed commands should be undoable.");
        Assert.IsFalse(stack.CanRedo, "A fresh push should not create redo history.");
    }

    /// <summary>
    /// Verifies that Undo calls the latest command and enables redo.
    /// </summary>
    [TestMethod]
    public void Undo_ShouldUndoLatestCommandAndEnableRedo()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        stack.Push(new RecordingCommand("one", log));

        // Act
        stack.Undo();

        // Assert
        CollectionAssert.AreEqual(new[] { "execute:one", "undo:one" }, log, "Undo should call the command undo path.");
        Assert.IsFalse(stack.CanUndo, "Undoing the only command should empty undo history.");
        Assert.IsTrue(stack.CanRedo, "Undo should create redo history.");
    }

    /// <summary>
    /// Verifies that Redo re-executes the latest undone command.
    /// </summary>
    [TestMethod]
    public void Redo_ShouldExecuteLatestUndoneCommand()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        stack.Push(new RecordingCommand("one", log));
        stack.Undo();

        // Act
        stack.Redo();

        // Assert
        CollectionAssert.AreEqual(new[] { "execute:one", "undo:one", "execute:one" }, log, "Redo should execute the undone command.");
        Assert.IsTrue(stack.CanUndo, "Redo should restore undo history.");
        Assert.IsFalse(stack.CanRedo, "Redoing the only command should empty redo history.");
    }

    /// <summary>
    /// Verifies that Push after Undo clears redo history.
    /// </summary>
    [TestMethod]
    public void PushAfterUndo_ShouldClearRedoHistory()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        stack.Push(new RecordingCommand("one", log));
        stack.Undo();

        // Act
        stack.Push(new RecordingCommand("two", log));

        // Assert
        Assert.IsTrue(stack.CanUndo, "The new command should be undoable.");
        Assert.IsFalse(stack.CanRedo, "A divergent new command should clear redo history.");
    }

    /// <summary>
    /// Verifies that Undo on an empty stack is a no-op.
    /// </summary>
    [TestMethod]
    public void Undo_WhenEmpty_ShouldNoOp()
    {
        // Arrange
        var stack = new CommandStack();

        // Act
        stack.Undo();

        // Assert
        Assert.IsFalse(stack.CanUndo, "Undo on empty stack should keep undo unavailable.");
        Assert.IsFalse(stack.CanRedo, "Undo on empty stack should keep redo unavailable.");
    }

    /// <summary>
    /// Verifies that Redo on an empty stack is a no-op.
    /// </summary>
    [TestMethod]
    public void Redo_WhenEmpty_ShouldNoOp()
    {
        // Arrange
        var stack = new CommandStack();

        // Act
        stack.Redo();

        // Assert
        Assert.IsFalse(stack.CanUndo, "Redo on empty stack should keep undo unavailable.");
        Assert.IsFalse(stack.CanRedo, "Redo on empty stack should keep redo unavailable.");
    }

    /// <summary>
    /// Verifies the null command guard.
    /// </summary>
    [TestMethod]
    public void Push_NullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        var stack = new CommandStack();

        // Act
        var exception = Assert.ThrowsException<ArgumentNullException>(() => stack.Push(null!));

        // Assert
        Assert.AreEqual("command", exception.ParamName, "Null command pushes should identify the command parameter.");
    }

    /// <summary>
    /// Verifies the history limit guard.
    /// </summary>
    [TestMethod]
    public void Constructor_InvalidLimit_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange / Act
        var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CommandStack(0));

        // Assert
        Assert.AreEqual("historyLimit", exception.ParamName, "History limits must be positive.");
    }

    /// <summary>
    /// Verifies that undo uses last-in-first-out ordering.
    /// </summary>
    [TestMethod]
    public void Undo_WithMultipleCommands_ShouldUseLifoOrder()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        stack.Push(new RecordingCommand("one", log));
        stack.Push(new RecordingCommand("two", log));

        // Act
        stack.Undo();
        stack.Undo();

        // Assert
        CollectionAssert.AreEqual(
            new[] { "execute:one", "execute:two", "undo:two", "undo:one" },
            log,
            "Undo should walk commands from newest to oldest.");
    }

    /// <summary>
    /// Verifies that redo restores commands in the original execution order after multiple undos.
    /// </summary>
    [TestMethod]
    public void Redo_WithMultipleCommands_ShouldRestoreOriginalOrder()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        stack.Push(new RecordingCommand("one", log));
        stack.Push(new RecordingCommand("two", log));
        stack.Undo();
        stack.Undo();

        // Act
        stack.Redo();
        stack.Redo();

        // Assert
        CollectionAssert.AreEqual(
            new[] { "execute:one", "execute:two", "undo:two", "undo:one", "execute:one", "execute:two" },
            log,
            "Redo should restore the oldest undone command first.");
    }

    /// <summary>
    /// Verifies that pushing beyond the history limit drops the oldest undo entry.
    /// </summary>
    [TestMethod]
    public void Push_WhenHistoryLimitExceeded_ShouldDropOldestCommand()
    {
        // Arrange
        var log = new List<string>();
        var stack = new CommandStack();
        var commands = Enumerable.Range(1, 51)
            .Select(i => new RecordingCommand(i.ToString(), log))
            .ToArray();

        // Act
        foreach (var command in commands)
        {
            stack.Push(command);
        }

        for (var i = 0; i < CommandStack.DefaultHistoryLimit; i++)
        {
            stack.Undo();
        }

        // Assert
        Assert.IsFalse(stack.CanUndo, "Only the newest 50 commands should remain undoable.");
        Assert.IsFalse(commands[0].WasUndone, "The oldest command should be dropped when the limit is exceeded.");
        Assert.IsTrue(commands[1].WasUndone, "The second command should become the oldest retained entry.");
        Assert.IsTrue(commands[50].WasUndone, "The newest command should be retained.");
    }

    private sealed class RecordingCommand : IUndoableCommand
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingCommand(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public bool WasUndone { get; private set; }

        public void Execute()
        {
            _log.Add("execute:" + _name);
        }

        public void Undo()
        {
            WasUndone = true;
            _log.Add("undo:" + _name);
        }
    }
}
