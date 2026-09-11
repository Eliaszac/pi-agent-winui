using PiAgentGui.Models.SourceControl;
using PiAgentGui.ViewModels.SourceControl;

namespace PiAgentGui.Views;

public sealed partial class MergeConflictDialog : Controls.ActionContentDialog
{
    private readonly MergeConflictViewModel model;
    private readonly SourceControlViewModel workspace;
    private bool updating;
    private bool saving;
    public MergeConflictDialog(MergeConflict conflict, SourceControlViewModel workspace)
    {
        model = new(conflict); this.workspace = workspace;
        InitializeComponent(); DataContext = model; Title = "Resolve · " + conflict.Path;
        LeftEditor.Text = conflict.Left ?? "(Deleted on this side)";
        RightEditor.Text = conflict.Right ?? "(Deleted on this side)";
        RefreshEditor();
        Opened += (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; SelectConflict(); };
        Closed += (_, _) => { if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
        Closing += (_, args) => { if (saving) args.Cancel = true; };
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();
    private void Resize() { Body.Width = Math.Max(240, Math.Min(1600, XamlRoot.Size.Width - 120)); Body.Height = Math.Max(200, XamlRoot.Size.Height - 170); }
    private void OnResultChanged(object sender, TextChangedEventArgs args)
    {
        if (updating || ResultEditor is null) return;
        model.Result = ResultEditor.Text;
        IsPrimaryButtonEnabled = model.CanApply;
    }
    private void OnPrevious(object sender, RoutedEventArgs args) { model.Navigate(-1); SelectConflict(); }
    private void OnNext(object sender, RoutedEventArgs args) { model.Navigate(1); SelectConflict(); }
    private void OnAccept(object sender, RoutedEventArgs args) { if (sender is FrameworkElement { Tag: string side }) { model.Accept(side); RefreshEditor(); SelectConflict(); } }
    private void OnAcceptFile(object sender, RoutedEventArgs args) { if (sender is FrameworkElement { Tag: string side }) { model.Accept(side, true); RefreshEditor(); } }
    private void OnUndo(object sender, RoutedEventArgs args) { model.Undo(); RefreshEditor(); SelectConflict(); }
    private void OnReviewed(object sender, RoutedEventArgs args) { model.MarkReviewed(); IsPrimaryButtonEnabled = model.CanApply; }
    private void RefreshEditor()
    {
        updating = true; ResultEditor.Text = model.Result; model.SynchronizeEditorText(ResultEditor.Text); updating = false;
        IsPrimaryButtonEnabled = model.CanApply;
    }
    private void SelectConflict()
    {
        if (model.Selected is not { } block) return;
        ResultEditor.Focus(FocusState.Programmatic);
        ResultEditor.Select(Math.Min(block.Start, ResultEditor.Text.Length), Math.Min(block.Length, ResultEditor.Text.Length - Math.Min(block.Start, ResultEditor.Text.Length)));
    }
    private async void OnApply(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (!model.CanApply || saving) return;
        var deferral = args.GetDeferral(); saving = true; IsEnabled = false; IsPrimaryButtonEnabled = false;
        try
        {
            if (await workspace.ApplyConflictAsync(model.Snapshot, model.TextToSave, model.Delete)) args.Cancel = false;
            else { ErrorText.Text = workspace.Error.Length > 0 ? workspace.Error : "Repository is busy. Try again."; ErrorText.Visibility = Visibility.Visible; }
        }
        catch (Exception) { ErrorText.Text = "Couldn't apply the resolution. Refresh the repository before retrying."; ErrorText.Visibility = Visibility.Visible; }
        finally { saving = false; IsEnabled = true; IsPrimaryButtonEnabled = model.CanApply; deferral.Complete(); }
    }
}
