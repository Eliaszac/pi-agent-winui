using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ComposerCommandTests
{
    [DataTestMethod]
    [DataRow("/com", 4, 0, "com")]
    [DataRow("Please check /sess", 18, 13, "sess")]
    [DataRow("First line\r\n/skill:review", 25, 12, "skill:review")]
    public void FindsCommandsAtCaretOnAnyLine(string text, int caret, int start, string query)
    {
        var token = SlashCommandToken.Find(text, caret);
        Assert.IsNotNull(token);
        Assert.AreEqual(start, token.Start);
        Assert.AreEqual(query, token.Query);
    }

    [DataTestMethod]
    [DataRow("https://pi.dev/docs")]
    [DataRow("C:/projects/repo")]
    [DataRow("/usr/local")]
    [DataRow("text/compact")]
    [DataRow("`/compact")]
    public void DoesNotInterpretPathsUrlsOrInlineCodeAsCommands(string text) => Assert.IsNull(SlashCommandToken.Find(text, text.Length));

    [TestMethod]
    public void ConsumingCommandPreservesSurroundingDraftAndHandlesCaretInMiddle()
    {
        const string text = "Before /session after";
        var token = SlashCommandToken.Find(text, 10)!;
        Assert.AreEqual("se", token.Query);
        Assert.AreEqual("Before  after", token.RemoveFrom(text));
        Assert.IsNull(SlashCommandToken.Find(text, 10, 2));
    }

    [TestMethod]
    public void DiscoveryKeepsNativeMappingsAndExcludesInvalidEntries()
    {
        using var json = JsonDocument.Parse("""[{"name":"session","source":"extension"},{"name":"review","source":"prompt","description":"Check code"},{"name":"skill:tests","source":"skill"},{"name":"auto-name","source":"extension"},{"name":"../bad","source":"extension"},{"name":"mystery","source":"unknown"}]""");
        var catalog = ComposerCommandCatalog.Create(json.RootElement);
        Assert.AreEqual("details", catalog.Single(command => command.Name == "session").Action);
        Assert.AreEqual("Template", catalog.Single(command => command.Name == "review").Origin);
        Assert.AreEqual("auto-name", ComposerCommandCatalog.Filter(catalog, "regenerate").Single().Name);
        Assert.AreEqual("review", ComposerCommandCatalog.Filter(catalog, "CHECK CODE").Single().Name);
        Assert.IsFalse(catalog.Any(command => command.Name is "../bad" or "mystery"));
    }

    [TestMethod]
    public void UnavailableStatsDoNotBecomeZeroOrLeakSessionPaths()
    {
        using var json = JsonDocument.Parse("""{"sessionFile":"C:/private/session.jsonl","tokens":{"input":1200},"contextUsage":{"tokens":null,"percent":null,"contextWindow":200000}}""");
        var formatted = SessionDetailsFormatter.Format(json.RootElement, new("provider", "model", "Model"));
        StringAssert.Contains(formatted, "Context estimate: Unavailable");
        StringAssert.Contains(formatted, "Reported cost (USD): Unavailable");
        Assert.IsFalse(formatted.Contains("private"));
    }

    [TestMethod]
    public async Task ManualCompactionReturnsToIdleWithoutAnAgentSettledEvent()
    {
        var transport = new FakePiTransport { AutoReply = true, EmitManualCompaction = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "commands.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.ConnectAsync();
        bool? running = null;
        session.Updated += update => { if (update.IsRunning is { } value) running = value; };
        await session.RunOperationAsync(ConversationOperation.Compact, "Keep the decisions");
        Assert.AreEqual(false, running);
        await session.RunOperationAsync(ConversationOperation.Details);
        JsonElement request;
        do { request = await transport.NextRequestAsync(); } while (request.GetProperty("type").GetString() != "compact");
        Assert.AreEqual("Keep the decisions", request.GetProperty("customInstructions").GetString());
        transport.RejectCompaction = true;
        await Assert.ThrowsExceptionAsync<PiCommandException>(() => session.RunOperationAsync(ConversationOperation.Compact));
        Assert.AreEqual(false, running);
        await session.RunOperationAsync(ConversationOperation.Details);
    }

    [TestMethod]
    public async Task NativeComposerCommandsAreNotSentAsPrompts()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new PiAgentGui.ViewModels.Conversations.ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        workspace.Draft = "/compact keep important decisions";
        workspace.HandleComposerCommand = text => Task.FromResult(text.StartsWith("/compact"));
        await workspace.SendCommand.ExecuteAsync();
        Assert.AreEqual(0, session.Sent.Count);
        Assert.AreEqual("/compact keep important decisions", workspace.Draft);
        workspace.Draft = "Ordinary message";
        await workspace.SendCommand.ExecuteAsync();
        CollectionAssert.AreEqual(new[] { "Ordinary message" }, session.Sent);
    }

    [TestMethod]
    public async Task StopRemainsAvailableDuringALongCommand()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new PiAgentGui.ViewModels.Conversations.ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OperationHandler = (_, _) =>
        {
            session.Emit(new() { IsRunning = true });
            started.TrySetResult();
            return finish.Task;
        };
        var operation = workspace.RunOperationAsync(ConversationOperation.Compact);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        dispatcher.Drain();
        Assert.IsTrue(workspace.CanStop);
        Assert.IsFalse(workspace.CanUseCommands);
        await workspace.StopCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.IsFalse(workspace.IsRunning);
        finish.SetResult(default);
        await operation;
        Assert.IsTrue(workspace.CanUseCommands);
    }

    [TestMethod]
    public async Task ExplicitNamingIsAppliedOnlyWhenTheExtensionChangesTheName()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new PiAgentGui.ViewModels.Conversations.ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        var stateReads = 0;
        session.OperationHandler = (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { sessionName = stateReads++ == 0 ? "Manual name" : "Generated name" }));
        var names = new List<string>();
        workspace.ExplicitSessionNameChanged += names.Add;
        await workspace.SendExtensionCommandAsync("/auto-name");
        CollectionAssert.AreEqual(new[] { "Generated name" }, names);
        session.Emit(new() { IsRunning = false });
        dispatcher.Drain();
        await workspace.SendExtensionCommandAsync("/auto-name");
        Assert.AreEqual(1, names.Count);
    }

    [TestMethod]
    public async Task OperationsStayOnTheirSessionAndRejectMutationsDuringRuns()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "commands-export.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.ConnectAsync();
        var path = Path.Combine(Path.GetTempPath(), "conversation.html");
        await session.RunOperationAsync(ConversationOperation.ExportHtml, path);
        JsonElement request;
        do { request = await transport.NextRequestAsync(); } while (request.GetProperty("type").GetString() != "export_html");
        Assert.AreEqual(path, request.GetProperty("outputPath").GetString());
        await session.SendAsync("Run");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => session.RunOperationAsync(ConversationOperation.Compact));
        Assert.IsFalse(transport.Commands.Contains("switch_session"));
        Assert.IsFalse(transport.Commands.Contains("new_session"));
    }
}
