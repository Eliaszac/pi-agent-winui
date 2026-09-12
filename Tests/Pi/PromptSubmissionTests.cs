using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PromptSubmissionTests
{
    [TestMethod]
    public async Task DelayedSendShowsPromptImmediatelyAndProcessingStaysWithIt()
    {
        var dispatcher = new QueuedUiDispatcher();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeConversationSession { SendDelay = gate.Task };
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync();
        session.Emit(new() { Entry = new ChatEntry("old", "You", "Previous", IsUser: true) });
        session.Emit(new() { Entry = new ChatEntry("answer", "Assistant", "Done", IsAssistant: true) });
        dispatcher.Drain();
        model.Draft = "New prompt";
        var send = model.SendCommand.ExecuteAsync();
        Assert.AreEqual("New prompt", model.DisplayEntries[^2].Text);
        Assert.IsTrue(model.DisplayEntries[^1].IsProcessing);
        Assert.IsFalse(model.IsEmpty);
        gate.SetResult();
        await send;
        dispatcher.Drain();
        Assert.AreEqual("New prompt", model.DisplayEntries[^2].Text);
        session.Emit(new() { Entry = new ChatEntry("new", "You", "New prompt", IsUser: true) });
        dispatcher.Drain();
        Assert.AreEqual(1, model.DisplayEntries.Count(entry => entry.Text == "New prompt"));
        Assert.IsTrue(model.DisplayEntries[^1].IsProcessing);
        session.Emit(new() { IsRunning = false, TurnCompleted = true });
        dispatcher.Drain();
        Assert.IsFalse(model.DisplayEntries.Any(entry => entry.IsProcessing));
    }

    [TestMethod]
    public async Task FailedSendRemovesPreviewAndRestoresDraft()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession { SendError = new InvalidOperationException("Send failed") };
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync();
        dispatcher.Drain();
        model.Draft = "Keep me";
        await model.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.AreEqual("Keep me", model.Draft);
        Assert.AreEqual(0, model.DisplayEntries.Count);
    }
}
