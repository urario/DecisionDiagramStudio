using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DecisionDiagramStudio.Commands;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

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
    private readonly TimeSpan _debounceDelay;
    private readonly object _buildSync = new();
    private CancellationTokenSource? _buildCancellation;
    private Task _pendingBuildTask = Task.CompletedTask;
    private int[] _lastCommittedValues;
    private bool _disposed;

    [ObservableProperty]
    private string[] _variableNames = ["a"];

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
        : this(diagramService, presetService, commandStack, DefaultDebounceDelay)
    {
    }

    internal WorkbenchViewModel(
        IDiagramService diagramService,
        IPresetService presetService,
        CommandStack commandStack,
        TimeSpan debounceDelay)
    {
        if (debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceDelay), "The debounce delay cannot be negative.");
        }

        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));
        _commandStack = commandStack ?? throw new ArgumentNullException(nameof(commandStack));
        _debounceDelay = debounceDelay;
        _lastCommittedValues = (int[])IntValueTable.Clone();

        SelectPresetCommand = new RelayCommand<string?>(SelectPreset);
        ChangeTruthTableCellCommand = new RelayCommand<TruthTableCellChange>(change =>
        {
            ArgumentNullException.ThrowIfNull(change);
            ChangeTruthTableCell(change.Index, change.Value);
        });
    }

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
            return;
        }

        nextValues[index] = value;
        IntValueTable = nextValues;
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

    private void ScheduleDebouncedBuild(int[] beforeValues, int[] afterValues)
    {
        var cancellation = ReplaceBuildCancellation();
        var task = RunDebouncedBuildAsync(beforeValues, afterValues, cancellation.Token);
        lock (_buildSync)
        {
            _pendingBuildTask = task;
        }
    }

    private async Task RunDebouncedBuildAsync(int[] beforeValues, int[] afterValues, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, cancellationToken).ConfigureAwait(false);
            PushTruthTableCommand(beforeValues, afterValues, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void PushTruthTableCommand(int[] beforeValues, int[] afterValues, CancellationToken cancellationToken)
    {
        IsBuilding = true;
        ErrorMessage = string.Empty;

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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
