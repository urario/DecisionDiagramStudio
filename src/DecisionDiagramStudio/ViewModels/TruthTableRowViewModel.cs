namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Represents one formatted row in the BDD truth-table editor.
/// </summary>
/// <param name="Index">The zero-based truth-table row index.</param>
/// <param name="RowLabel">The short row label shown in the grid.</param>
/// <param name="AssignmentText">The formatted variable assignment for the row.</param>
/// <param name="Value">The BDD truth-table value for the row.</param>
public sealed record TruthTableRowViewModel(int Index, string RowLabel, string AssignmentText, int Value)
{
    /// <summary>
    /// Gets a value indicating whether the row value is one.
    /// </summary>
    public bool IsOne => Value == 1;
}
