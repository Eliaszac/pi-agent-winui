using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class PackageScriptImporterTests
{
    private string directory = null!;
    [TestInitialize]
    public void Initialize() => directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup]
    public void Cleanup()
    {
        var allowed = Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(directory).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        Directory.Delete(directory, true);
    }

    [TestMethod]
    public async Task NestedPackageUsesItsDirectoryAndDeclaredManager()
    {
        var nested = Directory.CreateDirectory(Path.Combine(directory, "frontend")).FullName;
        var file = Path.Combine(nested, "package.json");
        await File.WriteAllTextAsync(file, """{"packageManager":"pnpm@10.0.0","scripts":{"dev":"vite","test:unit":"vitest run"}}""");
        var results = await PackageScriptImporter.ReadAsync(file, directory);
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("pnpm run 'dev'", results[0].Script.Command);
        Assert.AreEqual("frontend", results[0].Script.WorkingDirectory);
        Assert.AreEqual("vitest run", results[1].Description);
    }

    [DataTestMethod]
    [DataRow("pnpm-lock.yaml", "pnpm")]
    [DataRow("yarn.lock", "yarn")]
    [DataRow("bun.lock", "bun")]
    [DataRow("package-lock.json", "npm")]
    public async Task LockfileSelectsManager(string lockfile, string expected)
    {
        var file = Path.Combine(directory, "package.json");
        await File.WriteAllTextAsync(file, """{"scripts":{"build":"build command"}}""");
        await File.WriteAllTextAsync(Path.Combine(directory, lockfile), "");
        var result = (await PackageScriptImporter.ReadAsync(file, directory)).Single();
        Assert.AreEqual(expected + " run 'build'", result.Script.Command);
        Assert.AreEqual("", result.Script.WorkingDirectory);
    }

    [TestMethod]
    public async Task DuplicateImportsAreSkippedAndNamesNeverOverwriteManualScripts()
    {
        var file = Path.Combine(directory, "package.json");
        await File.WriteAllTextAsync(file, """{"scripts":{"dev":"vite"}}""");
        var imported = (await PackageScriptImporter.ReadAsync(file, directory)).Single().Script;
        var manual = new ProjectScript(Guid.NewGuid(), "dev", "Write-Output manual");
        var first = PackageScriptImporter.Merge(directory, [manual], [imported]);
        Assert.AreEqual(2, first.Count);
        Assert.AreEqual(manual, first[0]);
        Assert.AreEqual("dev (2)", first[1].Name);
        var repeated = PackageScriptImporter.Merge(directory, first, [imported]);
        Assert.AreEqual(2, repeated.Count);
        Assert.AreEqual(first[1].Id, repeated[1].Id);
    }

    [DataTestMethod]
    [DataRow("{\"scripts\":[]}")]
    [DataRow("{\"scripts\":{\"dev\":1}}")]
    [DataRow("{\"scripts\":{\"dev;evil\":\"a\"}}")]
    [DataRow("{\"scripts\":{\"--help\":\"a\"}}")]
    [DataRow("{\"scripts\":{\"dev\":\"a\",\"dev\":\"b\"}}")]
    public async Task InvalidEntriesDoNotProduceImportCandidates(string json)
    {
        var file = Path.Combine(directory, "package.json");
        await File.WriteAllTextAsync(file, json);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => PackageScriptImporter.ReadAsync(file, directory));
    }

    [TestMethod]
    public async Task MissingScriptsAndOversizedFilesAreHandled()
    {
        var file = Path.Combine(directory, "package.json");
        await File.WriteAllTextAsync(file, "{}");
        Assert.AreEqual(0, (await PackageScriptImporter.ReadAsync(file, directory)).Count);
        await File.WriteAllTextAsync(file, new string(' ', 1024 * 1024 + 1));
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => PackageScriptImporter.ReadAsync(file, directory));
    }
}
