using System.ComponentModel;
using System.Diagnostics;
using System.Text;
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

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

}
