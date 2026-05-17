using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using DecisionDiagramStudio.Commands;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Coordinates BDD workbench input state, presets, and diagram rebuild requests.
/// </summary>
public sealed partial class WorkbenchViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Gets the default delay used to debounce truth-table edits.
    /// </summary>
    public static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(150);

    private readonly IDiagramService _diagramService;
    private readonly IPresetService _presetService;
    private readonly CommandStack _commandStack;
    private readonly ILogger<WorkbenchViewModel> _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly object _buildSync = new();
    private Func<Action, Task> _runOnUiThreadAsync = RunInlineAsync;
    private CancellationTokenSource? _buildCancellation;
    private Task _pendingBuildTask = Task.CompletedTask;
    private int[] _lastCommittedValues;
    private IReadOnlyList<IReadOnlyList<string>> _lastCommittedSetInput = [new[] { "a" }];
    private DiagramFamily _familyBeforeChange;
    private bool _suppressFamilyChangeCommand;
    private bool _disposed;

    [ObservableProperty]
    private string[] _variableNames = ["a"];

    [ObservableProperty]
    private string _variableNamesText = "a";

    [ObservableProperty]
    private int[] _intValueTable = [0, 1];

    [ObservableProperty]
    private string _setInputText = "{a}";

    [ObservableProperty]
    private IReadOnlyList<IReadOnlyList<string>> _setInput = [new[] { "a" }];

    [ObservableProperty]
    private string _zddOperationInputText = "{a}";

    [ObservableProperty]
    private DiagramFamily _selectedFamily = DiagramFamily.BDD;

    [ObservableProperty]
    private DiagramSession? _currentSession;

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkbenchViewModel"/> class.
    /// </summary>
    /// <param name="diagramService">The service used to build sessions.</param>
    /// <param name="presetService">The service used to load presets.</param>
    /// <param name="commandStack">The command stack used for undoable input changes.</param>
    public WorkbenchViewModel(
        IDiagramService diagramService,
        IPresetService presetService,
        CommandStack commandStack)
        : this(diagramService, presetService, commandStack, DefaultDebounceDelay, NullLogger<WorkbenchViewModel>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkbenchViewModel"/> class.
    /// </summary>
    /// <param name="diagramService">The service used to build sessions.</param>
    /// <param name="presetService">The service used to load presets.</param>
    /// <param name="commandStack">The command stack used for undoable input changes.</param>
    /// <param name="logger">The logger used for workbench diagnostics.</param>
    public WorkbenchViewModel(
        IDiagramService diagramService,
        IPresetService presetService,
        CommandStack commandStack,
        ILogger<WorkbenchViewModel> logger)
        : this(diagramService, presetService, commandStack, DefaultDebounceDelay, logger)
    {
    }

    internal WorkbenchViewModel(
        IDiagramService diagramService,
        IPresetService presetService,
        CommandStack commandStack,
        TimeSpan debounceDelay)
        : this(diagramService, presetService, commandStack, debounceDelay, NullLogger<WorkbenchViewModel>.Instance)
    {
    }

    internal WorkbenchViewModel(
        IDiagramService diagramService,
        IPresetService presetService,
        CommandStack commandStack,
        TimeSpan debounceDelay,
        ILogger<WorkbenchViewModel> logger)
    {
        if (debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceDelay), "The debounce delay cannot be negative.");
        }

        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));
        _commandStack = commandStack ?? throw new ArgumentNullException(nameof(commandStack));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceDelay = debounceDelay;
        _lastCommittedValues = (int[])IntValueTable.Clone();

        Presets = _presetService.GetPresets();
        RebuildTruthTableRows();
        _logger.LogDebug(
            "Workbench initialized. PresetCount={PresetCount} VariableCount={VariableCount} RowCount={RowCount}",
            Presets.Count,
            VariableNames.Length,
            IntValueTable.Length);

        SelectPresetCommand = new RelayCommand<string?>(SelectPreset);
        ApplyVariableNamesCommand = new RelayCommand(ApplyVariableNamesFromCommand);
        ApplySetInputCommand = new RelayCommand(ApplySetInputFromCommand);
        ApplyZddOperationCommand = new RelayCommand<string?>(ApplyZddOperationFromCommand);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        RebuildCommand = new RelayCommand(RebuildCurrentSession);
        ChangeTruthTableCellCommand = new RelayCommand<TruthTableCellChange>(change =>
        {
            ArgumentNullException.ThrowIfNull(change);
            ChangeTruthTableCell(change.Index, change.Value);
        });
        ChangeValueTableCellCommand = new RelayCommand<TruthTableCellChange>(change =>
        {
            ArgumentNullException.ThrowIfNull(change);
            ChangeValueTableCell(change.Index, change.Value);
        });
    }

    /// <summary>
    /// Gets the presets that can be selected from the workbench.
    /// </summary>
    public IReadOnlyList<DiagramPreset> Presets { get; }

    /// <summary>
    /// Gets truth-table rows formatted for the view.
    /// </summary>
    public ObservableCollection<TruthTableRowViewModel> TruthTableRows { get; } = [];

    /// <summary>
    /// Gets the selected diagram family formatted for display.
    /// </summary>
    public string SelectedFamilyLabel => SelectedFamily.ToString();

    /// <summary>
    /// Gets a value indicating whether BDD-specific input controls should be visible.
    /// </summary>
    public bool IsBddInputVisible => SelectedFamily == DiagramFamily.BDD;

    /// <summary>
    /// Gets a value indicating whether ZDD-specific input controls should be visible.
    /// </summary>
    public bool IsZddInputVisible => SelectedFamily == DiagramFamily.ZDD;

    /// <summary>
    /// Gets a value indicating whether MTBDD/ZMTBDD integer value-table controls should be visible.
    /// </summary>
    public bool IsMtbddInputVisible => SelectedFamily is DiagramFamily.MTBDD or DiagramFamily.ZMTBDD;

    /// <summary>
    /// Gets the integer value-table panel title for the selected MT family.
    /// </summary>
    public string ValueTableTitle => SelectedFamily == DiagramFamily.ZMTBDD ? "ZMTBDD value table" : "MTBDD value table";

    /// <summary>
    /// Gets the integer value-table build button label.
    /// </summary>
    public string ValueTableBuildButtonLabel => "Build " + SelectedFamily.ToString();

    /// <summary>
    /// Gets a value indicating whether the command stack can undo.
    /// </summary>
    public bool CanUndo => _commandStack.CanUndo;

    /// <summary>
    /// Gets a value indicating whether the command stack can redo.
    /// </summary>
    public bool CanRedo => _commandStack.CanRedo;

    /// <summary>
    /// Gets the command that applies the text in the variable-name input.
    /// </summary>
    public IRelayCommand ApplyVariableNamesCommand { get; }

    /// <summary>
    /// Gets the command that applies ZDD set-family text.
    /// </summary>
    public IRelayCommand ApplySetInputCommand { get; }

    /// <summary>
    /// Gets the command that applies a ZDD set-family operation.
    /// </summary>
    public IRelayCommand<string?> ApplyZddOperationCommand { get; }

    /// <summary>
    /// Gets the command that undoes the latest undoable workbench change.
    /// </summary>
    public IRelayCommand UndoCommand { get; }

    /// <summary>
    /// Gets the command that redoes the latest undone workbench change.
    /// </summary>
    public IRelayCommand RedoCommand { get; }

    /// <summary>
    /// Gets the command that rebuilds the current input without changing it.
    /// </summary>
    public IRelayCommand RebuildCommand { get; }

    /// <summary>
    /// Gets the command that applies a bundled preset.
    /// </summary>
    public IRelayCommand<string?> SelectPresetCommand { get; }

    /// <summary>
    /// Gets the command that updates one truth-table cell.
    /// </summary>
    public IRelayCommand<TruthTableCellChange> ChangeTruthTableCellCommand { get; }

    /// <summary>
    /// Gets the command that updates one integer value-table cell.
    /// </summary>
    public IRelayCommand<TruthTableCellChange> ChangeValueTableCellCommand { get; }

    /// <summary>
    /// Applies one truth-table cell edit and schedules a debounced rebuild.
    /// </summary>
    /// <param name="index">The zero-based truth-table row index.</param>
    /// <param name="value">The new cell value.</param>
    public void ChangeTruthTableCell(int index, int value)
    {
        ThrowIfDisposed();
        if (SelectedFamily != DiagramFamily.BDD)
        {
            throw new InvalidOperationException("BDD truth-table cells can only be edited while BDD is selected.");
        }

        ValidateTruthTableCell(index, value);

        var nextValues = (int[])IntValueTable.Clone();
        if (nextValues[index] == value)
        {
            _logger.LogTrace("Truth-table edit ignored because the value was unchanged. RowIndex={RowIndex}", index);
            return;
        }

        nextValues[index] = value;
        IntValueTable = nextValues;
        _logger.LogDebug(
            "Truth-table edit scheduled a debounced build. RowIndex={RowIndex} RowCount={RowCount}",
            index,
            nextValues.Length);
        ScheduleDebouncedBuild((int[])_lastCommittedValues.Clone(), nextValues);
    }

    /// <summary>
    /// Applies one integer value-table cell edit and schedules a debounced rebuild.
    /// </summary>
    /// <param name="index">The zero-based value-table row index.</param>
    /// <param name="value">The new integer value.</param>
    public void ChangeValueTableCell(int index, int value)
    {
        ThrowIfDisposed();
        if (SelectedFamily == DiagramFamily.ZDD)
        {
            throw new InvalidOperationException("ZDD sessions use set-family input instead of integer value tables.");
        }

        ValidateValueTableCell(index);
        if (SelectedFamily == DiagramFamily.BDD)
        {
            ValidateTruthTableCell(index, value);
        }

        var nextValues = (int[])IntValueTable.Clone();
        if (nextValues[index] == value)
        {
            _logger.LogTrace("Value-table edit ignored because the value was unchanged. RowIndex={RowIndex}", index);
            return;
        }

        nextValues[index] = value;
        IntValueTable = nextValues;
        _logger.LogDebug(
            "Value-table edit scheduled a debounced build. Family={Family} RowIndex={RowIndex} RowCount={RowCount}",
            SelectedFamily,
            index,
            nextValues.Length);
        ScheduleDebouncedBuild((int[])_lastCommittedValues.Clone(), nextValues);
    }

    /// <summary>
    /// Applies a preset immediately and rebuilds the current session.
    /// </summary>
    /// <param name="presetId">The stable preset identifier.</param>
    public void SelectPreset(string? presetId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(presetId))
        {
            throw new ArgumentException("A preset id is required.", nameof(presetId));
        }

        var preset = _presetService.GetPreset(presetId);
        var presetVariables = (string[])preset.VariableNames.Clone();
        var presetValues = (int[])preset.TruthTableValues.Clone();
        _logger.LogInformation(
            "Preset selection requested. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            preset.DefaultFamily,
            presetVariables.Length,
            presetValues.Length);

        CancelPendingBuild();

        VariableNames = presetVariables;
        IntValueTable = presetValues;
        SetSelectedFamilySilently(preset.DefaultFamily);

        var beforeValues = _lastCommittedValues.Length == presetValues.Length
            ? (int[])_lastCommittedValues.Clone()
            : (int[])presetValues.Clone();
        var cancellation = ReplaceBuildCancellation();

        PushTruthTableCommand(beforeValues, presetValues, cancellation.Token);
    }

    /// <summary>
    /// Rebuilds the diagram from the current workbench state.
    /// </summary>
    public void RebuildCurrentSession()
    {
        ThrowIfDisposed();
        CancelPendingBuild();
        _logger.LogInformation(
            "Manual rebuild requested. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            SelectedFamily,
            VariableNames.Length,
            IntValueTable.Length);

        var cancellation = ReplaceBuildCancellation();
        if (SelectedFamily == DiagramFamily.ZDD)
        {
            var setInput = ParseSetInputText(SetInputText);
            var variableNames = MergeVariableNames(VariableNames, setInput);
            PushSetInputCommand(
                VariableNames,
                _lastCommittedSetInput,
                variableNames,
                setInput,
                cancellation.Token);
            return;
        }

        PushTruthTableCommand((int[])_lastCommittedValues.Clone(), (int[])IntValueTable.Clone(), cancellation.Token);
    }

    /// <summary>
    /// Applies the current ZDD set-family text and rebuilds a ZDD session.
    /// </summary>
    public void ApplySetInput()
    {
        ThrowIfDisposed();
        CancelPendingBuild();

        var afterSetInput = ParseSetInputText(SetInputText);
        var beforeSetInput = CurrentSession?.Family == DiagramFamily.ZDD && CurrentSession.SetInput is not null
            ? CurrentSession.SetInput
            : _lastCommittedSetInput;
        var afterVariableNames = MergeVariableNames(VariableNames, afterSetInput);
        var beforeVariableNames = CurrentSession?.Family == DiagramFamily.ZDD
            ? CurrentSession.VariableNames
            : afterVariableNames;

        _logger.LogInformation(
            "ZDD set input applied. VariableCount={VariableCount} SetInputCount={SetInputCount}",
            afterVariableNames.Length,
            afterSetInput.Count);

        SetSelectedFamilySilently(DiagramFamily.ZDD);
        var cancellation = ReplaceBuildCancellation();
        PushSetInputCommand(
            beforeVariableNames,
            beforeSetInput,
            afterVariableNames,
            afterSetInput,
            cancellation.Token);
    }

    /// <summary>
    /// Applies a ZDD operation between the current ZDD session and the operation-input text.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    public void ApplyZddOperation(ZddOperation operation)
    {
        ThrowIfDisposed();
        if (CurrentSession is null || CurrentSession.Family != DiagramFamily.ZDD || CurrentSession.SetInput is null)
        {
            throw new InvalidOperationException("Build a ZDD session before applying a ZDD operation.");
        }

        CancelPendingBuild();
        var operandSetInput = ParseSetInputText(ZddOperationInputText);
        var variableNames = MergeVariableNames(CurrentSession.VariableNames, operandSetInput);
        var leftSetInput = CurrentSession.SetInput;

        _logger.LogInformation(
            "ZDD operation applying. Operation={Operation} VariableCount={VariableCount} OperandSetCount={OperandSetCount}",
            operation,
            variableNames.Length,
            operandSetInput.Count);

        var cancellation = ReplaceBuildCancellation();
        var beforeSession = CurrentSession;
        IsBuilding = true;
        ErrorMessage = string.Empty;
        try
        {
            _ = _diagramService
                .BuildAsync((string[])variableNames.Clone(), CloneSetInput(leftSetInput), DiagramFamily.ZDD, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            _ = _diagramService
                .BuildAsync((string[])variableNames.Clone(), CloneSetInput(operandSetInput), DiagramFamily.ZDD, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            var afterSession = _diagramService
                .ApplyZddOperationAsync(operation, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            PushCommand(new ApplyDiagramSessionCommand(beforeSession, afterSession, ApplySession));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "ZDD operation failed. Operation={Operation} ExceptionType={ExceptionType}",
                operation,
                ex.GetType().Name);
            ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            IsBuilding = false;
        }
    }

    /// <summary>
    /// Undoes the latest command-stack operation.
    /// </summary>
    public void Undo()
    {
        ThrowIfDisposed();
        ErrorMessage = string.Empty;
        _commandStack.Undo();
        RefreshUndoRedoState();
    }

    /// <summary>
    /// Redoes the latest command-stack operation.
    /// </summary>
    public void Redo()
    {
        ThrowIfDisposed();
        ErrorMessage = string.Empty;
        _commandStack.Redo();
        RefreshUndoRedoState();
    }

    /// <summary>
    /// Releases pending rebuild work.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingBuild();
        _buildCancellation?.Dispose();
    }

    internal Task PendingBuildTask
    {
        get
        {
            lock (_buildSync)
            {
                return _pendingBuildTask;
            }
        }
    }

    /// <summary>
    /// Sets the dispatcher used to return debounced work to the UI thread before mutating bound state.
    /// </summary>
    /// <param name="runOnUiThreadAsync">The dispatcher callback.</param>
    public void SetUiThreadDispatcher(Func<Action, Task> runOnUiThreadAsync)
    {
        _runOnUiThreadAsync = runOnUiThreadAsync ?? throw new ArgumentNullException(nameof(runOnUiThreadAsync));
    }

    partial void OnVariableNamesChanged(string[] value)
    {
        VariableNamesText = string.Join(", ", value);
        RebuildTruthTableRows();
    }

    partial void OnIntValueTableChanged(int[] value)
    {
        RebuildTruthTableRows();
    }

    partial void OnSelectedFamilyChanged(DiagramFamily value)
    {
        OnPropertyChanged(nameof(SelectedFamilyLabel));
        OnPropertyChanged(nameof(IsBddInputVisible));
        OnPropertyChanged(nameof(IsZddInputVisible));
        OnPropertyChanged(nameof(IsMtbddInputVisible));
        OnPropertyChanged(nameof(ValueTableTitle));
        OnPropertyChanged(nameof(ValueTableBuildButtonLabel));
        _logger.LogInformation("Diagram family changed. Family={Family}", value);

        if (!_suppressFamilyChangeCommand && _familyBeforeChange != value)
        {
            ChangeFamily(_familyBeforeChange, value);
        }
    }

    partial void OnSelectedFamilyChanging(DiagramFamily oldValue, DiagramFamily newValue)
    {
        _familyBeforeChange = oldValue;
    }

    private void ApplyVariableNamesFromCommand()
    {
        try
        {
            ApplyVariableNamesText();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Variable-name apply failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            ErrorMessage = ex.Message;
        }
    }

    private void ApplySetInputFromCommand()
    {
        try
        {
            ApplySetInput();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "ZDD set input apply failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyZddOperationFromCommand(string? operationName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(operationName) || !Enum.TryParse<ZddOperation>(operationName, out var operation))
            {
                throw new ArgumentException("A supported ZDD operation is required.", nameof(operationName));
            }

            ApplyZddOperation(operation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "ZDD operation command failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyVariableNamesText()
    {
        ThrowIfDisposed();
        CancelPendingBuild();

        var beforeVariableNames = (string[])VariableNames.Clone();
        var parsedVariables = ParseVariableNames(VariableNamesText);
        var nextValues = ResizeTruthTable(IntValueTable, parsedVariables.Length);
        _logger.LogInformation(
            "Variable names applied. VariableCount={VariableCount} RowCount={RowCount}",
            parsedVariables.Length,
            nextValues.Length);
        var beforeValues = _lastCommittedValues.Length == nextValues.Length
            ? (int[])_lastCommittedValues.Clone()
            : (int[])nextValues.Clone();
        var cancellation = ReplaceBuildCancellation();

        VariableNames = parsedVariables;
        IntValueTable = nextValues;
        if (SelectedFamily == DiagramFamily.ZDD)
        {
            var setInput = ParseSetInputText(SetInputText);
            var afterVariableNames = MergeVariableNames(parsedVariables, setInput);
            PushSetInputCommand(
                beforeVariableNames,
                _lastCommittedSetInput,
                afterVariableNames,
                setInput,
                cancellation.Token);
            return;
        }

        PushTruthTableCommand(beforeValues, nextValues, cancellation.Token);
    }

    private void ScheduleDebouncedBuild(int[] beforeValues, int[] afterValues)
    {
        var cancellation = ReplaceBuildCancellation();
        var task = RunDebouncedBuildAsync(beforeValues, afterValues, cancellation.Token);
        lock (_buildSync)
        {
            _pendingBuildTask = task;
        }

        _logger.LogTrace(
            "Debounced build scheduled. RowCount={RowCount} DebounceMs={DebounceMs}",
            afterValues.Length,
            _debounceDelay.TotalMilliseconds);
    }

    private async Task RunDebouncedBuildAsync(int[] beforeValues, int[] afterValues, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, cancellationToken).ConfigureAwait(false);
            await _runOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug(
                    "Debounced build started. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
                    SelectedFamily,
                    VariableNames.Length,
                    afterValues.Length);
                PushTruthTableCommand(beforeValues, afterValues, cancellationToken);
                _logger.LogDebug(
                    "Debounced build completed. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
                    SelectedFamily,
                    VariableNames.Length,
                    afterValues.Length);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Debounced build failed. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ExceptionType={ExceptionType}",
                SelectedFamily,
                VariableNames.Length,
                afterValues.Length,
                ex.GetType().Name);
            ErrorMessage = ex.Message;
        }
    }

    private static Task RunInlineAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private void ChangeFamily(DiagramFamily beforeFamily, DiagramFamily afterFamily)
    {
        CancelPendingBuild();
        var cancellation = ReplaceBuildCancellation();
        var needsSetInput = beforeFamily == DiagramFamily.ZDD || afterFamily == DiagramFamily.ZDD;
        var setInput = needsSetInput ? ParseSetInputText(SetInputText) : _lastCommittedSetInput;
        var variableNames = afterFamily == DiagramFamily.ZDD ? MergeVariableNames(VariableNames, setInput) : VariableNames;
        var intValueTable = IntValueTable.Length == 1 << variableNames.Length
            ? IntValueTable
            : ResizeTruthTable(IntValueTable, variableNames.Length);
        if (!ReferenceEquals(intValueTable, IntValueTable))
        {
            IntValueTable = intValueTable;
        }

        IsBuilding = true;
        ErrorMessage = string.Empty;
        try
        {
            var command = new ChangeFamilyCommand(
                _diagramService,
                beforeFamily,
                afterFamily,
                variableNames,
                intValueTable,
                setInput,
                ApplySession,
                cancellation.Token);
            PushCommand(command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Family change failed. BeforeFamily={BeforeFamily} AfterFamily={AfterFamily} ExceptionType={ExceptionType}",
                beforeFamily,
                afterFamily,
                ex.GetType().Name);
            ErrorMessage = ex.Message;
            SetSelectedFamilySilently(beforeFamily);
            throw;
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void PushTruthTableCommand(int[] beforeValues, int[] afterValues, CancellationToken cancellationToken)
    {
        IsBuilding = true;
        ErrorMessage = string.Empty;
        _logger.LogDebug(
            "Truth-table command push started. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            SelectedFamily,
            VariableNames.Length,
            afterValues.Length);

        try
        {
            var command = new ChangeTruthTableCommand(
                _diagramService,
                VariableNames,
                beforeValues,
                afterValues,
                SelectedFamily,
                ApplySession,
                cancellationToken);
            PushCommand(command);
            _logger.LogDebug(
                "Truth-table command push completed. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
                SelectedFamily,
                VariableNames.Length,
                afterValues.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Truth-table command push failed. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ExceptionType={ExceptionType}",
                SelectedFamily,
                VariableNames.Length,
                afterValues.Length,
                ex.GetType().Name);
            ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void PushSetInputCommand(
        string[] beforeVariableNames,
        IReadOnlyList<IReadOnlyList<string>> beforeSetInput,
        string[] afterVariableNames,
        IReadOnlyList<IReadOnlyList<string>> afterSetInput,
        CancellationToken cancellationToken)
    {
        IsBuilding = true;
        ErrorMessage = string.Empty;
        try
        {
            var command = new ChangeSetInputCommand(
                _diagramService,
                beforeVariableNames,
                beforeSetInput,
                afterVariableNames,
                afterSetInput,
                ApplySession,
                cancellationToken);
            PushCommand(command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "ZDD set input command push failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void PushCommand(IUndoableCommand command)
    {
        _commandStack.Push(command);
        RefreshUndoRedoState();
    }

    private void ApplySession(DiagramSession session)
    {
        CurrentSession = session;
        if (session.IntValueTable is not null)
        {
            _lastCommittedValues = (int[])session.IntValueTable.Clone();
            IntValueTable = (int[])session.IntValueTable.Clone();
        }
        else
        {
            if (session.VariableNames.Length <= 10 && IntValueTable.Length != 1 << session.VariableNames.Length)
            {
                var resized = ResizeTruthTable(IntValueTable, session.VariableNames.Length);
                _lastCommittedValues = (int[])resized.Clone();
                IntValueTable = resized;
            }
        }

        if (session.SetInput is not null)
        {
            _lastCommittedSetInput = CloneSetInput(session.SetInput);
            SetInput = _lastCommittedSetInput;
            SetInputText = FormatSetInput(session.SetInput);
        }

        VariableNames = (string[])session.VariableNames.Clone();
        SetSelectedFamilySilently(session.Family);
        _logger.LogDebug(
            "Diagram session applied to workbench. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            session.Family,
            session.VariableNames.Length,
            session.IntValueTable?.Length ?? 0);
    }

    private void RefreshUndoRedoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void SetSelectedFamilySilently(DiagramFamily family)
    {
        _suppressFamilyChangeCommand = true;
        try
        {
            SelectedFamily = family;
        }
        finally
        {
            _suppressFamilyChangeCommand = false;
        }
    }

    private CancellationTokenSource ReplaceBuildCancellation()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _buildCancellation, next);
        previous?.Cancel();
        previous?.Dispose();
        return next;
    }

    private void CancelPendingBuild()
    {
        _buildCancellation?.Cancel();
    }

    private void ValidateTruthTableCell(int index, int value)
    {
        ValidateValueTableCell(index);

        if (value is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "BDD truth-table values must be 0 or 1.");
        }
    }

    private void ValidateValueTableCell(int index)
    {
        if (index < 0 || index >= IntValueTable.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The value-table index is outside the current table.");
        }
    }

    private void RebuildTruthTableRows()
    {
        TruthTableRows.Clear();
        var variableNames = VariableNames;
        var values = IntValueTable;
        for (var i = 0; i < values.Length; i++)
        {
            var variableValues = BuildVariableValues(i, variableNames.Length);
            TruthTableRows.Add(new TruthTableRowViewModel(
                i,
                "#" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FormatAssignment(variableValues, variableNames),
                variableValues,
                values[i]));
        }
    }

    private static string[] ParseVariableNames(string variableNamesText)
    {
        var variables = variableNamesText.Split(
            new[] { ',', ';', ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (variables.Length == 0)
        {
            throw new ArgumentException("At least one variable name is required.", nameof(variableNamesText));
        }

        return variables;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseSetInputText(string setInputText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setInputText);

        var text = setInputText.Trim();
        if (text.StartsWith("{{", StringComparison.Ordinal) && text.EndsWith("}}", StringComparison.Ordinal))
        {
            text = text[1..^1];
        }

        var sets = new List<IReadOnlyList<string>>();
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
            {
                index++;
            }

            if (index >= text.Length)
            {
                break;
            }

            if (text[index] != '{')
            {
                throw new ArgumentException("ZDD set input must use braces, for example {a,b},{c}.", nameof(setInputText));
            }

            var end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                throw new ArgumentException("ZDD set input contains an unterminated set.", nameof(setInputText));
            }

            var body = text[(index + 1)..end];
            var members = body.Split(
                new[] { ',', ';', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            sets.Add(members);
            index = end + 1;
        }

        if (sets.Count == 0)
        {
            throw new ArgumentException("At least one ZDD set is required.", nameof(setInputText));
        }

        return sets;
    }

    private static string[] MergeVariableNames(
        IReadOnlyList<string> currentVariableNames,
        IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        var variables = new List<string>(currentVariableNames);
        var seen = new HashSet<string>(variables, StringComparer.Ordinal);
        foreach (var set in setInput)
        {
            foreach (var member in set)
            {
                if (seen.Add(member))
                {
                    variables.Add(member);
                }
            }
        }

        return variables.ToArray();
    }

    private static string FormatSetInput(IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        return string.Join(",", setInput.Select(set => "{" + string.Join(",", set) + "}"));
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

    private static int[] ResizeTruthTable(int[] currentValues, int variableCount)
    {
        if (variableCount > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(variableCount), "The v0.1 workbench supports up to 10 variables.");
        }

        var nextLength = 1 << variableCount;
        var nextValues = new int[nextLength];
        Array.Copy(currentValues, nextValues, Math.Min(currentValues.Length, nextValues.Length));
        return nextValues;
    }

    private static int[] BuildVariableValues(int rowIndex, int variableCount)
    {
        var variableValues = new int[variableCount];
        for (var variable = 0; variable < variableCount; variable++)
        {
            variableValues[variable] = (rowIndex >> variable) & 1;
        }

        return variableValues;
    }

    private static string FormatAssignment(IReadOnlyList<int> variableValues, IReadOnlyList<string> variableNames)
    {
        var parts = new string[variableNames.Count];
        for (var variable = 0; variable < variableNames.Count; variable++)
        {
            var value = variableValues[variable].ToString(System.Globalization.CultureInfo.InvariantCulture);
            parts[variable] = variableNames[variable] + "=" + value;
        }

        return string.Join(", ", parts);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
