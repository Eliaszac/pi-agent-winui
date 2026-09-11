using PiAgentGui.Controls;
using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Views;

public sealed class ProjectScriptsDialog : ActionContentDialog
{
    private readonly ProjectScriptsViewModel model;
    private readonly ListView list = new() { MaxHeight = 140, DisplayMemberPath = nameof(ProjectScript.Name), SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox name = new() { Header = "Name", MaxLength = 80, PlaceholderText = "Start backend" };
    private readonly TextBox command = new() { Header = "PowerShell command", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 180, MaxLength = 8000, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), PlaceholderText = "npm run dev" };
    private readonly TextBox directory = new() { Header = "Working directory (optional)", PlaceholderText = "Project folder", MaxLength = 1024 };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true };
    private readonly ActionButton save = new() { Content = "Save" };
    private readonly ActionButton delete = new() { Content = "Delete", IsEnabled = false };
    private readonly ActionButton add = new() { Content = "New script" };
    private readonly ActionButton import = new() { Content = "Import package.json…" };
    private readonly StackPanel importPanel = new() { Spacing = 8, Visibility = Visibility.Collapsed };
    private readonly StackPanel candidates = new() { Spacing = 4 };
    private readonly ActionButton importSelected = new() { Content = "Import selected" };
    private readonly ActionButton cancelImport = new() { Content = "Cancel import" };
    private Guid? editingId;
    private bool saving;

    public ProjectScriptsDialog(ProjectScriptsViewModel model)
    {
        this.model = model;
        Title = "Project scripts";
        CloseButtonText = "Close";
        DefaultButton = ContentDialogButton.Close;
        list.ItemsSource = model.Scripts;
        list.SelectionChanged += (_, _) =>
        {
            if (saving || list.SelectedItem is not ProjectScript script) return;
            editingId = script.Id; name.Text = script.Name; command.Text = script.Command; directory.Text = script.WorkingDirectory;
            delete.IsEnabled = true; notice.IsOpen = false;
        };
        add.Click += (_, _) => Reset();
        import.Click += async (_, _) => await PreviewImportAsync();
        importSelected.Click += async (_, _) => await ImportAsync();
        cancelImport.Click += (_, _) => { importPanel.Visibility = Visibility.Collapsed; candidates.Children.Clear(); };
        save.Click += async (_, _) => await SaveAsync();
        delete.Click += async (_, _) => await DeleteAsync();
        Closing += (_, args) => { if (saving) args.Cancel = true; };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(save); actions.Children.Add(delete);
        var panel = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 560 };
        panel.Children.Add(new TextBlock { Text = "Scripts are shared across this project's conversations. Relative directories start from the project folder.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var creation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        creation.Children.Add(add); creation.Children.Add(import);
        importPanel.Children.Add(new TextBlock { Text = "Select scripts to import. Existing commands are kept; duplicates are skipped.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        importPanel.Children.Add(new ScrollViewer { Content = candidates, MaxHeight = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var importActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        importActions.Children.Add(importSelected); importActions.Children.Add(cancelImport); importPanel.Children.Add(importActions);
        panel.Children.Add(list); panel.Children.Add(creation); panel.Children.Add(importPanel); panel.Children.Add(name); panel.Children.Add(command); panel.Children.Add(directory);
        panel.Children.Add(actions); panel.Children.Add(notice);
        Content = new ScrollViewer { Content = panel, MaxHeight = 600, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private void Reset()
    {
        editingId = null; list.SelectedItem = null; name.Text = ""; command.Text = ""; directory.Text = "";
        delete.IsEnabled = false; notice.IsOpen = false;
    }

    private void SetBusy(bool value)
    {
        saving = value;
        import.IsEnabled = importSelected.IsEnabled = cancelImport.IsEnabled = !value;
        save.IsEnabled = delete.IsEnabled = add.IsEnabled = list.IsEnabled = name.IsEnabled = command.IsEnabled = directory.IsEnabled = !value;
        delete.IsEnabled = !value && editingId is not null;
    }

    private async Task SaveAsync()
    {
        if (saving) return;
        SetBusy(true);
        try { await model.SaveAsync(editingId, name.Text, command.Text, directory.Text); Reset(); notice.Severity = InfoBarSeverity.Success; notice.Message = "Script saved."; notice.IsOpen = true; }
        catch (Exception exception) { notice.Severity = InfoBarSeverity.Error; notice.Message = exception.Message; notice.IsOpen = true; }
        finally { SetBusy(false); }
    }

    private async Task DeleteAsync()
    {
        if (saving || editingId is not { } id) return;
        SetBusy(true);
        try { await model.DeleteAsync(id); Reset(); }
        catch (Exception exception) { notice.Severity = InfoBarSeverity.Error; notice.Message = exception.Message; notice.IsOpen = true; }
        finally { SetBusy(false); }
    }

    private async Task PreviewImportAsync()
    {
        if (saving) return;
        SetBusy(true);
        try
        {
            var file = await new Services.Dialogs.PackageJsonPicker().PickAsync(XamlRoot);
            if (file is null) return;
            importPanel.Visibility = Visibility.Collapsed;
            candidates.Children.Clear();
            var preview = await model.PreviewPackageAsync(file);
            candidates.Children.Clear();
            foreach (var item in preview)
            {
                var text = new StackPanel { Spacing = 2 };
                text.Children.Add(new TextBlock { Text = item.Script.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                text.Children.Add(new TextBlock { Text = item.Script.Command, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                text.Children.Add(new TextBlock { Text = item.Description, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
                candidates.Children.Add(new CheckBox { Content = text, Tag = item.Script, IsChecked = true });
            }
            importPanel.Visibility = preview.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            notice.Severity = InfoBarSeverity.Informational;
            notice.Message = preview.Count == 0 ? "This package has no scripts." : $"Found {preview.Count} scripts. Select which ones to save.";
            notice.IsOpen = true;
        }
        catch (Exception exception) { notice.Severity = InfoBarSeverity.Error; notice.Message = exception.Message; notice.IsOpen = true; }
        finally { SetBusy(false); }
    }

    private async Task ImportAsync()
    {
        if (saving) return;
        var selected = candidates.Children.OfType<CheckBox>().Where(item => item.IsChecked == true).Select(item => (ProjectScript)item.Tag).ToArray();
        if (selected.Length == 0) { notice.Message = "Select at least one script."; notice.Severity = InfoBarSeverity.Informational; notice.IsOpen = true; return; }
        SetBusy(true);
        try
        {
            var count = await model.ImportAsync(selected);
            importPanel.Visibility = Visibility.Collapsed; candidates.Children.Clear();
            notice.Severity = InfoBarSeverity.Success; notice.Message = $"Imported {count} scripts. {selected.Length - count} duplicates skipped."; notice.IsOpen = true;
        }
        catch (Exception exception) { notice.Severity = InfoBarSeverity.Error; notice.Message = exception.Message; notice.IsOpen = true; }
        finally { SetBusy(false); }
    }
}
