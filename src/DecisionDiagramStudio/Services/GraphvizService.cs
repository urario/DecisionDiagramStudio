using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Renders DOT text to SVG by invoking Graphviz dot.
/// </summary>
public sealed class GraphvizService : IGraphvizService
{
    /// <summary>
    /// The default Graphviz render timeout.
    /// </summary>
    public static readonly TimeSpan DefaultRenderTimeout = TimeSpan.FromSeconds(30);

    private const string DefaultDotCommand = "dot";
    private const int FastRenderXSpacing = 140;
    private const int FastRenderYSpacing = 96;
    private const int FastRenderMargin = 56;

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Regex AppDotNodeRegex = new(
        "^\\s*(?<id>(?:n|bdt)\\d+)\\s+\\[label=\"(?<label>(?:\\\\.|[^\"])*)\"(?<attrs>[^\\]]*)\\];\\s*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex AppDotEdgeRegex = new(
        "^\\s*(?<from>(?:n|bdt)\\d+)\\s*->\\s*(?<to>(?:n|bdt)\\d+)\\s+\\[(?<attrs>[^\\]]*)\\];\\s*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex DotLabelAttributeRegex = new(
        "label=\"(?<label>(?:\\\\.|[^\"])*)\"",
        RegexOptions.CultureInvariant);
    private static readonly Regex VariableNameRegex = new(
        "^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.CultureInvariant);

    private readonly string _dotExecutablePath;
    private readonly TimeSpan _renderTimeout;
    private readonly ILogger<GraphvizService> _logger;
    private readonly object _resolveSync = new();
    private string? _resolvedDotExecutablePath;
    private bool _hasResolvedDotExecutablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class.
    /// </summary>
    public GraphvizService()
        : this(string.Empty, DefaultRenderTimeout, NullLogger<GraphvizService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for render diagnostics.</param>
    public GraphvizService(ILogger<GraphvizService> logger)
        : this(string.Empty, DefaultRenderTimeout, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class from session options.
    /// </summary>
    /// <param name="options">The session options containing the optional Graphviz path.</param>
    public GraphvizService(SessionOptions options)
        : this(GetGraphvizPath(options), DefaultRenderTimeout, NullLogger<GraphvizService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class from session options.
    /// </summary>
    /// <param name="options">The session options containing the optional Graphviz path.</param>
    /// <param name="logger">The logger used for render diagnostics.</param>
    public GraphvizService(SessionOptions options, ILogger<GraphvizService> logger)
        : this(GetGraphvizPath(options), DefaultRenderTimeout, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class.
    /// </summary>
    /// <param name="dotExecutablePath">The Graphviz executable path or command name.</param>
    /// <param name="renderTimeout">The render timeout.</param>
    public GraphvizService(string? dotExecutablePath, TimeSpan renderTimeout)
        : this(dotExecutablePath, renderTimeout, NullLogger<GraphvizService>.Instance)
    {
    }

    private GraphvizService(string? dotExecutablePath, TimeSpan renderTimeout, ILogger<GraphvizService> logger)
    {
        if (renderTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(renderTimeout), "The render timeout must be positive.");
        }

        _dotExecutablePath = string.IsNullOrWhiteSpace(dotExecutablePath) ? DefaultDotCommand : dotExecutablePath;
        _renderTimeout = renderTimeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsAvailable()
    {
        return ResolveDotExecutablePath() is not null;
    }

    /// <inheritdoc />
    public async Task<string> RenderSvgAsync(string dotText, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dotText);
        ct.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Graphviz SVG render requested. DotLength={DotLength} TimeoutMs={TimeoutMs}",
            dotText.Length,
            _renderTimeout.TotalMilliseconds);

        if (TryRenderAppGeneratedDot(dotText, out var fastSvg))
        {
            _logger.LogDebug(
                "App-generated DOT rendered with the built-in SVG renderer. DotLength={DotLength} SvgLength={SvgLength} ElapsedMs={ElapsedMs}",
                dotText.Length,
                fastSvg.Length,
                stopwatch.ElapsedMilliseconds);
            return fastSvg;
        }

        var resolvedPath = ResolveDotExecutablePath();
        if (resolvedPath is null)
        {
            _logger.LogWarning(
                "Graphviz executable was not found. ConfiguredPathKind={ConfiguredPathKind}",
                GetConfiguredPathKind(_dotExecutablePath));
            throw new GraphvizNotFoundException(_dotExecutablePath);
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(resolvedPath),
            EnableRaisingEvents = true,
        };

        try
        {
            if (!process.Start())
            {
                _logger.LogError("Graphviz process failed to start without an exception.");
                throw new InvalidOperationException("Graphviz dot process failed to start.");
            }
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(
                "Graphviz process could not start. ConfiguredPathKind={ConfiguredPathKind} ExceptionType={ExceptionType}",
                GetConfiguredPathKind(_dotExecutablePath),
                ex.GetType().Name);
            throw new GraphvizNotFoundException(_dotExecutablePath)
            {
                Source = ex.Source,
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_renderTimeout);

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.StandardInput.WriteAsync(dotText.AsMemory(), ct).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
            process.StandardInput.Close();

            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            KillProcess(process);
            await DrainProcessOutputAsync(outputTask, errorTask).ConfigureAwait(false);
            _logger.LogError(
                "Graphviz SVG render timed out. TimeoutMs={TimeoutMs}",
                _renderTimeout.TotalMilliseconds);
            throw new GraphvizTimeoutException(_dotExecutablePath, _renderTimeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            KillProcess(process);
            await DrainProcessOutputAsync(outputTask, errorTask).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            KillProcess(process);
            await DrainProcessOutputAsync(outputTask, errorTask).ConfigureAwait(false);
            _logger.LogError(
                "Graphviz SVG render failed while communicating with the process. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "Graphviz process exited with an error. ExitCode={ExitCode} ErrorLength={ErrorLength}",
                process.ExitCode,
                error.Length);
            throw new InvalidOperationException("Graphviz dot failed: " + error);
        }

        var svg = ExtractSvg(output);
        _logger.LogDebug(
            "Graphviz SVG render completed. DotLength={DotLength} SvgLength={SvgLength} ElapsedMs={ElapsedMs}",
            dotText.Length,
            svg.Length,
            stopwatch.ElapsedMilliseconds);
        return svg;
    }

    private static bool TryRenderAppGeneratedDot(string dotText, out string svg)
    {
        svg = string.Empty;
        var isSupported = dotText.StartsWith("digraph BDD", StringComparison.Ordinal)
            || dotText.StartsWith("digraph BDT", StringComparison.Ordinal);
        if (!isSupported)
        {
            return false;
        }

        var nodes = new Dictionary<string, FastDotNode>(StringComparer.Ordinal);
        var edges = new List<FastDotEdge>();
        foreach (var line in dotText.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var nodeMatch = AppDotNodeRegex.Match(line);
            if (nodeMatch.Success)
            {
                var id = nodeMatch.Groups["id"].Value;
                var label = UnescapeDotLabel(nodeMatch.Groups["label"].Value);
                var attrs = nodeMatch.Groups["attrs"].Value;
                nodes[id] = new FastDotNode(id, label, attrs.Contains("shape=box", StringComparison.Ordinal));
                continue;
            }

            var edgeMatch = AppDotEdgeRegex.Match(line);
            if (edgeMatch.Success)
            {
                var attrs = edgeMatch.Groups["attrs"].Value;
                var labelMatch = DotLabelAttributeRegex.Match(attrs);
                var label = labelMatch.Success ? UnescapeDotLabel(labelMatch.Groups["label"].Value) : string.Empty;
                edges.Add(new FastDotEdge(
                    edgeMatch.Groups["from"].Value,
                    edgeMatch.Groups["to"].Value,
                    label,
                    attrs.Contains("style=dashed", StringComparison.Ordinal)));
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        svg = RenderFastSvg(nodes, edges);
        return true;
    }

    private static string RenderFastSvg(IReadOnlyDictionary<string, FastDotNode> nodes, IReadOnlyList<FastDotEdge> edges)
    {
        var levels = AssignFastRenderLevels(nodes, edges);
        var levelGroups = levels
            .GroupBy(pair => pair.Value)
            .OrderBy(group => group.Key)
            .ToArray();
        var maxColumns = Math.Max(1, levelGroups.Max(group => group.Count()));
        var width = Math.Max(320, (maxColumns - 1) * FastRenderXSpacing + (FastRenderMargin * 2));
        var height = Math.Max(220, (levelGroups.Length - 1) * FastRenderYSpacing + (FastRenderMargin * 2));
        var positions = new Dictionary<string, FastPoint>(StringComparer.Ordinal);

        foreach (var group in levelGroups)
        {
            var orderedNodes = group
                .Select(pair => pair.Key)
                .OrderBy(GetDotNodeNumber)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var rowWidth = (orderedNodes.Length - 1) * FastRenderXSpacing;
            var startX = (width - rowWidth) / 2;
            var y = FastRenderMargin + (group.Key * FastRenderYSpacing);
            for (var i = 0; i < orderedNodes.Length; i++)
            {
                positions[orderedNodes[i]] = new FastPoint(startX + (i * FastRenderXSpacing), y);
            }
        }

        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ");
        sb.Append(width.ToString(CultureInfo.InvariantCulture));
        sb.Append(' ');
        sb.Append(height.ToString(CultureInfo.InvariantCulture));
        sb.Append("\" role=\"img\" aria-label=\"Decision diagram\">");
        sb.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"6\" markerHeight=\"6\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#5f6368\"/></marker></defs>");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

        foreach (var edge in edges)
        {
            if (!positions.TryGetValue(edge.From, out var from) || !positions.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            sb.Append("<g class=\"edge\"><line x1=\"");
            sb.Append(from.X.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" y1=\"");
            sb.Append((from.Y + 22).ToString(CultureInfo.InvariantCulture));
            sb.Append("\" x2=\"");
            sb.Append(to.X.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" y2=\"");
            sb.Append((to.Y - 22).ToString(CultureInfo.InvariantCulture));
            sb.Append("\" stroke=\"#5f6368\" stroke-width=\"1.4\" marker-end=\"url(#arrow)\"");
            if (edge.IsDashed)
            {
                sb.Append(" stroke-dasharray=\"5 4\"");
            }

            sb.Append("/>");
            if (!string.IsNullOrEmpty(edge.Label))
            {
                sb.Append("<text x=\"");
                sb.Append(((from.X + to.X) / 2).ToString(CultureInfo.InvariantCulture));
                sb.Append("\" y=\"");
                sb.Append(((from.Y + to.Y) / 2).ToString(CultureInfo.InvariantCulture));
                sb.Append("\" fill=\"#4f5358\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"12\" text-anchor=\"middle\">");
                sb.Append(WebUtility.HtmlEncode(edge.Label));
                sb.Append("</text>");
            }

            sb.Append("</g>");
        }

        foreach (var node in nodes.Values.OrderBy(node => GetDotNodeNumber(node.Id)).ThenBy(node => node.Id, StringComparer.Ordinal))
        {
            if (!positions.TryGetValue(node.Id, out var position))
            {
                continue;
            }

            var nodeType = node.IsTerminal ? "terminal" : "internal";
            var variableName = !node.IsTerminal && VariableNameRegex.IsMatch(node.Label) ? node.Label : "_terminal";
            sb.Append("<g class=\"node\" id=\"");
            sb.Append(WebUtility.HtmlEncode(node.Id));
            sb.Append("\" data-node-id=\"");
            sb.Append(WebUtility.HtmlEncode(node.Id));
            sb.Append("\" data-variable=\"");
            sb.Append(WebUtility.HtmlEncode(variableName));
            sb.Append("\" data-node-type=\"");
            sb.Append(nodeType);
            sb.Append("\"><title>");
            sb.Append(WebUtility.HtmlEncode(node.Id));
            sb.Append("</title>");
            if (node.IsTerminal)
            {
                sb.Append("<rect x=\"");
                sb.Append((position.X - 32).ToString(CultureInfo.InvariantCulture));
                sb.Append("\" y=\"");
                sb.Append((position.Y - 22).ToString(CultureInfo.InvariantCulture));
                sb.Append("\" width=\"64\" height=\"44\" rx=\"5\" fill=\"#f8fafc\" stroke=\"#2563eb\" stroke-width=\"1.6\"/>");
            }
            else
            {
                sb.Append("<circle cx=\"");
                sb.Append(position.X.ToString(CultureInfo.InvariantCulture));
                sb.Append("\" cy=\"");
                sb.Append(position.Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("\" r=\"24\" fill=\"#eff6ff\" stroke=\"#2563eb\" stroke-width=\"1.6\"/>");
            }

            sb.Append("<text x=\"");
            sb.Append(position.X.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" y=\"");
            sb.Append((position.Y + 4).ToString(CultureInfo.InvariantCulture));
            sb.Append("\" fill=\"#172033\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"13\" font-weight=\"600\" text-anchor=\"middle\">");
            sb.Append(WebUtility.HtmlEncode(node.Label));
            sb.Append("</text></g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static Dictionary<string, int> AssignFastRenderLevels(IReadOnlyDictionary<string, FastDotNode> nodes, IReadOnlyList<FastDotEdge> edges)
    {
        if (nodes.Keys.All(id => id.StartsWith("bdt", StringComparison.Ordinal)))
        {
            return nodes.Keys.ToDictionary(id => id, id => GetHeapLevel(GetDotNodeNumber(id)), StringComparer.Ordinal);
        }

        var outgoing = edges.GroupBy(edge => edge.From, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).ToArray(), StringComparer.Ordinal);
        var incoming = edges.Select(edge => edge.To).ToHashSet(StringComparer.Ordinal);
        var roots = outgoing.Keys.Where(id => !incoming.Contains(id)).OrderBy(GetDotNodeNumber).ToArray();
        if (roots.Length == 0)
        {
            roots = nodes.Keys.Where(id => !nodes[id].IsTerminal).OrderBy(GetDotNodeNumber).Take(1).ToArray();
        }

        var levels = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var queue = new Queue<string>(roots);
        foreach (var root in roots)
        {
            levels[root] = 0;
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!outgoing.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                var nextLevel = levels[current] + 1;
                if (nextLevel > levels[child])
                {
                    levels[child] = nextLevel;
                    queue.Enqueue(child);
                }
            }
        }

        var terminalLevel = levels.Where(pair => !nodes[pair.Key].IsTerminal).Select(pair => pair.Value).DefaultIfEmpty(0).Max() + 1;
        foreach (var node in nodes.Values.Where(node => node.IsTerminal))
        {
            levels[node.Id] = Math.Max(levels[node.Id], terminalLevel);
        }

        return levels;
    }

    private static int GetDotNodeNumber(string id)
    {
        var digitStart = 0;
        while (digitStart < id.Length && !char.IsDigit(id[digitStart]))
        {
            digitStart++;
        }

        return digitStart < id.Length && int.TryParse(id[digitStart..], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static int GetHeapLevel(int nodeIndex)
    {
        var level = 0;
        var firstIndexAtLevel = 0;
        var nodesAtLevel = 1;

        while (nodeIndex >= firstIndexAtLevel + nodesAtLevel)
        {
            firstIndexAtLevel += nodesAtLevel;
            nodesAtLevel *= 2;
            level++;
        }

        return level;
    }

    private static string UnescapeDotLabel(string label)
    {
        return label.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static ProcessStartInfo CreateStartInfo(string resolvedPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
        };
        startInfo.ArgumentList.Add("-Tsvg");
        return startInfo;
    }

    private static string ExtractSvg(string output)
    {
        var svgStart = output.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgStart < 0)
        {
            throw new InvalidOperationException("Graphviz dot did not return SVG output.");
        }

        return output[svgStart..].TrimStart();
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task DrainProcessOutputAsync(Task<string> outputTask, Task<string> errorTask)
    {
        try
        {
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private string? ResolveDotExecutablePath()
    {
        lock (_resolveSync)
        {
            if (_hasResolvedDotExecutablePath)
            {
                return _resolvedDotExecutablePath;
            }

            _resolvedDotExecutablePath = ResolveDotExecutablePath(_dotExecutablePath);
            _hasResolvedDotExecutablePath = true;
            if (_resolvedDotExecutablePath is null)
            {
                _logger.LogWarning(
                    "Graphviz executable resolution failed. ConfiguredPathKind={ConfiguredPathKind}",
                    GetConfiguredPathKind(_dotExecutablePath));
            }
            else
            {
                _logger.LogDebug(
                    "Graphviz executable resolved. ConfiguredPathKind={ConfiguredPathKind}",
                    GetConfiguredPathKind(_dotExecutablePath));
            }

            return _resolvedDotExecutablePath;
        }
    }

    internal static string? ResolveDotExecutablePath(string dotExecutablePath)
    {
        if (HasDirectoryPart(dotExecutablePath))
        {
            return File.Exists(dotExecutablePath) ? dotExecutablePath : null;
        }

        return FindOnPath(dotExecutablePath)
            ?? FindInKnownGraphvizLocations(dotExecutablePath);
    }

    private static string GetGraphvizPath(SessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.GraphvizPath;
    }

    private static bool HasDirectoryPart(string path)
    {
        return Path.IsPathRooted(path)
            || path.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || path.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string GetConfiguredPathKind(string path)
    {
        return HasDirectoryPart(path) ? "ExplicitPath" : "CommandName";
    }

    private static string? FindOnPath(string commandName)
    {
        foreach (var directory in EnumeratePathDirectories())
        {
            var candidate = FindExecutableInDirectory(commandName, directory);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindInKnownGraphvizLocations(string commandName)
    {
        foreach (var directory in EnumerateKnownGraphvizBinDirectories())
        {
            var candidate = FindExecutableInDirectory(commandName, directory);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindExecutableInDirectory(string commandName, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var expandedDirectory = Environment.ExpandEnvironmentVariables(directory.Trim());
        foreach (var candidateName in GetCandidateNames(commandName))
        {
            var candidate = Path.Combine(expandedDirectory, candidateName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in new[]
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine,
        })
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH", target);
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                continue;
            }

            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var expandedDirectory = Environment.ExpandEnvironmentVariables(directory);
                if (directories.Add(expandedDirectory))
                {
                    yield return expandedDirectory;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateKnownGraphvizBinDirectories()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddRoot(roots, Environment.GetEnvironmentVariable("ProgramW6432"));
        AddRoot(roots, Environment.GetEnvironmentVariable("ProgramFiles"));
        AddRoot(roots, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));

        foreach (var root in roots)
        {
            var directBin = Path.Combine(root, "Graphviz", "bin");
            if (Directory.Exists(directBin))
            {
                yield return directBin;
            }

            IEnumerable<string> graphvizDirectories;
            try
            {
                graphvizDirectories = Directory.EnumerateDirectories(root, "Graphviz*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var graphvizDirectory in graphvizDirectories)
            {
                var binDirectory = Path.Combine(graphvizDirectory, "bin");
                if (Directory.Exists(binDirectory))
                {
                    yield return binDirectory;
                }
            }
        }
    }

    private static void AddRoot(HashSet<string> roots, string? root)
    {
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            roots.Add(root);
        }
    }

    private static IEnumerable<string> GetCandidateNames(string commandName)
    {
        yield return commandName;

        if (!Path.HasExtension(commandName))
        {
            yield return commandName + ".exe";
        }
    }

    private sealed record FastDotNode(string Id, string Label, bool IsTerminal);

    private sealed record FastDotEdge(string From, string To, string Label, bool IsDashed);

    private readonly record struct FastPoint(double X, double Y);
}
