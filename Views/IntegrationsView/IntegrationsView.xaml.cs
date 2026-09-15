namespace PiAgentGui.Views;

public sealed partial class IntegrationsView : UserControl
{
    public event EventHandler<string>? ActionRequested;
    public IntegrationsView() => InitializeComponent();
    private void OnAction(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, (string)((FrameworkElement)sender).Tag);
}
