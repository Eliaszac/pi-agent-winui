using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Settings;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class StorageOverviewTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pi-storage-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private async Task WriteAsync(string relative, int length)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, new byte[length]);
    }

    [TestMethod]
    public async Task CountsSeparateCategoriesWithoutDoubleCountingArtifacts()
    {
        await WriteAsync("app/sessions/project/chat.jsonl", 10);
        await WriteAsync("app/sessions/project/chat.jsonl.artifacts/id/version/report.txt", 20);
        await WriteAsync("app/research/task.jsonl", 30);
        await WriteAsync("checkpoints/data", 40);
        await WriteAsync("app/diagnostics/last-ui-crash.txt", 50);
        await WriteAsync("app/auth.json", 100);
        var service = new StorageOverviewService(Path.Combine(root, "app"), Path.Combine(root, "checkpoints"));
        var result = await service.ReadAsync();
        CollectionAssert.AreEqual(new long[] { 10, 20, 30, 40, 50 }, result.Select(item => item.Bytes).ToArray());
        Assert.IsFalse(result.Any(item => item.Incomplete));
    }

    [TestMethod]
    public async Task DiagnosticCleanupOnlyDeletesKnownReportAndCanBeRepeated()
    {
        await WriteAsync("app/diagnostics/last-ui-crash.txt", 10);
        await WriteAsync("app/diagnostics/other.txt", 20);
        await WriteAsync("app/sessions/chat.jsonl", 30);
        var service = new StorageOverviewService(Path.Combine(root, "app"), Path.Combine(root, "checkpoints"));
        await service.ClearDiagnosticsAsync();
        await service.ClearDiagnosticsAsync();
        Assert.IsFalse(File.Exists(Path.Combine(root, "app/diagnostics/last-ui-crash.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "app/diagnostics/other.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "app/sessions/chat.jsonl")));
    }

    [TestMethod]
    public async Task MissingStorageHasZeroTotalsAndCleanupIsHarmless()
    {
        var service = new StorageOverviewService(Path.Combine(root, "app"), Path.Combine(root, "checkpoints"));
        await service.ClearDiagnosticsAsync();
        Assert.IsTrue((await service.ReadAsync()).All(item => item.Bytes == 0 && !item.Incomplete));
    }
}
