using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CapabilitiesTests
{
    [TestMethod]
    public void SkillsSupportBothPiSourceFormatsAndExcludeOtherCommands()
    {
        using var data = JsonDocument.Parse("""
            [{"name":"skill:old","source":"skill","location":"user","path":"C:/skills/old/SKILL.md"},
             {"name":"skill:new","source":"skill","sourceInfo":{"scope":"project","path":"C:/project/SKILL.md"}},
             {"name":"mcp","source":"extension"}]
            """);
        var skills = CapabilityParser.Skills(data.RootElement);
        Assert.AreEqual(2, skills.Count);
        Assert.AreEqual("Project", skills[0].Scope);
        Assert.AreEqual("Global", skills[1].Scope);
        Assert.AreEqual("C:/project/SKILL.md", skills[0].Path);
    }

    [TestMethod]
    [DataRow("bad JSON")]
    [DataRow("{\"version\":2,\"servers\":[]}")]
    [DataRow("{\"version\":1,\"servers\":[{\"name\":\"x\",\"status\":\"invented\",\"toolCount\":1}]}")]
    [DataRow("{\"version\":1,\"servers\":[{\"name\":\"x\",\"status\":\"connected\",\"toolCount\":\"oops\"}]}")]
    public void MalformedStatusIsIgnored(string json) => Assert.IsNull(CapabilityParser.Mcp(json));

    [TestMethod]
    public async Task InventoryCanBeReadWhileRunningWithoutSendingAPromptOrStopping()
    {
        var transport = new FakePiTransport { AutoReply = true, ExtensionCommands = "[]" };
        await using var session = new ConversationSession(new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "capabilities.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.ConnectAsync();
        await session.SendAsync("Start a fake run");
        var count = transport.Commands.Count;
        var result = await session.RunOperationAsync(ConversationOperation.Commands);
        Assert.AreEqual(JsonValueKind.Array, PiJson.Field(result, "commands").ValueKind);
        CollectionAssert.AreEqual(new[] { "get_commands" }, transport.Commands.Skip(count).ToArray());
    }

    [TestMethod]
    public async Task SwitchingIgnoresLateInventoryAndDisconnectClearsLiveStatus()
    {
        var dispatcher = new QueuedUiDispatcher();
        var firstSession = new FakeConversationSession();
        var secondSession = new FakeConversationSession();
        await using var first = new ConversationViewModel(firstSession, dispatcher);
        await using var second = new ConversationViewModel(secondSession, dispatcher);
        await first.InitializeAsync(); await second.InitializeAsync(); dispatcher.Drain();
        var pending = new TaskCompletionSource<JsonElement>();
        firstSession.OperationHandler = (_, _) => pending.Task;
        secondSession.OperationHandler = (_, _) => Task.FromResult(JsonDocument.Parse("""{"commands":[{"name":"skill:second","source":"skill"}]}""").RootElement.Clone());
        var panel = new CapabilitiesPanelViewModel(() => ("Installed", false));
        panel.Select(first);
        var oldRead = panel.RefreshAsync();
        panel.Select(second);
        await panel.RefreshAsync();
        pending.SetResult(JsonDocument.Parse("""{"commands":[{"name":"skill:first","source":"skill"}]}""").RootElement.Clone());
        await oldRead;
        Assert.AreEqual("second", panel.Skills.Single().Name);
        Assert.AreEqual("", second.Draft);
        Assert.AreEqual("", first.Draft);
        Assert.AreEqual(0, secondSession.Sent.Count);
        secondSession.Emit(new() { McpStatus = new([new("docs", "connected", 3, 1)]) }); dispatcher.Drain();
        Assert.AreEqual("Connected", panel.Servers.Single().Status);
        await secondSession.DisconnectAsync(); dispatcher.Drain();
        Assert.AreEqual(0, panel.Servers.Count);
        Assert.IsNull(second.McpStatus);
        Assert.AreEqual(0, panel.Skills.Count);
    }
}
