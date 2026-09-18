using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PiAgentGui.Tests.Updates;

[TestClass]
public sealed class InstallerPackagingTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Packaging");

    private static string[] ObsoletePaths()
    {
        var lines = File.ReadAllLines(Path.Combine(Fixtures, "ObsoleteArtwork.iss"))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith(';')).ToArray();
        return lines.Select(line =>
        {
            var match = Regex.Match(line, "^Type: files; Name: \"\\{app\\}\\\\(Assets\\\\(?:Providers|OpenIn|Integrations)\\\\[a-z0-9-]+\\.(?:svg|png))\"$");
            Assert.IsTrue(match.Success, "Cleanup must target an exact artwork file, without wildcards, traversal or directory deletion: " + line);
            return match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
        }).ToArray();
    }

    [TestMethod]
    public void CleanupOnlyListsRemovedArtworkAndNeverCurrentAssetsOrNotices()
    {
        var paths = ObsoletePaths();
        Assert.AreEqual(63, paths.Length, "Review the historical artwork inventory before changing cleanup scope.");
        Assert.AreEqual(paths.Length, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var path in paths)
            Assert.IsFalse(File.Exists(Path.Combine(Fixtures, path)), "A current asset must not be deleted: " + path);
        var installer = File.ReadAllText(Path.Combine(Fixtures, "PiAgent.iss"));
        var section = installer.Split("[InstallDelete]", StringSplitOptions.None)[1].Split("[Icons]", StringSplitOptions.None)[0];
        var entries = section.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        CollectionAssert.AreEqual(new[]
        {
            "#include \"ObsoleteArtwork.iss\"",
            "Type: files; Name: \"{userprograms}\\Pi Agent.lnk\"",
            "Type: files; Name: \"{userdesktop}\\Pi Agent.lnk\""
        }, entries);
    }

    [TestMethod]
    public void CleanupInventorySimulationPreservesCurrentArtworkAndUserData()
    {
        // Exercises the literal inventory, not the Inno Setup installation engine.
        var root = Path.Combine(Path.GetTempPath(), "PiPackagingTests-" + Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "installation");
        var obsolete = ObsoletePaths();
        var preserved = Directory.GetFiles(Path.Combine(Fixtures, "Assets"), "*", SearchOption.AllDirectories)
            .Select(path => Path.Combine(install, Path.GetRelativePath(Fixtures, path)))
            .Concat(new[]
            {
                Path.Combine(install, "Assets", "Pi.ico"),
                Path.Combine(install, "Assets", "Providers", "custom.svg"),
                Path.Combine(install, "Legal", "THIRD-PARTY-NOTICES.md"),
                Path.Combine(root, "PiAgentGui", "settings.json"),
                Path.Combine(root, "PiAgentGui", "sessions", "conversation.jsonl"),
                Path.Combine(root, "project", "source.cs"),
                Path.Combine(root, ".pi", "agent", "auth.json")
            }).ToArray();
        try
        {
            foreach (var path in obsolete.Select(path => Path.Combine(install, path)).Concat(preserved))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "sentinel");
            }
            for (var pass = 0; pass < 2; pass++)
                foreach (var path in obsolete) File.Delete(Path.Combine(install, path));
            Assert.IsTrue(obsolete.All(path => !File.Exists(Path.Combine(install, path))));
            foreach (var path in preserved) Assert.AreEqual("sentinel", File.ReadAllText(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public void PublisherMetadataKeepsExistingProductAndUpgradeIdentity()
    {
        var installer = File.ReadAllText(Path.Combine(Fixtures, "PiAgent.iss"));
        foreach (var expected in new[]
        {
            "#define AppIdentity \"PiAgentGui.Desktop\"", "AppId={#AppIdentity}", "AppName=Pi desktop", "AppPublisher=Eliaszac",
            "DefaultDirName={localappdata}\\Programs\\Pi Agent", "UninstallDisplayName=Pi desktop",
            "AppSupportURL=https://github.com/Eliaszac/pi-agent-winui/issues", "OutputBaseFilename=PiDesktop-Setup-{#AppVersion}-x64"
        }) StringAssert.Contains(installer, expected);
        var project = XDocument.Load(Path.Combine(Fixtures, "AppProject.xml"));
        Assert.AreEqual("Eliaszac", project.Descendants("Company").Single().Value);
        Assert.AreEqual("Pi desktop", project.Descendants("Product").Single().Value);
        Assert.AreEqual("None", project.Descendants("WindowsPackageType").Single().Value);
    }
}
