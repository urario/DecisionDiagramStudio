namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Defines a command that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverts the command.
    /// </summary>
    void Undo();
}
