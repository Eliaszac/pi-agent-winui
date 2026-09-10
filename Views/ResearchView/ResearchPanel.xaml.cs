using PiAgentGui.ViewModels.Conversations;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

public sealed partial class ResearchPanel : UserControl
{
    private bool changingResearch;
    public event Action<string>? ShareRequested;
    public ResearchPanel() => InitializeComponent();
    private ResearchPanelViewModel? ViewModel => DataContext as ResearchPanelViewModel;
    private async void OnResearchToggled(object sender, RoutedEventArgs args)
    {
        if (changingResearch || sender is not ToggleSwitch toggle || ViewModel is not { } model || toggle.IsOn == model.Enabled) return;
        changingResearch = true;
        toggle.IsEnabled = false;
        try { await model.ToggleEnabledCommand.ExecuteAsync(null); }
        finally
        {
            toggle.IsOn = model.Enabled;
            toggle.IsEnabled = true;
            changingResearch = false;
        }
    }
    private void OnClose(object sender, RoutedEventArgs args) { if (ViewModel is { } model) model.IsOpen = false; }
    private void OnShare(object sender, RoutedEventArgs args)
    {
        if (ViewModel is { CanShare: true, Selected: { } task }) ShareRequested?.Invoke($"Background research: {task.Title}\n\n{task.Result}");
    }
    private void OnCopy(object sender, RoutedEventArgs args)
    {
        try { if (ViewModel is { CanShare: true, Selected: { } task }) { var data = new DataPackage(); data.SetText(task.Result); Clipboard.SetContent(data); if (sender is PiAgentGui.Controls.CopyFeedbackButton button) button.ShowCopied(); } }
        catch (System.Runtime.InteropServices.COMException) { ViewModel?.ReportError("Couldn't copy the result. Try again."); }
    }
}
