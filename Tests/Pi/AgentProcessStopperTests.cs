using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class AgentProcessStopperTests
{
    [TestMethod]
    public void StopsOnlySelectedTreeAndLeavesAgentAndSiblingAlone()
    {
        var start = DateTime.UtcNow;
        var root = new ProcessIdentity(10, start);
        var target = new ProcessIdentity(11, start.AddSeconds(1));
        var child = new ProcessIdentity(12, start.AddSeconds(2));
        AgentProcess[] rows = [new(target, 10, "shell"), new(child, 11, "server"), new(new(13, start.AddSeconds(3)), 10, "sibling")];
        var stopped = new List<ProcessIdentity>();
        var service = new AgentProcessStopper((_, _) => rows, stopped.Add);
        service.Stop(root, target, new HashSet<ProcessIdentity> { target });
        CollectionAssert.AreEqual(new[] { child, target }, stopped);
    }

    [TestMethod]
    public void RejectsAgentAndUnknownTargetsAndIgnoresExitedOrReusedPid()
    {
        var start = DateTime.UtcNow;
        var root = new ProcessIdentity(10, start);
        var target = new ProcessIdentity(11, start.AddSeconds(1));
        var stopped = new List<ProcessIdentity>();
        var service = new AgentProcessStopper((_, _) => [new(new(11, start.AddSeconds(3)), 10, "replacement")], stopped.Add);
        Assert.ThrowsException<InvalidOperationException>(() => service.Stop(root, root, new HashSet<ProcessIdentity> { root }));
        Assert.ThrowsException<InvalidOperationException>(() => service.Stop(root, target, new HashSet<ProcessIdentity>()));
        service.Stop(root, target, new HashSet<ProcessIdentity> { target });
        Assert.AreEqual(0, stopped.Count);
    }

    [TestMethod]
    public void ReportsPartialFailureAndStillAttemptsParent()
    {
        var start = DateTime.UtcNow;
        var root = new ProcessIdentity(10, start);
        var target = new ProcessIdentity(11, start.AddSeconds(1));
        var child = new ProcessIdentity(12, start.AddSeconds(2));
        var attempted = new List<ProcessIdentity>();
        var service = new AgentProcessStopper((_, _) => [new(target, 10, "shell"), new(child, 11, "server")], identity =>
        {
            attempted.Add(identity);
            if (identity == child) throw new System.ComponentModel.Win32Exception(5);
        });
        Assert.ThrowsException<IOException>(() => service.Stop(root, target, new HashSet<ProcessIdentity> { target }));
        CollectionAssert.AreEqual(new[] { child, target }, attempted);
    }
}
