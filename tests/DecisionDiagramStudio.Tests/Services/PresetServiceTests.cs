using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests.Services;

/// <summary>
/// Verifies the preset service contract and bundled BDD presets.
/// </summary>
[TestClass]
public sealed class PresetServiceTests
{
    /// <summary>
    /// Verifies that the concrete service satisfies the public service contract.
    /// </summary>
    [TestMethod]
    public void PresetService_ShouldImplement_IPresetService()
    {
        // Arrange / Act
        IPresetService service = new PresetService(GetPresetPath());

        // Assert
        Assert.IsNotNull(service, "PresetService should be constructible through the IPresetService contract.");
    }

    /// <summary>
    /// Verifies the four required BDD learning presets.
    /// </summary>
    [TestMethod]
    public void GetPreset_RequiredBddLearningPresets_ShouldReturnExpectedTruthTables()
    {
        // Arrange
        var service = new PresetService(GetPresetPath());
        var expected = new Dictionary<string, (string[] Variables, int[] Values)>
        {
            ["bdd.identity.a"] = (new[] { "a" }, new[] { 0, 1 }),
            ["bdd.and"] = (new[] { "a", "b" }, new[] { 0, 0, 0, 1 }),
            ["bdd.or"] = (new[] { "a", "b" }, new[] { 0, 1, 1, 1 }),
            ["bdd.xor"] = (new[] { "a", "b" }, new[] { 0, 1, 1, 0 }),
        };

        // Act / Assert
        foreach (var (id, contract) in expected)
        {
            var preset = service.GetPreset(id);
            Assert.AreEqual(id, preset.Id, "The requested preset id should be returned.");
            Assert.AreEqual(DiagramFamily.BDD, preset.DefaultFamily, "BDD learning presets should default to BDD.");
            CollectionAssert.AreEqual(contract.Variables, preset.VariableNames, id + " should expose the expected variables.");
            CollectionAssert.AreEqual(contract.Values, preset.TruthTableValues, id + " should expose the expected truth table.");
        }

        Assert.AreEqual(4, service.GetPresets().Count, "The v0.1 preset asset should contain the required four BDD presets.");
    }

    /// <summary>
    /// Verifies that returned presets are defensive snapshots.
    /// </summary>
    [TestMethod]
    public void GetPreset_ReturnedArrays_ShouldBeSnapshots()
    {
        // Arrange
        var service = new PresetService(GetPresetPath());
        var first = service.GetPreset("bdd.xor");
        first.VariableNames[0] = "changed";
        first.TruthTableValues[0] = 1;

        // Act
        var second = service.GetPreset("bdd.xor");

        // Assert
        CollectionAssert.AreEqual(new[] { "a", "b" }, second.VariableNames, "Mutating one returned preset should not affect later calls.");
        CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, second.TruthTableValues, "Mutating one returned table should not affect later calls.");
    }

    private static string GetPresetPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Presets", "presets.json");
    }
}
