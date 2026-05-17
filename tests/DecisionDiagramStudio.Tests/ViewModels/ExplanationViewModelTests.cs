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

    /// <summary>
    /// Verifies that a valid WebView2 node-click message updates the explanation state.
    /// </summary>
    [TestMethod]
    public void TrySelectNodeFromWebMessage_ValidNodeClick_ShouldUpdateExplanation()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var session = new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = ["a", "b"],
        };
        const string Json = """{"type":"nodeClick","nodeId":"n3","variableName":"b","nodeType":"internal"}""";

        // Act
        var accepted = viewModel.TrySelectNodeFromWebMessage(Json, session);

        // Assert
        Assert.IsTrue(accepted, "Valid node-click messages should be accepted.");
        Assert.AreEqual("n3", viewModel.SelectedNodeId, "The node id from the validated message should be selected.");
        StringAssert.Contains(viewModel.ExplanationText, "n3");
    }

    /// <summary>
    /// Verifies that malformed or hostile WebView2 messages are ignored without changing state.
    /// </summary>
    [TestMethod]
    public void TrySelectNodeFromWebMessage_InvalidMessages_ShouldIgnoreAndKeepState()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var session = new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = ["a", "b"],
        };
        viewModel.SelectNode("n1", session);
        var originalText = viewModel.ExplanationText;

        var invalidMessages = new[]
        {
            """{"type":"exec","cmd":"rm -rf /"}""",
            """{"type":"nodeClick","nodeId":"node3","variableName":"b","nodeType":"internal"}""",
            """{"type":"nodeClick","nodeId":"n3","variableName":"<script>","nodeType":"internal"}""",
            """{"type":"nodeClick","nodeId":"n3","variableName":"b","nodeType":"process"}""",
            "not-json",
        };

        foreach (var message in invalidMessages)
        {
            // Act
            var accepted = viewModel.TrySelectNodeFromWebMessage(message, session);

            // Assert
            Assert.IsFalse(accepted, "Invalid WebView2 messages should be ignored.");
            Assert.AreEqual("n1", viewModel.SelectedNodeId, "Invalid messages must not change the selected node.");
            Assert.AreEqual(originalText, viewModel.ExplanationText, "Invalid messages must not change the explanation text.");
        }
    }

    /// <summary>
    /// Verifies family-specific explanation text for all v0.3 diagram families.
    /// </summary>
    [TestMethod]
    public void SelectNode_AllFamilies_ShouldMentionFamilyVariablesAndValues()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var sessions = new[]
        {
            new DiagramSession { Family = DiagramFamily.BDD, VariableNames = ["a"], IntValueTable = [0, 1] },
            new DiagramSession { Family = DiagramFamily.ZDD, VariableNames = ["a"], SetInput = [new[] { "a" }] },
            new DiagramSession { Family = DiagramFamily.MTBDD, VariableNames = ["a", "b"], IntValueTable = [0, 2, 2, 5] },
            new DiagramSession { Family = DiagramFamily.ZMTBDD, VariableNames = ["a", "b"], IntValueTable = [0, 0, 0, 7] },
        };

        foreach (var session in sessions)
        {
            // Act
            viewModel.SelectNode("n1", "a", "internal", session);

            // Assert
            StringAssert.Contains(viewModel.ExplanationText, session.Family.ToString());
            StringAssert.Contains(viewModel.ExplanationText, "Variables: a");
            StringAssert.Contains(viewModel.ExplanationText, "Values:");
            StringAssert.Contains(viewModel.ExplanationText, "internal");
        }
    }

    /// <summary>
    /// Verifies terminal node-click messages with MTBDD numeric output metadata are accepted.
    /// </summary>
    [TestMethod]
    public void TrySelectNodeFromWebMessage_TerminalNode_ShouldUpdateExplanation()
    {
        // Arrange
        var viewModel = new ExplanationViewModel();
        var session = new DiagramSession
        {
            Family = DiagramFamily.MTBDD,
            VariableNames = ["a"],
            IntValueTable = [0, 42],
        };
        const string Json = """{"type":"nodeClick","nodeId":"t1","variableName":"_terminal","nodeType":"terminal"}""";

        // Act
        var accepted = viewModel.TrySelectNodeFromWebMessage(Json, session);

        // Assert
        Assert.IsTrue(accepted, "Terminal node-click messages should be accepted.");
        Assert.AreEqual("t1", viewModel.SelectedNodeId, "Terminal node ids should be preserved.");
        StringAssert.Contains(viewModel.ExplanationText, "terminal");
        StringAssert.Contains(viewModel.ExplanationText, "42");
    }
}
