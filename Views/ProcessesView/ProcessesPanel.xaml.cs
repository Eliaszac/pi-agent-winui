using PiAgentGui.ViewModels.Processes;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Views;

public sealed partial class ProcessesPanel : UserControl
{
    public ProcessesPanel() => InitializeComponent();
    private bool confirming;

    private async void OnStop(object sender, RoutedEventArgs args)
    {
        if (confirming || DataContext is not ProcessesPanelViewModel model || !model.CanStop
            || sender is not FrameworkElement { Tag: AgentProcess process }) return;
        confirming = true;
        try
        {
            var dialog = new Controls.ActionContentDialog
            {
                XamlRoot = XamlRoot, Title = $"Stop {process.Presentation.Title}?",
                Content = $"This will forcibly terminate {process.Name} (process ID {process.Identity.Id}) and its child processes. Active work may fail and unsaved output may be lost. This cannot be undone."
                    + (process.Presentation.IsWindowsHelper ? " Stopping a Windows helper may interrupt the console application using it." : ""),
                PrimaryButtonText = "Stop", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) await model.StopAsync(process);
        }
        catch (Exception) { model.ReportError("Couldn't open the stop confirmation. Close any other dialog and try again."); }
        finally { confirming = false; }
    }
    private void OnClose(object sender, RoutedEventArgs args)
    {
        if (DataContext is ProcessesPanelViewModel model) model.IsOpen = false;
    }
}
