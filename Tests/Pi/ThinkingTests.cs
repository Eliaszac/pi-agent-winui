using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ThinkingTests
{
    [TestMethod]
    public async Task NewSessionDefaultsLowAndChangesStayInOneConversation()
    {
        var firstTransport = new FakePiTransport { AutoReply = true };
        var secondTransport = new FakePiTransport { AutoReply = true };
        await using var first = Create(firstTransport);
        await using var second = Create(secondTransport);
        await Task.WhenAll(first.ConnectAsync(), second.ConnectAsync());
        Assert.AreEqual("low", firstTransport.ThinkingLevel);
        Assert.AreEqual("low", secondTransport.ThinkingLevel);
        await first.SetThinkingLevelAsync("high");
        Assert.AreEqual("high", firstTransport.ThinkingLevel);
        Assert.AreEqual("low", secondTransport.ThinkingLevel);
        await first.SendAsync("work");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => first.SetThinkingLevelAsync("low"));
    }

    [TestMethod]
    public async Task SavedSessionKeepsItsEffortAndRejectedSelectionKeepsConfirmedState()
    {
        var file = Path.GetTempFileName();
        try
        {
            var transport = new FakePiTransport { AutoReply = true, ThinkingLevel = "high" };
            await using var session = Create(transport, file);
            string? selected = null;
            session.Updated += update => { if (update.HasThinkingLevelUpdate) selected = update.ThinkingLevel; };
            await session.ConnectAsync();
            Assert.AreEqual("high", selected);
            Assert.IsFalse(transport.Commands.Contains("set_thinking_level"));
            transport.RejectThinking = true;
            await Assert.ThrowsExceptionAsync<PiCommandException>(() => session.SetThinkingLevelAsync("low"));
            Assert.AreEqual("high", selected);
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public async Task UnsupportedLowKeepsPiChoiceAndModelSwitchRefreshesCapabilities()
    {
        var transport = new FakePiTransport { AutoReply = true, ThinkingLevels = ["off"], ThinkingLevel = "off" };
        await using var session = Create(transport);
        IReadOnlyList<string>? levels = null;
        session.Updated += update => { if (update.ThinkingLevels is not null) levels = update.ThinkingLevels; };
        await session.ConnectAsync();
        Assert.IsFalse(transport.Commands.Contains("set_thinking_level"));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => session.SetThinkingLevelAsync("low"));
        transport.ThinkingLevels = ["low", "high", "max"];
        transport.ThinkingLevel = "high";
        await session.SetModelAsync("test", "other");
        CollectionAssert.AreEqual(transport.ThinkingLevels, levels!.ToArray());
        Assert.AreEqual("high", transport.ThinkingLevel);
    }

    private static ConversationSession Create(FakePiTransport transport, string? file = null) =>
        new(new PiLaunchRequest(Path.GetTempPath(), file ?? Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jsonl")),
            () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), () => false);
}
