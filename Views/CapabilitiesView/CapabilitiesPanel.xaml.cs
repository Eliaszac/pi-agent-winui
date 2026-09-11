using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class CapabilitiesPanel : UserControl
{
    private bool dialogOpen;
    public Services.Pi.CapabilityImportServices? Imports { get; set; }
    public Func<Task<string?>>? PickSkillFolder { get; set; }
    private async void OnAddSkills(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Imports is null || PickSkillFolder is null) return;
        dialogOpen = true;
        try
        {
            var dialog = new AddSkillDialog(Imports, PickSkillFolder) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.Saved && DataContext is CapabilitiesPanelViewModel model) model.ReportError("Added globally · restart Pi desktop to load the new skills.");
        }
        catch (Exception) { if (DataContext is CapabilitiesPanelViewModel model) model.ReportError("Couldn't open skill setup. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async void OnImportMcp(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || Imports is null || DataContext is not CapabilitiesPanelViewModel model) return;
        dialogOpen = true;
        try
        {
            var dialog = new McpImportDialog(Imports.Mcp, model.KnownServerNames) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.Saved) model.ReportError("Configured globally · restart Pi desktop to load the imported MCP servers.");
        }
        catch (Exception) { model.ReportError("Couldn't open MCP import. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    public CapabilitiesPanel() => InitializeComponent();
    private void OnClose(object sender, RoutedEventArgs args) { if (DataContext is CapabilitiesPanelViewModel model) model.IsOpen = false; }
    private async void OnRefresh(object sender, RoutedEventArgs args) { if (DataContext is CapabilitiesPanelViewModel model) await model.RefreshAsync(); }
    private async void OnSkillClicked(object sender, ItemClickEventArgs args)
    {
        if (dialogOpen || args.ClickedItem is not AvailableSkill skill) return;
        dialogOpen = true;
        try
        {
            string text;
            try { text = await SkillPreviewReader.ReadAsync(skill); }
            catch (Exception) { text = "Couldn't read this skill's Markdown file. It may have moved or be unavailable."; }
            await new SkillPreviewDialog(skill.Name, text) { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception) { if (DataContext is CapabilitiesPanelViewModel model) model.ReportError("Couldn't open the skill preview. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
    private async void OnSetup(object sender, RoutedEventArgs args)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try { await new ExtensionSetupDialog(SupportedExtensions.Mcp) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception) { if (DataContext is CapabilitiesPanelViewModel model) model.ReportError("Couldn't open MCP setup. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }

    private async void OnInstructionClicked(object sender, ItemClickEventArgs args)
    {
        if (dialogOpen || args.ClickedItem is not InstructionItem item || DataContext is not CapabilitiesPanelViewModel model) return;
        dialogOpen = true;
        try
        {
            var store = new InstructionFileStore();
            var document = await store.ReadAsync(item.File.Path);
            var dialog = new InstructionFileDialog(item, document, store) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (dialog.Saved) { await model.Instructions.RefreshAsync(); model.ReportError("Saved to disk · restart Pi desktop to load the updated instructions."); }
        }
        catch (Exception) { model.ReportError("Couldn't open this instruction file. It may be unavailable, linked, or use an unsupported encoding."); }
        finally { dialogOpen = false; }
    }
}
