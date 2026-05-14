namespace DecisionDiagramStudio.Services;

/// <summary>
/// Represents a Graphviz render operation that exceeded its timeout.
/// </summary>
public sealed class GraphvizTimeoutException : TimeoutException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizTimeoutException"/> class.
    /// </summary>
    /// <param name="dotExecutablePath">The Graphviz executable path or command name.</param>
    /// <param name="renderTimeout">The configured render timeout.</param>
    public GraphvizTimeoutException(string dotExecutablePath, TimeSpan renderTimeout)
        : base("Graphviz rendering exceeded the configured timeout.")
    {
        DotExecutablePath = dotExecutablePath;
        RenderTimeout = renderTimeout;
    }

    /// <summary>
    /// Gets the Graphviz executable path or command name.
    /// </summary>
    public string DotExecutablePath { get; }

    /// <summary>
    /// Gets the configured render timeout.
    /// </summary>
    public TimeSpan RenderTimeout { get; }
}
