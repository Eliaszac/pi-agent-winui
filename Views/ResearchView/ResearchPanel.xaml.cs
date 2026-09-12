using PiAgentGui.ViewModels.Conversations;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

public sealed partial class ResearchPanel : UserControl
{

    public event Action<string>? ShareRequested;
    public ResearchPanel() => InitializeComponent();
    private ResearchPanelViewModel? ViewModel => DataContext as ResearchPanelViewModel;
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

