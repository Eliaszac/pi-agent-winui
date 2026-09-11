using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Tests.Terminal;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ProjectScriptsTests
{
    [TestMethod]
    public async Task SaveRunReopenAndDeleteKeepProjectSettingsIndependent()
    {
        var first = new Project { Id = Guid.NewGuid(), Name = "First", Path = Path.GetTempPath() };
        var second = first with { Id = Guid.NewGuid(), Name = "Second" };
        var repository = new InMemoryProjectRepository(first, second);
        var launches = new List<(string Directory, string Command)>();
        await using var terminals = new TerminalPanelViewModel(_ => new FakeTerminalSession(), (directory, command) =>
        { launches.Add((directory, command)); return new FakeTerminalSession(); });
        var model = new ProjectScriptsViewModel(repository, terminals);
        await model.SelectAsync(first.Id, first.Path);
        await model.SaveAsync(null, "Tests", "dotnet test", "");
        await model.SaveAsync(null, "Lint", "npm run lint", "");
        var lint = model.Scripts.Last();
        Assert.AreEqual(0, launches.Count, "Saving never executes scripts.");
        await model.RunAsync(lint.Id);
        Assert.AreEqual("npm run lint", launches.Single().Command);
        var reopen = new ProjectScriptsViewModel(repository, terminals);
        await reopen.SelectAsync(first.Id, first.Path);
        Assert.AreEqual("Lint", reopen.Label);
        await reopen.RunAsync();
        Assert.AreEqual(1, launches.Count, "The active script is focused across conversations.");
        await reopen.SelectAsync(second.Id, second.Path);
        Assert.AreEqual(0, reopen.Scripts.Count);
        await reopen.SaveAsync(null, "Second", "Write-Output second", "");
        await reopen.RunAsync();
        Assert.AreEqual(2, launches.Count);
        await model.DeleteAsync(lint.Id);
        Assert.AreEqual("Tests", model.Label);
        Assert.AreEqual(2, terminals.Tabs.Count, "Deleting configuration does not stop a running command.");
    }

    [TestMethod]
    public async Task ActiveRunIsFocusedButFinishedOrClosedRunCanBeStartedAgain()
    {
        var sessions = new List<FakeTerminalSession>();
        await using var panel = new TerminalPanelViewModel(_ => new FakeTerminalSession(), (_, _) =>
        { var session = new FakeTerminalSession(); sessions.Add(session); return session; });
        var project = Guid.NewGuid(); var script = Guid.NewGuid();
        panel.RunScript(project, script, "Server", Path.GetTempPath(), "command");
        var first = panel.Selected;
        panel.Hide(); panel.RunScript(project, script, "Server", Path.GetTempPath(), "command");
        Assert.IsTrue(panel.IsOpen); Assert.AreSame(first, panel.Selected); Assert.AreEqual(1, sessions.Count);
        sessions[0].Complete();
        panel.RunScript(project, script, "Server", Path.GetTempPath(), "command");
        Assert.AreEqual(2, sessions.Count); Assert.AreNotSame(first, panel.Selected);
        await panel.CloseAsync(panel.Selected!);
        panel.RunScript(project, script, "Server", Path.GetTempPath(), "command");
        Assert.AreEqual(3, sessions.Count);
        panel.RunScript(Guid.NewGuid(), script, "Server", Path.GetTempPath(), "command");
        Assert.AreEqual(4, sessions.Count, "Script identities are scoped by project.");
    }

    [TestMethod]
    public void MetadataPreservesUnrelatedFieldsAndRejectsInvalidCommands()
    {
        var metadata = JsonSerializer.SerializeToElement(new { unrelated = new { enabled = true } });
        var script = new ProjectScript(Guid.NewGuid(), "Name", "Write-Output 'hello'", "");
        var saved = ProjectScripts.Write(metadata, new() { Scripts = [script], SelectedId = script.Id });
        Assert.IsTrue(saved.GetProperty("unrelated").GetProperty("enabled").GetBoolean());
        Assert.AreEqual(script, ProjectScripts.Read(saved).Scripts.Single());
        Assert.ThrowsException<InvalidDataException>(() => ProjectScripts.Write(metadata, new() { Scripts = [script with { Command = " " }] }));
        Assert.ThrowsException<InvalidDataException>(() => ProjectScripts.Write(metadata, new() { Scripts = [script, script] }));
        Assert.ThrowsException<InvalidDataException>(() => ProjectScripts.Write(metadata, new() { Scripts = [script], SelectedId = Guid.NewGuid() }));
        Assert.AreEqual(Path.GetTempPath(), ProjectScripts.ResolveDirectory(Path.GetTempPath(), ""));
        Assert.ThrowsException<DirectoryNotFoundException>(() => ProjectScripts.ResolveDirectory(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    }

    [TestMethod]
    public async Task InvalidDirectoryDoesNotSaveOrLaunch()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Example", Path = Path.GetTempPath() };
        var repository = new InMemoryProjectRepository(project);
        await using var terminals = new TerminalPanelViewModel(_ => new FakeTerminalSession());
        var model = new ProjectScriptsViewModel(repository, terminals);
        await model.SelectAsync(project.Id, project.Path);
        await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => model.SaveAsync(null, "Test", "command", Guid.NewGuid().ToString("N")));
        Assert.AreEqual(0, model.Scripts.Count); Assert.AreEqual(0, terminals.Tabs.Count);
    }
}
