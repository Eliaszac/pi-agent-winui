using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class ExtensionSetupDialog : Controls.ActionContentDialog
{
    public ExtensionSetupDialog() : this(SupportedExtensions.Permissions) { }

    public ExtensionSetupDialog(ExtensionDefinition definition, bool details = false)
    {
        InitializeComponent();
        Title = details ? definition.Name : $"Set up {definition.Name}";
        if (definition.Bundled)
        {
            Title = $"Manage {definition.Name}";
            Sections.Children.Add(new TextBlock { Text = definition.Details, TextWrapping = TextWrapping.Wrap });
            Sections.Children.Add(new TextBlock { Text = "Default limits: 8 MiB per file and 1 GiB of snapshot content per target user. Keep the latest five completed checkpoints per conversation, with a 30-day age limit. Pending recovery and Undo data are protected from the five-checkpoint limit. Generated files, dependencies, ignored files and common secret files are excluded. Existing conversations apply the setting before their next request. Older versions require an app restart.", TextWrapping = TextWrapping.Wrap });
            var toggle = new ToggleSwitch { Header = "Capture workspace checkpoints", IsOn = Utilities.CheckpointSettings.IsEnabled() };
            var notice = new TextBlock { TextWrapping = TextWrapping.Wrap };
            toggle.Toggled += async (_, _) =>
            {
                var enabled = toggle.IsOn;
                toggle.IsEnabled = false;
                try { await Task.Run(() => Utilities.CheckpointSettings.SetEnabled(enabled)); notice.Text = "Saved. Disabling capture keeps existing recovery data."; }
                catch (Exception error) { notice.Text = "Could not save: " + error.Message; }
                finally { toggle.IsEnabled = true; }
            };
            Sections.Children.Add(toggle);
            Sections.Children.Add(notice);
            AddCheckpointDataManagement();
            return;
        }
        Sections.Children.Add(new TextBlock { Text = $"Third-party extension by {definition.Author} · {(definition.RecommendationOnly ? "recommended" : "supported")} version {definition.Version}", TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Sections.Children.Add(new TextBlock { Text = "Created and maintained by its independent author. We do not develop or own this extension.", TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new TextBlock { Text = definition.Details, TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new Controls.ActionHyperlinkButton { Content = "Documentation", NavigateUri = definition.Documentation });
        Sections.Children.Add(new Controls.ActionHyperlinkButton { Content = "Source code", NavigateUri = definition.Source });
        Sections.Children.Add(new TextBlock { Text = "Installation and configuration", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Sections.Children.Add(new TextBlock { Text = "Finish active runs, run this command in your terminal to install globally, then restart Pi desktop.", TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new Controls.CodeBlockView("PowerShell", definition.InstallCommand));
        Sections.Children.Add(new TextBlock { Text = definition.Configuration, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(definition.ConfigurationJson))
            Sections.Children.Add(new Controls.CodeBlockView("JSON", definition.ConfigurationJson));
    }
}


