using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Home;
using PiAgentGui.Models.Pi;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class MostUsedDefaultTests
{
    private readonly string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
    private static UsageSample Sample(string key, string model = "other", string? effort = "high", int recency = 0) =>
        new(key, Guid.Empty, Guid.Empty, DateTimeOffset.UnixEpoch.AddDays(recency), "test", model, 1, effort);
    [TestCleanup] public void Cleanup() { File.Delete(file); File.Delete(file + ".settings.json"); }

    [TestMethod]
    public void CountsResponsesOnceAndFiltersUnavailableModels()
    {
        PiModel[] models = [new("test", "fake", "Fake"), new("test", "other", "Other")];
        UsageSample[] history = [Sample("a"), Sample("a"), Sample("b", "fake", recency: 1), Sample("c", "gone"), Sample("d", "gone")];
        Assert.AreEqual("fake", MostUsedSelectors.Model(history, models)?.Id);
        Assert.AreEqual("other", MostUsedSelectors.Model(history.Append(Sample("e")), models)?.Id);
        Assert.IsNull(MostUsedSelectors.Model(history, []));
    }

    [TestMethod]
    public void EffortIsSpecificToModelAndLimitedToSupportedLevels()
    {
        UsageSample[] history = [Sample("a"), Sample("b"), Sample("c", effort: "low"), Sample("d", "fake", "medium"), Sample("e", "fake", "medium")];
        Assert.AreEqual("high", MostUsedSelectors.Effort(history, "test", "other", ["low", "high"]));
        Assert.AreEqual("low", MostUsedSelectors.Effort(history, "test", "other", ["low"]));
        Assert.IsNull(MostUsedSelectors.Effort(history, "test", "other", ["off"]));
    }

    [TestMethod]
    public void EffortFollowsParentBranchInsteadOfLastPhysicalRecord()
    {
        var tracker = new SessionEffortTracker();
        string? Read(string json) { using var doc = JsonDocument.Parse(json); return tracker.Read(doc.RootElement); }
        Assert.AreEqual("low", Read("""{"type":"thinking_level_change","id":"root","parentId":null,"thinkingLevel":"low"}"""));
        Assert.AreEqual("high", Read("""{"type":"thinking_level_change","id":"branch","parentId":"root","thinkingLevel":"high"}"""));
        Assert.AreEqual("low", Read("""{"type":"message","id":"reply","parentId":"root"}"""));
        Assert.AreEqual("high", Read("""{"type":"message","id":"reply2","parentId":"branch"}"""));
        Assert.IsNull(Read("""{"type":"message","id":"newroot","parentId":null}"""));
    }

    [TestMethod]
    public async Task NewConversationUsesAndPersistsMostUsedChoices()
    {
        var transport = new FakePiTransport { AutoReply = true };
        await using var session = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)))
        { ReadDefaultHistory = _ => Task.FromResult<IReadOnlyList<UsageSample>>([Sample("a"), Sample("b")]) };
        await session.ConnectAsync();
        Assert.AreEqual("other", transport.ModelId); Assert.AreEqual("high", transport.ThinkingLevel);
        var saved = await new ConversationSettingsStore(file).ReadAsync(default);
        Assert.AreEqual("other", saved?.Model); Assert.AreEqual("high", saved?.Effort);
    }

    [TestMethod]
    public async Task SavedChoicesOverrideUsageEvenWithoutSessionFile()
    {
        await new ConversationSettingsStore(file).SaveAsync(new("test", "fake", "medium"), default);
        var transport = new FakePiTransport { AutoReply = true };
        await using var session = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)))
        { ReadDefaultHistory = _ => throw new AssertFailedException("Saved conversations must not read automatic defaults.") };
        await session.ConnectAsync();
        Assert.AreEqual("fake", transport.ModelId); Assert.AreEqual("medium", transport.ThinkingLevel);
    }

    [TestMethod]
    public async Task ExistingHistoryWithoutSidecarKeepsPiSettings()
    {
        await File.WriteAllTextAsync(file, "");
        var transport = new FakePiTransport { AutoReply = true, ThinkingLevel = "high" };
        await using var session = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)))
        { ReadDefaultHistory = _ => throw new AssertFailedException("Existing history must keep Pi's choices.") };
        await session.ConnectAsync();
        Assert.AreEqual("fake", transport.ModelId); Assert.AreEqual("high", transport.ThinkingLevel);
    }

    [TestMethod]
    public async Task RejectedAutomaticModelDoesNotPreventConnecting()
    {
        var transport = new FakePiTransport { AutoReply = true, RejectModel = true };
        await using var session = new ConversationSession(new(Path.GetTempPath(), file), () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)))
        { ReadDefaultHistory = _ => Task.FromResult<IReadOnlyList<UsageSample>>([Sample("a")]) };
        await session.ConnectAsync();
        Assert.AreEqual("fake", transport.ModelId); Assert.AreEqual("low", transport.ThinkingLevel);
        Assert.IsTrue(transport.Commands.Contains("get_messages"));
    }
}
