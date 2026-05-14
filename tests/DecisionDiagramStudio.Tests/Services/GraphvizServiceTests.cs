using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Services;

/// <summary>
/// Verifies the Graphviz rendering service contract.
/// </summary>
[TestClass]
public sealed class GraphvizServiceTests
{
    /// <summary>
    /// Verifies that the concrete service satisfies the public service contract.
    /// </summary>
    [TestMethod]
    public void GraphvizService_ShouldImplement_IGraphvizService()
    {
        // Arrange / Act
        IGraphvizService service = new GraphvizService("definitely-not-dot.exe", TimeSpan.FromSeconds(1));

        // Assert
        Assert.IsNotNull(service, "GraphvizService should be constructible through the IGraphvizService contract.");
    }

    /// <summary>
    /// Verifies that an invalid configured Graphviz path reports unavailable and throws the expected fallback exception.
    /// </summary>
    [TestMethod]
    public async Task RenderSvgAsync_InvalidPath_ShouldThrow_GraphvizNotFoundException()
    {
        // Arrange
        var service = new GraphvizService("C:\\missing\\dot.exe", TimeSpan.FromSeconds(1));

        // Act / Assert
        Assert.IsFalse(service.IsAvailable(), "An invalid absolute path should not be reported as available.");

        var exception = await Assert.ThrowsExceptionAsync<GraphvizNotFoundException>(
            () => service.RenderSvgAsync("digraph G { a -> b }", CancellationToken.None));
        Assert.AreEqual("C:\\missing\\dot.exe", exception.DotExecutablePath, "The exception should expose the requested path.");
    }

    /// <summary>
    /// Verifies rendering against a real Graphviz installation when dot is available on PATH.
    /// </summary>
    [TestMethod]
    public async Task RenderSvgAsync_WhenGraphvizAvailable_ShouldReturnSvg()
    {
        // Arrange
        var service = new GraphvizService();
        if (!service.IsAvailable())
        {
            return;
        }

        // Act
        var svg = await service.RenderSvgAsync("digraph G { a -> b }", CancellationToken.None);

        // Assert
        StringAssert.StartsWith(svg, "<svg", "Graphviz output should be normalized to the SVG element.");
    }
}
