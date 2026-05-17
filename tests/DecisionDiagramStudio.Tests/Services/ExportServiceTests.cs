using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Services;

/// <summary>
/// Verifies session export behavior.
/// </summary>
[TestClass]
public sealed class ExportServiceTests
{
    /// <summary>
    /// Verifies that BDD truth tables can be copied in all supported text formats.
    /// </summary>
    [TestMethod]
    public async Task CopyTruthTableAsync_BddSession_ShouldCopySupportedFormats()
    {
        // Arrange
        var clipboard = new RecordingClipboardService();
        var service = new ExportService(new RecordingGraphvizService(), clipboard);
        var session = CreateBddSession();

        // Act
        var csv = await service.CopyTruthTableAsync(session, ExportTableFormat.Csv, CancellationToken.None);
        var markdown = await service.CopyTruthTableAsync(session, ExportTableFormat.Markdown, CancellationToken.None);
        var asciiDoc = await service.CopyTruthTableAsync(session, ExportTableFormat.AsciiDoc, CancellationToken.None);

        // Assert
        Assert.AreEqual(
            "a,b,f\n0,0,0\n1,0,1\n0,1,1\n1,1,0\n",
            Normalize(csv),
            "CSV export should use LSB-first truth-table row order.");
        StringAssert.Contains(markdown, "| a | b | f |", "Markdown export should include variable and result columns.");
        StringAssert.Contains(asciiDoc, "|a |b |f", "AsciiDoc export should include variable and result columns.");
        Assert.AreEqual(asciiDoc, clipboard.CopiedTexts[^1], "The latest export text should be copied to the clipboard.");
        Assert.AreEqual(3, clipboard.CopiedTexts.Count, "Each export call should write to the clipboard.");
    }

    /// <summary>
    /// Verifies that truth-table export rejects non-BDD sessions.
    /// </summary>
    [TestMethod]
    public async Task CopyTruthTableAsync_ZddSession_ShouldThrowNotSupportedException()
    {
        // Arrange
        var service = new ExportService(new RecordingGraphvizService(), new RecordingClipboardService());
        var session = new DiagramSession
        {
            Family = DiagramFamily.ZDD,
            VariableNames = ["a"],
            SetInput = [new[] { "a" }],
            DotText = "digraph ZDD { root; }",
        };

        // Act / Assert
        await Assert.ThrowsExceptionAsync<NotSupportedException>(
            () => service.CopyTruthTableAsync(session, ExportTableFormat.Csv, CancellationToken.None),
            "Truth-table export should be scoped to BDD sessions.");
    }

    /// <summary>
    /// Verifies that MTBDD and ZMTBDD integer value tables can be copied as CSV.
    /// </summary>
    [TestMethod]
    public async Task CopyTruthTableAsync_MultiTerminalSessions_ShouldCopyValueTables()
    {
        // Arrange
        var clipboard = new RecordingClipboardService();
        var service = new ExportService(new RecordingGraphvizService(), clipboard);

        foreach (var family in new[] { DiagramFamily.MTBDD, DiagramFamily.ZMTBDD })
        {
            var session = new DiagramSession
            {
                Family = family,
                VariableNames = ["a", "b"],
                VariableOrder = [0, 1],
                IntValueTable = [0, -1, 3, 5],
                DotText = "digraph " + family.ToString() + " { root; }",
            };

            // Act
            var csv = await service.CopyTruthTableAsync(session, ExportTableFormat.Csv, CancellationToken.None);

            // Assert
            Assert.AreEqual(
                "a,b,Value\n0,0,0\n1,0,-1\n0,1,3\n1,1,5\n",
                Normalize(csv),
                family.ToString() + " CSV export should preserve integer values in LSB-first row order.");
        }

        Assert.AreEqual(2, clipboard.CopiedTexts.Count, "Each MT family export should write to the clipboard.");
    }

    /// <summary>
    /// Verifies that DOT and SVG file export write the expected content.
    /// </summary>
    [TestMethod]
    public async Task SaveDotAndSvgAsync_ShouldWriteExpectedFiles()
    {
        // Arrange
        var graphviz = new RecordingGraphvizService();
        var service = new ExportService(graphviz, new RecordingClipboardService());
        var session = CreateBddSession();
        var dotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dot");
        var svgPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".svg");

        try
        {
            // Act
            await service.SaveDotAsync(session, dotPath, CancellationToken.None);
            await service.SaveSvgAsync(session, svgPath, CancellationToken.None);

            // Assert
            Assert.AreEqual(session.DotText, await File.ReadAllTextAsync(dotPath), "DOT export should write the session DOT text.");
            Assert.AreEqual("<svg>digraph BDD { root; }</svg>", await File.ReadAllTextAsync(svgPath), "SVG export should write rendered Graphviz output.");
            Assert.AreEqual(1, graphviz.RenderRequests.Count, "SVG export should render DOT once.");
            Assert.AreEqual(session.DotText, graphviz.RenderRequests[0], "SVG export should render the session DOT text.");
        }
        finally
        {
            if (File.Exists(dotPath))
            {
                File.Delete(dotPath);
            }

            if (File.Exists(svgPath))
            {
                File.Delete(svgPath);
            }
        }
    }

    private static DiagramSession CreateBddSession()
    {
        return new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = ["a", "b"],
            VariableOrder = [0, 1],
            IntValueTable = [0, 1, 1, 0],
            DotText = "digraph BDD { root; }",
            Statistics = new AppDiagramStatistics
            {
                ReachableNodeCount = 1,
                TotalNodeCount = 1,
                VariableCount = 2,
            },
        };
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public List<string> CopiedTexts { get; } = [];

        public Task SetTextAsync(string text, CancellationToken ct)
        {
            CopiedTexts.Add(text);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGraphvizService : IGraphvizService
    {
        public List<string> RenderRequests { get; } = [];

        public bool IsAvailable()
        {
            return true;
        }

        public Task<string> RenderSvgAsync(string dotText, CancellationToken ct)
        {
            RenderRequests.Add(dotText);
            return Task.FromResult("<svg>" + dotText + "</svg>");
        }
    }
}
