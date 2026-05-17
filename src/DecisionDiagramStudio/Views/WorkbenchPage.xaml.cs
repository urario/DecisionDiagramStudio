using System.ComponentModel;
using System.Globalization;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using DecisionDiagramStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DecisionDiagramStudio.Views;

/// <summary>
/// Interaction logic for the BDD workbench page.
/// </summary>
public sealed partial class WorkbenchPage : Page
{
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly SemaphoreSlim _documentNavigationGate = new(1, 1);
    private readonly ISvgWebViewDocumentSource _svgDocumentSource;
    private readonly IExportService _exportService;
    private readonly ILogger<WorkbenchPage> _logger;
    private CancellationTokenSource? _renderCancellation;
    private Task? _webViewInitializationTask;
    private bool _isLoaded;
    private bool _isWebViewReady;
    private long _renderRequestVersion;
    private long _documentRequestVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkbenchPage"/> class.
    /// </summary>
    public WorkbenchPage()
    {
        ViewModel = App.Services.GetRequiredService<WorkbenchViewModel>();
        DiagramViewModel = App.Services.GetRequiredService<DiagramPanelViewModel>();
        StatisticsViewModel = App.Services.GetRequiredService<StatisticsViewModel>();
        ExplanationViewModel = App.Services.GetRequiredService<ExplanationViewModel>();
        _svgDocumentSource = App.Services.GetRequiredService<ISvgWebViewDocumentSource>();
        _exportService = App.Services.GetRequiredService<IExportService>();
        _logger = App.Services.GetRequiredService<ILogger<WorkbenchPage>>();

        InitializeComponent();
        ViewModel.SetUiThreadDispatcher(RunOnDispatcherAsync);
        DiagramViewModel.SetUiThreadDispatcher(RunOnDispatcherAsync);
        DataContext = ViewModel;
    }

    /// <summary>
    /// Gets the workbench input view model.
    /// </summary>
    public WorkbenchViewModel ViewModel { get; }

    /// <summary>
    /// Gets the diagram panel view model.
    /// </summary>
    public DiagramPanelViewModel DiagramViewModel { get; }

    /// <summary>
    /// Gets the statistics view model.
    /// </summary>
    public StatisticsViewModel StatisticsViewModel { get; }

    /// <summary>
    /// Gets the explanation view model updated from validated WebView2 node-click messages.
    /// </summary>
    public ExplanationViewModel ExplanationViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _logger.LogInformation("Workbench page loaded.");
        ViewModel.PropertyChanged += OnWorkbenchPropertyChanged;
        DiagramViewModel.PropertyChanged += OnDiagramPropertyChanged;

        UpdateReductionButton();
        UpdateRenderingState();
        UpdateFamilyInputVisibility();
        UpdateStatus();
        UpdateErrorInfoBar();
        await UpdateDiagramDocumentSafelyAsync(DiagramViewModel.SvgContent);

        if (ViewModel.CurrentSession is null)
        {
            try
            {
                ViewModel.RebuildCurrentSession();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    "Initial workbench rebuild failed. ExceptionType={ExceptionType}",
                    ex.GetType().Name);
                ShowError(ex.Message);
            }
        }
        else
        {
            QueueRenderSession(ViewModel.CurrentSession);
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _logger.LogInformation("Workbench page unloaded.");
        ViewModel.PropertyChanged -= OnWorkbenchPropertyChanged;
        DiagramViewModel.PropertyChanged -= OnDiagramPropertyChanged;
        UnwireDiagramWebViewEvents();
        CancelRender();
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string presetId })
        {
            return;
        }

        try
        {
            _logger.LogInformation("Preset button clicked.");
            ViewModel.SelectPreset(presetId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Preset button handling failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            ShowError(ex.Message);
        }
    }

    private void TruthValueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TruthTableRowViewModel row })
        {
            return;
        }

        var nextValue = row.Value == 1 ? 0 : 1;
        try
        {
            _logger.LogDebug("Truth-table value button clicked. RowIndex={RowIndex}", row.Index);
            ViewModel.ChangeTruthTableCell(row.Index, nextValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Truth-table toggle handling failed. RowIndex={RowIndex} ExceptionType={ExceptionType}",
                row.Index,
                ex.GetType().Name);
            ShowError(ex.Message);
        }
    }

    private void FamilyRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string familyName }
            || !Enum.TryParse<DiagramFamily>(familyName, out var family)
            || ViewModel.SelectedFamily == family)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Family radio button checked. Family={Family}", family);
            ViewModel.SelectedFamily = family;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Family radio button handling failed. Family={Family} ExceptionType={ExceptionType}",
                family,
                ex.GetType().Name);
            ShowError(ex.Message);
        }
    }

    private void ValueTableTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: TruthTableRowViewModel row } textBox)
        {
            return;
        }

        if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            ShowError("Value-table cells must contain integer values.");
            textBox.Text = row.ValueText;
            return;
        }

        try
        {
            _logger.LogDebug("Value-table cell edited. RowIndex={RowIndex}", row.Index);
            ViewModel.ChangeValueTableCell(row.Index, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Value-table edit handling failed. RowIndex={RowIndex} ExceptionType={ExceptionType}",
                row.Index,
                ex.GetType().Name);
            ShowError(ex.Message);
            textBox.Text = row.ValueText;
        }
    }

    private async void CopyTruthTableCsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentSession is null)
        {
            return;
        }

        try
        {
            await _exportService.CopyTruthTableAsync(ViewModel.CurrentSession, ExportTableFormat.Csv, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("Truth-table copy failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            ShowError(ex.Message);
        }
    }

    private async void SaveSvgButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveSessionFileAsync("SVG diagram", ".svg", "diagram.svg", path =>
            _exportService.SaveSvgAsync(ViewModel.CurrentSession!, path, CancellationToken.None));
    }

    private async void SaveDotButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveSessionFileAsync("DOT graph", ".dot", "diagram.dot", path =>
            _exportService.SaveDotAsync(ViewModel.CurrentSession!, path, CancellationToken.None));
    }

    private async Task SaveSessionFileAsync(string label, string extension, string suggestedFileName, Func<string, Task> saveAsync)
    {
        if (ViewModel.CurrentSession is null)
        {
            return;
        }

        try
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = suggestedFileName,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add(label, [extension]);
            if (App.MainAppWindow is not null)
            {
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
            }

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await saveAsync(file.Path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("File export failed. Extension={Extension} ExceptionType={ExceptionType}", extension, ex.GetType().Name);
            ShowError(ex.Message);
        }
    }

    private Task RunOnDispatcherAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("The UI dispatcher rejected the work item."));
        }

        return completion.Task;
    }

    private Task RunOnDispatcherAsync(Func<Task> actionAsync)
    {
        ArgumentNullException.ThrowIfNull(actionAsync);

        if (DispatcherQueue.HasThreadAccess)
        {
            return actionAsync();
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await actionAsync();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("The UI dispatcher rejected the async work item."));
        }

        return completion.Task;
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => OnWorkbenchPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(WorkbenchViewModel.CurrentSession) && ViewModel.CurrentSession is { } session)
        {
            QueueRenderSession(session);
        }

        if (e.PropertyName == nameof(WorkbenchViewModel.ErrorMessage))
        {
            UpdateErrorInfoBar();
        }

        if (e.PropertyName is nameof(WorkbenchViewModel.SelectedFamily) or nameof(WorkbenchViewModel.IsBddInputVisible) or nameof(WorkbenchViewModel.IsZddInputVisible) or nameof(WorkbenchViewModel.IsMtbddInputVisible))
        {
            UpdateFamilyInputVisibility();
        }
    }

    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => OnDiagramPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(DiagramPanelViewModel.SvgContent))
        {
            QueueDiagramDocumentUpdate(DiagramViewModel.SvgContent);
        }

        if (e.PropertyName is nameof(DiagramPanelViewModel.IsReduced) or nameof(DiagramPanelViewModel.IsBdtButtonVisible))
        {
            UpdateReductionButton();
        }

        if (e.PropertyName == nameof(DiagramPanelViewModel.IsRendering))
        {
            UpdateRenderingState();
        }

        if (e.PropertyName == nameof(DiagramPanelViewModel.ErrorMessage))
        {
            UpdateErrorInfoBar();
        }
    }

    private void QueueRenderSession(DiagramSession session)
    {
        var version = Interlocked.Increment(ref _renderRequestVersion);
        _ = RenderSessionAsync(session, version);
    }

    private async Task RenderSessionAsync(DiagramSession session, long version)
    {
        if (!_isLoaded)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _renderCancellation, cancellation);
        previousCancellation?.Cancel();
        var cancellationToken = cancellation.Token;

        await _renderGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        var isCurrent = false;

        try
        {
            cancellationToken = cancellation.Token;
            var shouldRender = false;
            await RunOnDispatcherAsync(() =>
            {
                if (!IsCurrentRender(version, cancellation))
                {
                    return;
                }

                isCurrent = true;
                shouldRender = true;
                StatisticsViewModel.Session = session;
                UpdateStatus();
                _logger.LogDebug(
                    "Session render started. RenderVersion={RenderVersion} Family={Family} VariableCount={VariableCount}",
                    version,
                    session.Family,
                    session.VariableNames.Length);
            }).ConfigureAwait(false);

            if (!shouldRender)
            {
                return;
            }

            await DiagramViewModel.UpdateSessionAsync(session, cancellationToken).ConfigureAwait(false);
            await RunOnDispatcherAsync(() =>
            {
                _logger.LogDebug(
                    "Session render completed. RenderVersion={RenderVersion} Family={Family} VariableCount={VariableCount}",
                    version,
                    session.Family,
                    session.VariableNames.Length);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await RunOnDispatcherAsync(() =>
            {
                _logger.LogError(
                    "Session render failed. RenderVersion={RenderVersion} ExceptionType={ExceptionType}",
                    version,
                    ex.GetType().Name);
                ShowError(ex.Message);
            }).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_renderCancellation, cancellation))
            {
                _renderCancellation = null;
            }

            cancellation.Dispose();
            _renderGate.Release();

            if (isCurrent)
            {
                await RunOnDispatcherAsync(() =>
                {
                    UpdateReductionButton();
                    UpdateRenderingState();
                    UpdateErrorInfoBar();
                }).ConfigureAwait(false);
            }
        }
    }

    private bool IsCurrentRender(long version, CancellationTokenSource cancellation)
    {
        return _isLoaded
            && version == Volatile.Read(ref _renderRequestVersion)
            && ReferenceEquals(_renderCancellation, cancellation)
            && !cancellation.IsCancellationRequested;
    }

    private void QueueDiagramDocumentUpdate(string svgContent)
    {
        var version = Interlocked.Increment(ref _documentRequestVersion);
        _ = UpdateDiagramDocumentSafelyAsync(svgContent, version);
    }

    private async Task UpdateDiagramDocumentSafelyAsync(string svgContent)
    {
        var version = Interlocked.Increment(ref _documentRequestVersion);
        await UpdateDiagramDocumentSafelyAsync(svgContent, version);
    }

    private async Task UpdateDiagramDocumentSafelyAsync(string svgContent, long version)
    {
        try
        {
            await UpdateDiagramDocumentAsync(svgContent, version);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RunOnDispatcherAsync(() =>
            {
                _logger.LogError(
                    "Diagram document update failed. ExceptionType={ExceptionType}",
                    ex.GetType().Name);
                ShowError(ex.Message);
            }).ConfigureAwait(false);
        }
    }

    private async Task UpdateDiagramDocumentAsync(string svgContent, long version)
    {
        if (!_isLoaded)
        {
            return;
        }

        await _documentNavigationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var shouldNavigate = false;
            await RunOnDispatcherAsync(() =>
            {
                shouldNavigate = _isLoaded && version == Volatile.Read(ref _documentRequestVersion);
            }).ConfigureAwait(false);

            if (!shouldNavigate)
            {
                return;
            }

            await RunOnDispatcherAsync(async () =>
            {
                await EnsureDiagramWebViewAsync();
                if (!_isLoaded || version != Volatile.Read(ref _documentRequestVersion))
                {
                    return;
                }

                await NavigateDiagramWebViewToStringAsync(_svgDocumentSource.CreateDocument(svgContent));
            }).ConfigureAwait(false);
        }
        finally
        {
            _documentNavigationGate.Release();
        }
    }

    private Task EnsureDiagramWebViewAsync()
    {
        if (_isWebViewReady)
        {
            return Task.CompletedTask;
        }

        _webViewInitializationTask ??= InitializeDiagramWebViewAsync();
        return _webViewInitializationTask;
    }

    private async Task InitializeDiagramWebViewAsync()
    {
        try
        {
            await DiagramWebView.EnsureCoreWebView2Async();
            WireDiagramWebViewEvents();
            _isWebViewReady = true;
            _logger.LogDebug("Diagram WebView2 initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Diagram WebView2 initialization failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            _webViewInitializationTask = null;
            throw;
        }
    }

    private void WireDiagramWebViewEvents()
    {
        var coreWebView = DiagramWebView.CoreWebView2;
        coreWebView.Settings.IsWebMessageEnabled = true;
        coreWebView.Settings.AreDefaultScriptDialogsEnabled = false;
        coreWebView.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        coreWebView.Settings.AreDevToolsEnabled = false;
#endif
        coreWebView.WebMessageReceived -= OnDiagramWebMessageReceived;
        coreWebView.WebMessageReceived += OnDiagramWebMessageReceived;
    }

    private void UnwireDiagramWebViewEvents()
    {
        if (!_isWebViewReady)
        {
            return;
        }

        DiagramWebView.CoreWebView2.WebMessageReceived -= OnDiagramWebMessageReceived;
        _isWebViewReady = false;
        _webViewInitializationTask = null;
    }

    private void OnDiagramWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (ViewModel.CurrentSession is not { } session)
        {
            return;
        }

        if (ExplanationViewModel.TrySelectNodeFromWebMessage(args.WebMessageAsJson, session))
        {
            _logger.LogDebug("Diagram node-click message accepted.");
            return;
        }

        _logger.LogWarning("Diagram WebView2 message failed schema validation.");
    }

    private async Task NavigateDiagramWebViewToStringAsync(string html)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TypedEventHandler<WebView2, CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            DiagramWebView.NavigationCompleted -= handler;
            if (args.IsSuccess)
            {
                completion.TrySetResult();
            }
            else
            {
                _logger.LogWarning(
                    "WebView2 navigation failed. WebErrorStatus={WebErrorStatus}",
                    args.WebErrorStatus);
                completion.TrySetException(new InvalidOperationException("WebView2 navigation failed: " + args.WebErrorStatus.ToString()));
            }
        };

        DiagramWebView.NavigationCompleted += handler;
        try
        {
            DiagramWebView.NavigateToString(html);
            await completion.Task;
        }
        finally
        {
            DiagramWebView.NavigationCompleted -= handler;
        }
    }

    private void UpdateReductionButton()
    {
        ReductionToggleButton.Visibility = DiagramViewModel.IsBdtButtonVisible ? Visibility.Visible : Visibility.Collapsed;
        ReductionToggleButton.Content = DiagramViewModel.IsReduced ? "Show BDT" : "Show BDD";
    }

    private void UpdateRenderingState()
    {
        RenderingRing.Visibility = DiagramViewModel.IsRendering ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateFamilyInputVisibility()
    {
        BddTruthTablePanel.Visibility = ViewModel.IsBddInputVisible ? Visibility.Visible : Visibility.Collapsed;
        ZddSetInputPanel.Visibility = ViewModel.IsZddInputVisible ? Visibility.Visible : Visibility.Collapsed;
        MtbddValueTablePanel.Visibility = ViewModel.IsMtbddInputVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        if (StatisticsViewModel.Session is null)
        {
            StatusInfoBar.Message = "No diagram has been built yet.";
            return;
        }

        if (StatisticsViewModel.Session.Family == DiagramFamily.ZDD)
        {
            StatusInfoBar.Message =
                "Variables: " + StatisticsViewModel.VariableCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | ZDD nodes: " + StatisticsViewModel.ReachableNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | Terminals: " + StatisticsViewModel.ReachableTerminalCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | Total nodes: " + StatisticsViewModel.TotalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | Sets: " + StatisticsViewModel.SetCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        if (StatisticsViewModel.Session.Family is DiagramFamily.MTBDD or DiagramFamily.ZMTBDD)
        {
            StatusInfoBar.Message =
                "Variables: " + StatisticsViewModel.VariableCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | " + StatisticsViewModel.Session.Family.ToString() + " nodes: " + StatisticsViewModel.ReachableNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | Values: " + StatisticsViewModel.ReachableTerminalCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " | Total nodes: " + StatisticsViewModel.TotalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        StatusInfoBar.Message =
            "Variables: " + StatisticsViewModel.VariableCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " | BDD nodes: " + StatisticsViewModel.ReachableNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " | Terminals: " + StatisticsViewModel.ReachableTerminalCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " | Total nodes: " + StatisticsViewModel.TotalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " | BDT nodes: " + StatisticsViewModel.BdtNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " | Reduced: " + StatisticsViewModel.ReducedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void UpdateErrorInfoBar()
    {
        var message = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage)
            ? ViewModel.ErrorMessage
            : DiagramViewModel.ErrorMessage;

        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    private void CancelRender()
    {
        _renderCancellation?.Cancel();
        _renderCancellation?.Dispose();
        _renderCancellation = null;
        _logger.LogTrace("Pending diagram render canceled.");
    }
}
