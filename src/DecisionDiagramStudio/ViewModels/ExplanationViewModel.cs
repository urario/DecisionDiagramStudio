using CommunityToolkit.Mvvm.ComponentModel;
using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Tracks the selected diagram node and exposes a short explanation.
/// </summary>
public sealed partial class ExplanationViewModel : ObservableObject
{
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
}
