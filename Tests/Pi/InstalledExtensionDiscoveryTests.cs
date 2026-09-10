using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class InstalledExtensionDiscoveryTests
{
    [TestMethod]
    public void FindsConfiguredPackagesAndLocalExtensionsWithoutListingDependenciesOrSupportedCards()
    {
        var allowed = Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests");
        var root = Directory.CreateDirectory(Path.Combine(allowed, Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), """{"packages":["npm:example@1.2.3","npm:pi-auto-session-name@0.1.1","npm:skills-only"],"extensions":["extensions/local.ts"]}""");
            var package = Directory.CreateDirectory(Path.Combine(root, "npm", "node_modules", "example")).FullName;
            File.WriteAllText(Path.Combine(package, "package.json"), """{"name":"example","version":"1.2.3","description":"Example extension","pi":{"extensions":["index.ts"]}}""");
            var supported = Directory.CreateDirectory(Path.Combine(root, "npm", "node_modules", "pi-auto-session-name")).FullName;
            File.WriteAllText(Path.Combine(supported, "package.json"), """{"name":"pi-auto-session-name","pi":{"extensions":["index.ts"]}}""");
            var skills = Directory.CreateDirectory(Path.Combine(root, "npm", "node_modules", "skills-only")).FullName;
            File.WriteAllText(Path.Combine(skills, "package.json"), """{"name":"skills-only","pi":{"skills":["skills"]}}""");
            var local = Directory.CreateDirectory(Path.Combine(root, "extensions")).FullName;
            File.WriteAllText(Path.Combine(local, "local.ts"), "This file must never be executed.");
            File.WriteAllText(Path.Combine(local, "config.json"), "{}");
            var result = new InstalledExtensionDiscovery().Discover(root);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("1.2.3", result.Single(item => item.Name == "example").Version);
            Assert.AreEqual(1, result.Count(item => item.Name == "local"));
        }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(allowed) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup outside test directory");
            Directory.Delete(root, true);
        }
    }
}
