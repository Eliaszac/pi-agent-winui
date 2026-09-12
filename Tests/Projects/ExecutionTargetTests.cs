using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Projects;
using PiAgentGui.Models.Pi;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Pi;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ExecutionTargetTests
{
    [TestMethod]
    public async Task LegacyConversationsKeepLocalTargetWhenDefaultChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-targets-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new JsonProjectRepository(new() { CatalogPath = Path.Combine(directory, "projects.json") });
            var id = Guid.NewGuid();
            var old = new ConversationDraft { Id = Guid.NewGuid(), Title = "Before targets", CreatedAt = DateTimeOffset.UtcNow };
            await repository.AddAsync(new Project { Id = id, Name = "App", Path = directory, Conversations = [old] });
            var remote = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Build", Kind = "ssh", Host = "builder", Path = "/srv/app" };
            await repository.AddTargetAsync(id, remote, true);
            var draft = await repository.AddConversationAsync(id);
            var local = await repository.AddConversationAsync(id, targetId: id);
            var loaded = (await repository.GetAllAsync()).Single();
            Assert.AreEqual(id, ProjectTargets.Resolve(loaded, old.TargetId ?? id).Id);
            Assert.AreEqual(remote.Id, draft.TargetId);
            Assert.AreEqual(id, local.TargetId);
            Assert.AreEqual(remote.Id, loaded.DefaultTargetId);
            Assert.AreEqual(3, loaded.Conversations.Count);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "sessions")), "Catalog changes do not write Pi sessions.");
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task RemoteFirstCatalogRoundTripsCaseSensitiveTargetPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-targets-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new JsonProjectRepository(new() { CatalogPath = Path.Combine(directory, "projects.json") });
            foreach (var path in new[] { "/srv/App", "/srv/app" })
            {
                var id = Guid.NewGuid();
                var target = new ExecutionTarget { Id = id, Name = "Host", Kind = "ssh", Host = "builder", Path = path };
                await repository.AddAsync(new() { Id = id, Name = path, Path = path, Targets = [target], DefaultTargetId = id });
            }
            var loaded = await repository.GetAllAsync();
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("/srv/App", loaded[0].Path);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => repository.AddConversationAsync(loaded[0].Id, targetId: Guid.NewGuid()));
            Assert.AreEqual(0, (await repository.GetAllAsync())[0].Conversations.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void RemoteLaunchRetainsLocalSessionAndDisablesLocalBuiltinTools()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-launch-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var executable = Path.Combine(directory, "pi.exe"); File.WriteAllText(executable, "not executed");
            var target = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Ubuntu", Kind = "wsl", Host = "Ubuntu", Path = "/home/user/app" };
            var session = Path.Combine(directory, "session.jsonl");
            var info = new PiProcessStartInfoFactory(new PiRuntimeOptions { ExecutablePath = executable }).Create(new(target.Path, session, Target: target));
            Assert.IsTrue(info.WorkingDirectory.StartsWith(directory));
            Assert.AreNotEqual(target.Path, info.WorkingDirectory);
            Assert.IsTrue(info.ArgumentList.Contains(session));
            Assert.IsTrue(info.ArgumentList.Contains("--no-builtin-tools"));
            Assert.IsTrue(info.ArgumentList.Contains("--no-extensions"));
            Assert.IsTrue(info.ArgumentList.Any(arg => arg.EndsWith("execution-target.ts")));
            StringAssert.Contains(info.Environment["PI_GUI_EXECUTION_TARGET"]!, target.Path);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void HostAndPathValidationRejectOptionInjectionAndWrongPathDialect()
    {
        var target = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Host", Kind = "ssh", Host = "user@host", Path = "/srv/project's folder" };
        Assert.AreEqual(target.Path, ProjectTargets.Normalize(target).Path);
        Assert.ThrowsException<ArgumentException>(() => ProjectTargets.Normalize(target with { Host = "-oProxyCommand=cmd" }));
        Assert.ThrowsException<ArgumentException>(() => ProjectTargets.Normalize(target with { Path = @"C:\workspace" }));
        Assert.ThrowsException<ArgumentException>(() => ProjectTargets.Normalize(target with { Path = "/srv/../other" }));
        var start = TargetCommandRunner.CreateStartInfo(target, "echo '$HOME'");
        Assert.AreEqual("ssh.exe", start.FileName);
        Assert.IsTrue(start.ArgumentList.Contains("StrictHostKeyChecking=yes"));
        Assert.IsTrue(start.ArgumentList.Contains("BatchMode=yes"));
        Assert.AreEqual("user@host", start.ArgumentList[^2]);
        Assert.AreEqual("'a'\"'\"'b'", PosixShell.Quote("a'b"));
    }

    [TestMethod]
    public void ScriptSettingsAreSeparatedByTargetAndPreserveLegacyScripts()
    {
        var metadata = System.Text.Json.JsonSerializer.SerializeToElement(new { preserved = true });
        var localScript = new ProjectScript(Guid.NewGuid(), "Build", "dotnet build", "");
        var remoteScript = new ProjectScript(Guid.NewGuid(), "Build Linux", "make", "");
        var id = Guid.NewGuid();
        metadata = ProjectScripts.Write(metadata, new() { Scripts = [localScript] });
        metadata = ProjectScripts.Write(metadata, new() { Scripts = [remoteScript] }, id);
        Assert.AreEqual(localScript, ProjectScripts.Read(metadata).Scripts.Single());
        Assert.AreEqual(remoteScript, ProjectScripts.Read(metadata, id).Scripts.Single());
        Assert.IsTrue(metadata.GetProperty("preserved").GetBoolean());
        Assert.AreEqual(0, ProjectScripts.Read(metadata, Guid.NewGuid()).Scripts.Count);
    }
}
