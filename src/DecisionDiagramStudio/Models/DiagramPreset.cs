namespace DecisionDiagramStudio.Models;

/// <summary>
/// Stores a preset that can initialize a diagram session.
/// </summary>
public sealed record DiagramPreset
{
    /// <summary>
    /// Gets the stable preset identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing preset label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing preset description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the variable names used by the preset.
    /// </summary>
    public string[] VariableNames { get; init; } = [];

    /// <summary>
    /// Gets the truth table values used by the preset.
    /// </summary>
    public int[] TruthTableValues { get; init; } = [];

    /// <summary>
    /// Gets the default diagram family for the preset.
    /// </summary>
    public DiagramFamily DefaultFamily { get; init; } = DiagramFamily.BDD;
}
