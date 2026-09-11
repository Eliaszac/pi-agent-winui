using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CompletionSoundTests
{
    [DataTestMethod]
    [DataRow(true, "", 1)]
    [DataRow(false, "", 0)]
    [DataRow(true, "Stopped", 0)]
    [DataRow(true, "Failed: unavailable", 0)]
    public async Task AlertsOnlyOnceForViewedSuccessfulRun(bool viewed, string status, int expected)
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var model = new ConversationViewModel(session, dispatcher);
        var alerts = 0;
        model.ViewedRunCompleted += () => alerts++;
        model.SetViewed(viewed);
        session.Emit(new() { History = [new("old", "Pi", "Done", IsAssistant: true)], IsRunning = false });
        dispatcher.Drain();
        Assert.AreEqual(0, alerts);
        session.Emit(new() { IsRunning = true });
        session.Emit(new() { Entry = new("a", "Pi", "Done", Status: status, IsAssistant: true) });
        session.Emit(new() { TurnCompleted = true, IsRunning = false });
        session.Emit(new() { TurnCompleted = true, IsRunning = false });
        dispatcher.Drain();
        Assert.AreEqual(expected, alerts);
    }
}
