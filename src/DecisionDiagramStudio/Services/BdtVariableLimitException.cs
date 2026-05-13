namespace DecisionDiagramStudio.Services;

/// <summary>
/// Indicates that an unreduced BDT request exceeds the application-supported variable limit.
/// </summary>
public sealed class BdtVariableLimitException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BdtVariableLimitException"/> class.
    /// </summary>
    /// <param name="variableCount">The requested variable count.</param>
    /// <param name="maxVariableCount">The maximum supported variable count.</param>
    public BdtVariableLimitException(int variableCount, int maxVariableCount)
        : base($"BDT DOT generation supports at most {maxVariableCount} variables. Requested: {variableCount}.")
    {
        VariableCount = variableCount;
        MaxVariableCount = maxVariableCount;
    }

    /// <summary>
    /// Gets the requested variable count.
    /// </summary>
    public int VariableCount { get; }

    /// <summary>
    /// Gets the maximum supported variable count.
    /// </summary>
    public int MaxVariableCount { get; }
}
