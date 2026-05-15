namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Represents one formatted row in the BDD truth-table editor.
/// </summary>
/// <param name="Index">The zero-based truth-table row index.</param>
/// <param name="RowLabel">The short row label shown in the grid.</param>
/// <param name="AssignmentText">The formatted variable assignment for the row.</param>
/// <param name="VariableValues">The variable values in display order for the row.</param>
/// <param name="Value">The BDD truth-table value for the row.</param>
public sealed record TruthTableRowViewModel(
    int Index,
    string RowLabel,
    string AssignmentText,
    IReadOnlyList<int> VariableValues,
    int Value)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TruthTableRowViewModel"/> class.
    /// </summary>
    /// <param name="index">The zero-based truth-table row index.</param>
    /// <param name="rowLabel">The short row label shown in the grid.</param>
    /// <param name="assignmentText">The formatted variable assignment for the row.</param>
    /// <param name="value">The BDD truth-table value for the row.</param>
    public TruthTableRowViewModel(int index, string rowLabel, string assignmentText, int value)
        : this(index, rowLabel, assignmentText, Array.Empty<int>(), value)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the row value is one.
    /// </summary>
    public bool IsOne => Value == 1;
}
