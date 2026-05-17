using DecisionDiagramStudio.Services.Interfaces;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
#endif

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Copies text to the operating system clipboard when a Windows app runtime is available.
/// </summary>
public sealed class SystemClipboardService : IClipboardService
{
    /// <summary>
    /// Gets the latest copied text in non-Windows test builds.
    /// </summary>
    public string LastCopiedText { get; private set; } = string.Empty;

    /// <inheritdoc />
    public Task SetTextAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();

#if WINDOWS
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
#else
        LastCopiedText = text;
#endif
        return Task.CompletedTask;
    }
}
