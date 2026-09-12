namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private void OnCapabilitiesClicked(object sender, RoutedEventArgs args)
    {
        if (ViewModel.SelectedTarget is { IsLocal: false })
        {
            ViewModel.Chat?.ReportAttachmentError("Remote project instructions load before each turn. Edit them through the target terminal or agent. Third-party skills and MCP tools are not yet enabled on remote targets.");
            return;
        }
        Terminals.Hide(); Research.IsOpen = false; Files.IsOpen = false; Processes.IsOpen = false; SourceControl.IsOpen = false;
        Capabilities.Select(ViewModel.Chat);
        Capabilities.IsOpen = !Capabilities.IsOpen;
    }
}
