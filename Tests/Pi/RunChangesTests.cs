using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class RunChangesTests
{
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
}
