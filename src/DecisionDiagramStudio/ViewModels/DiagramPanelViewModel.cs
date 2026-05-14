using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Manages DOT and SVG state for the diagram display panel.
/// </summary>
public sealed partial class DiagramPanelViewModel : ObservableObject
{
    private readonly IDiagramService _diagramService;
    private readonly IGraphvizService _graphvizService;

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
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
        _graphvizService = graphvizService ?? throw new ArgumentNullException(nameof(graphvizService));
        ToggleReductionCommand = new AsyncRelayCommand(ToggleReductionAsync);
    }

    /// <summary>
    /// Gets the command that toggles between reduced BDD DOT and unreduced BDT DOT.
    /// </summary>
    public IAsyncRelayCommand ToggleReductionCommand { get; }

    /// <summary>
    /// Updates the panel with a newly built session.
    /// </summary>
    /// <param name="session">The session to display.</param>
    /// <param name="ct">A cancellation token for abandoning rendering.</param>
    /// <returns>A task that completes after rendering finishes.</returns>
    public async Task UpdateSessionAsync(DiagramSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        CurrentSession = session;
        IsReduced = true;
        IsBdtButtonVisible = session.Family == DiagramFamily.BDD;
        await RenderDotAsync(session.DotText, ct).ConfigureAwait(false);
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
            return;
        }

        var nextIsReduced = !IsReduced;
        var nextDotText = nextIsReduced
            ? CurrentSession.DotText
            : await _diagramService.GetBdtDotAsync(CurrentSession, CancellationToken.None).ConfigureAwait(false);

        await RenderDotAsync(nextDotText, CancellationToken.None).ConfigureAwait(false);
        IsReduced = nextIsReduced;
    }

    private async Task RenderDotAsync(string dotText, CancellationToken ct)
    {
        IsRendering = true;
        ErrorMessage = string.Empty;

        try
        {
            DotText = dotText;
            SvgContent = await _graphvizService.RenderSvgAsync(dotText, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            IsRendering = false;
        }
    }
}
