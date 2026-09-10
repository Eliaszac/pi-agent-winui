using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Tests.Pi;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class SidebarManagementTests
{
    [TestMethod]
    public async Task InlineRenameUpdatesSidebarAndSelectedWindowTitle()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath() };
        var shell = new ShellViewModel(new InMemoryProjectRepository(project));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var item = shell.Projects[0].Conversations[0];
        item.Rename.BeginCommand.Execute(null);
        item.Rename.Draft = "  Fix sidebar  ";
        await item.Rename.SaveCommand.ExecuteAsync();
        Assert.AreEqual("Fix sidebar", item.Title);
        Assert.AreEqual("Pi Agent — Fix sidebar", shell.WindowTitle);
        Assert.IsFalse(item.Rename.IsEditing);
        shell.Projects[0].Rename.BeginCommand.Execute(null);
        shell.Projects[0].Rename.Draft = "Renamed project";
        await shell.Projects[0].Rename.SaveCommand.ExecuteAsync();
        Assert.AreEqual("Renamed project", shell.Projects[0].Name);
        await shell.LoadAsync();
        Assert.AreEqual("Fix sidebar", shell.WorkspaceTitle);
        Assert.AreEqual("Renamed project", shell.Projects[0].Name);
    }

    [TestMethod]
    public async Task SettledGroupPersistsAndRestoreReturnsSameConversation()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath() };
        var shell = new ShellViewModel(new InMemoryProjectRepository(project));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var group = shell.Projects[0];
        var conversation = group.Conversations[0];
        await shell.ToggleSettledAsync(conversation);
        Assert.AreEqual(0, group.ActiveConversations.Count);
        Assert.AreSame(conversation, group.SettledConversations.Single());
        Assert.AreEqual("Settled — (1)", group.SettledLabel);
        Assert.IsFalse(group.IsSettledExpanded);
        Assert.IsFalse(shell.HasConversation);
        group.ToggleSettledCommand.Execute(null);
        conversation.SelectCommand.Execute(null);
        Assert.IsTrue(shell.HasConversation);
        await shell.LoadAsync();
        group = shell.Projects[0];
        Assert.IsTrue(group.IsSettledExpanded);
        Assert.AreEqual(conversation.Conversation.Id, group.SettledConversations.Single().Conversation.Id);
        await shell.ToggleSettledAsync(group.SettledConversations.Single());
        Assert.AreEqual(1, group.ActiveConversations.Count);
        Assert.IsFalse(group.HasSettledConversations);
        Assert.IsTrue(shell.HasConversation);
    }

    [TestMethod]
    public async Task DeleteStopsOnlyRemovedRuntimeAndProjectDeletionClearsSelection()
    {
        var dispatcher = new QueuedUiDispatcher();
        var sessions = new List<FakeConversationSession>();
        await using var store = new ConversationWorkspaceStore((_, _) => { var session = new FakeConversationSession(); sessions.Add(session); return session; }, dispatcher);
        var first = new ConversationDraft { Id = Guid.NewGuid(), Title = "First", CreatedAt = DateTimeOffset.UtcNow };
        var second = first with { Id = Guid.NewGuid(), Title = "Second" };
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath(), Conversations = [first, second] };
        var shell = new ShellViewModel(new InMemoryProjectRepository(project), store);
        await shell.LoadAsync();
        var group = shell.Projects[0];
        await shell.ToggleSettledAsync(group.Conversations[0]);
        Assert.IsFalse(sessions[0].Disposed, "Settling is organization, not cancellation.");
        await shell.DeleteConversationAsync(group.Conversations[0]);
        Assert.IsTrue(sessions[0].Disposed);
        Assert.IsFalse(sessions[1].Disposed);
        Assert.AreEqual(second.Id, group.Conversations.Single().Conversation.Id);
        await shell.DeleteProjectAsync(group);
        Assert.IsTrue(sessions[1].Disposed);
        Assert.IsNull(shell.SelectedProject);
        Assert.IsTrue(shell.ShowEmptyProjects);
        Assert.AreEqual("Pi Agent", shell.WindowTitle);
    }

    [TestMethod]
    public async Task InlineRenameRejectsEmptyInputPreservesFailedDraftAndSupportsCancel()
    {
        var calls = 0;
        var rename = new InlineRenameViewModel(() => "Original", _ => { calls++; return Task.FromException(new IOException("Save failed")); });
        rename.BeginCommand.Execute(null);
        rename.Draft = " ";
        await rename.SaveCommand.ExecuteAsync();
        Assert.IsTrue(rename.HasError);
        Assert.AreEqual(0, calls);
        rename.Draft = "New name";
        await rename.SaveCommand.ExecuteAsync();
        Assert.AreEqual("New name", rename.Draft);
        Assert.AreEqual("Save failed", rename.Error);
        Assert.IsTrue(rename.IsEditing);
        rename.CancelCommand.Execute(null);
        Assert.IsFalse(rename.IsEditing);
        rename.BeginCommand.Execute(null);
        Assert.AreEqual("Original", rename.Draft);
    }
}
