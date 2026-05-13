using DecisionDiagramStudio.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Models;

/// <summary>
/// Verifies the domain model contracts defined by architecture section B3.1.
/// </summary>
[TestClass]
public sealed class DomainContractTests
{
    /// <summary>
    /// Verifies that DiagramFamily exposes the four supported decision diagram families.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-001. Downstream service and UI feature gates rely on these enum members existing
    /// even when only BDD is active in the v0.1 implementation phase.
    /// </remarks>
    [TestMethod]
    public void DiagramFamily_ShouldContainAllArchitectureFamilies()
    {
        // Arrange
        var expected = new[]
        {
            DiagramFamily.BDD,
            DiagramFamily.ZDD,
            DiagramFamily.MTBDD,
            DiagramFamily.ZMTBDD,
        };

        // Act
        var actual = Enum.GetValues<DiagramFamily>();

        // Assert
        CollectionAssert.AreEqual(expected, actual, "DiagramFamily should expose BDD, ZDD, MTBDD, and ZMTBDD in contract order.");
    }

    /// <summary>
    /// Verifies that DiagramSession derives emptiness from the DOT text.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-005. The architecture defines IsEmpty as string.IsNullOrEmpty(DotText), so the
    /// session should not need a separate mutable empty-state flag.
    /// </remarks>
    [TestMethod]
    public void DiagramSession_IsEmpty_ShouldReflectDotText()
    {
        // Arrange
        var emptySession = new DiagramSession
        {
            DotText = string.Empty,
        };
        var populatedSession = emptySession with
        {
            DotText = "digraph G { a -> b; }",
        };

        // Act
        var emptyActual = emptySession.IsEmpty;
        var populatedActual = populatedSession.IsEmpty;

        // Assert
        Assert.IsTrue(emptyActual, "A session with empty DOT text should be treated as empty.");
        Assert.IsFalse(populatedActual, "A session with DOT text should not be treated as empty.");
    }

    /// <summary>
    /// Verifies that DiagramSession carries the full application-owned session contract.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-005. The session model stores only derived data and input snapshots, keeping
    /// DecisionDiagramSharp handles inside the service layer.
    /// </remarks>
    [TestMethod]
    public void DiagramSession_ShouldStoreSessionContractValues()
    {
        // Arrange
        var variableNames = new[] { "a", "b" };
        var variableOrder = new[] { 1, 0 };
        var intValueTable = new[] { 0, 1, 1, 0 };
        IReadOnlyList<IReadOnlyList<string>> setInput = new[]
        {
            new[] { "a" },
            new[] { "a", "b" },
        };
        var statistics = new AppDiagramStatistics
        {
            ReachableNodeCount = 2,
        };
        var lastModified = new DateTime(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var session = new DiagramSession
        {
            Family = DiagramFamily.ZDD,
            VariableNames = variableNames,
            VariableOrder = variableOrder,
            IntValueTable = intValueTable,
            SetInput = setInput,
            DotText = "digraph G { root; }",
            Statistics = statistics,
            LastModified = lastModified,
        };

        // Assert
        Assert.AreEqual(DiagramFamily.ZDD, session.Family, "Session family should be stored.");
        CollectionAssert.AreEqual(variableNames, session.VariableNames, "Session variable names should be stored.");
        CollectionAssert.AreEqual(variableOrder, session.VariableOrder, "Session variable order should be stored.");
        CollectionAssert.AreEqual(intValueTable, session.IntValueTable, "Session integer value table should be stored.");
        Assert.AreSame(setInput, session.SetInput, "Session set input should be stored.");
        Assert.AreEqual("digraph G { root; }", session.DotText, "Session DOT text should be stored.");
        Assert.AreSame(statistics, session.Statistics, "Session statistics should be stored.");
        Assert.AreEqual(lastModified, session.LastModified, "Session last modified timestamp should be stored.");
    }

    /// <summary>
    /// Verifies that DiagramPreset carries the preset data needed to seed a BDD session.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-006. Preset loading later depends on the model storing identifiers, display text,
    /// truth table values, variable names, and the default family without referencing UI types.
    /// </remarks>
    [TestMethod]
    public void DiagramPreset_ShouldStorePresetContractValues()
    {
        // Arrange
        var variableNames = new[] { "a", "b" };
        var truthTableValues = new[] { 0, 1, 1, 0 };

        // Act
        var preset = new DiagramPreset
        {
            Id = "xor",
            Label = "XOR",
            Description = "Exclusive or",
            VariableNames = variableNames,
            TruthTableValues = truthTableValues,
            DefaultFamily = DiagramFamily.BDD,
        };

        // Assert
        Assert.AreEqual("xor", preset.Id, "Preset id should be stored.");
        Assert.AreEqual("XOR", preset.Label, "Preset label should be stored.");
        Assert.AreEqual("Exclusive or", preset.Description, "Preset description should be stored.");
        CollectionAssert.AreEqual(variableNames, preset.VariableNames, "Preset variable names should be stored.");
        CollectionAssert.AreEqual(truthTableValues, preset.TruthTableValues, "Preset truth table values should be stored.");
        Assert.AreEqual(DiagramFamily.BDD, preset.DefaultFamily, "Preset default family should be stored.");
    }

    /// <summary>
    /// Verifies that SessionOptions carries the configurable runtime limits and theme contract.
    /// </summary>
    /// <remarks>
    /// Guards MODEL-007. Settings persistence can serialize this pure model later without depending on
    /// WinUI runtime objects.
    /// </remarks>
    [TestMethod]
    public void SessionOptions_ShouldStoreConfigurationContractValues()
    {
        // Arrange / Act
        var options = new SessionOptions
        {
            GraphvizPath = "C:\\Graphviz\\bin\\dot.exe",
            Theme = AppTheme.Dark,
            MaxNodeCount = 10_000,
            MaxEnumerationCount = 2_048,
            UndoHistoryLimit = 50,
        };

        // Assert
        Assert.AreEqual("C:\\Graphviz\\bin\\dot.exe", options.GraphvizPath, "Graphviz path should be stored.");
        Assert.AreEqual(AppTheme.Dark, options.Theme, "Theme should be stored as the app theme enum.");
        Assert.AreEqual(10_000, options.MaxNodeCount, "Max node count should be stored.");
        Assert.AreEqual(2_048, options.MaxEnumerationCount, "Max enumeration count should be stored.");
        Assert.AreEqual(50, options.UndoHistoryLimit, "Undo history limit should be stored.");
    }
}
