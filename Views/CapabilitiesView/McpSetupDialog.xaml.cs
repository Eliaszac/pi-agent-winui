using System.ComponentModel;
using System.Text.Json;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class McpSetupDialog : Controls.ActionContentDialog
{
    private readonly McpSetupViewModel model;
    private readonly Func<Task<string?>>? pickFolder;
    private TaskCompletionSource<string?>? callback;
    public bool Saved => model.IsSaved;

    public McpSetupDialog(McpSetupViewModel model, string description, string? documentation, Func<Task<string?>>? pickFolder)
    {
        this.model = model; this.pickFolder = pickFolder;
        InitializeComponent();
        Title = "Set up " + model.Name;
        Description.Text = description;
        Authentication.ItemsSource = new[] { "Use existing / detect automatically", "Browser sign-in (OAuth)", "API token", "No authentication" };
        if (Uri.TryCreate(documentation, UriKind.Absolute, out var link) && link.Scheme == "https") Documentation.NavigateUri = link;
        else Documentation.Visibility = Visibility.Collapsed;
        DataContext = model;
        Resources["ContentDialogMaxWidth"] = 620d;
        model.Prompt = PromptAsync;
        model.PropertyChanged += OnModelChanged;
        Opened += async (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; await model.InitializeAsync(); UpdateActions(); };
        Closing += (_, _) => { model.Cancel(); callback?.TrySetResult(null); };
        Closed += (_, _) => { XamlRoot.Changed -= OnRootChanged; model.PropertyChanged -= OnModelChanged; Secret.Password = Callback.Password = ""; model.Dispose(); };
        PrimaryButtonClick += async (_, args) => { args.Cancel = true; await RunAsync(true); };
        SecondaryButtonClick += async (_, args) => { args.Cancel = true; await RunAsync(false); };
        UpdateActions();
    }

    private async Task RunAsync(bool connect)
    {
        var token = model.NeedsToken ? Secret.Password : "";
        Secret.Password = "";
        await model.RunAsync(connect, token);
        CallbackPanel.Visibility = Visibility.Collapsed;
        Callback.Password = "";
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => UpdateActions();
    private void UpdateActions() { PrimaryButtonText = model.PrimaryLabel; IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = model.CanAct; }
    private async void OnBrowse(object sender, RoutedEventArgs args)
    {
        if (pickFolder is null) return;
        try { if (await pickFolder() is { } folder) model.Folder = folder; }
        catch (Exception) { Description.Text = "Couldn't open the folder picker. You can enter the vault path directly."; }
    }
    private void OnCancelConnection(object sender, RoutedEventArgs args) { model.Cancel(); callback?.TrySetResult(null); }
    private void OnCompleteSignIn(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(Callback.Password)) return;
        callback?.TrySetResult(Callback.Password); Callback.Password = ""; CallbackPanel.Visibility = Visibility.Collapsed;
    }
    private async Task<string?> PromptAsync(JsonElement packet, CancellationToken token)
    {
        callback?.TrySetResult(null);
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        callback = pending;
        var url = McpAuthorizationLink.Parse(PiJson.Text(packet, "title"));
        AuthorizationLink.NavigateUri = url;
        AuthorizationLink.Visibility = url is null ? Visibility.Collapsed : Visibility.Visible;
        CallbackPanel.Visibility = Visibility.Visible;
        try { return await pending.Task.WaitAsync(token); }
        finally { if (ReferenceEquals(callback, pending)) { callback = null; CallbackPanel.Visibility = Visibility.Collapsed; Callback.Password = ""; } }
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();
    private void Resize() { Scroller.Width = Math.Max(180, Math.Min(520, XamlRoot.Size.Width - 96)); Scroller.MaxHeight = Math.Max(160, XamlRoot.Size.Height - 200); }
}
