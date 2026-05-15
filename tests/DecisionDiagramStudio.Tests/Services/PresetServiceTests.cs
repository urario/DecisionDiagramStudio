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
    /// Verifies the required BDD circuit presets.
    /// </summary>
    [TestMethod]
    public void GetPreset_RequiredBddCircuitPresets_ShouldReturnExpectedTruthTables()
    {
        // Arrange
        var service = new PresetService(GetPresetPath());
        var expected = new Dictionary<string, (string[] Variables, int[] Values)>
        {
            ["bdd.mux2"] = (new[] { "s", "d0", "d1" }, new[] { 0, 0, 1, 0, 0, 1, 1, 1 }),
            ["bdd.full_adder.sum"] = (new[] { "a", "b", "cin" }, new[] { 0, 1, 1, 0, 1, 0, 0, 1 }),
            ["bdd.full_adder.carry"] = (new[] { "a", "b", "cin" }, new[] { 0, 0, 0, 1, 0, 1, 1, 1 }),
            ["bdd.eq2"] = (new[] { "a0", "a1", "b0", "b1" }, new[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }),
            ["bdd.one_hot4"] = (new[] { "a", "b", "c", "d" }, new[] { 0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 }),
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

        Assert.AreEqual(5, service.GetPresets().Count, "The v0.1 preset asset should contain the required five BDD circuit presets.");
    }

    /// <summary>
    /// Verifies that returned presets are defensive snapshots.
    /// </summary>
    [TestMethod]
    public void GetPreset_ReturnedArrays_ShouldBeSnapshots()
    {
        // Arrange
        var service = new PresetService(GetPresetPath());
        var first = service.GetPreset("bdd.mux2");
        first.VariableNames[0] = "changed";
        first.TruthTableValues[0] = 1;

        // Act
        var second = service.GetPreset("bdd.mux2");

        // Assert
        CollectionAssert.AreEqual(new[] { "s", "d0", "d1" }, second.VariableNames, "Mutating one returned preset should not affect later calls.");
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0, 1, 1, 1 }, second.TruthTableValues, "Mutating one returned table should not affect later calls.");
    }

    private static string GetPresetPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Presets", "presets.json");
    }
}
