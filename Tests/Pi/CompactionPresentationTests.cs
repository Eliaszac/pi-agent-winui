using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CompactionPresentationTests
{
    [TestMethod]
    public async Task CompactionTransitionsWithoutChangingRunningStateRefreshTheLabel()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync();
        session.Emit(new() { Entry = new("u", "You", "Prompt", IsUser: true), IsRunning = true });
        dispatcher.Drain();
        var indicator = vm.DisplayEntries.Single(entry => entry.IsProcessing);
        Assert.AreEqual("Processing… 0s", indicator.Text);
        session.Emit(new() { IsCompacting = true });
        dispatcher.Drain();
        Assert.AreSame(indicator, vm.DisplayEntries[1]);
        Assert.AreEqual("Compacting context… 0s", indicator.Text);
        session.Emit(new() { IsCompacting = false });
        dispatcher.Drain();
        Assert.AreEqual("Processing… 0s", indicator.Text);
        session.Emit(new() { IsRunning = false });
        dispatcher.Drain();
        Assert.IsFalse(vm.DisplayEntries.Any(entry => entry.IsProcessing));
    }

    [TestMethod]
    public async Task PreviewWorksOnEmptyConversationWithoutBlockingTheComposer()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher, previewCompacting: true);
        await vm.InitializeAsync();
        dispatcher.Drain();
        vm.Draft = "Test";
        Assert.IsFalse(vm.IsRunning);
        Assert.IsTrue(vm.CanSend);
        Assert.IsFalse(vm.IsEmpty);
        Assert.AreEqual("Compacting context… 0s", vm.DisplayEntries.Single().Text);
        Assert.AreEqual(0, session.Sent.Count);
    }

    [TestMethod]
    public void ToolUsageSurvivesFinalExecutionEventAndHistoryReload()
    {
        const string message = """{"role":"toolResult","toolCallId":"t1","toolName":"delegate","content":[{"type":"text","text":"Done"}],"usage":{"input":10,"output":5,"cacheRead":20,"cacheWrite":0}}""";
        var transcript = new PiTranscript();
        using var packet = JsonDocument.Parse("{\"type\":\"message_end\",\"message\":" + message + "}");
        var entry = transcript.Apply(packet.RootElement)!;
        Assert.AreEqual(35L, entry.ToolTokens);
        using var end = JsonDocument.Parse("""{"type":"tool_execution_end","toolCallId":"t1","toolName":"delegate","result":{"content":[]}}""");
        Assert.AreEqual(35L, transcript.Apply(end.RootElement)!.ToolTokens);
        using var history = JsonDocument.Parse("[" + message + "]");
        var restored = new ChatEntryViewModel(transcript.Load(history.RootElement).Single());
        StringAssert.Contains(restored.ToolSummary, "35 tokens");
        Assert.IsFalse(new ChatEntryViewModel(new("tool:t2", "read", "Done", IsTool: true)).ToolSummary.Contains("tokens"));
    }
}
