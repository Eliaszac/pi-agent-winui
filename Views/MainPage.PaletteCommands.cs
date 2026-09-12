using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private IReadOnlyList<PaletteCommand> BuildPaletteCommands()
    {
        var chat = ViewModel.Chat;
        bool Current() => chat is not null && ReferenceEquals(chat, ViewModel.Chat);
        var commands = new List<PaletteCommand>
        {
            PaletteCommand.Action("home", "Open Home", "Recent conversations and local usage", ViewModel.OpenHome, aliases: "dashboard analytics tokens"),
            PaletteCommand.Action("settings", "Open Settings", "Preferences, local data and legal information", () => ViewModel.OpenSettings(), aliases: "privacy terms license startup analytics"),
            PaletteCommand.Action("extensions", "Open Extensions", "Manage optional features and integrations", ViewModel.OpenExtensions, aliases: "plugins addons"),
            PaletteCommand.Action("providers", "Open Providers", "Connect and manage model providers", ViewModel.OpenProviders, aliases: "models authentication login"),
            PaletteCommand.Action("sidebar", "Toggle sidebar", "Collapse or expand projects and conversations", ViewModel.Sidebar.Toggle),
            PaletteCommand.Action("return", "Return to conversation", "Close Extensions or Providers", ViewModel.CloseExtensions, () => ViewModel.HasConversation),
            PaletteCommand.Action("project.new", "New project", "Choose a project folder or execution target", () => OnNewProjectClicked(this, new()), () => ViewModel.CanManageSidebar),
            new("project.switch", "Switch project…", "Search saved projects", Children: () => ViewModel.Projects.Select(project => PaletteCommand.Action("project:" + project.Project.Id, project.Name, project.Path, () => ViewModel.SelectProject(project))).ToArray()),
            new("conversation.switch", "Switch conversation…", "Search conversations across projects", Children: () => ViewModel.Projects.SelectMany(project => project.Conversations.Select(conversation => PaletteCommand.Action("conversation:" + conversation.Conversation.Id, conversation.Title, project.Name + " · " + conversation.TargetLabel, () => conversation.SelectCommand.Execute(null)))).ToArray()),
            new("conversation.new", "New conversation", "Create a conversation in this project", () => ViewModel.NewConversationCommand.ExecuteAsync(ViewModel.SelectedProject), Available: () => ViewModel.SelectedProject is not null && ViewModel.CanManageSidebar),
            PaletteCommand.Action("targets", "Manage execution targets", "Windows, WSL and SSH project targets", () => OnManageTargetsClicked(new MenuFlyoutItem { Tag = ViewModel.SelectedProject }, new()), () => ViewModel.SelectedProject is not null && ViewModel.CanManageSidebar),
            new("extensions.refresh", "Refresh extension status", "Check installed extensions", () => ViewModel.Extensions.RefreshCommand.ExecuteAsync()),
            new("scripts.manage", "Manage project scripts", "Add and edit project commands", ManageScriptsAsync, Available: () => Scripts.CanUse),
            new("scripts.run", "Run project script…", "Choose a script to run on " + ViewModel.TargetLabel, Children: () => Scripts.Scripts.Select(script => new PaletteCommand("script:" + script.Id, script.Name, ViewModel.TargetLabel, () => RunScriptAsync(script.Id), Available: () => Current() && Scripts.CanUse)).ToArray(), Available: () => Current() && Scripts.CanUse && Scripts.Scripts.Count > 0),
            new("openin", "Open project in…", "Choose an editor or application", Children: () => OpenIn.Applications.Select(app => new PaletteCommand("openin:" + app.Id, app.Name, "Open project", () => OpenIn.OpenAsync(app.Id))).ToArray(), Available: () => OpenIn.CanOpen)
        };
        foreach (var card in ViewModel.Extensions.Cards.Where(card => card.Definition.Bundled))
            commands.Add(new("manage:" + card.Definition.Name, "Manage " + card.Definition.Name, card.Definition.Description,
                async () => { await new ExtensionSetupDialog(card.Definition, true) { XamlRoot = XamlRoot }.ShowAsync(); await card.RefreshCommand.ExecuteAsync(); }));
        if (chat is null) return commands;
        commands.Add(PaletteCommand.Action("composer", "Focus message input", "Return to your draft", () => { ViewModel.CloseExtensions(); ConversationPane.FocusComposer(); }, Current));
        commands.Add(PaletteCommand.Action("latest", "Jump to latest message", "Scroll to the end", () => { ViewModel.CloseExtensions(); ConversationPane.JumpToLatest(); }, Current));
        commands.Add(new("prompt.jump", "Jump to prompt…", "Navigate this conversation", Children: () => chat.Entries.Where(entry => entry.IsUser).Select(entry => PaletteCommand.Action("prompt:" + chat.ResearchOwnerId + ":" + entry.Id, entry.Text, "User prompt", () => { ViewModel.CloseExtensions(); ConversationPane.JumpToPrompt(entry); }, Current)).ToArray()));
        commands.Add(new("stop", "Stop current run", "Stop the agent operation", () => chat.StopCommand.ExecuteAsync(), Available: () => Current() && chat.CanStop));
        commands.Add(new("reconnect", "Reconnect conversation", "Retry the connection", () => chat.RetryCommand.ExecuteAsync(), Available: () => Current() && chat.CanRetry));
        commands.Add(new("model", "Change model…", "Choose an available model", Children: () => chat.AvailableModels.Select(model => new PaletteCommand("model:" + model.Provider + ":" + model.Id, model.DisplayName, model.Provider, () => chat.SelectModelCommand.ExecuteAsync(model), Available: () => Current() && chat.CanChangeModel)).ToArray(), Available: () => chat.CanChangeModel));
        commands.Add(new("thinking", "Change thinking level…", "Choose reasoning effort", Children: () => chat.ThinkingLevels.Select(level => new PaletteCommand("thinking:" + level, level, "Thinking level", () => chat.SelectThinkingLevelCommand.ExecuteAsync(level), Available: () => Current() && chat.CanChangeThinkingLevel)).ToArray(), Available: () => chat.CanChangeThinkingLevel));
        commands.Add(new("approval", "Change approval mode…", "Choose how Pi requests permission", Children: () => chat.ApprovalModes.Select(mode => new PaletteCommand("approval:" + mode, mode, "Approval mode", () => chat.SelectApprovalModeCommand.ExecuteAsync(mode), Available: () => Current() && chat.CanChangeApprovalMode)).ToArray(), Available: () => chat.CanChangeApprovalMode));
        foreach (var (id, title) in new[] { ("copy", "Copy latest response"), ("export", "Export conversation"), ("compact", "Compact context") })
            commands.Add(new(id, title, "Current conversation", async () => { ViewModel.CloseExtensions(); await ConversationPane.RunPaletteCommandAsync("/" + id); }, Available: () => Current() && chat.CanUseCommands));
        commands.Add(new("duplicate", "Duplicate conversation", "Create and open a copy", () => chat.RequestDuplicateAsync(true), Available: () => Current() && chat.CanDuplicateConversation));
        commands.Add(PaletteCommand.Action("rename", "Rename conversation", "Edit the conversation title", () => { ViewModel.CloseExtensions(); ViewModel.HeaderRename.BeginCommand.Execute(null); }, () => Current() && ViewModel.CanManageSidebar));
        commands.Add(PaletteCommand.Action("delete", "Delete conversation…", "Review confirmation before deleting", () => OnDeleteConversationClicked(new MenuFlyoutItem { Tag = ViewModel.SelectedConversation }, new()), () => Current() && ViewModel.CanManageSidebar));
        commands.Add(new("queue.edit", "Edit queued follow-up", "Restore the queued draft", () => chat.EditQueuedCommand.ExecuteAsync(), Available: () => Current() && chat.HasQueuedPrompt && chat.CanChangeQueue));
        commands.Add(new("queue.remove", "Remove queued follow-up", "Remove the pending follow-up", () => chat.RemoveQueuedCommand.ExecuteAsync(), Available: () => Current() && chat.HasQueuedPrompt && chat.CanChangeQueue));
        commands.Add(new("queue.release", "Send queued follow-up now", "Release the paused follow-up", () => chat.ReleaseQueuedCommand.ExecuteAsync(), Available: () => Current() && chat.CanReleaseQueue));
        foreach (var kind in SidePanelCatalog.Kinds)
            commands.Add(PaletteCommand.Action("panel:" + kind, kind == "terminal" ? "New terminal" : "Open " + SidePanelCatalog.Title(kind), "Conversation side panel", () => { ViewModel.CloseExtensions(); OpenSidePanel(kind); }, () => Current() && PanelAvailable(kind)));
        commands.Add(PaletteCommand.Action("panel.toggle", "Show / hide side panel", "Keep tabs and terminal processes", () => { if (activeSidePanels is not null) activeSidePanels.IsOpen = !activeSidePanels.IsOpen; ApplySidePanel(); }, Current));
        commands.Add(new("panel.switch", "Switch panel tab…", "Choose an open tab", Children: () => activeSidePanels?.Tabs.Select(tab => PaletteCommand.Action("tab:" + tab.Kind, tab.Title, "Open tab", () => { activeSidePanels!.Selected = tab; activeSidePanels.IsOpen = true; ApplySidePanel(); }, Current)).ToArray() ?? []));
        commands.Add(new("terminal.rename", "Rename terminal", "Rename the selected terminal tab", () => RenameSideTabAsync(activeSidePanels!.Selected!), Available: () => Current() && activeSidePanels?.Selected?.Terminal is not null));
        commands.Add(PaletteCommand.Action("panel.close", "Close current panel tab", "Closing a terminal stops its process", () => ClosePaletteTab(), () => Current() && activeSidePanels?.Selected is not null));
        foreach (var delta in new[] { -1, 1 }) commands.Add(PaletteCommand.Action("panel.step:" + delta, delta == 1 ? "Next panel tab" : "Previous panel tab", "Switch between open tabs", () => { var state = activeSidePanels!; state.Selected = state.Tabs[(state.Tabs.IndexOf(state.Selected!) + delta + state.Tabs.Count) % state.Tabs.Count]; ApplySidePanel(); }, () => Current() && activeSidePanels?.Tabs.Count > 1));
        commands.Add(PaletteCommand.Action("integrations", "Open Quick integrations", "Add a common MCP integration", () => { ViewModel.CloseExtensions(); OpenSidePanel("capabilities"); CapabilitiesPane.OpenQuickIntegrations(); }, () => Current() && PanelAvailable("capabilities")));
        commands.Add(PaletteCommand.Action("mcp.add", "Add an MCP server", "Import server configuration", () => { ViewModel.CloseExtensions(); OpenSidePanel("capabilities"); CapabilitiesPane.OpenMcpImport(); }, () => Current() && PanelAvailable("capabilities")));
        commands.Add(new("git.refresh", "Refresh source control", "Reload repository changes", () => SourceControl.RefreshAsync(), Available: () => Current()));
        commands.Add(PaletteCommand.Action("git.branches", "Open branches", "Inspect and manage branches", () => { ViewModel.CloseExtensions(); OpenSidePanel("source"); SourceControlPane.OpenBranches(); }, () => Current() && !chat.IsRemoteTarget));
        commands.Add(new("github.pr", "Open current pull request", "Open on GitHub", () => OpenPullRequestAsync(GitHub.SelectedPullRequest!.Url), Available: () => Current() && GitHub.SelectedPullRequest is not null));        return commands;
    }
    private async void ClosePaletteTab()
    {
        if (activeSidePanels?.Selected is not { } tab) return;
        activeSidePanels.Close(tab); ApplySidePanel();
        try { if (tab.Terminal is { } terminal) await Terminals.CloseAsync(terminal); }
        catch (Exception error) { ViewModel.ReportError(error); }
    }
}

