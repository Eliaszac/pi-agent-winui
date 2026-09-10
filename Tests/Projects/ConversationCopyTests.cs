using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Tests.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ConversationCopyTests
{
    [TestMethod]
    public async Task ClonePreservesSelectionAndDraftWhileForkOpensAnIndependentConversation()
    {
        var dispatcher = new QueuedUiDispatcher();
        var sessions = new List<FakeConversationSession>();
        await using var store = new ConversationWorkspaceStore((_, _) => { var session = new FakeConversationSession(); sessions.Add(session); return session; }, dispatcher);
        var repository = new InMemoryProjectRepository(new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath() });
        var shell = new ShellViewModel(repository, store);
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var source = shell.Chat!;
        sessions[0].Emit(new() { IsConnected = true, History = [new("response", "Pi", "Done", IsAssistant: true)] });
        dispatcher.Drain();
        source.Draft = "Keep my draft";
        await shell.DuplicateConversationAsync(source, false);
        Assert.AreSame(source, shell.Chat);
        Assert.AreEqual("Keep my draft", source.Draft);
        Assert.AreEqual(2, shell.Projects[0].Conversations.Count);
        var clone = shell.Projects[0].Conversations[1];
        Assert.AreNotSame(source, clone.Workspace);
        Assert.AreEqual(0, sessions[1].ConnectCount);
        Assert.IsTrue(clone.Conversation.IsTitleManual);
        Assert.AreEqual(1, sessions[0].Copies.Count);
        await shell.DuplicateConversationAsync(source, true);
        Assert.AreSame(shell.Projects[0].Conversations[2].Workspace, shell.Chat);
        Assert.AreNotSame(source, shell.Chat);
        Assert.AreNotEqual(sessions[0].Copies[0].Destination, sessions[0].Copies[1].Destination);
        Assert.AreEqual(3, repository.Projects[0].Conversations.Count);
        Assert.IsFalse(sessions[0].Disposed);
    }

    [TestMethod]
    public async Task CopyAndCatalogFailuresLeaveSidebarAndSelectionUntouched()
    {
        var dispatcher = new QueuedUiDispatcher();
        var runtime = new FakeConversationSession();
        await using var store = new ConversationWorkspaceStore((_, _) => runtime, dispatcher);
        var repository = new InMemoryProjectRepository(new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath() });
        var shell = new ShellViewModel(repository, store);
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var source = shell.Chat!;
        runtime.Emit(new() { IsConnected = true, History = [new("response", "Pi", "Done", IsAssistant: true)] });
        dispatcher.Drain();
        runtime.CopyError = new IOException("Copy failed");
        await Assert.ThrowsExceptionAsync<IOException>(() => shell.DuplicateConversationAsync(source, true));
        Assert.AreEqual(1, repository.Projects[0].Conversations.Count);
        await source.ForkCommand.ExecuteAsync();
        Assert.IsTrue(source.HasError);
        Assert.IsTrue(source.CanDuplicateConversation);
        runtime.CopyError = null;
        repository.CopyRegistrationError = new IOException("Catalog busy");
        await Assert.ThrowsExceptionAsync<IOException>(() => shell.DuplicateConversationAsync(source, true));
        Assert.AreEqual(1, shell.Projects[0].Conversations.Count);
        Assert.AreSame(source, shell.Chat);
        Assert.IsTrue(source.CanDuplicateConversation);
    }

    [TestMethod]
    public async Task ActionsAppearOnlyUnderTheLatestFinishedResponseAndHideDuringRuns()
    {
        var dispatcher = new QueuedUiDispatcher();
        var runtime = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(runtime, dispatcher);
        await workspace.InitializeAsync();
        runtime.Emit(new() { History = [new("first", "Pi", "First", IsAssistant: true), new("user", "You", "Again", IsUser: true), new("last", "Pi", "Last", IsAssistant: true)] });
        dispatcher.Drain();
        Assert.IsFalse(workspace.Entries[0].ShowConversationActions);
        Assert.IsTrue(workspace.Entries[2].ShowConversationActions);
        runtime.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsTrue(workspace.Entries.All(entry => !entry.ShowConversationActions));
        runtime.Emit(new() { IsRunning = false, Entry = new("new-user", "You", "Pending", IsUser: true) });
        dispatcher.Drain();
        Assert.IsFalse(workspace.CanDuplicateConversation);
        Assert.IsTrue(workspace.Entries.All(entry => !entry.ShowConversationActions));
    }
}
