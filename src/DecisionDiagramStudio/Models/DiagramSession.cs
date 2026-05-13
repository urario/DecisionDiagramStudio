namespace DecisionDiagramStudio.Models;

/// <summary>
/// Stores the application-owned state for a generated decision diagram session.
/// </summary>
public sealed record DiagramSession
{
    /// <summary>
    /// Gets the decision diagram family used by the session.
    /// </summary>
    public DiagramFamily Family { get; init; } = DiagramFamily.BDD;

    /// <summary>
    /// Gets the variable names registered for the session.
    /// </summary>
    public string[] VariableNames { get; init; } = [];

    /// <summary>
    /// Gets the variable order used to build the diagram.
    /// </summary>
    public int[] VariableOrder { get; init; } = [];

    /// <summary>
    /// Gets the integer value table used by BDD, MTBDD, and ZMTBDD sessions.
    /// </summary>
    public int[]? IntValueTable { get; init; }

    /// <summary>
    /// Gets the set-family input used by ZDD sessions.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>>? SetInput { get; init; }

    /// <summary>
    /// Gets the DOT text generated for the current diagram.
    /// </summary>
    public string DotText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the application statistics generated for the session.
    /// </summary>
    public AppDiagramStatistics Statistics { get; init; } = AppDiagramStatistics.Empty;

    /// <summary>
    /// Gets a value indicating whether the session has no DOT text.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(DotText);

    /// <summary>
    /// Gets the time at which the session was last modified.
    /// </summary>
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
}
