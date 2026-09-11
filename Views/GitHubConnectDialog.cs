using PiAgentGui.Controls;
using PiAgentGui.ViewModels.GitHub;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace PiAgentGui.Views;

public sealed class GitHubConnectDialog : ActionContentDialog
{
    private readonly CancellationTokenSource cancellation;
    private readonly TextBlock code = new() { FontSize = 24, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), IsTextSelectionEnabled = true };
    private readonly TextBlock status = new() { Text = "Requesting a sign-in code…", TextWrapping = TextWrapping.Wrap };

    public GitHubConnectDialog(GitHubViewModel viewModel, CancellationToken lifetime)
    {
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        Title = "Connect to GitHub";
        PrimaryButtonText = "Open GitHub";
        IsPrimaryButtonEnabled = false;
        CloseButtonText = "Cancel";
        var copy = new ActionButton { Content = "Copy code", IsEnabled = false };
        copy.Click += (_, _) => { var data = new DataPackage(); data.SetText(code.Text); Clipboard.SetContent(data); copy.Content = "Copied"; };
        var content = new StackPanel { Spacing = 16, MaxWidth = 420 };
        content.Children.Add(new TextBlock { Text = "Enter this code on GitHub to authorize Pi desktop. Only approve a code you requested here.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(code);
        content.Children.Add(copy);
        content.Children.Add(status);
        Content = content;
        PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            try { if (!await Launcher.LaunchUriAsync(new Uri("https://github.com/login/device"))) status.Text = "Open github.com/login/device in your browser."; }
            catch (Exception) { status.Text = "Open github.com/login/device in your browser."; }
        };
        Opened += async (_, _) =>
        {
            try
            {
                await viewModel.ConnectAsync(value =>
                {
                    code.Text = value;
                    copy.IsEnabled = true;
                    IsPrimaryButtonEnabled = true;
                    status.Text = "Waiting for authorization in your browser…";
                }, cancellation.Token);
                Hide();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                status.Text = exception is IOException ? exception.Message : "Couldn't sign in to GitHub. Check your connection and try again.";
                IsPrimaryButtonEnabled = false;
                CloseButtonText = "Close";
            }
        };
        Closed += (_, _) => cancellation.Cancel();
    }
}
