using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiBrowserSupportTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DetectsCompleteGitAndManualInstallsWithoutDuplicatingOtherExtensions(bool manual)
    {
        var allowed = Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests");
        var root = Directory.CreateDirectory(Path.Combine(allowed, Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.IsTrue(PiBrowserSupport.GetInstallationState(root).NeedsSetup);
            var directory = Directory.CreateDirectory(manual
                ? Path.Combine(root, "extensions", "pi-browser")
                : Path.Combine(root, "git", "github.com", "larsderidder", "pi-browser")).FullName;
            if (!manual)
                File.WriteAllText(Path.Combine(root, "settings.json"),
                    """{"packages":[{"source":"git:github.com/larsderidder/pi-browser@some-ref"}]}""");
            File.WriteAllText(Path.Combine(directory, "package.json"),
                """{"name":"pi-browser","version":"0.1.0","pi":{"extensions":["index.ts"]}}""");
            File.WriteAllText(Path.Combine(directory, "index.ts"), "Never execute this file.");
            Assert.IsTrue(PiBrowserSupport.GetInstallationState(root).NeedsSetup);
            var dependency = Directory.CreateDirectory(Path.Combine(directory, "node_modules", "playwright")).FullName;
            File.WriteAllText(Path.Combine(dependency, "package.json"), "{}");
            Assert.IsFalse(PiBrowserSupport.GetInstallationState(root).NeedsSetup);
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
