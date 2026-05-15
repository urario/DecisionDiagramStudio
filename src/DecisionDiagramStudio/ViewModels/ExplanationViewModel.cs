using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Tracks the selected diagram node and exposes a short explanation.
/// </summary>
public sealed partial class ExplanationViewModel : ObservableObject
{
    private static readonly Regex NodeIdRegex = new("^n\\d+$", RegexOptions.CultureInvariant);
    private static readonly Regex VariableNameRegex = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant);

    [ObservableProperty]
    private string _selectedNodeId = string.Empty;

    [ObservableProperty]
    private string _explanationText = string.Empty;

    /// <summary>
    /// Selects a diagram node and updates the explanation text.
    /// </summary>
    /// <param name="nodeId">The selected node id.</param>
    /// <param name="session">The session that owns the selected node.</param>
    public void SelectNode(string nodeId, DiagramSession session)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("A node id is required.", nameof(nodeId));
        }

        ArgumentNullException.ThrowIfNull(session);

        SelectedNodeId = nodeId;
        ExplanationText = "Node " + nodeId + " belongs to a " + session.Family
            + " session with " + session.VariableNames.Length.ToString()
            + " variable(s).";
    }

    /// <summary>
    /// Validates a WebView2 node-click message and applies it when it matches the supported schema.
    /// </summary>
    /// <param name="json">The JSON message received from WebView2.</param>
    /// <param name="session">The session that owns the selected node.</param>
    /// <returns><c>true</c> when the message was valid and applied; otherwise <c>false</c>.</returns>
    public bool TrySelectNodeFromWebMessage(string json, DiagramSession session)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(session);

        if (!TryParseNodeClickMessage(json, out var nodeId))
        {
            return false;
        }

        SelectNode(nodeId, session);
        return true;
    }

    private static bool TryParseNodeClickMessage(string json, out string nodeId)
    {
        nodeId = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetString(document.RootElement, "type", out var type)
                || !StringComparer.Ordinal.Equals(type, "nodeClick")
                || !TryGetString(document.RootElement, "nodeId", out var parsedNodeId)
                || !NodeIdRegex.IsMatch(parsedNodeId)
                || !TryGetString(document.RootElement, "variableName", out var variableName)
                || !VariableNameRegex.IsMatch(variableName)
                || !TryGetString(document.RootElement, "nodeType", out var nodeType)
                || (nodeType is not "internal" and not "terminal"))
            {
                return false;
            }

            nodeId = parsedNodeId;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }
}
