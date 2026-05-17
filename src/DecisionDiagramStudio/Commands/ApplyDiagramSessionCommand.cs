using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Applies prebuilt diagram session snapshots for undoable operations.
/// </summary>
public sealed class ApplyDiagramSessionCommand : IUndoableCommand
{
    private readonly DiagramSession _beforeSession;
    private readonly DiagramSession _afterSession;
    private readonly Action<DiagramSession> _applySession;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyDiagramSessionCommand"/> class.
    /// </summary>
    public ApplyDiagramSessionCommand(
        DiagramSession beforeSession,
        DiagramSession afterSession,
        Action<DiagramSession> applySession)
    {
        _beforeSession = beforeSession ?? throw new ArgumentNullException(nameof(beforeSession));
        _afterSession = afterSession ?? throw new ArgumentNullException(nameof(afterSession));
        _applySession = applySession ?? throw new ArgumentNullException(nameof(applySession));
    }

    /// <inheritdoc />
    public void Execute()
    {
        _applySession(_afterSession);
    }

    /// <inheritdoc />
    public void Undo()
    {
        _applySession(_beforeSession);
    }
}
