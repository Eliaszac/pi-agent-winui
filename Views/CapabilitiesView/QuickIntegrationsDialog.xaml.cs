namespace PiAgentGui.Views;

public sealed partial class QuickIntegrationsDialog : Controls.ActionContentDialog
{
    public QuickIntegration? SelectedIntegration { get; private set; }
    private void OnAdd(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: QuickIntegration integration })
        {
            SelectedIntegration = integration;
            Hide();
        }
    }
    public IReadOnlyList<QuickIntegration> Integrations { get; } =
    [
        new("Obsidian", "Search and update notes in a local vault. No API key required.", "Community · StevenStavrakis/obsidian-mcp", "obsidian", "https://github.com/StevenStavrakis/obsidian-mcp")
    ];

    public QuickIntegrationsDialog(IEnumerable<string>? configured = null)
    {
        var names = new HashSet<string>(configured ?? [], StringComparer.Ordinal);
        Integrations = Integrations.Select(item => item with { Configured = names.Contains(item.Icon) }).ToArray();
        InitializeComponent();
        Resources["ContentDialogMaxWidth"] = 620d;
        Opened += (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => { if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();

    private void Resize()
    {
        Body.Width = Math.Max(180, Math.Min(520, XamlRoot.Size.Width - 96));
        Body.MaxHeight = Math.Max(160, XamlRoot.Size.Height - 190);
    }
}
