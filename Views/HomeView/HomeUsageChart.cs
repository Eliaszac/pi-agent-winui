using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using PiAgentGui.Models.Home;

namespace PiAgentGui.Views;

public sealed class HomeUsageChart : UserControl
{
    private IReadOnlyList<HomeUsageDay> renderedDays = [];
    public static readonly DependencyProperty DaysProperty = DependencyProperty.Register(nameof(Days), typeof(IReadOnlyList<HomeUsageDay>),
        typeof(HomeUsageChart), new PropertyMetadata(null, (owner, _) => ((HomeUsageChart)owner).Render()));
    public IReadOnlyList<HomeUsageDay>? Days { get => (IReadOnlyList<HomeUsageDay>?)GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    public HomeUsageChart() { ActualThemeChanged += (_, _) => Render(true); Loaded += (_, _) => Render(); }

    private void Render(bool force = false)
    {
        if (!force && Content is not null && renderedDays.SequenceEqual(Days ?? [])) return;
        renderedDays = Days?.ToArray() ?? [];
        var chart = new Grid { Height = 72, ColumnSpacing = 2 };
        foreach (var day in Days ?? [])
        {
            var column = chart.ColumnDefinitions.Count;
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var bar = new Border { Height = Math.Max(1, day.Height), CornerRadius = new CornerRadius(2, 2, 0, 0),
                Background = (Brush)Application.Current.Resources[day.Tokens > 0 ? "TextFillColorSecondaryBrush" : "DividerStrokeColorDefaultBrush"] };
            var button = new Controls.ActionButton { Content = bar, Padding = new Thickness(0), MinWidth = 0, MinHeight = 0,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Bottom };
            ToolTipService.SetToolTip(button, day.Description);
            AutomationProperties.SetName(button, day.Description);
            button.Flyout = new Flyout { Content = new TextBlock { Text = day.Description } };
            Grid.SetColumn(button, column); chart.Children.Add(button);
        }
        Content = chart;
    }
}
