using DecisionDiagramSharp;
using DecisionDiagramStudio.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Models;

/// <summary>
/// Verifies the application statistics contract used by the studio model layer.
/// </summary>
[TestClass]
public sealed class AppDiagramStatisticsTests
{
    /// <summary>
    /// Verifies that BDD statistics calculate the unreduced BDT non-terminal node count for two variables.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-003 and ADR-009. For a two-variable BDD, the full binary decision tree has
    /// 2^2 - 1 non-terminal nodes, so the application reduced count is that value minus reachable nodes.
    /// </remarks>
    [TestMethod]
    public void ForBdd_VariableCount2_ShouldReturn_BdtNodeCount3()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            ReachableNodeCount = 2,
            ReachableTerminalCount = 2,
            TotalNodeCount = 5,
            VariableCount = 2,
        };

        // Act
        var actual = AppDiagramStatistics.ForBdd(statistics);

        // Assert
        Assert.AreEqual(2, actual.ReachableNodeCount, "Reachable node count should be copied from library statistics.");
        Assert.AreEqual(2, actual.ReachableTerminalCount, "Reachable terminal count should be copied from library statistics.");
        Assert.AreEqual(5, actual.TotalNodeCount, "Total node count should be copied from library statistics.");
        Assert.AreEqual(2, actual.VariableCount, "Variable count should be copied from library statistics.");
        Assert.AreEqual(3, actual.BdtNodeCount, "Two variables should produce 2^2 - 1 BDT non-terminal nodes.");
        Assert.AreEqual(1, actual.ReducedCount, "Reduced count should be BDT nodes minus reachable BDD nodes.");
        Assert.AreEqual(0L, actual.SetCount, "BDD statistics should not report a ZDD set count.");
    }

    /// <summary>
    /// Verifies that BDD statistics reject a null library statistics instance.
    /// </summary>
    /// <remarks>
    /// Guards the factory API contract. A null library statistics object indicates a service-layer bug
    /// and should fail at the boundary with the standard parameter exception.
    /// </remarks>
    [TestMethod]
    public void ForBdd_NullStatistics_ShouldThrowArgumentNullException()
    {
        // Arrange / Act
        var exception = Assert.ThrowsException<ArgumentNullException>(
            () => AppDiagramStatistics.ForBdd(null!));

        // Assert
        Assert.AreEqual("statistics", exception.ParamName, "Null BDD statistics should identify the statistics parameter.");
    }

    /// <summary>
    /// Verifies that BDD statistics reject a negative variable count.
    /// </summary>
    /// <remarks>
    /// Guards the BDT count calculation boundary. Library statistics should never contain negative
    /// variable counts, so the app model treats that as an invalid input contract.
    /// </remarks>
    [TestMethod]
    public void ForBdd_NegativeVariableCount_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            VariableCount = -1,
        };

        // Act
        var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AppDiagramStatistics.ForBdd(statistics));

        // Assert
        Assert.AreEqual("variableCount", exception.ParamName, "Negative variable counts should identify the variableCount parameter.");
    }

    /// <summary>
    /// Verifies that BDD statistics reject BDT counts that cannot fit in the model contract.
    /// </summary>
    /// <remarks>
    /// Guards the Int32-sized BdtNodeCount contract. Variable counts above 31 produce 2^n - 1 values
    /// that cannot be represented by the architecture-defined integer field.
    /// </remarks>
    [TestMethod]
    public void ForBdd_VariableCountAboveInt32Capacity_ShouldThrowOverflowException()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            VariableCount = 32,
        };

        // Act / Assert
        Assert.ThrowsException<OverflowException>(
            () => AppDiagramStatistics.ForBdd(statistics),
            "Variable counts above 31 should fail before truncating the BDT node count.");
    }

    /// <summary>
    /// Verifies that ZDD statistics preserve the set count supplied by the ZDD manager layer.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-004. The library exposes ZDD set-family cardinality separately from DiagramStatistics,
    /// so the application model must store the supplied value without deriving it from node counts.
    /// </remarks>
    [TestMethod]
    public void ForZdd_WithSetCount_ShouldStoreSetCount()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            ReachableNodeCount = 4,
            ReachableTerminalCount = 2,
            TotalNodeCount = 7,
            VariableCount = 3,
        };

        // Act
        var actual = AppDiagramStatistics.ForZdd(statistics, setCount: 5L);

        // Assert
        Assert.AreEqual(4, actual.ReachableNodeCount, "Reachable node count should be copied from library statistics.");
        Assert.AreEqual(2, actual.ReachableTerminalCount, "Reachable terminal count should be copied from library statistics.");
        Assert.AreEqual(7, actual.TotalNodeCount, "Total node count should be copied from library statistics.");
        Assert.AreEqual(3, actual.VariableCount, "Variable count should be copied from library statistics.");
        Assert.AreEqual(0, actual.BdtNodeCount, "ZDD statistics should not calculate BDD-only BDT nodes.");
        Assert.AreEqual(0, actual.ReducedCount, "ZDD statistics should not calculate BDD-only reduced count.");
        Assert.AreEqual(5L, actual.SetCount, "SetCount should match the CountSets result supplied by the service layer.");
    }

    /// <summary>
    /// Verifies that ZDD statistics reject a null library statistics instance.
    /// </summary>
    /// <remarks>
    /// Guards the factory API contract. ZDD statistics still require the common DiagramStatistics
    /// fields from the library in addition to the separately supplied set count.
    /// </remarks>
    [TestMethod]
    public void ForZdd_NullStatistics_ShouldThrowArgumentNullException()
    {
        // Arrange / Act
        var exception = Assert.ThrowsException<ArgumentNullException>(
            () => AppDiagramStatistics.ForZdd(null!, setCount: 0L));

        // Assert
        Assert.AreEqual("statistics", exception.ParamName, "Null ZDD statistics should identify the statistics parameter.");
    }

    /// <summary>
    /// Verifies that ZDD statistics reject a negative set count.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-004. A set-family count is a cardinality and therefore cannot be negative.
    /// </remarks>
    [TestMethod]
    public void ForZdd_NegativeSetCount_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var statistics = new DiagramStatistics();

        // Act
        var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AppDiagramStatistics.ForZdd(statistics, setCount: -1L));

        // Assert
        Assert.AreEqual("setCount", exception.ParamName, "Negative ZDD set counts should identify the setCount parameter.");
    }

    /// <summary>
    /// Verifies that MTBDD statistics copy common counts without BDD/ZDD-only derived fields.
    /// </summary>
    [TestMethod]
    public void ForMtbdd_WithDistinctTerminals_ShouldCopyCommonCounts()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            ReachableNodeCount = 3,
            ReachableTerminalCount = 4,
            TotalNodeCount = 3,
            VariableCount = 2,
        };

        // Act
        var actual = AppDiagramStatistics.ForMtbdd(statistics);

        // Assert
        Assert.AreEqual(3, actual.ReachableNodeCount, "MTBDD reachable nodes should be copied.");
        Assert.AreEqual(4, actual.ReachableTerminalCount, "MTBDD terminal count should represent distinct integer results.");
        Assert.AreEqual(3, actual.TotalNodeCount, "MTBDD total nodes should be copied.");
        Assert.AreEqual(2, actual.VariableCount, "MTBDD variable count should be copied.");
        Assert.AreEqual(0, actual.BdtNodeCount, "MTBDD statistics should not calculate BDD-only BDT nodes.");
        Assert.AreEqual(0, actual.ReducedCount, "MTBDD statistics should not calculate BDD-only reduced count.");
        Assert.AreEqual(0L, actual.SetCount, "MTBDD statistics should not report ZDD set count.");
    }

    /// <summary>
    /// Verifies that ZMTBDD statistics reject null input and copy sparse terminal counts.
    /// </summary>
    [TestMethod]
    public void ForZmtbdd_WithSparseTerminals_ShouldCopyCommonCounts()
    {
        // Arrange
        var statistics = new DiagramStatistics
        {
            ReachableNodeCount = 1,
            ReachableTerminalCount = 2,
            TotalNodeCount = 1,
            VariableCount = 2,
        };

        // Act
        var actual = AppDiagramStatistics.ForZmtbdd(statistics);

        // Assert
        Assert.AreEqual(1, actual.ReachableNodeCount, "ZMTBDD reachable nodes should be copied.");
        Assert.AreEqual(2, actual.ReachableTerminalCount, "ZMTBDD terminal count should include sparse integer outputs.");
        Assert.AreEqual(1, actual.TotalNodeCount, "ZMTBDD total nodes should be copied.");
        Assert.AreEqual(2, actual.VariableCount, "ZMTBDD variable count should be copied.");
        Assert.ThrowsException<ArgumentNullException>(
            () => AppDiagramStatistics.ForZmtbdd(null!),
            "Null ZMTBDD statistics should fail at the model boundary.");
    }
}
