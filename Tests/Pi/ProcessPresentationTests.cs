using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ProcessPresentationTests
{
    [TestMethod]
    public void ConsoleHostHasAnExplanatoryHelperIdentity()
    {
        var role = ProcessPresentation.ForExecutable("CONHOST.EXE");
        Assert.AreEqual("Windows Console Host", role.Title);
        Assert.IsTrue(role.IsWindowsHelper);
        StringAssert.Contains(role.Description, "console applications");
    }

    [TestMethod]
    public void UnknownExecutableKeepsItsNameWithoutGuessingItsTask()
    {
        var role = ProcessPresentation.ForExecutable("custom-worker.exe");
        Assert.AreEqual("custom-worker.exe", role.Title);
        Assert.IsFalse(role.IsWindowsHelper);
        StringAssert.Contains(role.Description, "specific task is unavailable");
    }

    [TestMethod]
    public void RuntimeIsNotMisrepresentedAsASpecificServerOrExtension()
    {
        var role = ProcessPresentation.ForExecutable("node.exe");
        Assert.AreEqual("Node.js", role.Title);
        Assert.IsFalse(role.IsWindowsHelper);
        Assert.AreEqual("Subprocess", role.Category);
    }

    [TestMethod]
    public void RefreshUpdatesDurationWithoutResettingExpandedDetails()
    {
        var process = new AgentProcess(new(42, DateTime.UtcNow.AddMinutes(-2)), 10, "conhost.exe");
        process.IsExpanded = true;
        var changes = new List<string?>();
        process.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        process.RefreshElapsed();
        Assert.IsTrue(process.IsExpanded);
        CollectionAssert.Contains(changes, nameof(AgentProcess.Details));
        StringAssert.StartsWith(process.Details, "Running for 2m");
        Assert.AreEqual("Process ID: 42", process.ProcessId);
        Assert.AreEqual("Parent process ID: 10", process.ParentProcessId);
    }

    [TestMethod]
    public void FutureStartTimeDoesNotProduceNegativeDuration()
    {
        var process = new AgentProcess(new(42, DateTime.UtcNow.AddMinutes(1)), 10, "test.exe");
        Assert.AreEqual("Running for 0s", process.Details);
    }
}
