using PiAgentGui.Models.SourceControl;
using PiAgentGui.ViewModels.SourceControl;

namespace PiAgentGui.Views;

public sealed partial class SourceControlPanel : UserControl
{
    public void OpenBranches() => OnBranches(this, new());
    private bool dialogOpen;
    private SourceControlViewModel? Model => DataContext as SourceControlViewModel;
    public SourceControlPanel() => InitializeComponent();
    private void OnClose(object sender, RoutedEventArgs args) { if (Model is { } model) model.IsOpen = false; }
    private async void OnRefresh(object sender, RoutedEventArgs args) { if (Model is { } model) await model.RefreshAsync(); }
    private async void OnStageAll(object sender, RoutedEventArgs args) { if (Model is { } model) await model.StageAsync(null); }
    private async void OnUnstageAll(object sender, RoutedEventArgs args) { if (Model is { } model) await model.UnstageAsync(null); }
    private async void OnCommit(object sender, RoutedEventArgs args) { if (Model is { } model && model.CanCommit) await model.CommitAsync(); }
    private async void OnPush(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Model is not { CanPush: true } model) return;
        dialogOpen = true;
        try
        {
            var remote = string.IsNullOrWhiteSpace(model.SelectedRemote) ? "the selected remote" : model.SelectedRemote;
            var content = new StackPanel { Spacing = 10, MaxWidth = 440 };
            content.Children.Add(new TextBlock
            {
                Text = $"Push branch '{model.BranchName}' to {remote}?",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            content.Children.Add(new TextBlock
            {
                Text = "This sends committed changes from this repository to the remote. It does not commit unstaged or staged local changes.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            var dialog = new Controls.ActionContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Push changes",
                Content = content,
                PrimaryButtonText = "Push",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && IsLoaded) await model.PushAsync();
        }
        catch (Exception) { model.ReportError("Couldn't open the push confirmation. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async void OnFetch(object sender, RoutedEventArgs args) { if (Model is { } model) await model.FetchAsync(); }
    private async void OnPull(object sender, RoutedEventArgs args) { if (Model is { } model) await model.PullAsync(); }
    private async void OnChangeAction(object sender, RoutedEventArgs args)
    {
        if (Model is not { } model || sender is not FrameworkElement { Tag: GitChange change }) return;
        if (change.IsConflict) { await OpenConflictAsync(model, change); return; }
        if (change.Staged) await model.UnstageAsync(change); else await model.StageAsync(change);
    }
    private async void OnBranches(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Model is not { } model) return;
        dialogOpen = true;
        try { await new BranchesDialog(model) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception) { model.ReportError("Couldn't open branches. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async void OnRevert(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Model is not { CanAct: true, RepositoryIdentity: { } root } model || sender is not FrameworkElement { Tag: GitChange { CanRevert: true } change }) return;
        dialogOpen = true;
        try
        {
            var dialog = new Controls.ActionContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Revert file changes?",
                Content = new TextBlock { Text = change.Status == '?'
                    ? $"Move \"{change.Path}\" to the Recycle Bin? This file is not tracked by Git."
                    : $"Discard unstaged changes to \"{change.Path}\"? The file will be restored to its staged version. This cannot be undone here.", TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Revert", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && IsLoaded)
                await model.RevertAsync(change, root);
        }
        catch (Exception) { model.ReportError("Couldn't revert the file. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async void OnViewDiff(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Model is not { } model || sender is not FrameworkElement { Tag: GitChange change }) return;
        if (change.IsConflict) { await OpenConflictAsync(model, change); return; }
        dialogOpen = true;
        try
        {
            var content = await model.ReadDiffAsync(change);
            if (content is not null && IsLoaded) await new FileDiffDialog(content) { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception) { model.ReportError("Couldn't open the diff. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async Task OpenConflictAsync(SourceControlViewModel model, GitChange change)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            var conflict = await model.ReadConflictAsync(change);
            if (conflict is not null && IsLoaded) await new MergeConflictDialog(conflict, model) { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception exception) { model.ReportError(Utilities.GitErrorMessage.Format(exception.Message)); }
        finally { dialogOpen = false; }
    }
}
