using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class AgentProcessTreeTests
{
    private static readonly DateTime Started = new(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void WindowsSnapshotReadsCurrentProcessWithoutIncludingItAsAChild()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var root = new ProcessIdentity(process.Id, process.StartTime.ToUniversalTime());
        var rows = new Services.Pi.AgentProcessReader().Read(root, new HashSet<ProcessIdentity>());
        Assert.IsFalse(rows.Any(row => row.Identity.Id == root.Id));
        Assert.IsTrue(rows.All(row => row.Identity.StartedUtc >= root.StartedUtc));
    }

    [TestMethod]
    public void IncludesNestedProcessesButNotOtherConversationsOrReusedParentIds()
    {
        var root = new ProcessIdentity(10, Started);
        AgentProcess[] snapshot =
        [
            new(new(12, Started.AddSeconds(2)), 11, "node.exe"),
            new(new(11, Started.AddSeconds(1)), 10, "bash.exe"),
            new(new(20, Started.AddSeconds(1)), 99, "other.exe"),
            new(new(21, Started.AddSeconds(-1)), 10, "older.exe")
        ];
        var rows = AgentProcessTree.Select(root, snapshot, new HashSet<ProcessIdentity>());
        CollectionAssert.AreEquivalent(new[] { 11, 12 }, rows.Select(row => row.Identity.Id).ToArray());
    }

    [TestMethod]
    public void RetainsObservedServerAfterLauncherExitsButRejectsReusedServerPid()
    {
        var root = new ProcessIdentity(10, Started);
        var server = new ProcessIdentity(12, Started.AddSeconds(2));
        var known = new HashSet<ProcessIdentity> { server };
        AgentProcess[] snapshot = [new(server, 11, "node.exe")];
        Assert.AreEqual(1, AgentProcessTree.Select(root, snapshot, known).Count);
        snapshot = [new(new(12, Started.AddSeconds(5)), 99, "unrelated.exe")];
        Assert.AreEqual(0, AgentProcessTree.Select(root, snapshot, known).Count);
        Assert.AreEqual(0, AgentProcessTree.Select(root, [], known).Count);
    }
}
