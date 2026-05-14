namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines the application boundary for rendering DOT text through Graphviz.
/// </summary>
public interface IGraphvizService
{
    /// <summary>
    /// Renders DOT text to SVG.
    /// </summary>
    /// <param name="dotText">The DOT text to render.</param>
    /// <param name="ct">A cancellation token for abandoning the render.</param>
    /// <returns>The rendered SVG text.</returns>
    Task<string> RenderSvgAsync(string dotText, CancellationToken ct);

    /// <summary>
    /// Gets a value indicating whether the configured Graphviz executable can be found.
    /// </summary>
    /// <returns><see langword="true" /> when Graphviz is available; otherwise <see langword="false" />.</returns>
    bool IsAvailable();
}
