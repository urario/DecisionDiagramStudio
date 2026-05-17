namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines the application boundary for copying text to the system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes after the text is copied.</returns>
    Task SetTextAsync(string text, CancellationToken ct);
}
