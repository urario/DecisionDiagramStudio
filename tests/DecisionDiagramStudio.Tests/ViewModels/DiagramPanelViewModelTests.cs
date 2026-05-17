using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using DecisionDiagramStudio.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.ViewModels;

/// <summary>
/// Verifies the diagram panel view model.
/// </summary>
[TestClass]
public sealed class DiagramPanelViewModelTests
{
    /// <summary>
    /// Verifies that a BDD session renders in reduced mode with the BDT button visible.
    /// </summary>
    [TestMethod]
    public async Task UpdateSessionAsync_BddSession_ShouldRenderReducedDotAndShowBdtButton()
    {
        // Arrange
        var graphvizService = new RecordingGraphvizService();
        var viewModel = new DiagramPanelViewModel(new RecordingDiagramService(), graphvizService);
        var session = CreateSession(DiagramFamily.BDD, "digraph BDD { root; }");

        // Act
        await viewModel.UpdateSessionAsync(session).ConfigureAwait(false);

        // Assert
        Assert.IsTrue(viewModel.IsReduced, "New sessions should start in reduced BDD display mode.");
        Assert.IsTrue(viewModel.IsBdtButtonVisible, "BDD sessions should expose the BDT toggle.");
        Assert.IsNotNull(viewModel.ToggleReductionCommand, "The toggle command should be available for binding.");
        Assert.AreEqual(session.DotText, viewModel.DotText, "The reduced DOT should be displayed first.");
        Assert.AreEqual("<svg>digraph BDD { root; }</svg>", viewModel.SvgContent, "The rendered SVG should be stored.");
        Assert.AreEqual(1, graphvizService.RenderRequests.Count, "Updating the session should render once.");
    }

    /// <summary>
    /// Verifies that toggling switches between BDD and BDT DOT.
    /// </summary>
    [TestMethod]
    public async Task ToggleReductionAsync_BddSession_ShouldSwitchBetweenBddAndBdt()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var graphvizService = new RecordingGraphvizService();
        var viewModel = new DiagramPanelViewModel(diagramService, graphvizService);
        var session = CreateSession(DiagramFamily.BDD, "digraph BDD { root; }");
        await viewModel.UpdateSessionAsync(session).ConfigureAwait(false);

        // Act
        await viewModel.ToggleReductionAsync().ConfigureAwait(false);

        // Assert
        Assert.IsFalse(viewModel.IsReduced, "The first toggle should switch to unreduced BDT mode.");
        Assert.AreEqual("digraph BDT { root; }", viewModel.DotText, "The BDT DOT should be displayed after toggling.");
        Assert.AreEqual(1, diagramService.BdtRequests.Count, "The BDT DOT should be requested once.");

        // Act
        await viewModel.ToggleReductionAsync().ConfigureAwait(false);

        // Assert
        Assert.IsTrue(viewModel.IsReduced, "The second toggle should switch back to reduced BDD mode.");
        Assert.AreEqual(session.DotText, viewModel.DotText, "The original BDD DOT should be restored.");
    }

    /// <summary>
    /// Verifies that non-BDD sessions hide the BDT button and ignore toggles.
    /// </summary>
    [TestMethod]
    public async Task UpdateSessionAsync_ZddSession_ShouldHideBdtButtonAndIgnoreToggle()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new DiagramPanelViewModel(diagramService, new RecordingGraphvizService());
        var session = CreateSession(DiagramFamily.ZDD, "digraph ZDD { root; }");
        await viewModel.UpdateSessionAsync(session).ConfigureAwait(false);

        // Act
        await viewModel.ToggleReductionAsync().ConfigureAwait(false);

        // Assert
        Assert.IsFalse(viewModel.IsBdtButtonVisible, "Non-BDD sessions should hide the BDT toggle.");
        Assert.AreEqual(0, diagramService.BdtRequests.Count, "Non-BDD toggles should not request BDT DOT.");
        Assert.IsTrue(viewModel.IsReduced, "Non-BDD sessions should remain in their normal rendered mode.");
    }

    /// <summary>
    /// Verifies constructor and render failure behavior.
    /// </summary>
    [TestMethod]
    public async Task ConstructorAndRendering_InvalidUsage_ShouldThrowOrSetError()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();

        // Act / Assert
        Assert.ThrowsException<ArgumentNullException>(() => new DiagramPanelViewModel(null!, new RecordingGraphvizService()));
        Assert.ThrowsException<ArgumentNullException>(() => new DiagramPanelViewModel(diagramService, null!));

        var viewModel = new DiagramPanelViewModel(diagramService, new ThrowingGraphvizService());
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => viewModel.UpdateSessionAsync(CreateSession(DiagramFamily.BDD, "digraph BDD { root; }")),
            "Render failures should be surfaced.").ConfigureAwait(false);
        Assert.AreEqual("render failed", exception.Message, "The render exception should be preserved.");
        Assert.AreEqual("render failed", viewModel.ErrorMessage, "Render failures should update the view-model error message.");
    }

    private static DiagramSession CreateSession(DiagramFamily family, string dotText)
    {
        return new DiagramSession
        {
            Family = family,
            VariableNames = ["a", "b"],
            VariableOrder = [0, 1],
            IntValueTable = [0, 1, 1, 0],
            DotText = dotText,
        };
    }

    private sealed class RecordingDiagramService : IDiagramService
    {
        public List<DiagramSession> BdtRequests { get; } = [];

        public Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct)
        {
            return Task.FromResult(new DiagramSession
            {
                Family = family,
                VariableNames = variableNames,
                IntValueTable = intValueTable,
                DotText = "digraph BDD { root; }",
            });
        }

        public Task<DiagramSession> BuildAsync(
            string[] variableNames,
            IReadOnlyList<IReadOnlyList<string>> setInput,
            DiagramFamily family,
            CancellationToken ct)
        {
            return Task.FromResult(new DiagramSession
            {
                Family = family,
                VariableNames = variableNames,
                SetInput = setInput,
                DotText = "digraph ZDD { root; }",
            });
        }

        public Task<DiagramSession> ApplyZddOperationAsync(ZddOperation operation, CancellationToken ct)
        {
            return Task.FromResult(new DiagramSession
            {
                Family = DiagramFamily.ZDD,
                VariableNames = ["a"],
                SetInput = [new[] { "a" }],
                DotText = "digraph ZDD { root; }",
            });
        }

        public Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct)
        {
            BdtRequests.Add(session);
            return Task.FromResult("digraph BDT { root; }");
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

    private sealed class ThrowingGraphvizService : IGraphvizService
    {
        public bool IsAvailable()
        {
            return true;
        }

        public Task<string> RenderSvgAsync(string dotText, CancellationToken ct)
        {
            throw new InvalidOperationException("render failed");
        }
    }
}
