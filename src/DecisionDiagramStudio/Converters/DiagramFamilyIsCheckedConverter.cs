using DecisionDiagramStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DecisionDiagramStudio.Converters;

/// <summary>
/// Converts the selected diagram family to a radio button checked state.
/// </summary>
public sealed class DiagramFamilyIsCheckedConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is DiagramFamily selectedFamily
            && parameter is string familyName
            && Enum.TryParse<DiagramFamily>(familyName, out var family)
            && selectedFamily == family;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isChecked
            && isChecked
            && parameter is string familyName
            && Enum.TryParse<DiagramFamily>(familyName, out var family))
        {
            return family;
        }

        return DependencyProperty.UnsetValue;
    }
}
