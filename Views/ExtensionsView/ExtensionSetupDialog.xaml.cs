using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class ExtensionSetupDialog : Controls.ActionContentDialog
{
    public ExtensionSetupDialog() : this(SupportedExtensions.Permissions) { }

    public ExtensionSetupDialog(ExtensionDefinition definition, bool details = false)
    {
        InitializeComponent();
        Title = details ? definition.Name : $"Set up {definition.Name}";
        Sections.Children.Add(new TextBlock { Text = $"Third-party extension by {definition.Author} · supported version {definition.Version}", TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Sections.Children.Add(new TextBlock { Text = "Created and maintained by its independent author. Pi Agent supports using this extension; we do not develop or own it.", TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new TextBlock { Text = definition.Details, TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new Controls.ActionHyperlinkButton { Content = "Documentation", NavigateUri = definition.Documentation });
        Sections.Children.Add(new Controls.ActionHyperlinkButton { Content = "Source code", NavigateUri = definition.Source });
        Sections.Children.Add(new TextBlock { Text = "Installation and configuration", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Sections.Children.Add(new TextBlock { Text = "Run this command in your terminal to install globally. Finish active runs, then restart Pi Agent.", TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new Controls.CodeBlockView("PowerShell", definition.InstallCommand));
        Sections.Children.Add(new TextBlock { Text = definition.Configuration, TextWrapping = TextWrapping.Wrap });
        Sections.Children.Add(new Controls.CodeBlockView("JSON", definition.ConfigurationJson));
    }
}


