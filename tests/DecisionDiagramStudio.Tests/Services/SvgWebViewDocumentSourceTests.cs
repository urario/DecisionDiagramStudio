using System.Net;
using System.Text.RegularExpressions;
using DecisionDiagramStudio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Services;

/// <summary>
/// Verifies the WebView2 SVG document security envelope.
/// </summary>
[TestClass]
public sealed class SvgWebViewDocumentSourceTests
{
    /// <summary>
    /// Verifies that the generated document uses CSP nonce protection for the only trusted script.
    /// </summary>
    [TestMethod]
    public void CreateDocument_ShouldUseCspNonceForTrustedScript()
    {
        // Arrange
        var source = new SvgWebViewDocumentSource();

        // Act
        var html = source.CreateDocument("<svg><script>alert(1)</script><g class=\"node\"><title>n1</title></g></svg>");
        var decodedHtml = WebUtility.HtmlDecode(html);
        var nonceMatch = Regex.Match(decodedHtml, "script-src 'nonce-([^']+)'");

        // Assert
        Assert.IsTrue(nonceMatch.Success, "The CSP should restrict script execution to a per-render nonce.");
        var nonce = nonceMatch.Groups[1].Value;
        StringAssert.Contains(decodedHtml, "<script nonce=\"" + nonce + "\">");
        StringAssert.Contains(decodedHtml, "<script>alert(1)</script>");
        Assert.AreEqual(
            1,
            Regex.Matches(decodedHtml, "<script nonce=\"").Count,
            "Only the trusted host script should carry the CSP nonce; SVG scripts remain nonce-less and blocked by CSP.");
    }

    /// <summary>
    /// Verifies that node-click script wiring is present in the generated document.
    /// </summary>
    [TestMethod]
    public void CreateDocument_ShouldIncludeNodeClickPostMessageScript()
    {
        // Arrange
        var source = new SvgWebViewDocumentSource();

        // Act
        var html = source.CreateDocument("<svg><g class=\"node\"><title>n1</title></g></svg>");

        // Assert
        StringAssert.Contains(html, "window.chrome?.webview?.postMessage");
        StringAssert.Contains(html, "type: 'nodeClick'");
    }
}
