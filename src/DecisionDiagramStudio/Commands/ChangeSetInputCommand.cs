using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Rebuilds a ZDD session when the set-family input changes.
/// </summary>
public sealed class ChangeSetInputCommand : IUndoableCommand
{
    private readonly IDiagramService _diagramService;
    private readonly string[] _beforeVariableNames;
    private readonly string[] _afterVariableNames;
    private readonly IReadOnlyList<IReadOnlyList<string>> _beforeSetInput;
    private readonly IReadOnlyList<IReadOnlyList<string>> _afterSetInput;
    private readonly Action<DiagramSession>? _applySession;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSetInputCommand"/> class.
    /// </summary>
    public ChangeSetInputCommand(
        IDiagramService diagramService,
        string[] beforeVariableNames,
        IReadOnlyList<IReadOnlyList<string>> beforeSetInput,
        string[] afterVariableNames,
        IReadOnlyList<IReadOnlyList<string>> afterSetInput,
        Action<DiagramSession>? applySession = null,
        CancellationToken cancellationToken = default)
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        ArgumentNullException.ThrowIfNull(beforeVariableNames);
        ArgumentNullException.ThrowIfNull(beforeSetInput);
        ArgumentNullException.ThrowIfNull(afterVariableNames);
        ArgumentNullException.ThrowIfNull(afterSetInput);

        _beforeVariableNames = (string[])beforeVariableNames.Clone();
        _afterVariableNames = (string[])afterVariableNames.Clone();
        _beforeSetInput = CloneSetInput(beforeSetInput);
        _afterSetInput = CloneSetInput(afterSetInput);
        _applySession = applySession;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the most recent session returned by the diagram service.
    /// </summary>
    public DiagramSession? CurrentSession { get; private set; }

    /// <inheritdoc />
    public void Execute()
    {
        Apply(_afterVariableNames, _afterSetInput);
    }

    /// <inheritdoc />
    public void Undo()
    {
        Apply(_beforeVariableNames, _beforeSetInput);
    }

    private void Apply(string[] variableNames, IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        CurrentSession = _diagramService
            .BuildAsync((string[])variableNames.Clone(), CloneSetInput(setInput), DiagramFamily.ZDD, _cancellationToken)
            .GetAwaiter()
            .GetResult();
        _applySession?.Invoke(CurrentSession);
    }

    private static IReadOnlyList<IReadOnlyList<string>> CloneSetInput(IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        var clone = new IReadOnlyList<string>[setInput.Count];
        for (var i = 0; i < setInput.Count; i++)
        {
            clone[i] = setInput[i].ToArray();
        }

        return clone;
    }
}
