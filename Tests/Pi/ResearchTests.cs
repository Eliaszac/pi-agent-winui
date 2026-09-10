using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ResearchTests
{
    private string root = "";
    [TestInitialize] public void Setup() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup] public void Cleanup()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        Directory.Delete(root, true);
    }
    private ResearchTask NewTask() => new(Guid.NewGuid(), Guid.NewGuid(), root, "Research", "Explain this", "test", "fake", "low", "Queued", "", DateTimeOffset.UtcNow);

    [TestMethod]
    public async Task SecondCoordinatorCannotRecoverOverwriteOrCancelOwnersTasks()
    {
        var store = new ResearchStore(root);
        var runner = new FakeResearchRunner();
        await using var owner = new ResearchCoordinator(store, runner);
        await owner.InitializeAsync();
        await owner.SetEnabledAsync(true);
        var task = NewTask();
        await owner.DispatchAsync(task);
        await WaitUntil(() => runner.Started.ContainsKey(task.Id));
        var before = await File.ReadAllTextAsync(Path.Combine(root, "research-tasks.json"));
        await using var second = new ResearchCoordinator(new ResearchStore(root), new FakeResearchRunner());
        await Assert.ThrowsExceptionAsync<IOException>(() => second.InitializeAsync());
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => second.CancelAsync(task.Id));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => second.SetEnabledAsync(false));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => second.DispatchAsync(NewTask()));
        Assert.AreEqual(before, await File.ReadAllTextAsync(Path.Combine(root, "research-tasks.json")));
        Assert.IsTrue(store.Enabled);
        runner.Started[task.Id].SetResult("Owner's answer");
        await WaitUntil(() => runner.Started[task.Id].Task.IsCompleted);
        await owner.DisposeAsync();
        await second.InitializeAsync();
        var restored = await store.LoadAsync();
        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual("Owner's answer", restored[0].Result);
        Assert.AreNotEqual("Interrupted", restored[0].Status);
    }

    [TestMethod]
    public async Task FailedInitializationReleasesOwnershipAndRetryDoesNotDuplicateTasks()
    {
        var store = new ResearchStore(root);
        var path = Path.Combine(root, "research-tasks.json");
        await File.WriteAllTextAsync(path, "invalid JSON");
        await using var first = new ResearchCoordinator(store, new FakeResearchRunner());
        await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() => first.InitializeAsync());
        using (store.AcquireOwnership()) { }
        await store.SaveAsync([NewTask() with { Status = "Completed", Result = "Saved" }]);
        await first.InitializeAsync();
        await first.InitializeAsync();
        Assert.AreEqual(1, (await store.LoadAsync()).Count);
    }

    [TestMethod]
    public async Task OptInQueueAndDisableKeepWorkersIndependentAndBounded()
    {
        var runner = new FakeResearchRunner();
        var store = new ResearchStore(root);
        await using var coordinator = new ResearchCoordinator(store, runner);
        await coordinator.InitializeAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => coordinator.DispatchAsync(NewTask()));
        await coordinator.SetEnabledAsync(true);
        var first = NewTask(); var second = NewTask(); var third = NewTask();
        await coordinator.DispatchAsync(first); await coordinator.DispatchAsync(second); await coordinator.DispatchAsync(third);
        await WaitUntil(() => runner.Started.Count == 2);
        Assert.IsFalse(runner.Started.ContainsKey(third.Id));
        runner.Started[first.Id].SetResult("Independent answer");
        await WaitUntil(() => runner.Started.ContainsKey(third.Id));
        await coordinator.SetEnabledAsync(false);
        await coordinator.DisposeAsync();
        var saved = await store.LoadAsync();
        Assert.AreEqual("Completed", saved.Single(task => task.Id == first.Id).Status);
        Assert.AreEqual("Independent answer", saved.Single(task => task.Id == first.Id).Result);
        Assert.AreEqual(2, saved.Count(task => task.Status == "Cancelled"));
        Assert.IsFalse(store.Enabled);
    }

    [TestMethod]
    public async Task ReopenPreservesResultsAndDoesNotRestartInterruptedTasks()
    {
        var store = new ResearchStore(root);
        await store.SetEnabledAsync(true);
        await store.SaveAsync([NewTask() with { Status = "Running" }, NewTask() with { Status = "Completed", Result = "Saved answer" }]);
        var runner = new FakeResearchRunner();
        await using var coordinator = new ResearchCoordinator(store, runner);
        await coordinator.InitializeAsync();
        var saved = await store.LoadAsync();
        Assert.AreEqual(1, saved.Count(task => task.Status == "Interrupted"));
        Assert.AreEqual("Saved answer", saved.Single(task => task.Status == "Completed").Result);
        Assert.AreEqual(0, runner.Started.Count);
    }

    [TestMethod]
    public void WorkerLaunchExcludesGeneralExtensionsAndWriteTools()
    {
        var executable = Path.Combine(root, "pi.exe"); File.WriteAllText(executable, "Never executed");
        var info = new PiProcessStartInfoFactory(new PiRuntimeOptions { ExecutablePath = executable }, () => null).Create(
            new PiLaunchRequest(root, Path.Combine(root, "worker.jsonl"), ResearchWorker: true, Provider: "test", Model: "fake", Effort: "low"));
        var args = info.ArgumentList.ToList();
        Assert.AreEqual("read,grep,find,ls,read_web_page", args[args.IndexOf("--tools") + 1]);
        foreach (var flag in new[] { "--no-extensions", "--no-skills", "--no-context-files", "--no-prompt-templates", "--no-session" }) CollectionAssert.Contains(args, flag);
        Assert.AreEqual(1, args.Count(value => value == "--extension"));
        StringAssert.EndsWith(args[args.IndexOf("--extension") + 1], "research-worker.ts");
        Assert.IsFalse(args.Any(value => value.Contains("research-dispatch")));
    }

    [TestMethod]
    public async Task MainSessionAcknowledgesDispatchWithoutShowingAQuestionOrSendingAnotherPrompt()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = new ConversationSession(new(root, Path.Combine(root, "main.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), () => false)
        {
            ResearchRequested = payload => { received.SetResult(payload.GetProperty("question").GetString()!); return Task.CompletedTask; }
        };
        var prompts = new System.Collections.Concurrent.ConcurrentQueue<ExtensionPrompt>();
        session.Updated += update => { if (update.Prompt is { } prompt) prompts.Enqueue(prompt); };
        await session.ConnectAsync();
        transport.Push("""{"type":"extension_ui_request","id":"research","method":"input","title":"pi-gui-background-research-v1","placeholder":"{\"question\":\"Explain this\"}"}""");
        Assert.AreEqual("Explain this", await received.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        System.Text.Json.JsonElement reply;
        do { reply = await transport.NextRequestAsync(); } while (reply.GetProperty("type").GetString() != "extension_ui_response");
        Assert.AreEqual("{\"accepted\":true}", reply.GetProperty("value").GetString());
        Assert.AreEqual(0, prompts.Count);
        Assert.IsFalse(transport.Commands.Contains("prompt"));
    }

    [TestMethod]
    public async Task RunnerWaitsForSettlementThenDisposesItsProcess()
    {
        var transport = new FakePiTransport { AutoReply = true, History = """[{"role":"assistant","content":[{"type":"text","text":"Answer"}]}]""" };
        var runner = new PiResearchRunner(() => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), root);
        var work = runner.RunAsync(NewTask(), CancellationToken.None);
        await WaitUntil(() => transport.Commands.Contains("prompt"));
        Assert.IsFalse(work.IsCompleted);
        transport.Push("""{"type":"agent_end"}""");
        Assert.IsFalse(work.IsCompleted);
        transport.Push("""{"type":"agent_settled"}""");
        Assert.AreEqual("Answer", await work.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.IsTrue(transport.Disposed);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
