using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiLaunchTests
{
    [TestMethod]
    public void PathsWithSpacesAndShellCharactersRemainIndividualArguments()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Pi launch test & " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var executable = Path.Combine(folder, "pi.exe");
            File.WriteAllText(executable, "test placeholder, never executed");
            var session = Path.Combine(folder, "session & example.jsonl");
            var info = new PiProcessStartInfoFactory(new PiRuntimeOptions { ExecutablePath = executable }).Create(new(folder, session));
            Assert.AreEqual(executable, info.FileName);
            Assert.AreEqual(folder, info.WorkingDirectory);
            Assert.IsFalse(info.UseShellExecute);
            Assert.IsTrue(info.CreateNoWindow);
            Assert.IsTrue(info.RedirectStandardInput && info.RedirectStandardOutput && info.RedirectStandardError);
            CollectionAssert.AreEqual(new[] { "--mode", "rpc", "--extension", Path.Combine(AppContext.BaseDirectory, "PiExtensions", "write-diff.ts"), "--session", session, "--session-dir", folder }, info.ArgumentList.ToArray());
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [TestMethod]
    public void MissingProjectFolderFailsBeforeProcessLaunch()
    {
        var factory = new PiProcessStartInfoFactory(new PiRuntimeOptions());
        Assert.ThrowsException<DirectoryNotFoundException>(() => factory.Create(new PiLaunchRequest(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Path.Combine(Path.GetTempPath(), "unused.jsonl"))));
    }
}
