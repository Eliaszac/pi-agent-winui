using PiAgentGui.Controls;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private bool managingScripts;
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
        refresh.Click += async (_, _) => await Scripts.SelectAsync(ViewModel.SelectedProject?.Project.Id, ViewModel.SelectedProject?.Path);
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
            Capabilities.IsOpen = false; SourceControl.IsOpen = false; Processes.IsOpen = false; Files.IsOpen = false; Research.IsOpen = false;
        }
        catch (Exception exception) { ScriptError.Message = exception.Message; ScriptError.IsOpen = true; }
    }
}
