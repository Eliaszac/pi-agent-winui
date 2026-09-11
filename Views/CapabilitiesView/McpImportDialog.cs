using System.Text.Json;
using PiAgentGui.Controls;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Views;

public sealed class McpImportDialog : ActionContentDialog
{
    private readonly TextBox json = new() { Header = "MCP configuration JSON", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 220, MaxLength = 262144, PlaceholderText = "{ \"mcpServers\": { … } }" };
    private readonly StackPanel entriesPanel = new() { Spacing = 8 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private IReadOnlyList<McpImportEntry>? entries;
    private bool busy;
    private readonly McpServerForm manual = new();
    private readonly ComboBox mode = new ActionComboBox { ItemsSource = new[] { "Enter details", "Import JSON" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    public bool Saved { get; private set; }

    public McpImportDialog(McpConfigImporter importer, IEnumerable<string> runtimeNames)
    {
        Title = "Add MCP servers globally"; PrimaryButtonText = "Preview"; CloseButtonText = "Cancel";
        var names = runtimeNames.ToArray();
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = "Add a server globally, then restart Pi Agent to load it. Existing server names are preserved.", TextWrapping = TextWrapping.Wrap });
        var browse = new ActionButton { Content = "Choose JSON file" };
        var import = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
        import.Children.Add(browse); import.Children.Add(json);
        import.Children.Add(new TextBlock { Text = "Only mcpServers entries are imported. Other top-level settings are ignored.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(mode); body.Children.Add(manual); body.Children.Add(import); body.Children.Add(entriesPanel); body.Children.Add(status);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mode, "Add server method");
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Resources["ContentDialogMaxWidth"] = 720d;
        Content = scroll;
        Opened += (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => { XamlRoot.Changed -= OnRootChanged; };
        void Resize() { scroll.Width = Math.Max(160, Math.Min(560, XamlRoot.Size.Width - 120)); scroll.MaxHeight = Math.Max(120, XamlRoot.Size.Height - 200); }
        void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();
        mode.SelectionChanged += (_, _) =>
        {
            manual.Visibility = mode.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            import.Visibility = mode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            ResetPreview();
        };
        manual.Changed += ResetPreview;
        browse.Click += async (_, _) =>
        {
            try
            {
                var path = await new Services.Dialogs.PackageJsonPicker().PickAsync(XamlRoot, "Import JSON");
                if (path is null) return;
                using var stream = File.OpenRead(path);
                if (stream.Length > 262144) throw new ArgumentException("Import JSON is limited to 256 KB.");
                using var reader = new StreamReader(stream);
                var buffer = new char[262145];
                var count = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                if (count > 262144) throw new ArgumentException("Import JSON is limited to 256 KB.");
                json.Text = new string(buffer, 0, count);
            }
            catch (Exception exception) { status.Text = exception is ArgumentException ? exception.Message : "Couldn't read the JSON file."; }
        };
        json.TextChanged += (_, _) => ResetPreview();
        Closing += (_, args) => { if (busy) args.Cancel = true; };
        PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            if (busy) return;
            var deferral = args.GetDeferral(); busy = true; mode.IsEnabled = manual.IsEnabled = json.IsEnabled = browse.IsEnabled = IsPrimaryButtonEnabled = false;
            try
            {
                if (entries is null)
                {
                    entries = await importer.PreviewAsync(mode.SelectedIndex == 0 ? manual.BuildConfiguration() : json.Text, names);
                    foreach (var entry in entries)
                    {
                        var label = new StackPanel();
                        label.Children.Add(new TextBlock { Text = entry.Name });
                        label.Children.Add(new TextBlock { Text = entry.Description, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                        var check = new CheckBox { Content = label, IsChecked = entry.Selected, IsEnabled = entry.CanImport };
                        check.Checked += (_, _) => entry.Selected = true; check.Unchecked += (_, _) => entry.Selected = false;
                        entriesPanel.Children.Add(check);
                    }
                    PrimaryButtonText = mode.SelectedIndex == 0 ? "Add server" : "Import selected"; status.Text = "Review the selected servers. Credentials and adapter options are preserved in the global configuration.";
                }
                else { await importer.ImportAsync(entries); Saved = true; args.Cancel = false; }
            }
            catch (Exception exception)
            {
                entries = null; entriesPanel.Children.Clear(); PrimaryButtonText = "Preview";
                status.Text = exception is JsonException ? "Invalid JSON. Check commas, quotes and brackets." : exception is ArgumentException or IOException or InvalidOperationException ? exception.Message : "Couldn't import these servers.";
            }
            finally { busy = false; mode.IsEnabled = manual.IsEnabled = json.IsEnabled = browse.IsEnabled = IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
    }

    private void ResetPreview() { entries = null; entriesPanel.Children.Clear(); PrimaryButtonText = "Preview"; status.Text = ""; }
}
