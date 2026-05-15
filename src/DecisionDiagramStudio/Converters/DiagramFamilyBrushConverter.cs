using DecisionDiagramStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DecisionDiagramStudio.Converters;

/// <summary>
/// Resolves the brush resource for a decision diagram family.
/// </summary>
public sealed class DiagramFamilyBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var family = value is DiagramFamily diagramFamily ? diagramFamily : DiagramFamily.BDD;
        var variant = parameter as string;
        var key = "DiagramFamily" + family.ToString() + NormalizeVariant(variant) + "Brush";

        return Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : Application.Current.Resources["DiagramFamilyBDDAccentBrush"];
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }

    private static string NormalizeVariant(string? variant)
    {
        return variant switch
        {
            "Soft" => "Soft",
            "Muted" => "Muted",
            _ => "Accent",
        };
    }
}
