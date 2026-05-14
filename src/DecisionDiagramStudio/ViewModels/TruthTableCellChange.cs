namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Describes a single truth-table cell update request.
/// </summary>
/// <param name="Index">The zero-based truth-table row index.</param>
/// <param name="Value">The new truth-table cell value.</param>
public sealed record TruthTableCellChange(int Index, int Value);
