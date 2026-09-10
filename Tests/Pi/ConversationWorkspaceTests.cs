using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ConversationWorkspaceTests
{
    [TestMethod]
    public async Task EffortHandlesOffAndIndependentStateAndCapabilityUpdates()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        session.Emit(new() { HasThinkingLevelUpdate = true, ThinkingLevel = "off" });
        dispatcher.Drain();
        session.Emit(new() { ThinkingLevels = ["off", "low", "high"] });
        dispatcher.Drain();
        Assert.AreEqual("off", workspace.SelectedThinkingLevel);
        Assert.IsTrue(workspace.CanChangeThinkingLevel);
        session.Emit(new() { HasThinkingLevelUpdate = true, ThinkingLevel = "high" });
        dispatcher.Drain();
        Assert.AreEqual("high", workspace.SelectedThinkingLevel);
        session.Emit(new() { ThinkingLevels = ["off"] });
        dispatcher.Drain();
        Assert.AreEqual("off", workspace.SelectedThinkingLevel);
        Assert.IsFalse(workspace.CanChangeThinkingLevel);
        session.Emit(new() { ThinkingLevels = ["low", "high"] });
        dispatcher.Drain();
        Assert.AreEqual("high", workspace.SelectedThinkingLevel);
    }

    [TestMethod]
    public async Task TypingUpdatesCombinedSendButtonEnabledState()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        var enabledStates = new List<bool>();
        workspace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(workspace.CanUseComposerAction)) enabledStates.Add(workspace.CanUseComposerAction);
        };
        Assert.IsFalse(workspace.CanUseComposerAction);
        workspace.Draft = "send this";
        workspace.Draft = " ";
        CollectionAssert.AreEqual(new[] { true, false }, enabledStates);
        workspace.Draft = "send this";
        await workspace.ComposerActionCommand.ExecuteAsync();
        dispatcher.Drain();
        CollectionAssert.AreEqual(new[] { "send this" }, session.Sent);
        Assert.AreSame(workspace.StopCommand, workspace.ComposerActionCommand);
    }

    [TestMethod]
    public async Task SidebarIndicatorsPrioritizeApprovalAndAcknowledgeCompletionOnViewing()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsConnected = true, IsRunning = true });
        dispatcher.Drain();
        Assert.IsTrue(workspace.ShowRunningIndicator);
        session.Emit(new() { Prompt = new ExtensionPrompt("approval", "confirm", "Allow?", "", [], "", null) });
        dispatcher.Drain();
        Assert.IsTrue(workspace.ShowBlockedIndicator);
        Assert.IsFalse(workspace.ShowRunningIndicator);
        workspace.SetViewed(true);
        Assert.IsFalse(workspace.ShowBlockedIndicator);
        workspace.SetViewed(false);
        Assert.IsTrue(workspace.ShowBlockedIndicator);
        session.Emit(new() { IsRunning = false, DismissPrompts = true, TurnCompleted = true });
        dispatcher.Drain();
        Assert.IsTrue(workspace.ShowDoneIndicator);
        workspace.SetViewed(true);
        workspace.SetViewed(false);
        Assert.IsFalse(workspace.ShowDoneIndicator);
        workspace.SetViewed(true);
        session.Emit(new() { TurnCompleted = true });
        dispatcher.Drain();
        workspace.SetViewed(false);
        Assert.IsFalse(workspace.ShowDoneIndicator);
        session.Emit(new() { IsRunning = false, IsConnected = false, Error = "Connection failed" });
        dispatcher.Drain();
        Assert.IsFalse(workspace.ShowDoneIndicator);
    }

    [TestMethod]
    public async Task StartupModelSelectionSurvivesSeparateSnapshotsAndReorderedLists()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        var current = new PiAgentGui.Models.Pi.PiModel("provider", "default", "Session name");
        var listed = new PiAgentGui.Models.Pi.PiModel("provider", "default", "Catalog name");
        var other = new PiAgentGui.Models.Pi.PiModel("other-provider", "default", "Other provider");
        session.Emit(new() { HasModelUpdate = true, Model = current });
        dispatcher.Drain();
        session.Emit(new() { AvailableModels = [other, listed] });
        dispatcher.Drain();
        Assert.AreEqual(1, workspace.SelectedModelIndex);
        Assert.AreSame(listed, workspace.AvailableModels[workspace.SelectedModelIndex]);
        Assert.AreEqual("Model: Catalog name · provider", workspace.ModelOptions[workspace.SelectedModelIndex]);
        session.Emit(new() { AvailableModels = [listed, other] });
        dispatcher.Drain();
        Assert.AreEqual(0, workspace.SelectedModelIndex);
        session.Emit(new() { AvailableModels = [other] });
        dispatcher.Drain();
        Assert.AreEqual(1, workspace.SelectedModelIndex);
        Assert.AreEqual(current, workspace.SelectedModel);
        Assert.AreEqual("Model: Session name · provider", workspace.ModelOptions[workspace.SelectedModelIndex]);
    }

    [TestMethod]
    public async Task ModelSelectionUsesConfirmedStateAndRetainsItOnRejection()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        var first = new PiAgentGui.Models.Pi.PiModel("test", "first", "first");
        var second = new PiAgentGui.Models.Pi.PiModel("test", "second", "second");
        session.Emit(new() { HasModelUpdate = true, Model = first, AvailableModels = [first, second] });
        dispatcher.Drain();
        workspace.Draft = "keep my draft";
        await workspace.SelectModelCommand.ExecuteAsync(second);
        dispatcher.Drain();
        Assert.AreEqual(second, workspace.SelectedModel);
        Assert.AreEqual("keep my draft", workspace.Draft);
        session.ModelError = new IOException("Model unavailable");
        await workspace.SelectModelCommand.ExecuteAsync(first);
        dispatcher.Drain();
        Assert.AreEqual(second, workspace.SelectedModel);
        Assert.AreEqual("Model unavailable", workspace.Error);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(workspace.CanChangeModel);
        session.ModelError = null;
        await workspace.SelectModelCommand.ExecuteAsync(first);
        dispatcher.Drain();
        Assert.AreEqual(second, workspace.SelectedModel);
    }

    [TestMethod]
    public async Task WorkspaceIdentityRetainsDraftsAndKeepsBackgroundUpdatesSeparate()
    {
        var dispatcher = new QueuedUiDispatcher();
        var sessions = new List<FakeConversationSession>();
        await using var store = new ConversationWorkspaceStore((_, _) => { var session = new FakeConversationSession(); sessions.Add(session); return session; }, dispatcher);
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Path = Path.GetTempPath() };
        var firstId = new ConversationDraft { Id = Guid.NewGuid(), Title = "First", CreatedAt = DateTimeOffset.UtcNow };
        var secondId = firstId with { Id = Guid.NewGuid(), Title = "Second" };
        var first = store.GetOrCreate(project, firstId);
        var second = store.GetOrCreate(project, secondId);
        first.Draft = "first draft";
        second.Draft = "second draft";
        sessions[0].Emit(new() { Entry = new ChatEntry("a", "Pi", "Background response"), IsRunning = true });
        dispatcher.Drain();
        Assert.AreSame(first, store.GetOrCreate(project, firstId));
        Assert.AreEqual("first draft", first.Draft);
        Assert.AreEqual("second draft", second.Draft);
        Assert.AreEqual(1, first.Entries.Count);
        Assert.AreEqual(0, second.Entries.Count);
        Assert.IsTrue(first.IsRunning);
        Assert.IsFalse(second.IsRunning);
        await store.DisposeAsync();
        Assert.IsTrue(sessions.All(session => session.Disposed));
    }

    [TestMethod]
    public async Task AcceptedPromptClearsDraftAndRejectedPromptRetainsIt()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        workspace.Draft = "send once";
        await workspace.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.AreEqual("", workspace.Draft);
        Assert.IsTrue(workspace.IsRunning);
        await workspace.StopCommand.ExecuteAsync();
        dispatcher.Drain();
        session.SendError = new IOException("Connection lost; acceptance uncertain");
        workspace.Draft = "keep this";
        await workspace.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.AreEqual("keep this", workspace.Draft);
        Assert.IsTrue(workspace.HasError);
        Assert.AreEqual(1, session.Sent.Count);
    }

    [TestMethod]
    public void SessionPathsAreStableAndUniqueAcrossProjectAndConversationIdentities()
    {
        var paths = new PiSessionPaths(new ProjectStorageOptions { CatalogPath = Path.Combine(Path.GetTempPath(), "PiGuiTest", "projects.json") });
        var project = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        Assert.AreEqual(paths.GetSessionFile(project, conversation), paths.GetSessionFile(project, conversation));
        Assert.AreNotEqual(paths.GetSessionFile(project, conversation), paths.GetSessionFile(Guid.NewGuid(), conversation));
        Assert.AreNotEqual(paths.GetSessionFile(project, conversation), paths.GetSessionFile(project, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task StartupShowsLoadingUntilReadyAndSelectionDoesNotReload()
    {
        var dispatcher = new QueuedUiDispatcher();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeConversationSession { ConnectDelay = ready.Task };
        await using var workspace = new ConversationViewModel(session, dispatcher);
        workspace.Draft = "keep this draft";
        Assert.IsTrue(workspace.IsLoading);
        Assert.IsFalse(workspace.IsReady);
        Assert.IsFalse(workspace.CanSend);
        var initialization = workspace.InitializeAsync();
        Assert.IsTrue(workspace.IsLoading);
        Assert.IsFalse(workspace.CanRetry);
        ready.SetResult();
        await initialization;
        dispatcher.Drain();
        Assert.IsTrue(workspace.IsReady);
        Assert.IsFalse(workspace.IsLoading);
        Assert.IsTrue(workspace.CanSend);
        await workspace.InitializeAsync();
        Assert.AreEqual(1, session.ConnectCount);
        Assert.AreEqual("keep this draft", workspace.Draft);
    }

    [TestMethod]
    public async Task FailedStartupOffersRetryWithoutSendingDraftOrLosingFailureToQueuedEvents()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession { ConnectError = new IOException("Pi could not start") };
        await using var workspace = new ConversationViewModel(session, dispatcher);
        workspace.Draft = "unsent text";
        await workspace.InitializeAsync();
        dispatcher.Drain();
        Assert.IsFalse(workspace.IsLoading);
        Assert.IsTrue(workspace.ShowRecovery);
        Assert.IsTrue(workspace.CanRetry);
        Assert.IsFalse(workspace.CanSend);
        Assert.AreEqual("Pi could not start", workspace.Error);
        await workspace.InitializeAsync();
        Assert.AreEqual(1, session.ConnectCount, "Changing selection must not implicitly retry failures.");
        session.ConnectError = null;
        await workspace.RetryCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.IsTrue(workspace.IsReady);
        Assert.IsFalse(workspace.ShowRecovery);
        Assert.IsFalse(workspace.HasError);
        Assert.AreEqual("unsent text", workspace.Draft);
        Assert.AreEqual(0, session.Sent.Count);
    }

    [TestMethod]
    public async Task ActiveTurnStaysInChatAndUnexpectedExitRequiresExplicitRetry()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        workspace.Draft = "hello";
        await workspace.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.IsTrue(workspace.IsReady);
        Assert.IsTrue(workspace.CanStop);
        Assert.IsFalse(workspace.IsLoading);
        session.Emit(new() { IsConnected = false, IsRunning = false, Error = "Pi exited" });
        dispatcher.Drain();
        Assert.IsTrue(workspace.ShowRecovery);
        Assert.IsFalse(workspace.CanSend);
        await workspace.RetryCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.IsTrue(workspace.IsReady);
        Assert.AreEqual(1, session.Sent.Count, "Retry must not replay an earlier prompt.");
    }

    [TestMethod]
    public async Task ExtensionQuestionRemainsAvailableDuringStartup()
    {
        var dispatcher = new QueuedUiDispatcher();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeConversationSession { ConnectDelay = ready.Task };
        await using var workspace = new ConversationViewModel(session, dispatcher);
        var initialization = workspace.InitializeAsync();
        await session.ConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Emit(new() { Prompt = new ExtensionPrompt("startup", "confirm", "Continue?", "Startup question", [], "", null) });
        dispatcher.Drain();
        Assert.IsTrue(workspace.IsLoading);
        Assert.AreEqual(1, workspace.Prompts.Count);
        ready.SetResult();
        await initialization;
        dispatcher.Drain();
    }
}
