using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.ViewModels;

/// <summary>
/// Verifies the explanation view model.
/// </summary>
[TestClass]
public sealed class ExplanationViewModelTests
{
    /// <summary>
    /// Verifies that selecting a node stores selection state and produces text.
    /// </summary>
    [TestMethod]
    public void SelectNode_ShouldStoreSelectedNodeAndGenerateExplanationText()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var session = new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = ["a", "b"],
        };

        // Act
        viewModel.SelectNode("n1", session);

        // Assert
        Assert.AreEqual("n1", viewModel.SelectedNodeId, "The selected node id should be stored.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ExplanationText), "The v0.1 explanation should be non-empty.");
        StringAssert.Contains(viewModel.ExplanationText, "BDD");
    }

    /// <summary>
    /// Verifies invalid selection guards.
    /// </summary>
    [TestMethod]
    public void SelectNode_InvalidInput_ShouldThrow()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var session = new DiagramSession();

        // Act / Assert
        Assert.ThrowsException<ArgumentException>(() => viewModel.SelectNode(string.Empty, session));
        Assert.ThrowsException<ArgumentNullException>(() => viewModel.SelectNode("n1", null!));
    }
}
