namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Creates the HTML document used to host a rendered SVG inside WebView2.
/// </summary>
public interface ISvgWebViewDocumentSource
{
    /// <summary>
    /// Creates a complete HTML document for the supplied SVG content.
    /// </summary>
    /// <param name="svgContent">The SVG markup to embed in the WebView2 document.</param>
    /// <returns>A complete HTML document string.</returns>
    string CreateDocument(string svgContent);
}
