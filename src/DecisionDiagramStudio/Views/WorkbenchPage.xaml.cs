using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace DecisionDiagramStudio.Views;

/// <summary>
/// Interaction logic for the BDD workbench page.
/// </summary>
public sealed partial class WorkbenchPage : Page
{
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly SemaphoreSlim _documentNavigationGate = new(1, 1);
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

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _logger.LogInformation("Workbench page loaded.");
        ViewModel.PropertyChanged += OnWorkbenchPropertyChanged;
        DiagramViewModel.PropertyChanged += OnDiagramPropertyChanged;

        UpdateReductionButton();
        UpdateRenderingState();
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

                await NavigateDiagramWebViewToStringAsync(CreateSvgDocument(svgContent));
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
        ReductionToggleButton.Content = DiagramViewModel.IsReduced ? "削減前 (BDT)" : "削減後 (BDD)";
    }

    private void UpdateRenderingState()
    {
        RenderingRing.Visibility = DiagramViewModel.IsRendering ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        if (StatisticsViewModel.Session is null)
        {
            StatusInfoBar.Message = "No diagram has been built yet.";
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

    private static string CreateSvgDocument(string svgContent)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        const string Style = "html,body{height:100%;margin:0;background:#f8f8f8;color:#1f1f1f;font-family:Segoe UI,Arial,sans-serif;overflow:hidden;}" +
            ".surface{position:relative;height:100%;overflow:hidden;box-sizing:border-box;}" +
            ".viewport{position:absolute;inset:0;overflow:hidden;cursor:grab;touch-action:none;}" +
            ".viewport.is-panning{cursor:grabbing;}" +
            ".diagram-layer{position:absolute;left:0;top:0;transform-origin:0 0;will-change:transform;}" +
            ".diagram-layer svg{display:block;max-width:none;height:auto;}" +
            ".controls{position:absolute;right:12px;top:12px;z-index:2;display:flex;gap:8px;}" +
            ".controls button{border:1px solid #c7c7c7;border-radius:4px;background:#ffffff;color:#1f1f1f;font:12px Segoe UI,Arial,sans-serif;padding:5px 10px;box-shadow:0 1px 3px rgba(0,0,0,0.12);}" +
            ".controls button:hover{background:#f0f0f0;}" +
            ".placeholder{height:100%;display:flex;align-items:center;justify-content:center;color:#666;font-size:14px;}";
        const string Script = """
(() => {
    const viewport = document.getElementById('diagramViewport');
    const layer = document.getElementById('diagramLayer');
    const resetButton = document.getElementById('resetZoomButton');
    if (!viewport || !layer) {
        return;
    }

    const state = {
        scale: 1,
        x: 0,
        y: 0,
        pointerId: null,
        startClientX: 0,
        startClientY: 0,
        startX: 0,
        startY: 0
    };
    const minScale = 0.2;
    const maxScale = 8;

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function render() {
        layer.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.scale})`;
    }

    function fitDiagram() {
        const svg = layer.querySelector('svg');
        const viewportRect = viewport.getBoundingClientRect();
        if (!svg || viewportRect.width <= 0 || viewportRect.height <= 0) {
            return;
        }

        layer.style.transform = 'none';
        const svgRect = svg.getBoundingClientRect();
        if (svgRect.width <= 0 || svgRect.height <= 0) {
            return;
        }

        const padding = 48;
        const scaleX = Math.max((viewportRect.width - padding) / svgRect.width, minScale);
        const scaleY = Math.max((viewportRect.height - padding) / svgRect.height, minScale);
        state.scale = clamp(Math.min(scaleX, scaleY, 1), minScale, maxScale);
        state.x = (viewportRect.width - (svgRect.width * state.scale)) / 2;
        state.y = (viewportRect.height - (svgRect.height * state.scale)) / 2;
        render();
    }

    function zoomAt(clientX, clientY, scaleFactor) {
        const viewportRect = viewport.getBoundingClientRect();
        const pointerX = clientX - viewportRect.left;
        const pointerY = clientY - viewportRect.top;
        const diagramX = (pointerX - state.x) / state.scale;
        const diagramY = (pointerY - state.y) / state.scale;
        const nextScale = clamp(state.scale * scaleFactor, minScale, maxScale);
        state.x = pointerX - (diagramX * nextScale);
        state.y = pointerY - (diagramY * nextScale);
        state.scale = nextScale;
        render();
    }

    viewport.addEventListener('wheel', event => {
        event.preventDefault();
        const scaleFactor = Math.exp(-event.deltaY * 0.001);
        zoomAt(event.clientX, event.clientY, scaleFactor);
    }, { passive: false });

    viewport.addEventListener('pointerdown', event => {
        if (event.button !== 0) {
            return;
        }

        state.pointerId = event.pointerId;
        state.startClientX = event.clientX;
        state.startClientY = event.clientY;
        state.startX = state.x;
        state.startY = state.y;
        viewport.classList.add('is-panning');
        viewport.setPointerCapture(event.pointerId);
    });

    viewport.addEventListener('pointermove', event => {
        if (state.pointerId !== event.pointerId) {
            return;
        }

        state.x = state.startX + event.clientX - state.startClientX;
        state.y = state.startY + event.clientY - state.startClientY;
        render();
    });

    function endPan(event) {
        if (state.pointerId !== event.pointerId) {
            return;
        }

        state.pointerId = null;
        viewport.classList.remove('is-panning');
        if (viewport.hasPointerCapture(event.pointerId)) {
            viewport.releasePointerCapture(event.pointerId);
        }
    }

    viewport.addEventListener('pointerup', endPan);
    viewport.addEventListener('pointercancel', endPan);
    viewport.addEventListener('dblclick', fitDiagram);
    resetButton?.addEventListener('click', fitDiagram);
    window.addEventListener('resize', fitDiagram);
    requestAnimationFrame(fitDiagram);
})();
""";
        var csp = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'nonce-" + nonce + "'; object-src 'none'; base-uri 'none'";
        var body = string.IsNullOrWhiteSpace(svgContent)
            ? "<div class=\"placeholder\">Build a diagram to preview SVG.</div>"
            : "<div class=\"controls\"><button id=\"resetZoomButton\" type=\"button\">Reset zoom</button></div>" +
                "<div id=\"diagramViewport\" class=\"viewport\"><div id=\"diagramLayer\" class=\"diagram-layer\">" +
                svgContent +
                "</div></div>";

        return "<!doctype html><html><head><meta http-equiv=\"Content-Security-Policy\" content=\"" +
            WebUtility.HtmlEncode(csp) +
            "\"><style>" +
            Style +
            "</style></head><body><div class=\"surface\">" +
            body +
            "</div><script nonce=\"" +
            nonce +
            "\">" +
            Script +
            "</script></body></html>";
    }
}
