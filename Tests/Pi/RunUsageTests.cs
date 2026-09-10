using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class RunUsageTests
{
    [TestMethod]
    public void CountsFinalUsageAcrossToolCyclesAndDoesNotCountRepeatedSnapshotsTwice()
    {
        var clock = new ManualTimeProvider();
        var tracker = new RunUsageTracker(clock);
        tracker.Observe(Packet("""{"type":"agent_start"}"""));
        var completion = Packet("""{"type":"message_end","message":{"role":"assistant","timestamp":1,"usage":{"input":100,"output":20,"cacheRead":50,"cacheWrite":10}}}""");
        tracker.Observe(completion);
        tracker.Observe(completion);
        clock.Advance(TimeSpan.FromSeconds(42));
        tracker.Observe(Packet("""{"type":"agent_start"}"""));
        tracker.Observe(Packet("""{"type":"message_end","message":{"role":"toolResult","toolCallId":"tool","usage":{"input":10,"output":5,"cacheRead":0,"cacheWrite":0}}}"""));
        tracker.Observe(Packet("""{"type":"message_end","message":{"role":"assistant","timestamp":2,"usage":{"input":200,"output":30,"cacheRead":0,"cacheWrite":0}}}"""));
        clock.Advance(TimeSpan.FromSeconds(86));
        var usage = tracker.Complete()!;
        Assert.AreEqual(425L, usage.Tokens);
        Assert.AreEqual(TimeSpan.FromSeconds(128), usage.Elapsed);
        Assert.IsNull(tracker.Complete());
    }

    [TestMethod]
    public void MissingTokenUsageIsUnknownAndDoesNotInventZero()
    {
        var tracker = new RunUsageTracker();
        tracker.Observe(Packet("""{"type":"agent_start"}"""));
        tracker.Observe(Packet("""{"type":"message_end","message":{"role":"assistant","timestamp":1,"usage":{"input":10}}}"""));
        Assert.IsNull(tracker.Complete()!.Tokens);
        tracker.Observe(Packet("""{"type":"agent_start"}"""));
        tracker.Reset();
        Assert.IsNull(tracker.Complete());
    }

    [TestMethod]
    public async Task OnlyFinalResponseGetsUsageAfterRunSettlesAndItStaysWithThatResponse()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        session.Emit(new() { IsRunning = true });
        session.Emit(new() { Entry = new("a", "Pi", "Checking", IsAssistant: true) });
        session.Emit(new() { Entry = new("b", "Pi", "Done", IsAssistant: true) });
        dispatcher.Drain();
        Assert.IsTrue(workspace.Entries.All(entry => !entry.HasUsage));
        session.Emit(new() { IsRunning = false, TurnCompleted = true, RunUsage = new(425, TimeSpan.FromSeconds(128)) });
        dispatcher.Drain();
        Assert.IsFalse(workspace.Entries[0].HasUsage);
        Assert.AreEqual("425 tokens · 2 min 8 s", workspace.Entries[1].UsageLabel);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsTrue(workspace.Entries[1].HasUsage);
    }

    [DataTestMethod]
    [DataRow(0.2, "1 s")]
    [DataRow(59.9, "59 s")]
    [DataRow(60.0, "1 min")]
    [DataRow(61.0, "1 min 1 s")]
    public void FormatsSecondsAndMinutes(double seconds, string expected) =>
        StringAssert.EndsWith(RunUsageFormatter.Format(new(null, TimeSpan.FromSeconds(seconds))), " · " + expected);

    private static JsonElement Packet(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
}
