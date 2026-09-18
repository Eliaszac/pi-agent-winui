using PiAgentGui.Controls;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private bool managingScripts;
    private bool reviewingComposerScript;

    private async Task<bool> ReviewComposerScriptAsync(Models.Projects.ProjectScript script)
    {
        if (reviewingComposerScript || ViewModel.Chat is not { } owner) return false;
        reviewingComposerScript = true;
        try
        {
            var request = Scripts.PrepareRun(script);
            var content = new StackPanel { Spacing = 12, MaxWidth = 560 };
            content.Children.Add(new TextBlock { Text = "Runs in the project terminal. This does not attach context or send a message.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "Target: " + request.TargetLabel, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "Working folder: " + request.Directory, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            content.Children.Add(new TextBlock { Text = Scripts.CommandHeader, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var commandPreview = new TextBox { Text = script.Command, IsReadOnly = true, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, MaxHeight = 260, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(commandPreview, Scripts.CommandHeader);
            content.Children.Add(commandPreview);
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = "Run script: " + script.Name,
                Content = new ScrollViewer { Content = content, MaxHeight = 460 },
                PrimaryButtonText = "Run", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
            if (!ReferenceEquals(owner, ViewModel.Chat))
                throw new InvalidOperationException("The conversation changed. Review the script again before running it.");
            await Scripts.RunReviewedAsync(request);
            if (Terminals.Selected is { } terminal && terminal.ConversationId == owner.ResearchOwnerId)
            {
                activeSidePanels?.Open("terminal", terminal.Title, terminal);
                ApplySidePanel();
            }
            return true;
        }
        finally { reviewingComposerScript = false; }
    }
    private async void OnRunScriptClicked(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (Scripts.Scripts.Count == 0) { await ManageScriptsAsync(); return; }
        await RunScriptAsync();
    }

    private void OnScriptsMenuOpening(object sender, object args)
    {
        ScriptsMenu.Items.Clear();
        foreach (var script in Scripts.Scripts)
        {
            var item = new ActionMenuFlyoutItem { Text = script.Name, IsEnabled = Scripts.CanUse, Icon = new FontIcon { Glyph = "\uE768" } };
            item.Click += async (_, _) => await RunScriptAsync(script.Id);
            ScriptsMenu.Items.Add(item);
        }
        if (Scripts.Scripts.Count > 0) ScriptsMenu.Items.Add(new MenuFlyoutSeparator());
        var manage = new ActionMenuFlyoutItem { Text = "Manage scripts…", IsEnabled = Scripts.CanUse };
        manage.Click += async (_, _) => await ManageScriptsAsync();
        ScriptsMenu.Items.Add(manage);
        var refresh = new ActionMenuFlyoutItem { Text = "Refresh scripts" };
        refresh.Click += async (_, _) => await Scripts.SelectAsync(ViewModel.SelectedProject?.Project.Id, ViewModel.SelectedTarget?.Path, ViewModel.SelectedTarget);
        ScriptsMenu.Items.Add(refresh);
    }

    private async Task ManageScriptsAsync()
    {
        if (managingScripts || !Scripts.CanUse) return;
        managingScripts = true;
        try { await new ProjectScriptsDialog(Scripts) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception exception) { ScriptError.Message = exception.Message; ScriptError.IsOpen = true; }
        finally { managingScripts = false; }
    }

    private async Task RunScriptAsync(Guid? id = null)
    {
        try
        {
            if (!Scripts.CanUse) return;
            ScriptError.IsOpen = false;
            await Scripts.RunAsync(id);
            if (Terminals.Selected is { } terminal && terminal.ConversationId == ViewModel.Chat?.ResearchOwnerId)
            {
                activeSidePanels?.Open("terminal", terminal.Title, terminal);
                ApplySidePanel();
            }
        }
        catch (Exception exception) { ScriptError.Message = exception.Message; ScriptError.IsOpen = true; }
    }
}
