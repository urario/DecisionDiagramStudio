using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DecisionDiagramStudio.Infrastructure.Logging;

/// <summary>
/// Centralizes application logging configuration and log file paths.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// The maximum number of daily log files retained on disk.
    /// </summary>
    public const int RetainedFileCountLimit = 30;

    /// <summary>
    /// The application-local log directory name.
    /// </summary>
    public const string ApplicationDirectoryName = "DecisionDiagramStudio";

    /// <summary>
    /// The log subdirectory name under the application-local data directory.
    /// </summary>
    public const string LogDirectoryName = "logs";

    /// <summary>
    /// The Serilog rolling file name template.
    /// </summary>
    public const string LogFileNameTemplate = "app-.log";

    private const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Gets the fixed minimum log level for v0.1.
    /// </summary>
    public static LogLevel MinimumLevel => LogLevel.Trace;

    /// <summary>
    /// Gets the directory used for file logs.
    /// </summary>
    public static string LogDirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDirectoryName,
        LogDirectoryName);

    /// <summary>
    /// Gets the rolling file path template used by Serilog.
    /// </summary>
    public static string LogFilePath => Path.Combine(LogDirectoryPath, LogFileNameTemplate);

    /// <summary>
    /// Configures Microsoft.Extensions.Logging to route through Serilog.
    /// </summary>
    /// <param name="logging">The logging builder to configure.</param>
    public static void Configure(ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.SetMinimumLevel(MinimumLevel);
        logging.AddSerilog(CreateLogger(), dispose: true);
    }

    private static Logger CreateLogger()
    {
        Directory.CreateDirectory(LogDirectoryPath);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Verbose)
            .Enrich.FromLogContext()
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCountLimit,
                encoding: Utf8NoBom,
                outputTemplate: OutputTemplate);

#if DEBUG
        if (Debugger.IsAttached)
        {
            configuration = configuration.WriteTo.Debug(outputTemplate: OutputTemplate);
        }
#endif

        return configuration.CreateLogger();
    }
}
