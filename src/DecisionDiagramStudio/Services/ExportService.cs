using DecisionDiagramSharp.Diagnostics;
using DecisionDiagramSharp.Export;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Exports diagram session data to text, clipboard, and files.
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly IGraphvizService _graphvizService;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<ExportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="graphvizService">The service used to render DOT into SVG.</param>
    /// <param name="clipboardService">The service used to copy exported text.</param>
    public ExportService(IGraphvizService graphvizService, IClipboardService clipboardService)
        : this(graphvizService, clipboardService, NullLogger<ExportService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="graphvizService">The service used to render DOT into SVG.</param>
    /// <param name="clipboardService">The service used to copy exported text.</param>
    /// <param name="logger">The logger used for export diagnostics.</param>
    public ExportService(
        IGraphvizService graphvizService,
        IClipboardService clipboardService,
        ILogger<ExportService> logger)
    {
        _graphvizService = graphvizService ?? throw new ArgumentNullException(nameof(graphvizService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> CopyTruthTableAsync(DiagramSession session, ExportTableFormat format, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ct.ThrowIfCancellationRequested();

        var text = CreateTruthTableText(session, format);
        await _clipboardService.SetTextAsync(text, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Truth table copied. Family={Family} Format={Format} Length={Length}",
            session.Family,
            format,
            text.Length);
        return text;
    }

    /// <inheritdoc />
    public async Task SaveDotAsync(DiagramSession session, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        try
        {
            await File.WriteAllTextAsync(path, session.DotText, ct).ConfigureAwait(false);
            _logger.LogInformation("DOT file saved. Family={Family} Path={Path}", session.Family, path);
        }
        catch (IOException ex)
        {
            _logger.LogError(
                "DOT file save failed. Family={Family} Path={Path} ExceptionType={ExceptionType}",
                session.Family,
                path,
                ex.GetType().Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveSvgAsync(DiagramSession session, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        try
        {
            var svg = await _graphvizService.RenderSvgAsync(session.DotText, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(path, svg, ct).ConfigureAwait(false);
            _logger.LogInformation("SVG file saved. Family={Family} Path={Path}", session.Family, path);
        }
        catch (IOException ex)
        {
            _logger.LogError(
                "SVG file save failed. Family={Family} Path={Path} ExceptionType={ExceptionType}",
                session.Family,
                path,
                ex.GetType().Name);
            throw;
        }
    }

    internal static string CreateTruthTableText(DiagramSession session, ExportTableFormat format)
    {
        if (session.Family != DiagramFamily.BDD || session.IntValueTable is null)
        {
            throw new NotSupportedException("Truth table export is currently supported only for BDD sessions.");
        }

        var table = BuildTruthTableModel(session);
        return format switch
        {
            ExportTableFormat.Csv => CsvTableExporter.Export(table),
            ExportTableFormat.Markdown => MarkdownTableExporter.Export(table),
            ExportTableFormat.AsciiDoc => AsciiDocTableExporter.Export(table),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format."),
        };
    }

    private static TableModel BuildTruthTableModel(DiagramSession session)
    {
        var variableNames = session.VariableNames;
        var values = session.IntValueTable!;
        var columns = variableNames.Concat(["f"]).ToArray();
        var rows = new List<TableRow>(values.Length);

        for (var rowIndex = 0; rowIndex < values.Length; rowIndex++)
        {
            var cells = new string[variableNames.Length + 1];
            for (var variable = 0; variable < variableNames.Length; variable++)
            {
                cells[variable] = (((rowIndex >> variable) & 1) == 0 ? 0 : 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            cells[^1] = values[rowIndex].ToString(System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(new TableRow(cells));
        }

        return new TableModel("BDD Truth Table", columns, rows);
    }
}
