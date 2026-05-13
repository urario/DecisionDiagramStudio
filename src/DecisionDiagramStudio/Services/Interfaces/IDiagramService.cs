using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines the application boundary for building and inspecting decision diagrams.
/// </summary>
public interface IDiagramService
{
    /// <summary>
    /// Builds a decision diagram session from an integer value table.
    /// </summary>
    /// <param name="variableNames">The variable names in least-significant-bit order.</param>
    /// <param name="intValueTable">The input value table for the selected diagram family.</param>
    /// <param name="family">The diagram family to build.</param>
    /// <param name="ct">A cancellation token for abandoning the build.</param>
    /// <returns>The generated application-owned diagram session.</returns>
    Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct);

    /// <summary>
    /// Builds DOT text for the unreduced binary decision tree represented by a BDD session.
    /// </summary>
    /// <param name="session">The BDD session containing variable names and truth-table values.</param>
    /// <param name="ct">A cancellation token for abandoning generation.</param>
    /// <returns>DOT text for the complete binary decision tree.</returns>
    Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct);
}
