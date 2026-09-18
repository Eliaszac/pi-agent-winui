using Microsoft.UI.Xaml.Data;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Converters;

public sealed class SettingsFeedbackSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        SettingsFeedbackKind.Success => InfoBarSeverity.Success,
        SettingsFeedbackKind.Warning => InfoBarSeverity.Warning,
        SettingsFeedbackKind.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
