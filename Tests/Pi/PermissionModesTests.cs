using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PermissionModesTests
{
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("ask")]
    [DataRow("plan")]
    [DataRow("future")]
    public async Task DefaultAutoAppliesOnlyWhenNoModeHasBeenSaved(string? savedMode)
    {
        var transport = new FakePiTransport { AutoReply = true, ApplyModeCommands = true, StartTurnOnPrompt = false,
            ExtensionCommands = """[{"name":"mode","source":"extension"}]""",
            SessionEntries = savedMode is null ? "[]" : JsonSerializer.Serialize(new[] { new { type = "custom", customType = "modes", data = new { currentMode = savedMode } } }) };
        await using var client = new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync(new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "default-mode.jsonl")));
        string? mode = null;
        var integration = new PermissionModesIntegration(update => { if (update.HasApprovalModeUpdate) mode = update.ApprovalMode; }, () => true, _ => true);
        await integration.DiscoverAsync(client, default);
        Assert.IsTrue(integration.Available);
        Assert.AreEqual(savedMode is null, integration.DefaultApplied);
        Assert.AreEqual(savedMode is null ? "auto" : savedMode == "future" ? null : savedMode, mode);
        Assert.AreEqual(savedMode is null ? 1 : 0, transport.Commands.Count(command => command == "prompt"));
        await integration.DiscoverAsync(client, default);
        Assert.IsFalse(integration.DefaultApplied);
        Assert.AreEqual(savedMode is null ? 1 : 0, transport.Commands.Count(command => command == "prompt"));
    }

    [TestMethod]
    public void AgentDirectoryMatchesPiWhenInheritedProfileDiffersFromWindowsAccount()
    {
        Assert.AreEqual(@"C:\Users\Developer\.pi\agent", PiAgentDirectory.Resolve(null, @"C:\Users\Developer", @"C:\Users\Runner"));
        Assert.AreEqual(@"C:\Users\Developer\.pi\custom", PiAgentDirectory.Resolve("~/.pi/custom", @"C:\Users\Developer", @"C:\Users\Runner"));
        Assert.AreEqual(@"D:\Pi", PiAgentDirectory.Resolve(@"D:\Pi", @"C:\Users\Developer", @"C:\Users\Runner"));
        Assert.AreEqual(@"C:\Users\Runner\.pi\agent", PiAgentDirectory.Resolve(null, null, @"C:\Users\Runner"));
    }
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingExtensionNeverInvokesModeAsAnAgentPrompt(bool globallyConfigured)
    {
        var transport = new FakePiTransport { AutoReply = true };
        await using var client = new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync(new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "permissions.jsonl")));
        var integration = new PermissionModesIntegration(_ => { }, () => globallyConfigured);
        await integration.DiscoverAsync(client, default);
        Assert.IsFalse(integration.Available);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => integration.SetAsync(client, "bypass", default));
        Assert.IsFalse(transport.Commands.Contains("prompt"));
    }

    [TestMethod]
    public async Task LoadedExtensionUsesAuthoritativeEntriesAndUnsupportedRpcStaysUnavailable()
    {
        var transport = new FakePiTransport { AutoReply = true, StartTurnOnPrompt = false,
            ExtensionCommands = """[{"name":"mode","source":"extension"}]""",
            SessionEntries = """[{"type":"custom","customType":"modes","data":{"currentMode":"ask"}}]""" };
        await using var client = new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync(new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "permissions.jsonl")));
        string? mode = null;
        var integration = new PermissionModesIntegration(update => { if (update.HasApprovalModeUpdate) mode = update.ApprovalMode; }, () => true, _ => true);
        await integration.DiscoverAsync(client, default);
        Assert.IsTrue(integration.Available);
        Assert.AreEqual("ask", mode);
        // An accepted command alone is not proof of the requested mode.
        await integration.SetAsync(client, "plan", default);
        Assert.AreEqual("ask", mode);
        transport.RejectEntries = true;
        await integration.DiscoverAsync(client, default);
        Assert.IsFalse(integration.Available);
        Assert.IsNull(mode);
    }

    [TestMethod]
    public void UnknownLatestModeDoesNotReuseOlderPermissionState()
    {
        using var document = JsonDocument.Parse("""[{"type":"custom","customType":"modes","data":{"currentMode":"ask"}},{"type":"custom","customType":"modes","data":{"currentMode":"future"}}]""");
        Assert.IsNull(PermissionModesSupport.ReadMode(document.RootElement));
    }

    [TestMethod]
    public void DiscoveryRequiresGlobalConfigurationAndExactPackageIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PiAgentGui-permissions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.IsFalse(PermissionModesSupport.IsGloballyConfigured(directory));
            File.WriteAllText(Path.Combine(directory, "settings.json"), """{"packages":[{"source":"npm:@georgedong32/permission-modes@2.6.3"}]}""");
            Assert.IsTrue(PermissionModesSupport.IsGloballyConfigured(directory));
            File.WriteAllText(Path.Combine(directory, "package.json"), """{"name":"@georgedong32/permission-modes","version":"2.6.3"}""");
            var command = JsonSerializer.SerializeToElement(new { name = "mode", source = "extension", sourceInfo = new { path = Path.Combine(directory, "index.ts"), scope = "user" } });
            Assert.IsTrue(PermissionModesSupport.IsSupportedCommand(command));
            File.WriteAllText(Path.Combine(directory, "package.json"), """{"name":"@georgedong32/permission-modes","version":"99.0.0"}""");
            Assert.IsFalse(PermissionModesSupport.IsSupportedCommand(command));
            var projectCommand = JsonSerializer.SerializeToElement(new { name = "mode", source = "extension", sourceInfo = new { path = Path.Combine(directory, "index.ts"), scope = "project" } });
            Assert.IsFalse(PermissionModesSupport.IsSupportedCommand(projectCommand));
        }
        finally
        {
            File.Delete(Path.Combine(directory, "settings.json"));
            File.Delete(Path.Combine(directory, "package.json"));
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task ModelChangesAreIsolatedBetweenConversationProcesses()
    {
        var firstTransport = new FakePiTransport { AutoReply = true };
        var secondTransport = new FakePiTransport { AutoReply = true };
        await using var first = new ConversationSession(new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "model-first.jsonl")), () => new PiRpcClient(firstTransport, TimeSpan.FromSeconds(3)));
        await using var second = new ConversationSession(new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "model-second.jsonl")), () => new PiRpcClient(secondTransport, TimeSpan.FromSeconds(3)));
        await Task.WhenAll(first.ConnectAsync(), second.ConnectAsync());
        await first.SetModelAsync("test", "other");
        Assert.AreEqual("other", firstTransport.ModelId);
        Assert.AreEqual("fake", secondTransport.ModelId);
        Assert.IsFalse(secondTransport.Commands.Contains("set_model"));
        secondTransport.RejectModel = true;
        await Assert.ThrowsExceptionAsync<PiCommandException>(() => second.SetModelAsync("test", "other"));
        Assert.AreEqual("other", firstTransport.ModelId);
        Assert.AreEqual("fake", secondTransport.ModelId);
    }
}
