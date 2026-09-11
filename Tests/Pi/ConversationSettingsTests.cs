using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ConversationSettingsTests
{
    [TestMethod]
    public async Task ReopeningRestoresApprovalBeforeModelAndEffort()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "conversation.jsonl");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "package.json"), """{"name":"@georgedong32/permission-modes","version":"2.6.3"}""");
            await File.WriteAllTextAsync(Path.Combine(directory, "classifier-client.ts"), PiAgentGui.Utilities.PermissionModesCompatibility.FixedOptions);
            var commands = System.Text.Json.JsonSerializer.Serialize(new[] { new { name = "mode", source = "extension", sourceInfo = new { path = Path.Combine(directory, "index.ts"), scope = "user" } } });
            var first = new FakePiTransport { AutoReply = true, ApplyModeCommands = true, StartTurnOnPrompt = false, ExtensionCommands = commands };
            await using (var session = new ConversationSession(new(directory, file), () => new PiRpcClient(first, TimeSpan.FromSeconds(3)), () => true))
            {
                await session.ConnectAsync();
                await session.SetApprovalModeAsync("plan");
                await session.SetModelAsync("test", "other");
                await session.SetThinkingLevelAsync("high");
            }
            var reopened = new FakePiTransport { AutoReply = true, ApplyModeCommands = true, StartTurnOnPrompt = false, ExtensionCommands = commands };
            await using var restored = new ConversationSession(new(directory, file), () => new PiRpcClient(reopened, TimeSpan.FromSeconds(3)), () => true);
            await restored.ConnectAsync();
            StringAssert.Contains(reopened.SessionEntries, "plan");
            Assert.AreEqual("other", reopened.ModelId);
            Assert.AreEqual("high", reopened.ThinkingLevel);
            var sent = reopened.Commands.ToList();
            Assert.IsTrue(sent.LastIndexOf("prompt") < sent.LastIndexOf("set_model"));
            Assert.IsTrue(sent.LastIndexOf("set_model") < sent.LastIndexOf("set_thinking_level"));
        }
        finally
        {
            foreach (var name in new[] { "package.json", "classifier-client.ts", "conversation.jsonl.settings.json" }) File.Delete(Path.Combine(directory, name));
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task CompletedMessageActionsWaitForTheWholeRunToSettle()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new PiAgentGui.ViewModels.Conversations.ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        session.Emit(new() { IsRunning = true, Entry = new("reply", "Assistant", "Working", IsAssistant: true) });
        dispatcher.Drain();
        Assert.IsFalse(workspace.Entries.Single().ShowConversationActions);
        session.Emit(new() { IsRunning = false, TurnCompleted = true });
        dispatcher.Drain();
        Assert.IsTrue(workspace.Entries.Single().ShowConversationActions);
    }

    [TestMethod]
    public async Task ReopeningWithoutPiHistoryRestoresConfirmedModelAndEffort()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        try
        {
            var first = new FakePiTransport { AutoReply = true };
            await using (var session = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(first, TimeSpan.FromSeconds(3)), () => false))
            {
                await session.ConnectAsync();
                await session.SetModelAsync("test", "other");
                await session.SetThinkingLevelAsync("high");
                first.RejectThinking = true;
                await Assert.ThrowsExceptionAsync<PiCommandException>(() => session.SetThinkingLevelAsync("low"));
            }
            Assert.IsFalse(File.Exists(file));
            var reopened = new FakePiTransport { AutoReply = true };
            await using var restored = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(reopened, TimeSpan.FromSeconds(3)), () => false);
            await restored.ConnectAsync();
            Assert.AreEqual("other", reopened.ModelId);
            Assert.AreEqual("high", reopened.ThinkingLevel);
        }
        finally { File.Delete(file + ".settings.json"); }
    }

    [TestMethod]
    public async Task SettingsAreIsolatedAndPreserveApprovalMode()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        try
        {
            var settings = new ConversationSettings("test", "other", "high", "ask");
            await new ConversationSettingsStore(file).SaveAsync(settings, default);
            Assert.AreEqual(settings, await new ConversationSettingsStore(file).ReadAsync(default));
            Assert.IsNull(await new ConversationSettingsStore(file + "-other").ReadAsync(default));
        }
        finally { File.Delete(file + ".settings.json"); }
    }
}
