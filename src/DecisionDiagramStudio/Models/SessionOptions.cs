namespace DecisionDiagramStudio.Models;

/// <summary>
/// Stores user-configurable options for a studio session.
/// </summary>
public sealed record SessionOptions
{
    /// <summary>
    /// Gets the optional Graphviz dot executable path.
    /// </summary>
    public string GraphvizPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured application theme.
    /// </summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>
    /// Gets the maximum node count allowed for diagram construction.
    /// </summary>
    public int MaxNodeCount { get; init; } = 10_000;

    /// <summary>
    /// Gets the maximum row count allowed for model or set enumeration.
    /// </summary>
    public int MaxEnumerationCount { get; init; } = 2_048;

    /// <summary>
    /// Gets the maximum number of undoable operations to retain.
    /// </summary>
    public int UndoHistoryLimit { get; init; } = 50;
}
