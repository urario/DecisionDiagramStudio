using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DecisionDiagramStudio.Converters;

/// <summary>
/// Resolves truth-table value button brushes.
/// </summary>
public sealed class TruthValueBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (!IsOne(value))
        {
            return DependencyProperty.UnsetValue;
        }

        var resourceKey = (parameter as string) switch
        {
            "Foreground" => "TextOnAccentFillColorPrimaryBrush",
            "Border" => "DiagramFamilyBDDAccentMutedBrush",
            _ => "DiagramFamilyBDDAccentBrush",
        };

        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource))
        {
            return resource;
        }

        return resourceKey == "TextOnAccentFillColorPrimaryBrush"
            ? new SolidColorBrush(Colors.White)
            : DependencyProperty.UnsetValue;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }

    private static bool IsOne(object value)
    {
        return value switch
        {
            int intValue => intValue == 1,
            bool boolValue => boolValue,
            _ => false,
        };
    }
}
