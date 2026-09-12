namespace PiAgentGui.Views;

public sealed record QuickIntegration(string Name, string Description, string Publisher, string Icon, string Documentation, bool Configured = false)
{
    public string IconPath => $"ms-appx:///Assets/Integrations/{Icon}.svg";
    public string DocumentationLabel => $"Documentation for {Name}";
    public string ActionLabel => Configured ? "Manage" : "Add";
    public string AddLabel => $"{ActionLabel} {Name}";
}
