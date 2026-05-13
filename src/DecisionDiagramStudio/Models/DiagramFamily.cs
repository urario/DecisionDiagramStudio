namespace DecisionDiagramStudio.Models;

/// <summary>
/// Identifies the decision diagram family selected for a session.
/// </summary>
public enum DiagramFamily
{
    /// <summary>
    /// Binary decision diagram.
    /// </summary>
    BDD,

    /// <summary>
    /// Zero-suppressed decision diagram.
    /// </summary>
    ZDD,

    /// <summary>
    /// Multi-terminal binary decision diagram.
    /// </summary>
    MTBDD,

    /// <summary>
    /// Zero-suppressed multi-terminal binary decision diagram.
    /// </summary>
    ZMTBDD,
}
