namespace PiAgentGui.Views;

public sealed partial class IntegrationsView : UserControl
{
    public event EventHandler<string>? ActionRequested;
    public IntegrationsView() => InitializeComponent();
    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var columns = args.NewSize.Width >= 720 ? 2 : 1;
        SecondCardColumn.Width = columns == 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        IntegrationCards.ColumnSpacing = columns == 2 ? 16 : 0;
        var rows = (IntegrationCards.Children.Count + columns - 1) / columns;
        while (IntegrationCards.RowDefinitions.Count < rows) IntegrationCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < IntegrationCards.Children.Count; index++)
        {
            if (IntegrationCards.Children[index] is not FrameworkElement card) continue;
            Grid.SetRow(card, index / columns);
            Grid.SetColumn(card, index % columns);
        }
    }
    private void OnAction(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, (string)((FrameworkElement)sender).Tag);
}
