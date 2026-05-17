using DecisionDiagramStudio.Commands;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using DecisionDiagramStudio.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.ViewModels;

/// <summary>
/// Verifies the BDD workbench view model.
/// </summary>
[TestClass]
public sealed class WorkbenchViewModelTests
{
    /// <summary>
    /// Verifies the initial VM-BDD-001 workbench state.
    /// </summary>
    [TestMethod]
    public void Constructor_ShouldInitializeBddStateAndCommands()
    {
        // Arrange / Act
        var viewModel = new WorkbenchViewModel(
            new RecordingDiagramService(),
            new StubPresetService(),
            new CommandStack());

        // Assert
        Assert.AreEqual(DiagramFamily.BDD, viewModel.SelectedFamily, "The initial workbench family should be BDD.");
        CollectionAssert.AreEqual(new[] { "a" }, viewModel.VariableNames, "The default BDD workbench should start with one variable.");
        Assert.AreEqual("a", viewModel.VariableNamesText, "The variable editor should mirror the initial variables.");
        CollectionAssert.AreEqual(new[] { 0, 1 }, viewModel.IntValueTable, "The default BDD table should be the identity table.");
        CollectionAssert.AreEqual(new[] { "a" }, viewModel.SetInput[0].ToArray(), "The default ZDD input snapshot should be initialized.");
        Assert.AreEqual(2, viewModel.TruthTableRows.Count, "The formatted truth-table rows should mirror the initial table.");
        CollectionAssert.AreEqual(new[] { 0 }, viewModel.TruthTableRows[0].VariableValues.ToArray(), "Rows should expose per-variable values.");
        CollectionAssert.AreEqual(new[] { 1 }, viewModel.TruthTableRows[1].VariableValues.ToArray(), "Variable values should follow LSB-first row indexing.");
        Assert.AreEqual(1, viewModel.Presets.Count, "Available presets should be exposed for the view.");
        Assert.IsNotNull(viewModel.ApplyVariableNamesCommand, "Variable-name edits should be exposed as a command.");
        Assert.IsNotNull(viewModel.ApplySetInputCommand, "ZDD set-family edits should be exposed as a command.");
        Assert.IsNotNull(viewModel.ApplyZddOperationCommand, "ZDD operations should be exposed as a command.");
        Assert.IsNotNull(viewModel.UndoCommand, "Undo should be exposed as a command.");
        Assert.IsNotNull(viewModel.RedoCommand, "Redo should be exposed as a command.");
        Assert.IsNotNull(viewModel.RebuildCommand, "A rebuild command should be exposed for the initial view load.");
        Assert.IsNotNull(viewModel.SelectPresetCommand, "Preset selection should be exposed as a command.");
        Assert.IsNotNull(viewModel.ChangeTruthTableCellCommand, "Truth-table changes should be exposed as a command.");
        Assert.IsTrue(viewModel.IsBddInputVisible, "BDD input should be visible for the default family.");
        Assert.IsFalse(viewModel.IsZddInputVisible, "ZDD input should be hidden for the default family.");
    }

    /// <summary>
    /// Verifies that rapid truth-table edits debounce into a single build.
    /// </summary>
    [TestMethod]
    public async Task ChangeTruthTableCell_ThreeChangesAtOneHundredMilliseconds_ShouldBuildOnce()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            new CommandStack());

        // Act
        viewModel.ChangeTruthTableCell(0, 1);
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.ChangeTruthTableCell(1, 0);
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.ChangeTruthTableCell(0, 0);
        await viewModel.PendingBuildTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        // Assert
        Assert.AreEqual(1, diagramService.BuildRequests.Count, "Only the final debounced edit should rebuild the diagram.");
        CollectionAssert.AreEqual(new[] { 0, 0 }, diagramService.BuildRequests[0].Values, "The build should use the final table snapshot.");
        Assert.IsNotNull(viewModel.CurrentSession, "A successful debounced build should apply the current session.");
        CollectionAssert.AreEqual(new[] { 0, 0 }, viewModel.CurrentSession!.IntValueTable, "The applied session should match the final edit.");
    }

    /// <summary>
    /// Verifies the command wrapper for truth-table cell edits.
    /// </summary>
    [TestMethod]
    public async Task ChangeTruthTableCellCommand_ShouldApplyChangeRequest()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            new CommandStack(),
            TimeSpan.Zero);

        // Act
        viewModel.ChangeTruthTableCellCommand.Execute(new TruthTableCellChange(0, 1));
        await viewModel.PendingBuildTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        // Assert
        Assert.AreEqual(1, diagramService.BuildRequests.Count, "The command should delegate to the cell-change path.");
        CollectionAssert.AreEqual(new[] { 1, 1 }, viewModel.IntValueTable, "The command payload should update the table.");
    }

    /// <summary>
    /// Verifies validation and no-op behavior for truth-table edits.
    /// </summary>
    [TestMethod]
    public void ChangeTruthTableCell_InvalidOrUnchangedValues_ShouldNotBuild()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            new CommandStack());

        // Act / Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => viewModel.ChangeTruthTableCell(-1, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => viewModel.ChangeTruthTableCell(0, 2));

        viewModel.ChangeTruthTableCell(0, 0);
        Assert.AreEqual(0, diagramService.BuildRequests.Count, "Writing the existing value should be a no-op.");
    }

    /// <summary>
    /// Verifies that SelectPresetCommand uses the preset service, command stack, and diagram service.
    /// </summary>
    [TestMethod]
    public void SelectPresetCommand_ShouldApplyPresetAndBuildCurrentSession()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var commandStack = new CommandStack();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            commandStack);

        // Act
        viewModel.SelectPresetCommand.Execute("bdd.xor");

        // Assert
        Assert.AreEqual(1, diagramService.BuildRequests.Count, "Selecting a preset should perform one immediate build.");
        CollectionAssert.AreEqual(new[] { "a", "b" }, diagramService.BuildRequests[0].Variables, "The preset variables should be sent to the service.");
        CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, diagramService.BuildRequests[0].Values, "The preset truth table should be sent to the service.");
        CollectionAssert.AreEqual(new[] { 1, 0 }, viewModel.TruthTableRows[1].VariableValues.ToArray(), "Variable columns should stay aligned to the variable name order.");
        CollectionAssert.AreEqual(new[] { 0, 1 }, viewModel.TruthTableRows[2].VariableValues.ToArray(), "Variable columns should expose one bit per variable.");
        Assert.IsTrue(commandStack.CanUndo, "The preset change should be pushed through the command stack.");
        Assert.IsNotNull(viewModel.CurrentSession, "The rebuilt session should be applied to the workbench.");
        StringAssert.StartsWith(viewModel.CurrentSession!.DotText, "digraph BDD");
    }

    /// <summary>
    /// Verifies SelectPreset validation and service failure reporting.
    /// </summary>
    [TestMethod]
    public void SelectPreset_InvalidInputOrBuildFailure_ShouldSurfaceError()
    {
        // Arrange
        var viewModel = new WorkbenchViewModel(
            new ThrowingDiagramService(),
            new StubPresetService(),
            new CommandStack());

        // Act / Assert
        Assert.ThrowsException<ArgumentException>(() => viewModel.SelectPreset(null));

        var exception = Assert.ThrowsException<InvalidOperationException>(() => viewModel.SelectPreset("bdd.xor"));
        Assert.AreEqual("build failed", exception.Message, "Build failures should be surfaced to the caller.");
        Assert.AreEqual("build failed", viewModel.ErrorMessage, "Build failures should update the view-model error message.");
    }

    /// <summary>
    /// Verifies that debounced rebuild failures are captured on the view model.
    /// </summary>
    [TestMethod]
    public async Task ChangeTruthTableCell_DebouncedBuildFailure_ShouldSetErrorMessage()
    {
        // Arrange
        var viewModel = new WorkbenchViewModel(
            new ThrowingDiagramService(),
            new StubPresetService(),
            new CommandStack(),
            TimeSpan.Zero);

        // Act
        viewModel.ChangeTruthTableCell(0, 1);
        await viewModel.PendingBuildTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        // Assert
        Assert.AreEqual("build failed", viewModel.ErrorMessage, "Debounced build failures should be captured instead of faulting the task.");
    }

    /// <summary>
    /// Verifies constructor and dispose guards.
    /// </summary>
    [TestMethod]
    public void ConstructorAndDispose_InvalidUsage_ShouldThrow()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var presetService = new StubPresetService();
        var commandStack = new CommandStack();

        // Act / Assert
        Assert.ThrowsException<ArgumentNullException>(() => new WorkbenchViewModel(null!, presetService, commandStack));
        Assert.ThrowsException<ArgumentNullException>(() => new WorkbenchViewModel(diagramService, null!, commandStack));
        Assert.ThrowsException<ArgumentNullException>(() => new WorkbenchViewModel(diagramService, presetService, null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new WorkbenchViewModel(diagramService, presetService, commandStack, TimeSpan.FromMilliseconds(-1)));

        var viewModel = new WorkbenchViewModel(diagramService, presetService, commandStack);
        viewModel.Dispose();
        viewModel.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => viewModel.ChangeTruthTableCell(0, 1));
    }

    /// <summary>
    /// Verifies that ZDD set-family input builds a ZDD session and enables undo.
    /// </summary>
    [TestMethod]
    public void ApplySetInputCommand_ShouldBuildZddSessionAndEnableUndo()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            new CommandStack());

        // Act
        viewModel.SetInputText = "{a,b},{c}";
        viewModel.ApplySetInputCommand.Execute(null);

        // Assert
        Assert.AreEqual(1, diagramService.SetBuildRequests.Count, "Applying ZDD input should perform one ZDD build.");
        Assert.AreEqual(DiagramFamily.ZDD, viewModel.SelectedFamily, "Applying set input should switch the workbench to ZDD.");
        Assert.IsFalse(viewModel.IsBddInputVisible, "BDD input should be hidden for ZDD.");
        Assert.IsTrue(viewModel.IsZddInputVisible, "ZDD input should be visible for ZDD.");
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, diagramService.SetBuildRequests[0].Variables, "Variables should include set members.");
        Assert.IsNotNull(viewModel.CurrentSession, "A ZDD session should be applied.");
        Assert.AreEqual(DiagramFamily.ZDD, viewModel.CurrentSession!.Family, "The current session should be ZDD.");
        CollectionAssert.AreEqual(new[] { "a", "b" }, viewModel.SetInput[0].ToArray(), "The ZDD input snapshot should update after a successful build.");
        Assert.IsTrue(viewModel.CanUndo, "ZDD input changes should be undoable.");
    }

    /// <summary>
    /// Verifies that ZDD operation commands apply service results and preserve undo/redo behavior.
    /// </summary>
    [TestMethod]
    public void ApplyZddOperationCommand_ShouldApplyOperationAndSupportUndoRedo()
    {
        // Arrange
        var diagramService = new RecordingDiagramService();
        var viewModel = new WorkbenchViewModel(
            diagramService,
            new StubPresetService(),
            new CommandStack());
        viewModel.SetInputText = "{a,b},{c}";
        viewModel.ApplySetInput();

        // Act
        var before = viewModel.CurrentSession;
        viewModel.ZddOperationInputText = "{b},{c,d}";
        viewModel.ApplyZddOperationCommand.Execute("Union");

        // Assert
        Assert.AreEqual(1, diagramService.ZddOperationRequests.Count, "The operation command should delegate to the ZDD operation service.");
        Assert.AreEqual(ZddOperation.Union, diagramService.ZddOperationRequests[0], "The command parameter should select the operation.");
        Assert.AreEqual("digraph ZDD { Union; }", viewModel.CurrentSession!.DotText, "The operation result should be applied.");
        Assert.IsTrue(viewModel.CanUndo, "The operation result should be undoable.");

        // Act
        viewModel.UndoCommand.Execute(null);
        Assert.AreSame(before, viewModel.CurrentSession, "Undo should restore the pre-operation session snapshot.");

        viewModel.RedoCommand.Execute(null);
        Assert.AreEqual("digraph ZDD { Union; }", viewModel.CurrentSession!.DotText, "Redo should restore the operation result.");
    }

    /// <summary>
    /// Verifies that family changes are undoable.
    /// </summary>
    [TestMethod]
    public void SelectedFamily_ZddToBddUndo_ShouldRestoreZdd()
    {
        // Arrange
        var viewModel = new WorkbenchViewModel(
            new RecordingDiagramService(),
            new StubPresetService(),
            new CommandStack());

        // Act
        viewModel.SelectedFamily = DiagramFamily.ZDD;
        viewModel.SelectedFamily = DiagramFamily.BDD;
        viewModel.Undo();

        // Assert
        Assert.AreEqual(DiagramFamily.ZDD, viewModel.SelectedFamily, "Undo should restore the previous ZDD family.");
        Assert.IsTrue(viewModel.IsZddInputVisible, "The ZDD input panel state should follow the restored family.");
    }

    private sealed class RecordingDiagramService : IDiagramService
    {
        public List<(string[] Variables, int[] Values, DiagramFamily Family, CancellationToken Token)> BuildRequests { get; } = [];
        public List<(string[] Variables, IReadOnlyList<IReadOnlyList<string>> Sets, DiagramFamily Family, CancellationToken Token)> SetBuildRequests { get; } = [];
        public List<ZddOperation> ZddOperationRequests { get; } = [];

        public Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct)
        {
            BuildRequests.Add(((string[])variableNames.Clone(), (int[])intValueTable.Clone(), family, ct));
            return Task.FromResult(new DiagramSession
            {
                Family = family,
                VariableNames = (string[])variableNames.Clone(),
                VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
                IntValueTable = (int[])intValueTable.Clone(),
                DotText = "digraph BDD { root; }",
                Statistics = new AppDiagramStatistics
                {
                    ReachableNodeCount = 1,
                    TotalNodeCount = 1,
                    VariableCount = variableNames.Length,
                },
            });
        }

        public Task<DiagramSession> BuildAsync(
            string[] variableNames,
            IReadOnlyList<IReadOnlyList<string>> setInput,
            DiagramFamily family,
            CancellationToken ct)
        {
            var clonedSets = CloneSetInput(setInput);
            SetBuildRequests.Add(((string[])variableNames.Clone(), clonedSets, family, ct));
            return Task.FromResult(new DiagramSession
            {
                Family = family,
                VariableNames = (string[])variableNames.Clone(),
                VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
                SetInput = clonedSets,
                DotText = "digraph ZDD { root; }",
                Statistics = new AppDiagramStatistics
                {
                    ReachableNodeCount = 1,
                    TotalNodeCount = 1,
                    VariableCount = variableNames.Length,
                    SetCount = clonedSets.Count,
                },
            });
        }

        public Task<DiagramSession> ApplyZddOperationAsync(ZddOperation operation, CancellationToken ct)
        {
            ZddOperationRequests.Add(operation);
            return Task.FromResult(new DiagramSession
            {
                Family = DiagramFamily.ZDD,
                VariableNames = ["a", "b", "c", "d"],
                VariableOrder = [0, 1, 2, 3],
                SetInput = [new[] { "a", "b" }, new[] { "c" }, new[] { "b" }, new[] { "c", "d" }],
                DotText = "digraph ZDD { " + operation.ToString() + "; }",
                Statistics = new AppDiagramStatistics
                {
                    ReachableNodeCount = 2,
                    TotalNodeCount = 2,
                    VariableCount = 4,
                    SetCount = 4,
                },
            });
        }

        public Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct)
        {
            return Task.FromResult("digraph BDT { root; }");
        }

        private static IReadOnlyList<IReadOnlyList<string>> CloneSetInput(IReadOnlyList<IReadOnlyList<string>> setInput)
        {
            var clone = new IReadOnlyList<string>[setInput.Count];
            for (var i = 0; i < setInput.Count; i++)
            {
                clone[i] = setInput[i].ToArray();
            }

            return clone;
        }
    }

    private sealed class ThrowingDiagramService : IDiagramService
    {
        public Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct)
        {
            throw new InvalidOperationException("build failed");
        }

        public Task<DiagramSession> BuildAsync(
            string[] variableNames,
            IReadOnlyList<IReadOnlyList<string>> setInput,
            DiagramFamily family,
            CancellationToken ct)
        {
            throw new InvalidOperationException("build failed");
        }

        public Task<DiagramSession> ApplyZddOperationAsync(ZddOperation operation, CancellationToken ct)
        {
            throw new InvalidOperationException("build failed");
        }

        public Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct)
        {
            return Task.FromResult("digraph BDT { root; }");
        }
    }

    private sealed class StubPresetService : IPresetService
    {
        private static readonly DiagramPreset XorPreset = new()
        {
            Id = "bdd.xor",
            Label = "XOR",
            Description = "Exclusive or",
            VariableNames = ["a", "b"],
            TruthTableValues = [0, 1, 1, 0],
            DefaultFamily = DiagramFamily.BDD,
        };

        public IReadOnlyList<DiagramPreset> GetPresets()
        {
            return [XorPreset];
        }

        public DiagramPreset GetPreset(string id)
        {
            if (!StringComparer.Ordinal.Equals(id, XorPreset.Id))
            {
                throw new KeyNotFoundException(id);
            }

            return XorPreset with
            {
                VariableNames = (string[])XorPreset.VariableNames.Clone(),
                TruthTableValues = (int[])XorPreset.TruthTableValues.Clone(),
            };
        }
    }
}
