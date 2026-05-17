using DecisionDiagramSharp;

namespace DecisionDiagramStudio.Models;

/// <summary>
/// Stores diagram statistics in application-owned model form.
/// </summary>
public sealed record AppDiagramStatistics
{
    /// <summary>
    /// Gets an empty statistics instance.
    /// </summary>
    public static AppDiagramStatistics Empty { get; } = new();

    /// <summary>
    /// Gets the number of reachable non-terminal nodes.
    /// </summary>
    public int ReachableNodeCount { get; init; }

    /// <summary>
    /// Gets the number of reachable terminal nodes.
    /// </summary>
    public int ReachableTerminalCount { get; init; }

    /// <summary>
    /// Gets the total number of non-terminal nodes currently managed by the underlying manager.
    /// </summary>
    public int TotalNodeCount { get; init; }

    /// <summary>
    /// Gets the number of variables registered for the diagram.
    /// </summary>
    public int VariableCount { get; init; }

    /// <summary>
    /// Gets the number of non-terminal nodes in the unreduced binary decision tree.
    /// </summary>
    public int BdtNodeCount { get; init; }

    /// <summary>
    /// Gets the BDT-to-BDD non-terminal node reduction count.
    /// </summary>
    public int ReducedCount { get; init; }

    /// <summary>
    /// Gets the number of sets represented by a ZDD.
    /// </summary>
    public long SetCount { get; init; }

    /// <summary>
    /// Creates application statistics for a BDD from library statistics.
    /// </summary>
    /// <param name="statistics">The library statistics for the BDD root.</param>
    /// <returns>Application statistics with BDT and reduced counts calculated.</returns>
    public static AppDiagramStatistics ForBdd(DiagramStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        var bdtNodeCount = CalculateBdtNodeCount(statistics.VariableCount);

        return new AppDiagramStatistics
        {
            ReachableNodeCount = statistics.ReachableNodeCount,
            ReachableTerminalCount = statistics.ReachableTerminalCount,
            TotalNodeCount = statistics.TotalNodeCount,
            VariableCount = statistics.VariableCount,
            BdtNodeCount = bdtNodeCount,
            ReducedCount = bdtNodeCount - statistics.ReachableNodeCount,
            SetCount = 0L,
        };
    }

    /// <summary>
    /// Creates application statistics for a ZDD from library statistics and a set count.
    /// </summary>
    /// <param name="statistics">The library statistics for the ZDD root.</param>
    /// <param name="setCount">The number of sets represented by the ZDD.</param>
    /// <returns>Application statistics with the supplied set count stored.</returns>
    public static AppDiagramStatistics ForZdd(DiagramStatistics statistics, long setCount)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentOutOfRangeException.ThrowIfNegative(setCount);

        return new AppDiagramStatistics
        {
            ReachableNodeCount = statistics.ReachableNodeCount,
            ReachableTerminalCount = statistics.ReachableTerminalCount,
            TotalNodeCount = statistics.TotalNodeCount,
            VariableCount = statistics.VariableCount,
            BdtNodeCount = 0,
            ReducedCount = 0,
            SetCount = setCount,
        };
    }

    /// <summary>
    /// Creates application statistics for an MTBDD from library statistics.
    /// </summary>
    /// <param name="statistics">The library statistics for the MTBDD root.</param>
    /// <returns>Application statistics with common diagram counts copied.</returns>
    public static AppDiagramStatistics ForMtbdd(DiagramStatistics statistics)
    {
        return ForMultiTerminal(statistics);
    }

    /// <summary>
    /// Creates application statistics for a ZMTBDD from library statistics.
    /// </summary>
    /// <param name="statistics">The library statistics for the ZMTBDD root.</param>
    /// <returns>Application statistics with common diagram counts copied.</returns>
    public static AppDiagramStatistics ForZmtbdd(DiagramStatistics statistics)
    {
        return ForMultiTerminal(statistics);
    }

    private static AppDiagramStatistics ForMultiTerminal(DiagramStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        return new AppDiagramStatistics
        {
            ReachableNodeCount = statistics.ReachableNodeCount,
            ReachableTerminalCount = statistics.ReachableTerminalCount,
            TotalNodeCount = statistics.TotalNodeCount,
            VariableCount = statistics.VariableCount,
            BdtNodeCount = 0,
            ReducedCount = 0,
            SetCount = 0L,
        };
    }

    private static int CalculateBdtNodeCount(int variableCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variableCount);

        if (variableCount > 31)
        {
            throw new OverflowException("The BDT node count cannot be represented as an Int32.");
        }

        return checked((int)((1L << variableCount) - 1L));
    }
}
