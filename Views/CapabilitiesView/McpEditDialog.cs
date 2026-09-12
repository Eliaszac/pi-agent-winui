using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Controls;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Views;

/// <summary>Explicitly edits or removes one global server definition.</summary>
public sealed class McpEditDialog : ActionContentDialog
{
    public bool Saved { get; private set; }

    public McpEditDialog(McpSetupService store, string name, JsonObject original, bool remove)
    {
        Title = remove ? $"Remove {name}?" : $"Edit {name}";
        PrimaryButtonText = remove ? "Remove server" : "Save changes";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Close;
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock
        {
            Text = remove
                ? "Remove this server from the global MCP configuration? Running conversations keep their loaded connection until Pi desktop restarts. Saved credentials and remote account access are not revoked."
                : "Edit this server's configuration, including its URL, command, arguments, or environment settings. Changes load after restarting Pi desktop. Use Manage connection for secure token entry and sign-in.",
            TextWrapping = TextWrapping.Wrap
        });
        var editor = new TextBox
        {
            Header = "Server definition (JSON)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 260, MaxLength = 262144, Visibility = remove ? Visibility.Collapsed : Visibility.Visible,
            Text = remove ? "" : original.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        };
        body.Children.Add(editor);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        body.Children.Add(status);
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Content = scroll;
        Resources["ContentDialogMaxWidth"] = 720d;
        void Resize() { scroll.Width = Math.Max(160, Math.Min(560, XamlRoot.Size.Width - 120)); scroll.MaxHeight = Math.Max(120, XamlRoot.Size.Height - 200); }
        void Changed(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();
        Opened += (_, _) => { Resize(); XamlRoot.Changed += Changed; };
        Closed += (_, _) => { XamlRoot.Changed -= Changed; editor.Text = ""; };
        var busy = false;
        Closing += (_, args) => { if (busy) args.Cancel = true; };
        PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            if (busy) return;
            var deferral = args.GetDeferral();
            busy = true; IsPrimaryButtonEnabled = editor.IsEnabled = false;
            try
            {
                if (remove) await store.RemoveAsync(name, original);
                else await store.EditAsync(name, original, editor.Text);
                Saved = true; args.Cancel = false;
            }
            catch (JsonException) { status.Text = "Invalid JSON. Check commas, quotes, and brackets."; }
            catch (Exception exception)
            {
                status.Text = exception is IOException or ArgumentException or InvalidOperationException
                    ? exception.Message : "Couldn't update this server. Try again.";
            }
            finally { busy = false; IsPrimaryButtonEnabled = editor.IsEnabled = true; deferral.Complete(); }
        };
    }
}
