using PiAgentGui.Services.Dialogs;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>Compact table header menu with export feedback that does not resize the transcript.</summary>
internal sealed class MarkdownTableToolbar : UserControl
{
    private readonly MenuFlyoutItem reset;
    internal void SetSorted(bool sorted) => reset.IsEnabled = sorted;

    internal MarkdownTableToolbar(MarkdownTableData data, Action restore)
    {
        reset = new MenuFlyoutItem { Text = "Original order", Icon = new SymbolIcon(Symbol.Undo), IsEnabled = false };
        reset.Click += (_, _) => restore();
        var export = new ActionButton { Content = new FontIcon { Glyph = "\uE712", FontSize = 16 },
            MinWidth = 0, MinHeight = 0, Padding = new Thickness(4), BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        // Rotate only the glyph so the menu uses vertical dots while input and focus remain native.
        ((FontIcon)export.Content).RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        ((FontIcon)export.Content).RenderTransform = new Microsoft.UI.Xaml.Media.RotateTransform { Angle = 90 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(export, "Table options");
        ToolTipService.SetToolTip(export, "Table options");
        var menu = new MenuFlyout();
        var feedback = new TeachingTip { Target = export, IsLightDismissEnabled = true };
        foreach (var format in new[] { "csv", "json" })
        {
            var item = new MenuFlyoutItem { Text = "Export " + format.ToUpperInvariant() + "…" };
            item.Click += async (_, _) =>
            {
                export.IsEnabled = false;
                feedback.IsOpen = false;
                try
                {
                    // Capture the current order before the picker opens.
                    var content = format == "csv" ? data.ExportCsv() : data.ExportJson();
                    if (await new TableExportService().SaveAsync(XamlRoot, format, content))
                    { feedback.Title = "Table exported"; feedback.Subtitle = ""; feedback.IsOpen = true; }
                }
                catch (Exception)
                { feedback.Title = "Couldn't save the table"; feedback.Subtitle = "Choose another location or try again."; feedback.IsOpen = true; }
                finally { export.IsEnabled = true; }
            };
            menu.Items.Add(item);
        }
        export.Flyout = menu;
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(reset);
        var panel = new Grid { Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"] };
        panel.Children.Add(export); panel.Children.Add(feedback);
        Content = panel;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Unloaded += (_, _) => feedback.IsOpen = false;
    }
}
