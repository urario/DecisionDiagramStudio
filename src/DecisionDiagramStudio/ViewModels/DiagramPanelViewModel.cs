using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Manages DOT and SVG state for the diagram display panel.
/// </summary>
public sealed partial class DiagramPanelViewModel : ObservableObject
{
    private readonly IDiagramService _diagramService;
    private readonly IGraphvizService _graphvizService;
    private readonly ILogger<DiagramPanelViewModel> _logger;
    private Func<Action, Task> _runOnUiThreadAsync = RunInlineAsync;

    [ObservableProperty]
    private DiagramSession? _currentSession;

    [ObservableProperty]
    private string _svgContent = string.Empty;

    [ObservableProperty]
    private string _dotText = string.Empty;

    [ObservableProperty]
    private bool _isReduced = true;

    [ObservableProperty]
    private bool _isBdtButtonVisible;

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramPanelViewModel"/> class.
    /// </summary>
    /// <param name="diagramService">The service used to generate unreduced BDT DOT.</param>
    /// <param name="graphvizService">The service used to render DOT into SVG.</param>
    public DiagramPanelViewModel(IDiagramService diagramService, IGraphvizService graphvizService)
        : this(diagramService, graphvizService, NullLogger<DiagramPanelViewModel>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramPanelViewModel"/> class.
    /// </summary>
    /// <param name="diagramService">The service used to generate unreduced BDT DOT.</param>
    /// <param name="graphvizService">The service used to render DOT into SVG.</param>
    /// <param name="logger">The logger used for diagram-panel diagnostics.</param>
    public DiagramPanelViewModel(
        IDiagramService diagramService,
        IGraphvizService graphvizService,
        ILogger<DiagramPanelViewModel> logger)
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        _graphvizService = graphvizService ?? throw new ArgumentNullException(nameof(graphvizService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ToggleReductionCommand = new AsyncRelayCommand(ToggleReductionAsync);
    }

    /// <summary>
    /// Gets the command that toggles between reduced BDD DOT and unreduced BDT DOT.
    /// </summary>
    public IAsyncRelayCommand ToggleReductionCommand { get; }

    /// <summary>
    /// Sets the dispatcher used to return render results to the UI thread before mutating bound state.
    /// </summary>
    /// <param name="runOnUiThreadAsync">The dispatcher callback.</param>
    public void SetUiThreadDispatcher(Func<Action, Task> runOnUiThreadAsync)
    {
        _runOnUiThreadAsync = runOnUiThreadAsync ?? throw new ArgumentNullException(nameof(runOnUiThreadAsync));
    }

    /// <summary>
    /// Updates the panel with a newly built session.
    /// </summary>
    /// <param name="session">The session to display.</param>
    /// <param name="ct">A cancellation token for abandoning rendering.</param>
    /// <returns>A task that completes after rendering finishes.</returns>
    public async Task UpdateSessionAsync(DiagramSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _runOnUiThreadAsync(() =>
        {
            CurrentSession = session;
            IsReduced = true;
            IsBdtButtonVisible = session.Family == DiagramFamily.BDD;
            _logger.LogDebug(
                "Diagram panel received a session. Family={Family} VariableCount={VariableCount} DotLength={DotLength}",
                session.Family,
                session.VariableNames.Length,
                session.DotText.Length);
        }).ConfigureAwait(false);
        await RenderDotAsync(session.DotText, ct);
    }

    /// <summary>
    /// Toggles between reduced BDD display and unreduced BDT display for BDD sessions.
    /// </summary>
    /// <returns>A task that completes after the target DOT has been rendered.</returns>
    public async Task ToggleReductionAsync()
    {
        if (CurrentSession is null || CurrentSession.Family != DiagramFamily.BDD)
        {
            IsBdtButtonVisible = false;
            _logger.LogTrace("Reduction toggle ignored because no BDD session is active.");
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var nextIsReduced = !IsReduced;
            var nextDotText = nextIsReduced
                ? CurrentSession.DotText
                : await _diagramService.GetBdtDotAsync(CurrentSession, CancellationToken.None);

            _logger.LogInformation(
                "Diagram reduction mode toggled. IsReduced={IsReduced} VariableCount={VariableCount}",
                nextIsReduced,
                CurrentSession.VariableNames.Length);
            await RenderDotAsync(nextDotText, CancellationToken.None);
            await _runOnUiThreadAsync(() => IsReduced = nextIsReduced);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Diagram reduction toggle failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            await _runOnUiThreadAsync(() => ErrorMessage = ex.Message);
            throw;
        }
    }

    private async Task RenderDotAsync(string dotText, CancellationToken ct)
    {
        try
        {
            await _runOnUiThreadAsync(() =>
            {
                IsRendering = true;
                ErrorMessage = string.Empty;
                DotText = dotText;
                _logger.LogDebug("Diagram DOT render started. DotLength={DotLength}", dotText.Length);
            }).ConfigureAwait(false);

            var svgContent = await _graphvizService.RenderSvgAsync(dotText, ct).ConfigureAwait(false);
            await _runOnUiThreadAsync(() =>
            {
                SvgContent = svgContent;
                _logger.LogDebug(
                    "Diagram DOT render completed. DotLength={DotLength} SvgLength={SvgLength}",
                    dotText.Length,
                    SvgContent.Length);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _runOnUiThreadAsync(() =>
            {
                _logger.LogError(
                    "Diagram DOT render failed. DotLength={DotLength} ExceptionType={ExceptionType}",
                    dotText.Length,
                    ex.GetType().Name);
                ErrorMessage = ex.Message;
            }).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await _runOnUiThreadAsync(() => IsRendering = false).ConfigureAwait(false);
        }
    }

    private static Task RunInlineAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
