using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class McpSetupTests
{
    [TestMethod]
    public async Task EditingAndRemovingPreserveOtherSettingsAndRejectStaleDefinitions()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-edit-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "mcp.json"), """{"settings":{"timeout":123},"mcpServers":{"one":{"command":"node","args":["old.js"]},"two":{"url":"https://example.com"}}}""");
            var store = new McpSetupService(root, () => true);
            var original = (await store.ReadAsync("one"))!;
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.EditAsync("one", original, """{"command":"node","command":"other"}"""));
            await store.EditAsync("one", original, """{"command":"node","args":["new.js"],"customOption":true}""");
            await Assert.ThrowsExceptionAsync<IOException>(() => store.RemoveAsync("one", original));
            var updated = (await store.ReadAsync("one"))!;
            Assert.IsTrue(updated["customOption"]!.GetValue<bool>());
            await store.RemoveAsync("one", updated);
            Assert.IsNull(await store.ReadAsync("one"));
            Assert.IsNotNull(await store.ReadAsync("two"));
            var saved = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "mcp.json")))!;
            Assert.AreEqual(123, saved["settings"]!["timeout"]!.GetValue<int>());
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AuthChangesRemoveOldAuthorizationAndPreserveOtherOptions()
    {
        var original = JsonNode.Parse("""{"url":"https://example.com/mcp","auth":"bearer","bearerTokenEnv":"OLD_TOKEN","headers":{"authorization":"secret","X-Project":"dev"},"timeout":123} """)!.AsObject();
        var updated = McpAuthenticationSettings.Apply(original, "oauth");
        Assert.IsNull(updated["bearerTokenEnv"]);
        Assert.IsNull(updated["headers"]!["authorization"]);
        Assert.AreEqual("dev", updated["headers"]!["X-Project"]!.GetValue<string>());
        Assert.AreEqual(123, updated["timeout"]!.GetValue<int>());
        Assert.AreEqual("secret", original["headers"]!["authorization"]!.GetValue<string>());
        var bearer = McpAuthenticationSettings.Apply(original, "bearer");
        Assert.IsTrue(bearer["bearerTokenStore"]!.GetValue<bool>());
        Assert.IsNull(bearer["bearerToken"]);
    }

    [TestMethod]
    public async Task SavesPreserveOtherServersAndRejectConcurrentChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-setup-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var store = new McpSetupService(root, () => true);
            var original = new JsonObject { ["url"] = "https://example.com/mcp", ["auth"] = "oauth" };
            await store.SaveAsync("one", null, original);
            await store.SaveAsync("two", null, original);
            await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync("one", null, original));
            var updated = McpAuthenticationSettings.Apply(original, "bearer");
            await store.SaveAsync("one", original, updated);
            await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync("one", original, original));
            Assert.AreEqual("oauth", (await store.ReadAsync("two"))!["auth"]!.GetValue<string>());
            Assert.AreEqual(2, (await store.ReadNamesAsync()).Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SaveOnlyKeepsAuthenticationPendingAndNewTokenConnectRequiresInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-vm-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var store = new McpSetupService(root, () => true);
            using var model = new McpSetupViewModel("github", McpQuickConfiguration.Build("github"), new(null!, null!, null!, store));
            await model.InitializeAsync();
            Assert.IsTrue(model.NeedsToken);
            await model.RunAsync(true);
            Assert.IsFalse(model.IsSaved);
            StringAssert.Contains(model.Message, "Enter an API token");
            await model.RunAsync(false);
            Assert.IsTrue(model.IsSaved);
            Assert.IsTrue(model.CanAct);
            StringAssert.Contains(model.Message, "have not been checked");
            Assert.IsNull((await store.ReadAsync("github"))!["bearerToken"]);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AuthDetectionDoesNotGuessForCustomHttp()
    {
        Assert.AreEqual("auto", McpAuthenticationSettings.Detect(new() { ["url"] = "https://example.com" }));
        Assert.AreEqual("local", McpAuthenticationSettings.Detect(new() { ["command"] = "node" }));
        Assert.AreEqual("none", McpAuthenticationSettings.Detect(new() { ["url"] = "https://example.com", ["auth"] = false }));
    }
}
