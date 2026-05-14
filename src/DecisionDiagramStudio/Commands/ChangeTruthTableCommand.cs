using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

namespace DecisionDiagramStudio.Commands;

/// <summary>
/// Rebuilds a diagram session when a BDD truth table changes.
/// </summary>
public sealed class ChangeTruthTableCommand : IUndoableCommand
{
    private readonly IDiagramService _diagramService;
    private readonly string[] _variableNames;
    private readonly int[] _beforeValues;
    private readonly int[] _afterValues;
    private readonly DiagramFamily _family;
    private readonly Action<DiagramSession>? _applySession;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeTruthTableCommand"/> class.
    /// </summary>
    /// <param name="diagramService">The diagram service used to rebuild sessions.</param>
    /// <param name="variableNames">The variable names in least-significant-bit order.</param>
    /// <param name="beforeValues">The truth table snapshot before the change.</param>
    /// <param name="afterValues">The truth table snapshot after the change.</param>
    /// <param name="family">The diagram family to rebuild.</param>
    /// <param name="applySession">An optional callback that receives each rebuilt session.</param>
    /// <param name="cancellationToken">A cancellation token for the rebuild.</param>
    public ChangeTruthTableCommand(
        IDiagramService diagramService,
        string[] variableNames,
        int[] beforeValues,
        int[] afterValues,
        DiagramFamily family,
        Action<DiagramSession>? applySession = null,
        CancellationToken cancellationToken = default)
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(beforeValues);
        ArgumentNullException.ThrowIfNull(afterValues);

        _variableNames = (string[])variableNames.Clone();
        _beforeValues = (int[])beforeValues.Clone();
        _afterValues = (int[])afterValues.Clone();
        _family = family;
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
        Apply(_afterValues);
    }

    /// <inheritdoc />
    public void Undo()
    {
        Apply(_beforeValues);
    }

    private void Apply(int[] values)
    {
        CurrentSession = _diagramService
            .BuildAsync((string[])_variableNames.Clone(), (int[])values.Clone(), _family, _cancellationToken)
            .GetAwaiter()
            .GetResult();
        _applySession?.Invoke(CurrentSession);
    }
}
