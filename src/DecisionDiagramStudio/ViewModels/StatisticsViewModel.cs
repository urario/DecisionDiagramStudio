using CommunityToolkit.Mvvm.ComponentModel;
using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.ViewModels;

/// <summary>
/// Formats diagram statistics for status and detail views.
/// </summary>
public sealed partial class StatisticsViewModel : ObservableObject
{
    [ObservableProperty]
    private DiagramSession? _session;

    [ObservableProperty]
    private int _reachableNodeCount;

    [ObservableProperty]
    private int _reachableTerminalCount;

    [ObservableProperty]
    private int _totalNodeCount;

    [ObservableProperty]
    private int _variableCount;

    [ObservableProperty]
    private int _bdtNodeCount;

    [ObservableProperty]
    private int _reducedCount;

    [ObservableProperty]
    private long _setCount;

    [ObservableProperty]
    private string _reductionSummary = "BDT to BDD non-terminal nodes reduced: 0";

    partial void OnSessionChanged(DiagramSession? value)
    {
        var statistics = value?.Statistics ?? AppDiagramStatistics.Empty;
        ReachableNodeCount = statistics.ReachableNodeCount;
        ReachableTerminalCount = statistics.ReachableTerminalCount;
        TotalNodeCount = statistics.TotalNodeCount;
        VariableCount = statistics.VariableCount;
        BdtNodeCount = statistics.BdtNodeCount;
        ReducedCount = statistics.ReducedCount;
        SetCount = statistics.SetCount;
        ReductionSummary = value?.Family switch
        {
            DiagramFamily.ZDD => "ZDD set count: " + statistics.SetCount.ToString(),
            DiagramFamily.MTBDD or DiagramFamily.ZMTBDD => "Reachable terminal values: " + statistics.ReachableTerminalCount.ToString(),
            _ => "BDT to BDD non-terminal nodes reduced: " + statistics.ReducedCount.ToString(),
        };
    }
}
