using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class SnippetTests
{
    [TestMethod]
    public async Task DismissClearsResultsAndAllowsAnotherRun()
    {
        var service = new FakeSnippetService { Run = (output, _) => { output("hello", false); return Task.FromResult(0); } };
        using var model = new SnippetViewModel(service, new QueuedUiDispatcher(), "python", "code", _ => { });
        await model.RunAsync();
        Assert.IsTrue(model.CanDismiss);
        model.DismissOutput();
        Assert.IsFalse(model.HasOutput);
        Assert.AreEqual("", model.Status);
        Assert.IsFalse(model.CanDismiss);
        await model.RunAsync();
        Assert.AreEqual("hello", model.Output);
        await model.SaveAsync();
        model.DismissOutput();
        Assert.AreEqual("", model.Status);
    }

    [TestMethod]
    public void InterpreterArgumentsAreDirectAndPowerShellUsesUtf8Output()
    {
        var folder = Path.Combine(Path.GetTempPath(), "snippet-launch-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "pwsh.exe"), "");
            var file = Path.Combine(folder, "a'b.ps1");
            var start = SnippetProcess.Create(folder, "powershell", file, [folder]);
            Assert.IsFalse(start.UseShellExecute);
            Assert.IsTrue(start.RedirectStandardInput);
            Assert.IsTrue(start.ArgumentList.Contains("-NonInteractive"));
            var invoke = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList.Last()));
            StringAssert.Contains(invoke, "UTF8Encoding");
            StringAssert.Contains(invoke, "a''b.ps1");
            Assert.ThrowsException<IOException>(() => SnippetProcess.Create(folder, "bash", "script.sh", [folder]));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void LanguagesAreExplicitAndGeneratedNamesCannotEscapeTheRoot()
    {
        Assert.IsTrue(SnippetLanguage.CanRun("pwsh title=test"));
        Assert.IsTrue(SnippetLanguage.CanRun("python3"));
        Assert.IsTrue(SnippetLanguage.CanRun("bash"));
        Assert.IsFalse(SnippetLanguage.CanRun("javascript"));
        Assert.AreEqual("snippet-2.py", SnippetLanguage.FileName("python", 2));
        Assert.AreEqual("snippet.txt", SnippetLanguage.FileName("../../bad", 1));
    }

    [TestMethod]
    public async Task SaveIsExactAndConcurrentSavesNeverOverwrite()
    {
        var folder = Path.Combine(Path.GetTempPath(), "snippet-save-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var service = new SnippetService(new ExecutionTarget { Id = Guid.NewGuid(), Name = "test", Path = folder });
            await File.WriteAllTextAsync(Path.Combine(folder, "snippet.py"), "existing");
            const string code = "print('héllo')\r\n# exact ending";
            var paths = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.SaveAsync("python", code, default)));
            Assert.AreEqual(3, paths.Distinct().Count());
            Assert.AreEqual("existing", await File.ReadAllTextAsync(Path.Combine(folder, "snippet.py")));
            foreach (var path in paths) Assert.AreEqual(code, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task RunKeepsOutputExitCodeAndRequiresExplicitDraftAction()
    {
        var service = new FakeSnippetService { Run = (output, _) => { output("hello\n", false); output("error\n", true); return Task.FromResult(7); } };
        string? draft = null;
        using var model = new SnippetViewModel(service, new QueuedUiDispatcher(), "python", "code", text => draft = text);
        await model.RunAsync();
        StringAssert.Contains(model.Status, "Exit 7");
        Assert.AreEqual("hello\nerror\n", model.Output);
        Assert.IsNull(draft);
        model.AddOutputToDraft();
        StringAssert.Contains(draft!, "WSL · test");
        await model.SaveAsync();
        StringAssert.Contains(model.Status, "Saved /project/snippet.py");
    }

    [TestMethod]
    public async Task StopCancelsWithoutLosingTheActiveOperationAndOutputIsBounded()
    {
        var service = new FakeSnippetService { Run = async (output, token) =>
        {
            output(new string('x', 200000), false);
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        } };
        using var model = new SnippetViewModel(service, new QueuedUiDispatcher(), "bash", "code", _ => { });
        var task = model.RunAsync();
        Assert.IsTrue(model.IsRunning);
        Assert.IsFalse(model.CanDismiss);
        model.DismissOutput();
        StringAssert.StartsWith(model.Status, "Running");
        Assert.AreSame(task, model.RunAsync());
        model.Stop();
        await task;
        Assert.IsFalse(model.IsRunning);
        StringAssert.StartsWith(model.Status, "Stopped");
        Assert.IsTrue(model.Output.Length < 132000);
        StringAssert.Contains(model.Output, "Earlier output omitted");
    }
}
