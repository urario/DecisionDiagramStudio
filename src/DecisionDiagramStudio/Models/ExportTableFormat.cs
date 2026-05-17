namespace DecisionDiagramStudio.Models;

/// <summary>
/// Defines text table export formats.
/// </summary>
public enum ExportTableFormat
{
    /// <summary>
    /// Comma-separated values.
    /// </summary>
    Csv,

    /// <summary>
    /// GitHub-flavored Markdown table.
    /// </summary>
    Markdown,

    /// <summary>
    /// AsciiDoc table.
    /// </summary>
    AsciiDoc,
}
