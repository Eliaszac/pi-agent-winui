using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace PiAgentGui.Controls;

/// <summary>Filled vector shapes keep the composer action legible at every display scale.</summary>
public sealed class ComposerActionIcon : UserControl
{
    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(nameof(IsRunning), typeof(bool),
        typeof(ComposerActionIcon), new PropertyMetadata(false, (sender, _) => ((ComposerActionIcon)sender).Update()));
    public bool IsRunning { get => (bool)GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    private readonly Polygon arrow = new()
    {
        Points = new PointCollection
        {
            new Point(9, 1), new Point(16, 8), new Point(9, 15), new Point(7.5, 13.5),
            new Point(11.75, 9.25), new Point(1, 9.25), new Point(1, 6.75),
            new Point(11.75, 6.75), new Point(7.5, 2.5)
        }
    };
    private readonly Rectangle stop = new() { Width = 12, Height = 12, RadiusX = 1, RadiusY = 1 };

    public ComposerActionIcon()
    {
        var panel = new Grid { Width = 16, Height = 16 };
        panel.Children.Add(arrow);
        panel.Children.Add(stop);
        Content = panel;
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Update());
        Loaded += (_, _) => Update();
        ActualThemeChanged += (_, _) => Update();
        Update();
    }

    private void Update()
    {
        var iconBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
        arrow.Fill = iconBrush;
        stop.Fill = iconBrush;
        arrow.Visibility = IsRunning ? Visibility.Collapsed : Visibility.Visible;
        stop.Visibility = IsRunning ? Visibility.Visible : Visibility.Collapsed;
    }
}
