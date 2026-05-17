namespace DecisionDiagramStudio.Models;

/// <summary>
/// Defines set-family operations supported by ZDD sessions.
/// </summary>
public enum ZddOperation
{
    /// <summary>
    /// Set-family union.
    /// </summary>
    Union,

    /// <summary>
    /// Set-family intersection.
    /// </summary>
    Intersection,

    /// <summary>
    /// Set-family difference.
    /// </summary>
    Difference,
}
