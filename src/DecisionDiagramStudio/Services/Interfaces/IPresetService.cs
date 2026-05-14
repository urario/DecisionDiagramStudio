using DecisionDiagramStudio.Models;

namespace DecisionDiagramStudio.Services.Interfaces;

/// <summary>
/// Defines the application boundary for reading diagram presets.
/// </summary>
public interface IPresetService
{
    /// <summary>
    /// Gets all available presets.
    /// </summary>
    /// <returns>The available presets.</returns>
    IReadOnlyList<DiagramPreset> GetPresets();

    /// <summary>
    /// Gets one preset by stable identifier.
    /// </summary>
    /// <param name="id">The preset identifier.</param>
    /// <returns>The matching preset.</returns>
    DiagramPreset GetPreset(string id);
}
