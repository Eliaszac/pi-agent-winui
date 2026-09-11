using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CapabilityImportTests
{
    private string root = "";
    [TestMethod]
    public void ManualStdioPreservesPathsArgumentsAndEnvironmentValues()
    {
        var json = McpServerDefinitionBuilder.Build(" local ", true, @"C:\Program Files\server.exe", "--path\r\nC:\\My Files\r\n", "TOKEN=a=b==\nEMPTY=", "", "");
        var server = JsonNode.Parse(json)!["mcpServers"]!["local"]!;
        Assert.AreEqual(@"C:\Program Files\server.exe", server["command"]!.GetValue<string>());
        Assert.AreEqual(@"C:\My Files", server["args"]![1]!.GetValue<string>());
        Assert.AreEqual("a=b==", server["env"]!["TOKEN"]!.GetValue<string>());
        Assert.AreEqual("", server["env"]!["EMPTY"]!.GetValue<string>());
    }

    [TestMethod]
    public void ManualHttpOmitsHiddenStdioFieldsAndPreservesHeaders()
    {
        var json = McpServerDefinitionBuilder.Build("remote", false, "unused", "unused", "invalid hidden input", "https://example.com/mcp", "Authorization=Bearer abc==");
        var server = JsonNode.Parse(json)!["mcpServers"]!["remote"]!;
        Assert.IsNull(server["command"]);
        Assert.AreEqual("Bearer abc==", server["headers"]!["Authorization"]!.GetValue<string>());
    }

    [TestMethod]
    [DataRow("NAME=value\nNAME=other")]
    [DataRow("missing separator")]
    [DataRow(" =value")]
    public void ManualFieldsRejectInvalidPairs(string values) => Assert.ThrowsException<ArgumentException>(() => McpServerDefinitionBuilder.Build("test", true, "node", "", values, "", ""));

    [TestInitialize] public void Setup() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup] public void Cleanup()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe cleanup path");
        Directory.Delete(root, true);
    }

    [TestMethod]
    public async Task SkillRegistrationPreservesScriptsAndUnrelatedSettings()
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "my-skill")).FullName;
        var script = Path.Combine(folder, "run.ps1");
        await File.WriteAllTextAsync(script, "Write-Output 'unchanged'");
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "{\"theme\":\"dark\",\"skills\":[\"existing\"],\"packages\":[\"npm:example\"]}");
        var registration = new GlobalSkillRegistration(root);
        await registration.RegisterAsync(folder);
        var saved = await new GlobalConfigurationFile(Path.Combine(root, "settings.json")).ReadAsync();
        Assert.AreEqual("dark", saved["theme"]!.GetValue<string>());
        Assert.AreEqual("npm:example", saved["packages"]![0]!.GetValue<string>());
        Assert.AreEqual(folder, saved["skills"]![1]!.GetValue<string>());
        Assert.AreEqual("Write-Output 'unchanged'", await File.ReadAllTextAsync(script));
        await Assert.ThrowsExceptionAsync<IOException>(() => registration.RegisterAsync(folder));
    }

    [TestMethod]
    public async Task McpImportPreservesAuthAndSettingsAndSkipsConflicts()
    {
        var path = Path.Combine(root, "mcp.json");
        await File.WriteAllTextAsync(path, """{"settings":{"idleTimeout":50},"mcpServers":{"existing":{"command":"old"}}}""");
        var importer = new McpConfigImporter(root, () => true);
        var entries = await importer.PreviewAsync("""{"mcpServers":{"existing":{"command":"replacement"},"local":{"command":"pwsh","args":["-File","server.ps1"],"env":{"KEY":"test-value"}},"remote":{"url":"https://example.com/mcp","headers":{"Authorization":"test-value"},"auth":"bearer"}}}""", []);
        Assert.IsFalse(entries[0].CanImport);
        entries[0].Selected = true;
        Assert.IsFalse(entries[0].Selected);
        await importer.ImportAsync(entries);
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.AreEqual(50, saved["settings"]!["idleTimeout"]!.GetValue<int>());
        Assert.AreEqual("old", saved["mcpServers"]!["existing"]!["command"]!.GetValue<string>());
        Assert.AreEqual("test-value", saved["mcpServers"]!["local"]!["env"]!["KEY"]!.GetValue<string>());
        Assert.AreEqual("test-value", saved["mcpServers"]!["remote"]!["headers"]!["Authorization"]!.GetValue<string>());
        Assert.AreEqual("bearer", saved["mcpServers"]!["remote"]!["auth"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task LateDuplicateRejectsWholeImportWithoutPartialWrite()
    {
        var importer = new McpConfigImporter(root, () => true);
        var entries = await importer.PreviewAsync("""{"mcpServers":{"first":{"command":"node"},"second":{"command":"node"}}}""", []);
        var path = Path.Combine(root, "mcp.json");
        const string original = """{"mcpServers":{"second":{"command":"keep"}}}""";
        await File.WriteAllTextAsync(path, original);
        await Assert.ThrowsExceptionAsync<IOException>(() => importer.ImportAsync(entries));
        Assert.AreEqual(original, await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task MissingAdapterCannotWriteAndRuntimeNamesAreConflicts()
    {
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new McpConfigImporter(root, () => false).PreviewAsync("{}", []));
        Assert.IsFalse(File.Exists(Path.Combine(root, "mcp.json")));
        var preview = await new McpConfigImporter(root, () => true).PreviewAsync("""{"mcpServers":{"project-server":{"command":"node"}}}""", ["project-server"]);
        Assert.IsFalse(preview.Single().CanImport);
    }

    [TestMethod]
    [DataRow("{\"mcpServers\":{\"a\":{\"command\":\"node\",\"url\":\"https://example.com\"}}}")]
    [DataRow("{\"mcpServers\":{\"a\":{\"command\":\"node\",\"args\":\"script.js\"}}}")]
    [DataRow("{\"mcpServers\":{\"a\":{\"command\":\"node\",\"env\":{\"x\":1}}}}")]
    [DataRow("{\"mcpServers\":{\"a\":{\"command\":\"node\",\"command\":\"pwsh\"}}}")]
    [DataRow("{\"mcpServers\":{\"a\":{\"url\":\"file:///tmp/test\"}}}")]
    public void InvalidMcpDefinitionsAreRejected(string json) => Assert.ThrowsException<ArgumentException>(() => McpImportParser.Parse(json, new HashSet<string>()));

    [TestMethod]
    public async Task ConcurrentUpdatesPreserveBothChanges()
    {
        var file = new GlobalConfigurationFile(Path.Combine(root, "settings.json"));
        await Task.WhenAll(file.UpdateAsync(value => value["first"] = true), file.UpdateAsync(value => value["second"] = true));
        var saved = await file.ReadAsync();
        Assert.IsTrue(saved["first"]!.GetValue<bool>());
        Assert.IsTrue(saved["second"]!.GetValue<bool>());
    }

    [TestMethod]
    public async Task PackageInstallUsesSingleSourceArgumentAndGlobalDirectory()
    {
        var executable = Path.Combine(root, "pi.exe");
        await File.WriteAllTextAsync(executable, "");
        var installer = new PiPackageInstaller(new PiInstallationLocator(new PiRuntimeOptions { ExecutablePath = executable }), root);
        var start = installer.CreateStartInfo("git:github.com/owner/repo@v1");
        CollectionAssert.AreEqual(new[] { "install", "git:github.com/owner/repo@v1" }, start.ArgumentList.ToArray());
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.AreEqual(root, start.Environment["PI_CODING_AGENT_DIR"]);
        Assert.AreEqual(root, start.WorkingDirectory);
    }

    [TestMethod]
    [DataRow("npm:@scope/package@1.2.3")]
    [DataRow("git:github.com/owner/repo@v1")]
    [DataRow("https://github.com/owner/repo")]
    public void PackageSourcesAccepted(string source) => Assert.AreEqual(source, PiPackageSource.Validate(source));

    [TestMethod]
    [DataRow("pi install npm:foo")]
    [DataRow("--help")]
    [DataRow("npm:foo; exit")]
    [DataRow("https://user:password@github.com/owner/repo")]
    public void CommandsAndCredentialsRejected(string source) => Assert.ThrowsException<ArgumentException>(() => PiPackageSource.Validate(source));
}
