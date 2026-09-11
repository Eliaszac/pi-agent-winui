using PiAgentGui.Controls;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Views;

public sealed class AddSkillDialog : ActionContentDialog
{
    private readonly StackPanel form = new() { Spacing = 12 };
    private readonly ComboBox mode = new ActionComboBox { Header = "Source", ItemsSource = new[] { "Local folder", "Pi package" }, SelectedIndex = 0 };
    private readonly TextBox source = new() { Header = "Package source", PlaceholderText = "npm:package-name or git:github.com/owner/repo", Visibility = Visibility.Collapsed };
    private readonly TextBlock folderLabel = new() { Text = "No folder selected", TextWrapping = TextWrapping.Wrap };
    private readonly ActionButton browse = new() { Content = "Choose folder" };
    private readonly StackPanel review = new() { Spacing = 10 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CapabilityImportServices services;
    private string? folder;
    private string? reviewed;
    private bool busy;
    private CancellationTokenSource? operation;
    public bool Saved { get; private set; }

    public AddSkillDialog(CapabilityImportServices services, Func<Task<string?>> pick)
    {
        this.services = services;
        Title = "Add skills globally"; PrimaryButtonText = "Review"; CloseButtonText = "Cancel";
        form.Children.Add(mode); form.Children.Add(browse); form.Children.Add(folderLabel); form.Children.Add(source);
        form.Children.Add(review); form.Children.Add(status);
        Content = new ScrollViewer { Content = form, MaxHeight = 600, MinWidth = 360 };
        mode.SelectionChanged += (_, _) =>
        {
            source.Visibility = mode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            browse.Visibility = folderLabel.Visibility = mode.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ResetReview();
        };
        source.TextChanged += (_, _) => ResetReview();
        browse.Click += async (_, _) =>
        {
            try { if (await pick() is { } selected) { folder = selected; folderLabel.Text = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar)); ResetReview(); } }
            catch (Exception) { status.Text = "Couldn't open the folder picker. Try again."; }
        };
        PrimaryButtonClick += OnPrimary;
        Closing += (_, args) => { if (busy) { args.Cancel = true; operation?.Cancel(); status.Text = "Cancelling installation…"; } };
    }

    private void ResetReview() { reviewed = null; review.Children.Clear(); status.Text = ""; PrimaryButtonText = "Review"; }
    private async void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (busy) return;
        var deferral = args.GetDeferral();
        try
        {
            if (reviewed is null)
            {
                reviewed = mode.SelectedIndex == 0 ? folder ?? throw new ArgumentException("Choose a skill folder first.") : PiPackageSource.Validate(source.Text);
                review.Children.Add(new TextBlock { Text = mode.SelectedIndex == 0
                    ? "Register this entire folder globally in place. Scripts and supporting files are accepted unchanged. Keep the folder at its current location. Pi discovers the skills when restarted."
                    : "Install this Pi package globally. Packages can contain extensions, skills, prompts and themes, and installation may run package scripts. An existing package may be updated by Pi.", TextWrapping = TextWrapping.Wrap });
                if (mode.SelectedIndex == 1) review.Children.Add(new CodeBlockView("powershell", "pi install '" + reviewed.Replace("'", "''") + "'"));
                PrimaryButtonText = mode.SelectedIndex == 0 ? "Add globally" : "Install globally";
                return;
            }
            busy = true; mode.IsEnabled = browse.IsEnabled = source.IsEnabled = IsPrimaryButtonEnabled = false;
            operation = new CancellationTokenSource(); status.Text = mode.SelectedIndex == 0 ? "Saving…" : "Installing…";
            if (mode.SelectedIndex == 0) await services.Skills.RegisterAsync(reviewed);
            else await services.Packages.InstallAsync(reviewed, operation.Token);
            Saved = true; args.Cancel = false;
        }
        catch (OperationCanceledException) { status.Text = "Installation cancelled or timed out. Pi may have partially installed the package; check Extensions before retrying."; }
        catch (Exception exception) { status.Text = exception is ArgumentException or IOException or InvalidOperationException ? exception.Message : "Couldn't add this source. Check its location and try again."; }
        finally { busy = false; operation?.Dispose(); operation = null; mode.IsEnabled = browse.IsEnabled = source.IsEnabled = IsPrimaryButtonEnabled = true; deferral.Complete(); }
    }
}
