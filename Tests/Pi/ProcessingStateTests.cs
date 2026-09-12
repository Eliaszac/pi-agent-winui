using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ProcessingStateTests
{
    [DataTestMethod]
    [DataRow(0, "0s")]
    [DataRow(59, "59s")]
    [DataRow(60, "1m 00s")]
    [DataRow(65, "1m 05s")]
    [DataRow(3601, "60m 01s")]
    public void DurationSwitchesToMinutes(int seconds, string expected) =>
        Assert.AreEqual(expected, ProcessingDuration.Format(TimeSpan.FromSeconds(seconds)));

    [TestMethod]
    public async Task PlaceholderHidesBeforePiAcknowledgesFirstSend()
    {
        var dispatcher = new QueuedUiDispatcher();
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeConversationSession { SendDelay = accepted.Task };
        await using var vm = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsConnected = true });
        dispatcher.Drain();
        vm.Draft = "hello";
        var send = vm.SendCommand.ExecuteAsync();
        try { Assert.IsFalse(vm.IsEmpty); }
        finally { accepted.SetResult(); await send; dispatcher.Drain(); }
    }

    [TestMethod]
    public async Task EmptyRunHidesPlaceholderAndTimerSurvivesUpdatesThenResets()
    {
        var clock = new ManualTimeProvider();
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher, clock: clock);
        Assert.IsTrue(vm.IsEmpty);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(vm.IsEmpty);
        var row = vm.DisplayEntries.Single();
        Assert.AreEqual("Processing… 0s", row.Text);
        clock.Advance(TimeSpan.FromSeconds(65));
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.AreSame(row, vm.DisplayEntries.Single());
        Assert.AreEqual("Processing… 1m 05s", row.Text);
        session.Emit(new() { IsRunning = false });
        dispatcher.Drain();
        Assert.IsTrue(vm.IsEmpty);
        Assert.AreEqual(0, vm.DisplayEntries.Count);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.AreEqual("Processing… 0s", vm.DisplayEntries.Single().Text);
    }
}
