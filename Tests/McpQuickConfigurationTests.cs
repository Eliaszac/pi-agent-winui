using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using System.Text.Json.Nodes;

namespace PiAgentGui.Tests;

[TestClass]
public sealed class McpQuickConfigurationTests
{
    [TestMethod]
    public void AllPresetsPassImportValidationAndRespectDuplicateNames()
    {
        foreach (var id in new[] { "atlassian", "github", "linear", "notion", "supabase" })
        {
            var json = McpQuickConfiguration.Build(id);
            Assert.IsTrue(McpImportParser.Parse(json, new HashSet<string>()).Single().CanImport);
            Assert.IsFalse(McpImportParser.Parse(json, new HashSet<string> { id }).Single().CanImport);
            var server = JsonNode.Parse(json)!["mcpServers"]![id]!;
            Assert.IsNull(server["command"]);
            Assert.IsNull(server["bearerToken"]);
            Assert.IsNull(server["headers"]);
            Assert.AreEqual(id is "github" or "obsidian" ? "bearer" : "oauth", server["auth"]!.GetValue<string>());
        }
    }

    [TestMethod]
    public void UnknownPresetIsRejected() => Assert.ThrowsException<ArgumentException>(() => McpQuickConfiguration.Build("unknown"));

    [TestMethod]
    public void ObsidianUsesAnExplicitVaultAndNoCredentials()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vault-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, ".obsidian"));
        try
        {
            var json = McpQuickConfiguration.BuildObsidian(folder);
            var entry = McpImportParser.Parse(json, new HashSet<string>()).Single();
            var server = JsonNode.Parse(entry.Definition)!;
            Assert.AreEqual("npx.cmd", server["command"]!.GetValue<string>());
            Assert.AreEqual("obsidian=" + folder, server["args"]![4]!.GetValue<string>());
            Assert.IsNull(server["url"]);
            Assert.IsNull(server["auth"]);
            Assert.ThrowsException<ArgumentException>(() => McpQuickConfiguration.BuildObsidian(Path.Combine(folder, "missing")));
        }
        finally { Directory.Delete(folder, true); }
    }
}


