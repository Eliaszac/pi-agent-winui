using System.Text.Json;
using PiAgentGui.Controls;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.Views;

/// <summary>Native auth interaction. Secret fields never become composer text or persistent view-model state.</summary>
public sealed class ProviderSetupDialog : ActionContentDialog
{
    private readonly ProvidersViewModel viewModel;
    private readonly ProviderCardViewModel card;
    private readonly StackPanel actions = new() { Spacing = 8 };
    private readonly StackPanel events = new() { Spacing = 8 };
    private readonly StackPanel fields = new() { Spacing = 8 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private CancellationTokenSource? operation;
    private TaskCompletionSource<string?>? answer;
    private string? promptId;
    private PasswordBox? secret;
    private bool closed;

    public ProviderSetupDialog(ProvidersViewModel viewModel, ProviderCardViewModel card)
    {
        this.viewModel = viewModel;
        this.card = card;
        Title = card.Name;
        CloseButtonText = "Close";
        var content = new StackPanel { Spacing = 16, MinWidth = 260 };
        content.Children.Add(new TextBlock { Text = card.Source, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Credentials are managed globally by Pi. Signing in replaces the saved credential for this provider.", TextWrapping = TextWrapping.Wrap });
        if (card.CanSignIn) AddAction(card.LoginLabel, "login", "oauth");
        if (card.CanAddKey) AddAction(card.KeyLabel, "login", "api_key");
        if (card.CanSignOut) AddAction("Sign out of Pi", "logout", null);
        if (!card.CanSignIn && !card.CanAddKey)
            actions.Children.Add(new TextBlock { Text = "This provider uses environment or local configuration. Follow Pi's provider setup instructions, then refresh this page.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(actions);
        content.Children.Add(new HyperlinkButton { Content = "Pi provider setup documentation", NavigateUri = new Uri("https://pi.dev/docs/latest/providers") });
        content.Children.Add(progress);
        content.Children.Add(status);
        content.Children.Add(events);
        content.Children.Add(fields);
        Content = new ScrollViewer { MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content };
        Closed += (_, _) => { closed = true; operation?.Cancel(); ClearPrompt(); };
    }

    private void AddAction(string label, string action, string? method)
    {
        var button = new ActionButton { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += async (_, _) => await RunAsync(action, method);
        actions.Children.Add(button);
    }

    private async Task RunAsync(string action, string? method)
    {
        if (operation is not null) return;
        operation = new CancellationTokenSource();
        foreach (var button in actions.Children.OfType<Control>()) button.IsEnabled = false;
        progress.Visibility = Visibility.Visible;
        CloseButtonText = "Cancel";
        status.Text = action == "logout" ? "Removing Pi's saved credential…" : "Preparing sign-in…";
        events.Children.Clear();
        try
        {
            await viewModel.RunAsync(action, card, method, PromptAsync, Notify, operation.Token);
            status.Text = action == "logout"
                ? "Saved credential removed. Environment or local configuration may still provide access."
                : "Credentials saved in Pi. You can close this dialog.";
        }
        catch (OperationCanceledException) { status.Text = "Operation cancelled. Refresh to check the current status."; }
        catch (Exception exception) { status.Text = exception.Message; }
        finally
        {
            ClearPrompt();
            progress.Visibility = Visibility.Collapsed;
            CloseButtonText = "Close";
            operation.Dispose();
            operation = null;
            // Reopen setup after refresh to obtain the current credential/actions state.
        }
    }

    private Task<string?> PromptAsync(JsonElement prompt, CancellationToken token)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = token.Register(() => completion.TrySetCanceled(token));
        _ = completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (closed || token.IsCancellationRequested) { completion.TrySetCanceled(token); return; }
            ClearPrompt();
            answer = completion;
            promptId = PiJson.Text(prompt, "promptId");
            fields.Children.Add(new TextBlock { Text = PiJson.Text(prompt, "message") ?? "Continue sign-in", TextWrapping = TextWrapping.Wrap });
            var kind = PiJson.Text(prompt, "type");
            Func<string?> value;
            if (kind == "select")
            {
                var choices = PiJson.Field(prompt, "options").EnumerateArray().ToArray();
                var select = new ActionComboBox { ItemsSource = choices.Select(choice => PiJson.Text(choice, "label")).ToArray(), HorizontalAlignment = HorizontalAlignment.Stretch };
                fields.Children.Add(select);
                value = () => select.SelectedIndex < 0 ? null : PiJson.Text(choices[select.SelectedIndex], "id");
            }
            else if (kind is "secret" or "manual_code")
            {
                secret = new PasswordBox { PlaceholderText = PiJson.Text(prompt, "placeholder") ?? "", PasswordRevealMode = PasswordRevealMode.Peek };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(secret, PiJson.Text(prompt, "message") ?? "Authentication value");
                fields.Children.Add(secret);
                value = () => secret?.Password;
                secret.Focus(FocusState.Programmatic);
            }
            else
            {
                var input = new TextBox { PlaceholderText = PiJson.Text(prompt, "placeholder") ?? "" };
                fields.Children.Add(input);
                value = () => input.Text;
                input.Focus(FocusState.Programmatic);
            }
            var submit = new ActionButton { Content = "Continue" };
            submit.Click += (_, _) =>
            {
                var result = value();
                if (string.IsNullOrWhiteSpace(result)) return;
                completion.TrySetResult(result);
                ClearPrompt();
            };
            fields.Children.Add(submit);
        })) completion.TrySetException(new IOException("The sign-in window is unavailable."));
        return completion.Task;
    }

    private void Notify(JsonElement packet) => DispatcherQueue.TryEnqueue(() =>
    {
        if (closed || operation is null) return;
        if (PiJson.Text(packet, "kind") == "dismiss")
        {
            if (PiJson.Text(packet, "promptId") == promptId) ClearPrompt();
            return;
        }
        var data = PiJson.Field(packet, "event");
        switch (PiJson.Text(data, "type"))
        {
            case "progress": status.Text = PiJson.Text(data, "message") ?? "Signing in…"; break;
            case "auth_url":
                AddLink(PiJson.Text(data, "url"), "Open sign-in page");
                status.Text = PiJson.Text(data, "instructions") ?? "Continue in your browser.";
                break;
            case "device_code":
                AddLink(PiJson.Text(data, "verificationUri"), "Open verification page");
                events.Children.Add(new TextBlock { Text = "Code: " + PiJson.Text(data, "userCode"), IsTextSelectionEnabled = true, FontSize = 20 });
                status.Text = "Enter this code in your browser. Waiting for approval…";
                break;
            case "info":
                status.Text = PiJson.Text(data, "message") ?? "";
                if (PiJson.Field(data, "links") is { ValueKind: JsonValueKind.Array } links)
                    foreach (var link in links.EnumerateArray()) AddLink(PiJson.Text(link, "url"), PiJson.Text(link, "label") ?? "Open provider page");
                break;
        }
    });

    private void AddLink(string? url, string label)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            events.Children.Add(new HyperlinkButton { Content = label, NavigateUri = uri });
    }

    private void ClearPrompt()
    {
        answer?.TrySetResult(null);
        answer = null;
        promptId = null;
        if (secret is not null) secret.Password = "";
        secret = null;
        fields.Children.Clear();
    }
}
