using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class RunChangesTests
{
    [TestMethod]
    public async Task ReopenedConversationRestoresLatestCompletedSummary()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var viewModel = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsRunning = false, History = [
            new("u1", "You", "Old edit", IsUser: true),
            new("old", "edit", "done", IsTool: true, FileChange: new("old.js", "old", 9, 2, null)),
            new("a1", "Pi", "Done", IsAssistant: true),
            new("u2", "You", "Latest edit", IsUser: true),
            new("new", "edit", "done", IsTool: true, FileChange: new("index.js", "patch", 1, 1, null)),
            new("a2", "Pi", "Done", IsAssistant: true)
        ] });
        dispatcher.Drain();
        Assert.IsTrue(viewModel.HasRunChanges);
        Assert.AreEqual("index.js", viewModel.RunChanges!.Files.Single().Path);
        Assert.AreEqual("+1", viewModel.RunChanges.Added);
        Assert.AreEqual("old.js", viewModel.Entries.Single(entry => entry.Id == "a1").Summary!.Files.Single().Path);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(viewModel.HasRunChanges);
        Assert.AreEqual("index.js", viewModel.Entries.Single(entry => entry.Id == "a2").Summary!.Files.Single().Path);
    }

    [TestMethod]
    public void TrailingSessionNoticeDoesNotHideCompletedSummary()
    {
        var summary = RunChangesViewModel.FromHistory([
            new("u", "You", "Edit", IsUser: true),
            new("tool", "edit", "done", IsTool: true, FileChange: new("index.js", "patch", 1, 1, null)),
            new("a", "Pi", "Done", IsAssistant: true),
            new("notice", "Session", "Session named")]);
        Assert.AreEqual(1, summary!.Files.Count);
    }

    [TestMethod]
    public async Task EmptyConversationDoesNotShowSyntheticSummary()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var model = new ConversationViewModel(session, dispatcher);
        Assert.IsFalse(model.HasRunChanges);
        Assert.IsNull(model.RunChanges);
        Assert.AreEqual(0, model.Entries.Count);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(model.HasRunChanges);
    }

    [TestMethod]
    public void IncompleteHistoryDoesNotShowCompletedSummary()
    {
        Assert.IsNull(RunChangesViewModel.FromHistory([
            new("u", "You", "Edit", IsUser: true),
            new("tool", "edit", "done", IsTool: true, FileChange: new("index.js", "patch", 1, 1, null))
        ]));
    }

    [TestMethod]
    public async Task SummaryWaitsForSettledAndResetsForNextRun()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var viewModel = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsRunning = true });
        session.Emit(new() { Entry = new ChatEntry("write", "write", "done", IsTool: true,
            FileChange: new("file.cs", "patch", 4, 2, null)) });
        dispatcher.Drain();
        Assert.IsFalse(viewModel.HasRunChanges);
        session.Emit(new() { IsRunning = false });
        dispatcher.Drain();
        Assert.IsFalse(viewModel.HasRunChanges);
        session.Emit(new() { TurnCompleted = true });
        dispatcher.Drain();
        Assert.IsTrue(viewModel.HasRunChanges);
        Assert.AreEqual("+4", viewModel.RunChanges!.Added);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(viewModel.HasRunChanges);
        session.Emit(new() { IsRunning = false, TurnCompleted = true });
        dispatcher.Drain();
        Assert.IsFalse(viewModel.HasRunChanges);
    }

    [TestMethod]
    public void RepeatedFilesAreGroupedAndMissingDiffsAreExplicit()
    {
        var summary = new RunChangesViewModel([
            new("src/a.cs", "first", 3, 1, null), new("src\\a.cs", "second", 1, 2, null),
            new("b.cs", null, 0, 0, "Unavailable"), new("c.cs", "patch", 1, 0, null),
            new("d.cs", "patch", 1, 0, null)]);
        Assert.AreEqual(4, summary.Files.Count);
        Assert.AreEqual("+6", summary.Added);
        Assert.AreEqual("−3", summary.Removed);
        Assert.IsTrue(summary.HasUnknownCounts);
        Assert.AreEqual(3, summary.VisibleFiles.Count);
        summary.IsExpanded = true;
        Assert.AreEqual(4, summary.VisibleFiles.Count);
        Assert.AreEqual("first\nsecond", summary.Files[0].Patch);
    }

    [TestMethod]
    public void CreatedAndDeletedFilesWithSameNameAndHashAreShownAsMoved()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var summary = new RunChangesViewModel([
            new("old/Widget.cs", "deleted", 0, 8, null, "deleted", BeforeHash: hash),
            new("new/Widget.cs", "created", 8, 0, null, "created", AfterHash: hash),
            new("other.cs", "patch", 1, 0, null)]);
        Assert.AreEqual(2, summary.Files.Count);
        Assert.AreEqual("Moved", summary.Files[0].Kind);
        Assert.AreEqual("Moved · old/Widget.cs → new/Widget.cs", summary.Files[0].Label);
        Assert.AreEqual("+1", summary.Added);
        Assert.AreEqual("−0", summary.Removed);
    }
}
