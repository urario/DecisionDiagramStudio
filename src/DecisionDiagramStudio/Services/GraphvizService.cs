using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;

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

    private readonly string _dotExecutablePath;
    private readonly TimeSpan _renderTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class.
    /// </summary>
    public GraphvizService()
        : this(string.Empty, DefaultRenderTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class from session options.
    /// </summary>
    /// <param name="options">The session options containing the optional Graphviz path.</param>
    public GraphvizService(SessionOptions options)
        : this(GetGraphvizPath(options), DefaultRenderTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphvizService"/> class.
    /// </summary>
    /// <param name="dotExecutablePath">The Graphviz executable path or command name.</param>
    /// <param name="renderTimeout">The render timeout.</param>
    public GraphvizService(string? dotExecutablePath, TimeSpan renderTimeout)
    {
        if (renderTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(renderTimeout), "The render timeout must be positive.");
        }

        _dotExecutablePath = string.IsNullOrWhiteSpace(dotExecutablePath) ? DefaultDotCommand : dotExecutablePath;
        _renderTimeout = renderTimeout;
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

        var resolvedPath = ResolveDotExecutablePath();
        if (resolvedPath is null)
        {
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
                throw new InvalidOperationException("Graphviz dot process failed to start.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new GraphvizNotFoundException(_dotExecutablePath)
            {
                Source = ex.Source,
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_renderTimeout);

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

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
            throw new GraphvizTimeoutException(_dotExecutablePath, _renderTimeout);
        }
        catch
        {
            KillProcess(process);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Graphviz dot failed: " + error);
        }

        return ExtractSvg(output);
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
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
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

    private string? ResolveDotExecutablePath()
    {
        if (HasDirectoryPart(_dotExecutablePath))
        {
            return File.Exists(_dotExecutablePath) ? _dotExecutablePath : null;
        }

        return FindOnPath(_dotExecutablePath);
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

    private static string? FindOnPath(string commandName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidateName in GetCandidateNames(commandName))
            {
                var candidate = Path.Combine(directory, candidateName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
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
