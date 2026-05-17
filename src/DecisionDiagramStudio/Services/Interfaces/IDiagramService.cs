using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines the application boundary for building and inspecting decision diagrams.
/// </summary>
public interface IDiagramService
{
    /// <summary>
    /// Builds a BDD, MTBDD, or ZMTBDD session from an integer value table.
    /// </summary>
    /// <param name="variableNames">The variable names in least-significant-bit order.</param>
    /// <param name="intValueTable">The input value table for the selected diagram family. BDD values must be 0 or 1.</param>
    /// <param name="family">The diagram family to build. Must be BDD, MTBDD, or ZMTBDD.</param>
    /// <param name="ct">A cancellation token for abandoning the build.</param>
    /// <returns>The generated application-owned diagram session.</returns>
    Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct);

    /// <summary>
    /// Builds a ZDD session from a set-family input.
    /// </summary>
    /// <param name="variableNames">The variable names in canonical order.</param>
    /// <param name="setInput">The input set family.</param>
    /// <param name="family">The diagram family to build. Must be <see cref="DiagramFamily.ZDD"/>.</param>
    /// <param name="ct">A cancellation token for abandoning the build.</param>
    /// <returns>The generated application-owned diagram session.</returns>
    Task<DiagramSession> BuildAsync(string[] variableNames, IReadOnlyList<IReadOnlyList<string>> setInput, DiagramFamily family, CancellationToken ct);

    /// <summary>
    /// Applies a ZDD set-family operation to the two most recently built ZDD operands.
    /// </summary>
    /// <param name="operation">The operation to apply.</param>
    /// <param name="ct">A cancellation token for abandoning the operation.</param>
    /// <returns>The generated application-owned ZDD session.</returns>
    Task<DiagramSession> ApplyZddOperationAsync(ZddOperation operation, CancellationToken ct);

    /// <summary>
    /// Builds DOT text for the unreduced binary decision tree represented by a BDD session.
    /// </summary>
    /// <param name="session">The BDD session containing variable names and truth-table values.</param>
    /// <param name="ct">A cancellation token for abandoning generation.</param>
    /// <returns>DOT text for the complete binary decision tree.</returns>
    Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct);
}
