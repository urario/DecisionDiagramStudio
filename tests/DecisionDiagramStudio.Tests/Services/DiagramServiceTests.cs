using DecisionDiagramSharp;
using DecisionDiagramSharp.Diagnostics;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Services;

/// <summary>
/// Verifies the BDD service contract and core service behavior.
/// </summary>
[TestClass]
public sealed class DiagramServiceTests
{
    /// <summary>
    /// Verifies that the concrete service satisfies the public service contract.
    /// </summary>
    [TestMethod]
    public void DiagramService_ShouldImplement_IDiagramService()
    {
        // Arrange / Act
        IDiagramService service = new DiagramService();

        // Assert
        Assert.IsNotNull(service, "DiagramService should be constructible through the IDiagramService contract.");
    }

    /// <summary>
    /// Verifies that valid ASCII variable identifiers are accepted by BuildAsync.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ValidVariableNames_ShouldSucceed()
    {
        // Arrange
        var service = new DiagramService();
        var variableNames = new[] { "a", "B_2", "_c" };
        var values = new[] { 0, 1, 1, 0, 1, 0, 0, 1 };

        // Act
        var session = await service.BuildAsync(variableNames, values, DiagramFamily.BDD, CancellationToken.None);

        // Assert
        Assert.AreEqual(DiagramFamily.BDD, session.Family, "Valid variables should produce a BDD session.");
        StringAssert.StartsWith(session.DotText, "digraph BDD");
    }

    /// <summary>
    /// Verifies the feature-task invalid variable-name examples.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_InvalidVariableName_ShouldThrow_ArgumentException()
    {
        // Arrange
        var invalidNames = new[] { "1a", "a b", "<script>" };

        foreach (var invalidName in invalidNames)
        {
            var service = new DiagramService();

            // Act / Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.BuildAsync(
                    new[] { "a", invalidName },
                    new[] { 0, 0, 0, 1 },
                    DiagramFamily.BDD,
                    CancellationToken.None),
                invalidName + " should be rejected at the service boundary.");
        }
    }

    /// <summary>
    /// Verifies variable-name uniqueness and BDD truth-table shape validation.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_InvalidInputShape_ShouldThrow()
    {
        // Arrange
        var service = new DiagramService();

        // Act / Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => service.BuildAsync(
                new[] { "a", "a" },
                new[] { 0, 0, 0, 1 },
                DiagramFamily.BDD,
                CancellationToken.None),
            "Duplicate variable names should be rejected.");

        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => service.BuildAsync(
                new[] { "a", "b" },
                new[] { 0, 1 },
                DiagramFamily.BDD,
                CancellationToken.None),
            "A BDD truth table must contain 2^n rows.");

        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => service.BuildAsync(
                new[] { "a" },
                new[] { 2, 0 },
                DiagramFamily.BDD,
                CancellationToken.None),
            "A BDD truth table must contain only 0/1 values.");
    }

    /// <summary>
    /// Verifies that the integer-table overload rejects ZDD BuildAsync requests.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_UnsupportedFamily_ShouldThrow_NotSupportedException()
    {
        // Arrange
        var service = new DiagramService();

        // Act / Assert
        await Assert.ThrowsExceptionAsync<NotSupportedException>(
            () => service.BuildAsync(
                new[] { "a" },
                new[] { 0, 1 },
                DiagramFamily.ZDD,
                CancellationToken.None),
            "ZDD builds should use the set-family overload.");
    }

    /// <summary>
    /// Verifies the security rejection set for variable names.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_SecurityVariableNamePatterns_ShouldThrow_ArgumentException()
    {
        // Arrange
        var blockedNames = new[] { "<script>", "\"; DROP TABLE", "../", "line\nbreak", "a\0b" };

        foreach (var blockedName in blockedNames)
        {
            var service = new DiagramService();

            // Act / Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.BuildAsync(
                    new[] { "safe", blockedName },
                    new[] { 0, 1, 1, 0 },
                    DiagramFamily.BDD,
                    CancellationToken.None),
                "Security-sensitive variable input should be blocked.");
        }
    }

    /// <summary>
    /// Verifies that the BDD construction helper round-trips a known two-variable AND truth table.
    /// </summary>
    [TestMethod]
    public void BuildBddFromTruthTable_AndTable_ShouldRoundTrip()
    {
        // Arrange
        var service = new DiagramService();
        var variableNames = new[] { "a", "b" };
        var values = new[] { 0, 0, 0, 1 };

        // Act
        var bdd = service.BuildBddFromTruthTable(values, variableNames);
        var actual = ExtractResultValues(BddDiagnostics.BuildTruthTable(bdd.Manager, bdd));

        // Assert
        CollectionAssert.AreEqual(values, actual, "A AND B should round-trip through DecisionDiagramSharp diagnostics.");
    }

    /// <summary>
    /// Verifies all one- through four-variable truth table patterns against the real library diagnostics.
    /// </summary>
    [TestMethod]
    public void BuildBddFromTruthTable_AllPatternsUpToFourVariables_ShouldRoundTrip()
    {
        // Arrange
        var names = new[] { "a", "b", "c", "d" };
        var service = new DiagramService();

        for (var variableCount = 1; variableCount <= 4; variableCount++)
        {
            var variableNames = names.Take(variableCount).ToArray();
            var rowCount = 1 << variableCount;
            var patternCount = 1 << rowCount;

            for (var pattern = 0; pattern < patternCount; pattern++)
            {
                var values = BuildPatternValues(pattern, rowCount);

                // Act
                var bdd = service.BuildBddFromTruthTable(values, variableNames);
                var actual = ExtractResultValues(
                    BddDiagnostics.BuildTruthTable(
                        bdd.Manager,
                        bdd,
                        new TruthTableOptions { MaxVariables = variableCount, MaxRows = rowCount }));

                // Assert
                CollectionAssert.AreEqual(
                    values,
                    actual,
                    "Truth table pattern " + pattern + " for " + variableCount + " variable(s) should round-trip.");
            }
        }
    }

    /// <summary>
    /// Verifies that MTBDD and ZMTBDD library value-table diagnostics round-trip integer inputs.
    /// </summary>
    [TestMethod]
    public void MtbddAndZmtbddDiagnostics_ValueTable_ShouldRoundTripIntegerInputs()
    {
        // Arrange
        var variableNames = new[] { "a", "b" };
        var values = new[] { 0, -1, 3, 5 };

        var mtbddManager = new MtbddManager();
        var zmtbddManager = new ZmtbddManager();
        foreach (var variableName in variableNames)
        {
            _ = mtbddManager.GetOrAddVariable(variableName);
            _ = zmtbddManager.GetOrAddVariable(variableName);
        }

        // Act
        var mtbdd = mtbddManager.Create(values);
        var zmtbdd = zmtbddManager.Create(values);
        var mtbddActual = ExtractIntegerResultValues(MtbddDiagnostics.BuildValueTable(mtbddManager, mtbdd));
        var zmtbddActual = ExtractIntegerResultValues(ZmtbddDiagnostics.BuildValueTable(zmtbddManager, zmtbdd));

        // Assert
        CollectionAssert.AreEqual(values, mtbddActual, "MTBDD value-table diagnostics should round-trip every integer row.");
        CollectionAssert.AreEqual(values, zmtbddActual, "ZMTBDD value-table diagnostics should round-trip every integer row.");
    }

    /// <summary>
    /// Verifies that a same-sized variable schema change resets the internal BDD manager.
    /// </summary>
    [TestMethod]
    public void BuildBddFromTruthTable_SameSizeDifferentNames_ShouldResetVariableSchema()
    {
        // Arrange
        var service = new DiagramService();
        _ = service.BuildBddFromTruthTable(new[] { 0, 0, 0, 1 }, new[] { "a", "b" });
        var values = new[] { 0, 1, 1, 0 };

        // Act
        var bdd = service.BuildBddFromTruthTable(values, new[] { "x", "y" });
        var table = BddDiagnostics.BuildTruthTable(bdd.Manager, bdd);
        var actual = ExtractResultValues(table);

        // Assert
        CollectionAssert.AreEqual(new[] { "x", "y", "Result" }, table.Columns.ToArray(), "The manager should use the new variable names.");
        CollectionAssert.AreEqual(values, actual, "The rebuilt BDD should still round-trip after a schema reset.");
    }

    /// <summary>
    /// Verifies that BuildAsync returns a complete BDD session and DOT text.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_BddPath_ShouldReturnSessionWithDotAndStatistics()
    {
        // Arrange
        var service = new DiagramService();
        var variableNames = new[] { "a", "b", "c" };
        var values = new[] { 0, 1, 1, 0, 1, 0, 0, 1 };

        // Act
        var session = await service.BuildAsync(variableNames, values, DiagramFamily.BDD, CancellationToken.None);

        // Assert
        Assert.AreEqual(DiagramFamily.BDD, session.Family, "BuildAsync should return a BDD session.");
        CollectionAssert.AreEqual(variableNames, session.VariableNames, "The session should keep a variable-name snapshot.");
        CollectionAssert.AreEqual(values, session.IntValueTable, "The session should keep the input table snapshot.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, session.VariableOrder, "BDD variables should use LSB-first order.");
        Assert.AreEqual(3, session.Statistics.VariableCount, "Statistics should reflect the BDD variable count.");
        StringAssert.StartsWith(session.DotText, "digraph BDD");
    }

    /// <summary>
    /// Verifies that the MTBDD BuildAsync path materializes integer-valued sessions.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_MtbddPath_ShouldReturnSessionWithDotAndValueStatistics()
    {
        // Arrange
        var service = new DiagramService();
        var variableNames = new[] { "a", "b" };
        var values = new[] { 0, 1, 2, 3 };

        // Act
        var session = await service.BuildAsync(variableNames, values, DiagramFamily.MTBDD, CancellationToken.None);

        // Assert
        Assert.AreEqual(DiagramFamily.MTBDD, session.Family, "BuildAsync should return an MTBDD session.");
        CollectionAssert.AreEqual(variableNames, session.VariableNames, "The MTBDD session should keep a variable-name snapshot.");
        CollectionAssert.AreEqual(values, session.IntValueTable, "The MTBDD session should keep the integer value table.");
        Assert.AreEqual(2, session.Statistics.VariableCount, "Statistics should reflect the MTBDD variable count.");
        Assert.AreEqual(4, session.Statistics.ReachableTerminalCount, "Each distinct integer output should become a reachable terminal.");
        StringAssert.StartsWith(session.DotText, "digraph MTBDD");
    }

    /// <summary>
    /// Verifies that the ZMTBDD BuildAsync path uses zero-suppressed semantics for sparse value tables.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ZmtbddPath_ShouldReturnNoMoreReachableNodesThanMtbddForSparseTable()
    {
        // Arrange
        var variableNames = new[] { "a", "b" };
        var values = new[] { 0, 0, 0, 7 };
        var mtbddService = new DiagramService();
        var zmtbddService = new DiagramService();

        // Act
        var mtbdd = await mtbddService.BuildAsync(variableNames, values, DiagramFamily.MTBDD, CancellationToken.None);
        var zmtbdd = await zmtbddService.BuildAsync(variableNames, values, DiagramFamily.ZMTBDD, CancellationToken.None);

        // Assert
        Assert.AreEqual(DiagramFamily.ZMTBDD, zmtbdd.Family, "BuildAsync should return a ZMTBDD session.");
        CollectionAssert.AreEqual(values, zmtbdd.IntValueTable, "The ZMTBDD session should keep the sparse integer value table.");
        Assert.IsTrue(
            zmtbdd.Statistics.ReachableNodeCount <= mtbdd.Statistics.ReachableNodeCount,
            "A sparse ZMTBDD should use no more reachable nodes than the dense MTBDD for the same table.");
        StringAssert.StartsWith(zmtbdd.DotText, "digraph ZMTBDD");
    }

    /// <summary>
    /// Verifies that the ZDD BuildAsync overload materializes a set-family session.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ZddSetFamily_ShouldReturnSessionWithDotAndStatistics()
    {
        // Arrange
        var service = new DiagramService();
        var variableNames = new[] { "a", "b", "c" };
        IReadOnlyList<IReadOnlyList<string>> sets = [new[] { "a", "b" }, new[] { "c" }];

        // Act
        var session = await service.BuildAsync(variableNames, sets, DiagramFamily.ZDD, CancellationToken.None);

        // Assert
        Assert.AreEqual(DiagramFamily.ZDD, session.Family, "The ZDD overload should return a ZDD session.");
        CollectionAssert.AreEqual(variableNames, session.VariableNames, "The session should keep the variable-name snapshot.");
        Assert.IsNull(session.IntValueTable, "ZDD sessions should not carry a BDD truth table.");
        Assert.IsNotNull(session.SetInput, "ZDD sessions should carry a set-family snapshot.");
        Assert.AreEqual(2L, session.Statistics.SetCount, "The set count should come from the ZDD manager.");
        Assert.AreEqual(3, session.Statistics.VariableCount, "Statistics should reflect the ZDD variable count.");
        StringAssert.StartsWith(session.DotText, "digraph ZDD");
        CollectionAssert.AreEquivalent(
            new[] { "a,b", "c" },
            NormalizeSetInput(session.SetInput!),
            "The session should expose the represented set family.");
    }

    /// <summary>
    /// Verifies that invalid ZDD set-family input is rejected at the service boundary.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ZddInvalidSetInput_ShouldThrow()
    {
        // Arrange
        var service = new DiagramService();

        // Act / Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => service.BuildAsync(
                new[] { "a" },
                [new[] { "b" }],
                DiagramFamily.ZDD,
                CancellationToken.None),
            "ZDD set members must be declared variables.");

        await Assert.ThrowsExceptionAsync<NotSupportedException>(
            () => service.BuildAsync(
                new[] { "a" },
                [new[] { "a" }],
                DiagramFamily.BDD,
                CancellationToken.None),
            "The set-family overload is only for ZDD.");
    }

    /// <summary>
    /// Verifies Union, Intersection, and Difference over known set families.
    /// </summary>
    [TestMethod]
    public async Task ApplyZddOperationAsync_KnownFamilies_ShouldReturnExpectedSetFamilies()
    {
        await AssertZddOperationAsync(
            ZddOperation.Union,
            4L,
            ["a,b", "b", "c", "c,d"]).ConfigureAwait(false);

        await AssertZddOperationAsync(
            ZddOperation.Intersection,
            0L,
            []).ConfigureAwait(false);

        await AssertZddOperationAsync(
            ZddOperation.Difference,
            2L,
            ["a,b", "c"]).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that ZDD operations require two built operands.
    /// </summary>
    [TestMethod]
    public async Task ApplyZddOperationAsync_WithoutOperands_ShouldThrow()
    {
        // Arrange
        var service = new DiagramService();

        // Act / Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.ApplyZddOperationAsync(ZddOperation.Union, CancellationToken.None),
            "The service should require two ZDD operands before an operation.");
    }

    /// <summary>
    /// Verifies that BuildAsync serializes access to the underlying managers.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ConcurrentCalls_ShouldUseSemaphoreCriticalSection()
    {
        // Arrange
        var service = new DiagramService();
        var activeCriticalSections = 0;
        var maxActiveCriticalSections = 0;
        var enteredCriticalSections = 0;

        service.CriticalSectionProbeAsync = async ct =>
        {
            var active = Interlocked.Increment(ref activeCriticalSections);
            UpdateMax(ref maxActiveCriticalSections, active);
            Interlocked.Increment(ref enteredCriticalSections);

            try
            {
                await Task.Delay(100, ct);
            }
            finally
            {
                Interlocked.Decrement(ref activeCriticalSections);
            }
        };

        var variableNames = new[] { "a", "b" };
        var firstValues = new[] { 0, 0, 0, 1 };
        var secondValues = new[] { 0, 1, 1, 0 };

        // Act
        var first = service.BuildAsync(variableNames, firstValues, DiagramFamily.BDD, CancellationToken.None);
        var second = service.BuildAsync(variableNames, secondValues, DiagramFamily.BDD, CancellationToken.None);
        await Task.WhenAll(first, second);

        // Assert
        Assert.AreEqual(2, enteredCriticalSections, "Both builds should enter the critical section.");
        Assert.AreEqual(1, maxActiveCriticalSections, "The semaphore should allow only one active build at a time.");
    }

    /// <summary>
    /// Verifies two-variable BDT DOT generation and the variable-count limit.
    /// </summary>
    [TestMethod]
    public async Task GetBdtDotAsync_VariableCount2_ShouldCreateSevenNodes_AndRejectElevenVariables()
    {
        // Arrange
        var service = new DiagramService();
        var session = new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = new[] { "a", "b" },
            IntValueTable = new[] { 0, 1, 1, 0 },
        };

        // Act
        var dot = await service.GetBdtDotAsync(session, CancellationToken.None);

        // Assert
        StringAssert.StartsWith(dot, "digraph BDT");
        Assert.AreEqual(7, CountBdtNodeDefinitions(dot), "Two variables should create 2^(2+1)-1 BDT nodes.");

        var oversizedSession = new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = Enumerable.Range(0, 11).Select(i => "v" + i.ToString()).ToArray(),
            IntValueTable = new int[1 << 11],
        };
        var exception = await Assert.ThrowsExceptionAsync<BdtVariableLimitException>(
            () => service.GetBdtDotAsync(oversizedSession, CancellationToken.None),
            "Eleven variables should exceed the BDT display limit.");
        Assert.AreEqual(11, exception.VariableCount, "The exception should expose the requested variable count.");
        Assert.AreEqual(DiagramService.MaxBdtVariableCount, exception.MaxVariableCount, "The exception should expose the configured limit.");
    }

    /// <summary>
    /// Verifies that BDT DOT generation is restricted to BDD sessions.
    /// </summary>
    [TestMethod]
    public async Task GetBdtDotAsync_NonBddSession_ShouldThrow_NotSupportedException()
    {
        // Arrange
        var service = new DiagramService();
        var session = new DiagramSession
        {
            Family = DiagramFamily.ZDD,
            VariableNames = new[] { "a" },
            IntValueTable = new[] { 0, 1 },
        };

        // Act / Assert
        await Assert.ThrowsExceptionAsync<NotSupportedException>(
            () => service.GetBdtDotAsync(session, CancellationToken.None),
            "BDT DOT is defined only for BDD sessions.");
    }

    /// <summary>
    /// Verifies BDT DOT node counts for all supported BDD variable counts.
    /// </summary>
    [TestMethod]
    public async Task GetBdtDotAsync_VariableCountsOneThroughTen_ShouldCreateFullTreeNodeCounts()
    {
        // Arrange
        var service = new DiagramService();

        for (var variableCount = 1; variableCount <= DiagramService.MaxBdtVariableCount; variableCount++)
        {
            var session = new DiagramSession
            {
                Family = DiagramFamily.BDD,
                VariableNames = Enumerable.Range(0, variableCount).Select(i => "v" + i.ToString()).ToArray(),
                IntValueTable = new int[1 << variableCount],
            };

            // Act
            var dot = await service.GetBdtDotAsync(session, CancellationToken.None);

            // Assert
            var expectedNodeCount = (1 << (variableCount + 1)) - 1;
            Assert.AreEqual(
                expectedNodeCount,
                CountBdtNodeDefinitions(dot),
                variableCount + " variable(s) should create a complete BDT node set.");
        }
    }

    private static int[] BuildPatternValues(int pattern, int rowCount)
    {
        var values = new int[rowCount];
        for (var mask = 0; mask < rowCount; mask++)
        {
            values[mask] = (pattern & (1 << mask)) == 0 ? 0 : 1;
        }

        return values;
    }

    private static int[] ExtractResultValues(TableModel table)
    {
        var resultColumn = table.Columns.Count - 1;
        var values = new int[table.Rows.Count];
        for (var i = 0; i < table.Rows.Count; i++)
        {
            values[i] = table.Rows[i].Cells[resultColumn] == "True" ? 1 : 0;
        }

        return values;
    }

    private static int[] ExtractIntegerResultValues(TableModel table)
    {
        var resultColumn = table.Columns.Count - 1;
        var values = new int[table.Rows.Count];
        for (var i = 0; i < table.Rows.Count; i++)
        {
            values[i] = int.Parse(table.Rows[i].Cells[resultColumn], System.Globalization.CultureInfo.InvariantCulture);
        }

        return values;
    }

    private static int CountBdtNodeDefinitions(string dot)
    {
        return dot
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Count(line =>
            {
                var trimmed = line.TrimStart();
                return trimmed.StartsWith("bdt", StringComparison.Ordinal)
                    && trimmed.Contains(" [label=", StringComparison.Ordinal)
                    && !trimmed.Contains(" -> ", StringComparison.Ordinal);
            });
    }

    private static async Task AssertZddOperationAsync(
        ZddOperation operation,
        long expectedSetCount,
        string[] expectedSets)
    {
        var service = new DiagramService();
        var variableNames = new[] { "a", "b", "c", "d" };

        _ = await service.BuildAsync(
            variableNames,
            [new[] { "a", "b" }, new[] { "c" }],
            DiagramFamily.ZDD,
            CancellationToken.None).ConfigureAwait(false);
        _ = await service.BuildAsync(
            variableNames,
            [new[] { "b" }, new[] { "c", "d" }],
            DiagramFamily.ZDD,
            CancellationToken.None).ConfigureAwait(false);

        var session = await service.ApplyZddOperationAsync(operation, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(DiagramFamily.ZDD, session.Family, "Operations should return a ZDD session.");
        Assert.AreEqual(expectedSetCount, session.Statistics.SetCount, "The operation set count should match the expected family.");
        CollectionAssert.AreEquivalent(
            expectedSets,
            NormalizeSetInput(session.SetInput!),
            operation.ToString() + " should return the expected set family.");
    }

    private static string[] NormalizeSetInput(IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        return setInput
            .Select(set => string.Join(",", set.Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void UpdateMax(ref int target, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }
}
