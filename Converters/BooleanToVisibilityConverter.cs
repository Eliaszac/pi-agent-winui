using Microsoft.UI.Xaml.Data;

namespace PiAgentGui.Converters;

/// <summary>Converts presentation flags to XAML visibility.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
