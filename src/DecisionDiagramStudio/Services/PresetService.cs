using System.Text.Json;
using System.Text.Json.Serialization;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Loads and serves diagram presets from the application preset asset.
/// </summary>
public sealed class PresetService : IPresetService
{
    private const string DefaultPresetRelativePath = "Assets/Presets/presets.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<PresetService> _logger;
    private readonly DiagramPreset[] _presets;
    private readonly Dictionary<string, DiagramPreset> _presetsById;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresetService"/> class.
    /// </summary>
    public PresetService()
        : this(DefaultPresetRelativePath, NullLogger<PresetService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PresetService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for preset diagnostics.</param>
    public PresetService(ILogger<PresetService> logger)
        : this(DefaultPresetRelativePath, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PresetService"/> class.
    /// </summary>
    /// <param name="presetPath">The preset JSON file path.</param>
    public PresetService(string presetPath)
        : this(presetPath, NullLogger<PresetService>.Instance)
    {
    }

    private PresetService(string presetPath, ILogger<PresetService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetPath);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var resolvedPath = ResolvePresetPath(presetPath);
        var json = File.ReadAllText(resolvedPath);
        _presets = JsonSerializer.Deserialize<DiagramPreset[]>(json, JsonOptions) ?? [];
        ValidatePresets(_presets);
        _presetsById = _presets.ToDictionary(preset => preset.Id, StringComparer.Ordinal);
        _logger.LogInformation("Preset catalog loaded. PresetCount={PresetCount}", _presets.Length);
    }

    /// <inheritdoc />
    public IReadOnlyList<DiagramPreset> GetPresets()
    {
        return _presets.Select(ClonePreset).ToArray();
    }

    /// <inheritdoc />
    public DiagramPreset GetPreset(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_presetsById.TryGetValue(id, out var preset))
        {
            _logger.LogWarning("Preset lookup failed. PresetCount={PresetCount}", _presets.Length);
            throw new KeyNotFoundException("Preset id was not found: " + id);
        }

        _logger.LogInformation("Preset selected. PresetCount={PresetCount}", _presets.Length);
        return ClonePreset(preset);
    }

    private static string ResolvePresetPath(string presetPath)
    {
        if (Path.IsPathRooted(presetPath))
        {
            return presetPath;
        }

        var outputPath = Path.Combine(AppContext.BaseDirectory, presetPath);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        return presetPath;
    }

    private static void ValidatePresets(IReadOnlyList<DiagramPreset> presets)
    {
        if (presets.Count == 0)
        {
            throw new InvalidOperationException("At least one preset is required.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in presets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.Label);
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.Description);

            if (!ids.Add(preset.Id))
            {
                throw new InvalidOperationException("Duplicate preset id: " + preset.Id);
            }

            if (preset.DefaultFamily == DiagramFamily.BDD && preset.TruthTableValues.Length != 1 << preset.VariableNames.Length)
            {
                throw new InvalidOperationException("BDD preset truth table length must equal 2^variableCount: " + preset.Id);
            }
        }
    }

    private static DiagramPreset ClonePreset(DiagramPreset preset)
    {
        return preset with
        {
            VariableNames = (string[])preset.VariableNames.Clone(),
            TruthTableValues = (int[])preset.TruthTableValues.Clone(),
        };
    }
}
