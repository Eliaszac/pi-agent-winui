namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private void OnCapabilitiesClicked(object sender, RoutedEventArgs args)
    {
        Terminals.Hide(); Research.IsOpen = false; Files.IsOpen = false; Processes.IsOpen = false; SourceControl.IsOpen = false;
        Capabilities.Select(ViewModel.Chat);
        Capabilities.IsOpen = !Capabilities.IsOpen;
    }
}
