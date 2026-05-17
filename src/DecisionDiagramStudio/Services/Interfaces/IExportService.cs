using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines text and file export operations for diagram sessions.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Copies a BDD truth table to the clipboard.
    /// </summary>
    /// <param name="session">The source BDD session.</param>
    /// <param name="format">The text table format.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The copied text.</returns>
    Task<string> CopyTruthTableAsync(DiagramSession session, ExportTableFormat format, CancellationToken ct);

    /// <summary>
    /// Saves the session DOT text to a file.
    /// </summary>
    /// <param name="session">The source session.</param>
    /// <param name="path">The output path.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes after the file is written.</returns>
    Task SaveDotAsync(DiagramSession session, string path, CancellationToken ct);

    /// <summary>
    /// Renders and saves the session SVG to a file.
    /// </summary>
    /// <param name="session">The source session.</param>
    /// <param name="path">The output path.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes after the file is written.</returns>
    Task SaveSvgAsync(DiagramSession session, string path, CancellationToken ct);
}
