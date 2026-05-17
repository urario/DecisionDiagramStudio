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
    private static readonly Regex NodeIdRegex = new("^[nt]\\d+$", RegexOptions.CultureInvariant);
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
        SelectNode(nodeId, string.Empty, string.Empty, session);
    }

    /// <summary>
    /// Selects a diagram node and updates the explanation text with node metadata.
    /// </summary>
    /// <param name="nodeId">The selected node id.</param>
    /// <param name="variableName">The selected node variable name when available.</param>
    /// <param name="nodeType">The selected node kind.</param>
    /// <param name="session">The session that owns the selected node.</param>
    public void SelectNode(string nodeId, string variableName, string nodeType, DiagramSession session)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("A node id is required.", nameof(nodeId));
        }

        ArgumentNullException.ThrowIfNull(session);

        SelectedNodeId = nodeId;
        ExplanationText = BuildExplanationText(nodeId, variableName, nodeType, session);
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

        if (!TryParseNodeClickMessage(json, out var message))
        {
            return false;
        }

        SelectNode(message.NodeId, message.VariableName, message.NodeType, session);
        return true;
    }

    private static string BuildExplanationText(string nodeId, string variableName, string nodeType, DiagramSession session)
    {
        var kind = string.IsNullOrWhiteSpace(nodeType) ? "diagram" : nodeType;
        var variableText = string.IsNullOrWhiteSpace(variableName) || variableName == "_terminal"
            ? "output terminal"
            : "variable " + variableName;
        var values = session.IntValueTable is null
            ? "set-family membership"
            : "values {" + string.Join(", ", session.IntValueTable.Distinct().Order().Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "}";
        var familyText = session.Family switch
        {
            DiagramFamily.BDD => "BDD nodes choose low edge for 0 and high edge for 1; terminals are Boolean results.",
            DiagramFamily.ZDD => "ZDD nodes use zero-suppressed set-family semantics; a missing high branch removes sets containing the variable.",
            DiagramFamily.MTBDD => "MTBDD terminals are integer results, so multiple output values can share the same variable-order diagram.",
            DiagramFamily.ZMTBDD => "ZMTBDD terminals are integer results with zero-suppressed branches for sparse value tables.",
            _ => "Decision diagram nodes follow the selected family semantics.",
        };

        return "Node " + nodeId + " is a " + kind + " node for " + variableText + " in a "
            + session.Family + " session. Variables: " + string.Join(", ", session.VariableNames)
            + ". Values: " + values + ". " + familyText;
    }

    private static bool TryParseNodeClickMessage(string json, out NodeClickMessage message)
    {
        message = default;

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

            message = new NodeClickMessage(parsedNodeId, variableName, nodeType);
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

    private readonly record struct NodeClickMessage(string NodeId, string VariableName, string NodeType);
}
