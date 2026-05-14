using DecisionDiagramStudio.Commands;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Commands;

/// <summary>
/// Verifies truth-table change commands.
/// </summary>
[TestClass]
public sealed class ChangeTruthTableCommandTests
{
    /// <summary>
    /// Verifies that Execute, Undo, and Execute rebuild sessions from the expected snapshots.
    /// </summary>
    [TestMethod]
    public void ExecuteUndoExecute_ShouldApplyAfterBeforeAfterSnapshots()
    {
        // Arrange
        var service = new RecordingDiagramService();
        var applied = new List<DiagramSession>();
        var before = new[] { 0, 0, 0, 1 };
        var after = new[] { 0, 1, 1, 0 };
        var command = new ChangeTruthTableCommand(
            service,
            new[] { "a", "b" },
            before,
            after,
            DiagramFamily.BDD,
            applied.Add);

        before[0] = 1;
        after[0] = 1;

        // Act
        command.Execute();
        command.Undo();
        command.Execute();

        // Assert
        Assert.AreEqual(3, service.Requests.Count, "Each command transition should rebuild a session.");
        CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, service.Requests[0].Values, "Execute should use the original after snapshot.");
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 1 }, service.Requests[1].Values, "Undo should use the original before snapshot.");
        CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, service.Requests[2].Values, "A second Execute should use the after snapshot again.");
        Assert.AreSame(applied[^1], command.CurrentSession, "CurrentSession should track the last service result.");
        CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, applied[^1].IntValueTable, "The applied session should reflect the final execute state.");
    }

    private sealed class RecordingDiagramService : IDiagramService
    {
        public List<(string[] Variables, int[] Values, DiagramFamily Family)> Requests { get; } = [];

        public Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct)
        {
            Requests.Add(((string[])variableNames.Clone(), (int[])intValueTable.Clone(), family));
            var session = new DiagramSession
            {
                Family = family,
                VariableNames = (string[])variableNames.Clone(),
                VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
                IntValueTable = (int[])intValueTable.Clone(),
                DotText = "digraph BDD { }",
            };
            return Task.FromResult(session);
        }

        public Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct)
        {
            return Task.FromResult("digraph BDT { }");
        }
    }
}
