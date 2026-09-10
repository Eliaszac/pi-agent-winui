using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class McpAdapterSupportTests
{
    [DataTestMethod]
    [DataRow("\"npm:pi-mcp-adapter@2.10.0\"")]
    [DataRow("{\"source\":\"npm:pi-mcp-adapter\"}")]
    public void RequiresRegistrationAndPackageAndAvoidsDuplicateCard(string registration)
    {
        var allowed = Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests");
        var root = Directory.CreateDirectory(Path.Combine(allowed, Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.IsTrue(McpAdapterSupport.GetInstallationState(root).NeedsSetup);
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"packages\":[" + registration + "]}");
            Assert.IsTrue(McpAdapterSupport.GetInstallationState(root).NeedsSetup);
            var package = Directory.CreateDirectory(Path.Combine(root, "npm", "node_modules", "pi-mcp-adapter")).FullName;
            File.WriteAllText(Path.Combine(package, "package.json"), """{"name":"pi-mcp-adapter","version":"2.10.0","pi":{"extensions":["index.ts"]}}""");
            Assert.IsFalse(McpAdapterSupport.GetInstallationState(root).NeedsSetup);
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
