namespace DecisionDiagramStudio.Services;

/// <summary>
/// Represents a missing Graphviz executable.
/// </summary>
public sealed class GraphvizNotFoundException : FileNotFoundException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizNotFoundException"/> class.
    /// </summary>
    /// <param name="dotExecutablePath">The requested Graphviz executable path or command name.</param>
    public GraphvizNotFoundException(string dotExecutablePath)
        : base("Graphviz dot executable was not found.", dotExecutablePath)
    {
        DotExecutablePath = dotExecutablePath;
    }

    /// <summary>
    /// Gets the requested Graphviz executable path or command name.
    /// </summary>
    public string DotExecutablePath { get; }
}
