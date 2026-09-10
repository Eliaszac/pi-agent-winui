using System.Threading.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ConversationSessionTests
{
    [TestMethod]
    public async Task InformationalNotificationsDoNotBecomeErrorsAndWarningsKeepTheirSeverity()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "notices.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.ConnectAsync();
        var updates = new System.Collections.Concurrent.ConcurrentQueue<ConversationUpdate>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update =>
        {
            updates.Enqueue(update);
            if (update.SessionName == "barrier") finished.TrySetResult();
        };
        transport.Push("""{"type":"extension_ui_request","method":"notify","notifyType":"info","message":"Session named: Example"}""");
        transport.Push("""{"type":"extension_ui_request","method":"notify","message":"Default informational notice"}""");
        transport.Push("""{"type":"extension_ui_request","method":"notify","notifyType":"warning","message":"Naming unavailable"}""");
        transport.Push("""{"type":"extension_ui_request","method":"notify","notifyType":"error","message":"Actual failure"}""");
        transport.Push("""{"type":"session_info_changed","name":"barrier"}""");
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual("Actual failure", updates.Single(update => update.Error is not null).Error);
        Assert.AreEqual("Naming unavailable", updates.Single(update => update.Warning is not null).Warning);
    }

    [TestMethod]
    public async Task ManualNamesAreSentAtStartupAndThroughRpcAndGeneratedNamesAreForwarded()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "names.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.SetSessionNameAsync("Saved manual name");
        await session.ConnectAsync();
        Assert.AreEqual("Saved manual name", transport.Launch!.SessionName);
        await session.SetSessionNameAsync("Renamed while connected");
        Assert.IsTrue(transport.Commands.Contains("set_session_name"));
        var named = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update => { if (update.SessionName is not null) named.TrySetResult(update.SessionName); };
        transport.Push("""{"type":"session_info_changed","name":"Generated title"}""");
        Assert.AreEqual("Generated title", await named.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [TestMethod]
    public async Task ModelsLoadAndSwitchThroughRpcAndRejectChangesDuringRun()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "models.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        PiModel? selected = null;
        IReadOnlyList<PiModel>? available = null;
        session.Updated += update =>
        {
            if (update.HasModelUpdate) selected = update.Model;
            if (update.AvailableModels is not null) available = update.AvailableModels;
        };
        await session.ConnectAsync();
        Assert.AreEqual(2, available!.Count);
        Assert.AreEqual("fake", selected!.Id);
        await session.SetModelAsync("test", "other");
        Assert.AreEqual(new PiModel("test", "other", "other"), selected);
        await session.SendAsync("hello");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => session.SetModelAsync("test", "fake"));
        Assert.AreEqual(1, transport.Commands.Count(command => command == "set_model"));
    }

    [TestMethod]
    public async Task TwoConversationsRunIndependentlyAndStopTargetsOnlyOne()
    {
        var firstTransport = new FakePiTransport { AutoReply = true };
        var secondTransport = new FakePiTransport { AutoReply = true };
        var firstLaunch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "first.jsonl"));
        var secondLaunch = firstLaunch with { SessionFile = Path.Combine(Path.GetTempPath(), "second.jsonl") };
        await using var first = new ConversationSession(firstLaunch, () => new PiRpcClient(firstTransport, TimeSpan.FromSeconds(3)), () => false);
        await using var second = new ConversationSession(secondLaunch, () => new PiRpcClient(secondTransport, TimeSpan.FromSeconds(3)), () => false);
        var firstEvents = Channel.CreateUnbounded<ConversationUpdate>();
        var secondEvents = Channel.CreateUnbounded<ConversationUpdate>();
        first.Updated += update => firstEvents.Writer.TryWrite(update);
        second.Updated += update => secondEvents.Writer.TryWrite(update);
        await Task.WhenAll(first.SendAsync("first"), second.SendAsync("second"));
        Assert.AreNotEqual(firstTransport.Launch!.SessionFile, secondTransport.Launch!.SessionFile);
        await first.StopAsync();
        CollectionAssert.AreEqual(new[] { "prompt", "clear_queue", "abort" }, firstTransport.Commands.Where(command => command is "prompt" or "clear_queue" or "abort").ToArray());
        CollectionAssert.AreEqual(new[] { "prompt" }, secondTransport.Commands.Where(command => command is "prompt" or "clear_queue" or "abort").ToArray());
        secondTransport.Push("""{"type":"agent_end"}""");
        ConversationUpdate update;
        do { update = await secondEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); } while (update.Status != "Finishing…");
        Assert.IsNull(update.IsRunning, "agent_end must not clear the busy state before retries finish.");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => second.SendAsync("too soon"));
        secondTransport.Push("""{"type":"agent_settled"}""");
        do { update = await secondEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); } while (update.Status != "Ready");
        Assert.AreEqual(false, update.IsRunning);
        await second.SendAsync("next turn");
        Assert.AreEqual(2, secondTransport.Commands.Count(command => command == "prompt"));
        Assert.IsFalse(secondTransport.Disposed);
    }

    [TestMethod]
    public async Task ReconnectReopensSameSessionAndLoadsAuthoritativeHistory()
    {
        var transports = new List<FakePiTransport>();
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "persisted.jsonl"));
        await using var session = new ConversationSession(launch, () =>
        {
            var transport = new FakePiTransport { AutoReply = true,
                History = """[{"role":"user","timestamp":1,"content":"saved prompt"},{"role":"assistant","timestamp":2,"content":[{"type":"text","text":"saved reply"}]}]""" };
            transports.Add(transport);
            return new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        });
        IReadOnlyList<ChatEntry>? history = null;
        session.Updated += update => { if (update.History is not null) history = update.History; };
        await session.ConnectAsync();
        await session.DisconnectAsync();
        Assert.IsTrue(transports[0].Disposed);
        await session.ConnectAsync();
        Assert.AreEqual(transports[0].Launch, transports[1].Launch);
        Assert.AreEqual(2, history!.Count);
        Assert.AreEqual("saved reply", history[1].Text);
        Assert.IsFalse(transports.Any(transport => transport.Commands.Contains("prompt")));
    }

    [TestMethod]
    public async Task ClosingDuringUnansweredHandshakeCancelsWithoutWaitingForRequestTimeout()
    {
        var transport = new FakePiTransport();
        var session = new ConversationSession(new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "closing.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromMinutes(2)));
        var connect = session.ConnectAsync();
        await transport.NextRequestAsync();
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => connect);
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task ExtensionReplyUsesOriginatingProcessWithoutApprovingAutomatically()
    {
        var transport = new FakePiTransport { AutoReply = true };
        await using var session = new ConversationSession(new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "question.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        var asked = new TaskCompletionSource<ExtensionPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update => { if (update.Prompt is not null) asked.TrySetResult(update.Prompt); };
        await session.ConnectAsync();
        transport.Push("""{"type":"extension_ui_request","id":"question-1","method":"confirm","title":"Continue?","message":"Review this action"}""");
        var question = await asked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual("question-1", question.Id);
        Assert.IsFalse(transport.Commands.Contains("extension_ui_response"));
        await session.ReplyAsync(question.Id, new() { ["confirmed"] = false });
        Assert.AreEqual("extension_ui_response", transport.Commands.Last());
    }

    [TestMethod]
    public async Task AcceptedExtensionCommandWithoutAgentTurnDoesNotStayBusy()
    {
        var transport = new FakePiTransport { AutoReply = true, StartTurnOnPrompt = false };
        await using var session = new ConversationSession(new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "command.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        bool? running = null;
        session.Updated += update => { if (update.IsRunning.HasValue) running = update.IsRunning; };
        await session.SendAsync("/extension-command");
        Assert.AreEqual(false, running);
        await session.SendAsync("/extension-command");
        Assert.AreEqual(2, transport.Commands.Count(command => command == "prompt"));
    }

    [TestMethod]
    public async Task SnapshotIsPublishedBeforeEventsFollowingItsResponse()
    {
        var transport = new FakePiTransport();
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "snapshot.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), () => false);
        var updates = Channel.CreateUnbounded<ConversationUpdate>();
        session.Updated += update => updates.Writer.TryWrite(update);
        var connecting = session.ConnectAsync();
        transport.Reply(await transport.NextRequestAsync(), new { sessionFile = launch.SessionFile, isStreaming = false });
        transport.Reply(await transport.NextRequestAsync(), new { models = Array.Empty<object>() });
        transport.Reply(await transport.NextRequestAsync(), new { levels = new[] { "off" } });
        var historyRequest = await transport.NextRequestAsync();
        transport.Reply(historyRequest, new { messages = Array.Empty<object>() });
        transport.Push("""{"type":"message_end","message":{"role":"user","timestamp":1,"content":"arrived after snapshot"}}""");
        await connecting;
        var sawSnapshot = false;
        while (true)
        {
            var update = await updates.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            if (update.History is not null) sawSnapshot = true;
            if (update.Entry is not null) { Assert.IsTrue(sawSnapshot); break; }
        }
    }
}
