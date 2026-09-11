using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class McpAdapterSupportTests
{
    [DataTestMethod]
    [DataRow("\"npm:pi-mcp-adapter@2.33.0\"", "2.33.0", false)]
    [DataRow("{\"source\":\"npm:pi-mcp-adapter\"}", "2.33.0", false)]
    [DataRow("\"npm:pi-mcp-adapter@2.10.0\"", "2.10.0", true)]
    public void RequiresRegistrationAndPackageAndAvoidsDuplicateCard(string registration, string version, bool needsUpdate)
    {
        var allowed = Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests");
        var root = Directory.CreateDirectory(Path.Combine(allowed, Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.IsTrue(McpAdapterSupport.GetInstallationState(root).NeedsSetup);
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"packages\":[" + registration + "]}");
            Assert.IsTrue(McpAdapterSupport.GetInstallationState(root).NeedsSetup);
            var package = Directory.CreateDirectory(Path.Combine(root, "npm", "node_modules", "pi-mcp-adapter")).FullName;
            File.WriteAllText(Path.Combine(package, "package.json"), System.Text.Json.JsonSerializer.Serialize(
                new { name = "pi-mcp-adapter", version, pi = new { extensions = new[] { "index.ts" } } }));
            Assert.AreEqual(needsUpdate, McpAdapterSupport.GetInstallationState(root).NeedsSetup);
            Assert.AreEqual(0, new InstalledExtensionDiscovery().Discover(root).Count);
        }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(allowed) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup outside test directory");
            Directory.Delete(root, true);
        }
    }
}
