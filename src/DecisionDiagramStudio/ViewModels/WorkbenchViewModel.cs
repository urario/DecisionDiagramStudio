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
    public static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300);

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
    private bool _disposed;

    [ObservableProperty]
    private string[] _variableNames = ["a"];

    [ObservableProperty]
    private string _variableNamesText = "a";

    [ObservableProperty]
    private int[] _intValueTable = [0, 1];

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
        RebuildCommand = new RelayCommand(RebuildCurrentSession);
        ChangeTruthTableCellCommand = new RelayCommand<TruthTableCellChange>(change =>
        {
            ArgumentNullException.ThrowIfNull(change);
            ChangeTruthTableCell(change.Index, change.Value);
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
    /// Gets the command that applies the text in the variable-name input.
    /// </summary>
    public IRelayCommand ApplyVariableNamesCommand { get; }

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
    /// Applies one truth-table cell edit and schedules a debounced rebuild.
    /// </summary>
    /// <param name="index">The zero-based truth-table row index.</param>
    /// <param name="value">The new cell value.</param>
    public void ChangeTruthTableCell(int index, int value)
    {
        ThrowIfDisposed();
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
        SelectedFamily = preset.DefaultFamily;

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
        PushTruthTableCommand((int[])_lastCommittedValues.Clone(), (int[])IntValueTable.Clone(), cancellation.Token);
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
        _logger.LogInformation("Diagram family changed. Family={Family}", value);
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

    private void ApplyVariableNamesText()
    {
        ThrowIfDisposed();
        CancelPendingBuild();

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
            _commandStack.Push(command);
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

    private void ApplySession(DiagramSession session)
    {
        CurrentSession = session;
        if (session.IntValueTable is not null)
        {
            _lastCommittedValues = (int[])session.IntValueTable.Clone();
            IntValueTable = (int[])session.IntValueTable.Clone();
        }

        VariableNames = (string[])session.VariableNames.Clone();
        SelectedFamily = session.Family;
        _logger.LogDebug(
            "Diagram session applied to workbench. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            session.Family,
            session.VariableNames.Length,
            session.IntValueTable?.Length ?? 0);
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
        if (index < 0 || index >= IntValueTable.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The truth-table index is outside the current table.");
        }

        if (value is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "BDD truth-table values must be 0 or 1.");
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
