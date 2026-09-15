namespace PiAgentGui.Controls;

public sealed partial class PendingIntegrationCard : UserControl
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Integration { get; set; } = "";
    public PendingIntegrationCard() => InitializeComponent();
}
