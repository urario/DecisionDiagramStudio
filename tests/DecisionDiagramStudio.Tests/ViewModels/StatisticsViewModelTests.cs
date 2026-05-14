using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.ViewModels;

/// <summary>
/// Verifies the statistics view model.
/// </summary>
[TestClass]
public sealed class StatisticsViewModelTests
{
    /// <summary>
    /// Verifies that setting Session updates all display properties.
    /// </summary>
    [TestMethod]
    public void Session_Set_ShouldUpdateAllDisplayProperties()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var session = new DiagramSession
        {
            Statistics = new AppDiagramStatistics
            {
                ReachableNodeCount = 3,
                ReachableTerminalCount = 2,
                TotalNodeCount = 8,
                VariableCount = 3,
                BdtNodeCount = 7,
                ReducedCount = 4,
                SetCount = 0,
            },
        };

        // Act
        viewModel.Session = session;

        // Assert
        Assert.AreEqual(3, viewModel.ReachableNodeCount, "Reachable node count should follow the session statistics.");
        Assert.AreEqual(2, viewModel.ReachableTerminalCount, "Reachable terminal count should follow the session statistics.");
        Assert.AreEqual(8, viewModel.TotalNodeCount, "Total node count should follow the session statistics.");
        Assert.AreEqual(3, viewModel.VariableCount, "Variable count should follow the session statistics.");
        Assert.AreEqual(7, viewModel.BdtNodeCount, "BDT node count should follow the session statistics.");
        Assert.AreEqual(4, viewModel.ReducedCount, "Reduced count should follow the BDT-to-BDD reduction statistic.");
        Assert.AreEqual(0L, viewModel.SetCount, "Set count should follow the session statistics.");
        StringAssert.Contains(viewModel.ReductionSummary, "4");
    }

    /// <summary>
    /// Verifies that clearing Session resets display properties.
    /// </summary>
    [TestMethod]
    public void Session_Clear_ShouldResetDisplayProperties()
    {
        // Arrange
        var viewModel = new StatisticsViewModel
        {
            Session = new DiagramSession
            {
                Statistics = new AppDiagramStatistics
                {
                    ReachableNodeCount = 3,
                    ReducedCount = 2,
                },
            },
        };

        // Act
        viewModel.Session = null;

        // Assert
        Assert.AreEqual(0, viewModel.ReachableNodeCount, "Clearing the session should reset reachable nodes.");
        Assert.AreEqual(0, viewModel.ReducedCount, "Clearing the session should reset reduced count.");
    }
}
