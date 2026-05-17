using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Rebuilds the workbench when the active diagram family changes.
/// </summary>
public sealed class ChangeFamilyCommand : IUndoableCommand
{
    private readonly IDiagramService _diagramService;
    private readonly DiagramFamily _beforeFamily;
    private readonly DiagramFamily _afterFamily;
    private readonly string[] _variableNames;
    private readonly int[] _intValueTable;
    private readonly IReadOnlyList<IReadOnlyList<string>> _setInput;
    private readonly Action<DiagramSession>? _applySession;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeFamilyCommand"/> class.
    /// </summary>
    public ChangeFamilyCommand(
        IDiagramService diagramService,
        DiagramFamily beforeFamily,
        DiagramFamily afterFamily,
        string[] variableNames,
        int[] intValueTable,
        IReadOnlyList<IReadOnlyList<string>> setInput,
        Action<DiagramSession>? applySession = null,
        CancellationToken cancellationToken = default)
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(intValueTable);
        ArgumentNullException.ThrowIfNull(setInput);

        _beforeFamily = beforeFamily;
        _afterFamily = afterFamily;
        _variableNames = (string[])variableNames.Clone();
        _intValueTable = (int[])intValueTable.Clone();
        _setInput = CloneSetInput(setInput);
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
        Apply(_afterFamily);
    }

    /// <inheritdoc />
    public void Undo()
    {
        Apply(_beforeFamily);
    }

    private void Apply(DiagramFamily family)
    {
        CurrentSession = family switch
        {
            DiagramFamily.BDD => _diagramService
                .BuildAsync((string[])_variableNames.Clone(), (int[])_intValueTable.Clone(), DiagramFamily.BDD, _cancellationToken)
                .GetAwaiter()
                .GetResult(),
            DiagramFamily.MTBDD => _diagramService
                .BuildAsync((string[])_variableNames.Clone(), (int[])_intValueTable.Clone(), DiagramFamily.MTBDD, _cancellationToken)
                .GetAwaiter()
                .GetResult(),
            DiagramFamily.ZMTBDD => _diagramService
                .BuildAsync((string[])_variableNames.Clone(), (int[])_intValueTable.Clone(), DiagramFamily.ZMTBDD, _cancellationToken)
                .GetAwaiter()
                .GetResult(),
            DiagramFamily.ZDD => _diagramService
                .BuildAsync((string[])_variableNames.Clone(), CloneSetInput(_setInput), DiagramFamily.ZDD, _cancellationToken)
                .GetAwaiter()
                .GetResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported diagram family."),
        };

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
