using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Views;

public sealed class ExecutionTargetsDialog : Controls.ActionContentDialog
{
    private readonly IProjectRepository repository;
    private Project project;
    private readonly TargetSetupService setup = new(new TargetCommandRunner());
    private readonly TargetLocationViewModel location = new();
    private readonly Controls.TargetWorkspaceForm workspace = new();
    private readonly TextBox name = new() { Header = "Target name", PlaceholderText = "This computer", MaxLength = 120 };
    private readonly StackPanel list = new() { Spacing = 16 };
    private readonly StackPanel form = new() { Spacing = 24, Visibility = Visibility.Collapsed };
    private readonly StackPanel body = new() { Spacing = 20, Width = 560 };
    private readonly ContentControl bodyHost = new();
    private readonly InfoBar error = new() { Severity = InfoBarSeverity.Error, IsClosable = false };
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly StackPanel credentialsForm = new() { Spacing = 16, Visibility = Visibility.Collapsed };
    private readonly TextBlock credentialsDescription = new() { TextWrapping = TextWrapping.Wrap };
    private readonly PasswordBox credentialsSecret = new() { Header = "SSH password" };
    private ExecutionTarget? credentialsTarget;
    private bool adding;
    private bool busy;

    public ExecutionTargetsDialog(Project project, IProjectRepository repository, FolderPickerService picker, WslDistributionCache distributions)
    {
        this.repository = repository; this.project = project;
        Resources["ContentDialogMaxWidth"] = 648d;
        Title = "Execution targets"; PrimaryButtonText = "Add target"; CloseButtonText = "Done";
        workspace.Bind(location, picker, distributions);
        form.Children.Add(new TextBlock { Text = "Add another workspace to this project. Existing conversations keep their current target.", TextWrapping = TextWrapping.Wrap });
        form.Children.Add(name); form.Children.Add(workspace);
        credentialsForm.Children.Add(credentialsDescription); credentialsForm.Children.Add(credentialsSecret);
        credentialsForm.Children.Add(new TextBlock { Text = "Stored in Windows Credential Manager. The next SSH connection will use the updated credential.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        credentialsSecret.PasswordChanged += (_, _) => UpdateAction();
        body.Children.Add(list); body.Children.Add(form); body.Children.Add(credentialsForm); body.Children.Add(error); body.Children.Add(progress);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Opened += (_, _) => { ResizeForm(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => { workspace.ClearSecret(); credentialsSecret.Password = ""; if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
        bodyHost.Content = scroll; Content = bodyHost;
        RebuildList();
        name.TextChanged += (_, _) => UpdateAction();
        location.PropertyChanged += (_, _) =>
        {
            name.PlaceholderText = location.IsLocal ? "This computer" : location.IsWsl ? "Ubuntu workspace" : "Development server";
            UpdateAction();
        };
        PrimaryButtonClick += OnPrimary;
        SecondaryButtonClick += (_, args) => { args.Cancel = true; if (!busy && !workspace.IsPicking) ShowList(); };
        Closing += (_, args) => args.Cancel = busy || workspace.IsPicking;
    }
    private void UpdateAction() => IsPrimaryButtonEnabled = !busy && (credentialsTarget is not null ? credentialsSecret.Password.Length > 0 : !adding || name.Text.Trim().Length > 0 && location.IsLocationComplete);
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ResizeForm();
    private void ResizeForm() => body.Width = Math.Max(0, Math.Min(560, XamlRoot.Size.Width - 96));
    private void ShowList()
    {
        adding = false; credentialsTarget = null; credentialsSecret.Password = ""; credentialsForm.Visibility = Visibility.Collapsed; form.Visibility = Visibility.Collapsed; list.Visibility = Visibility.Visible;
        Title = "Execution targets"; PrimaryButtonText = "Add target"; SecondaryButtonText = ""; CloseButtonText = "Done";
        error.IsOpen = false; RebuildList(); UpdateAction();
    }
    private void RebuildList()
    {
        list.Children.Clear();
        list.Children.Add(new TextBlock { Text = "Choose where new conversations start. Each conversation keeps its own execution target.", TextWrapping = TextWrapping.Wrap });
        var preferred = ProjectTargets.Resolve(project).Id;
        foreach (var target in ProjectTargets.All(project))
        {
            var row = new Grid { ColumnSpacing = 14, Padding = new Thickness(0, 12, 0, 12) };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new FontIcon { Glyph = target.Glyph, FontSize = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(icon);
            var details = new StackPanel { Spacing = 6 }; Grid.SetColumn(details, 1);
            details.Children.Add(new TextBlock { Text = target.Name + (target.Id == preferred ? "  ·  Default" : ""), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            details.Children.Add(new TextBlock { Text = target.IsLocal ? "Local Windows · This computer" : target.Kind == "wsl" ? "WSL · " + target.Host : "SSH · " + target.Host, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            details.Children.Add(new TextBlock { Text = target.Path, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            if (target.Kind == "ssh" && target.HasSshSecret)
            {
                var credentials = new Controls.ActionButton { Content = target.SshAuthentication == "password" ? "Update password" : "Update key passphrase", HorizontalAlignment = HorizontalAlignment.Left };
                credentials.Click += (_, _) =>
                {
                    if (busy) return;
                    credentialsTarget = target; list.Visibility = Visibility.Collapsed; form.Visibility = Visibility.Collapsed; credentialsForm.Visibility = Visibility.Visible;
                    credentialsDescription.Text = target.Name + " · " + target.Host;
                    credentialsSecret.Header = target.SshAuthentication == "password" ? "SSH password" : "Key passphrase";
                    Title = "Update SSH credential"; PrimaryButtonText = "Save credential"; SecondaryButtonText = "Back"; CloseButtonText = "Cancel";
                    error.IsOpen = false; UpdateAction(); credentialsSecret.Focus(FocusState.Programmatic);
                };
                details.Children.Add(credentials);
            }
            if (target.Id != preferred)
            {
                var use = new Controls.ActionButton { Content = "Use as default", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(use, "Use " + target.Name + " as default");
                use.Click += async (_, _) => await RunAsync(async () =>
                {
                    await repository.SetDefaultTargetAsync(project.Id, target.Id);
                    await ReloadAsync(); RebuildList();
                });
                details.Children.Add(use);
            }
            row.Children.Add(details); list.Children.Add(row);
        }
    }
    private async void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (busy || workspace.IsPicking) return;
        if (credentialsTarget is { } savedTarget)
        {
            var credentialsDeferral = args.GetDeferral();
            var secret = credentialsSecret.Password;
            try { await RunAsync(async () => { await Task.Run(() => new SshCredentialSetup(new SshCredentialStore()).SaveAsync(savedTarget, secret)); ShowList(); }); }
            finally { credentialsDeferral.Complete(); }
            return;
        }
        if (!adding)
        {
            adding = true; list.Visibility = Visibility.Collapsed; form.Visibility = Visibility.Visible;
            Title = "Add execution target"; PrimaryButtonText = "Add target"; SecondaryButtonText = "Back"; CloseButtonText = "Cancel";
            error.IsOpen = false; UpdateAction(); name.Focus(FocusState.Programmatic); return;
        }
        var deferral = args.GetDeferral();
        try
        {
            await RunAsync(async () =>
            {
                var target = location.CreateTarget(Guid.NewGuid(), name.Text);
                var url = location.CloneRepository ? location.RepositoryUrl : null;
                var secret = location.IsSsh && location.SshSecret.Length > 0 ? location.SshSecret : null;
                target = await Task.Run(() => setup.PrepareAsync(target, url, sshSecret: secret));
                await repository.AddTargetAsync(project.Id, target, false);
                await ReloadAsync(); name.Text = ""; location.Path = ""; location.RepositoryUrl = ""; workspace.ClearSecret();
                ShowList();
            });
        }
        finally { deferral.Complete(); }
    }
    private async Task ReloadAsync() => project = (await repository.GetAllAsync()).Single(item => item.Id == project.Id);
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; bodyHost.IsEnabled = false; IsSecondaryButtonEnabled = false; error.IsOpen = false; progress.Visibility = Visibility.Visible; UpdateAction();
        try { await action(); }
        catch (Exception exception) { error.Message = exception is ArgumentException or IOException ? exception.Message : "Couldn't save the target. Check its settings and try again."; error.IsOpen = true; }
        finally { busy = false; bodyHost.IsEnabled = true; IsSecondaryButtonEnabled = true; progress.Visibility = Visibility.Collapsed; UpdateAction(); }
    }
}
