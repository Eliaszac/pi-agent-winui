using PiAgentGui.Controls;
using PiAgentGui.Utilities;

namespace PiAgentGui.Views;

/// <summary>Native manual entry fields; saving uses the shared MCP import service.</summary>
public sealed class McpServerForm : UserControl
{
    private readonly TextBox name = new() { Header = "Server name", PlaceholderText = "my-server", MaxLength = 128 };
    private readonly ComboBox transport = new ActionComboBox { Header = "Transport", ItemsSource = new[] { "stdio · local executable", "HTTP · remote URL" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox command = new() { Header = "Command or executable path", PlaceholderText = "npx, uvx, pwsh, or an executable path" };
    private readonly TextBox arguments = new() { Header = "Arguments · one per line", PlaceholderText = "-y\n@package/server", AcceptsReturn = true, Height = 85, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox environment = new() { Header = "Environment variables · optional", PlaceholderText = "API_KEY=value", AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox url = new() { Header = "Server URL", PlaceholderText = "https://example.com/mcp" };
    private readonly TextBox headers = new() { Header = "Headers · optional", PlaceholderText = "Authorization=Bearer token", AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
    public event Action? Changed;

    public McpServerForm()
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(name); body.Children.Add(transport);
        var local = new StackPanel { Spacing = 12 }; local.Children.Add(command); local.Children.Add(arguments); local.Children.Add(environment);
        local.Children.Add(new TextBlock { Text = "Enter arguments separately, without shell quotes. Use NAME=value on each environment-variable line.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var remote = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed }; remote.Children.Add(url); remote.Children.Add(headers);
        body.Children.Add(local); body.Children.Add(remote); Content = body;
        foreach (var input in new[] { name, command, arguments, environment, url, headers })
        {
            input.MaxLength = input == name ? 128 : 32768;
            input.TextChanged += (_, _) => Changed?.Invoke();
        }
        transport.SelectionChanged += (_, _) =>
        {
            local.Visibility = transport.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            remote.Visibility = transport.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            Changed?.Invoke();
        };
    }

    public string BuildConfiguration() => McpServerDefinitionBuilder.Build(name.Text, transport.SelectedIndex == 0, command.Text, arguments.Text, environment.Text, url.Text, headers.Text);
}
