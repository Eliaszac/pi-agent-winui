using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Docker;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Docker;
using PiAgentGui.Services.Projects;
using PiAgentGui.Tests.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Docker;

namespace PiAgentGui.Tests.Docker;

[TestClass]
public sealed class DockerTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PiDockerTests-" + Guid.NewGuid().ToString("N"));
    private static DockerSource Source(string kind = "local") => new(Guid.NewGuid(), new ExecutionTarget
        { Id = Guid.NewGuid(), Name = "Test", Kind = kind, Host = kind == "local" ? "" : "test-host", Path = kind == "local" ? @"C:\test" : "/tmp" });
    private static DockerContainer Container(DockerSource source, char id = 'a', string state = "exited") => new(source.Id, new string(id, 64), "db", "postgres:17", state, state);
    private DockerPreferenceStore Store => new(Path.Combine(directory, "docker.json"));
    private DockerPanelViewModel Model(FakeDockerClient client, params Project[] projects) => new(Store, client,
        new(new InMemoryProjectRepository(projects), new WslDistributionCache(() => Task.FromResult(new WslDistributionSnapshot([], null))), client));

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    public void ParsesJsonLinesWithoutSplittingNamesAndPreservesStates()
    {
        var id = new string('a', 64);
        var rows = DockerOutput.Parse(Guid.NewGuid(), $"{{\"ID\":\"{id}\",\"Names\":\"db name\",\"Image\":\"postgres:17\",\"State\":\"running\",\"Status\":\"Up 2 hours (healthy)\"}}\r\n");
        Assert.AreEqual("db name", rows.Single().Name);
        Assert.IsTrue(rows[0].CanStop);
        Assert.IsFalse(rows[0].CanStart);
        Assert.AreEqual("Up 2 hours (healthy)", rows[0].Status);
        Assert.AreEqual(0, DockerOutput.Parse(Guid.NewGuid(), "\n").Count);
    }

    [TestMethod]
    public async Task RejectsMalformedIdsBeforeStartingAnyProcess()
    {
        foreach (var id in new[] { "", "--help", "abc; touch /tmp/x", new string('z', 64), "abcdef" })
        {
            Assert.IsFalse(DockerOutput.IsContainerId(id));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => new DockerCliClient().SetRunningAsync(Source(), id, true, default));
        }
        Assert.ThrowsException<IOException>(() => DockerOutput.Parse(Guid.NewGuid(), "{\"ID\":\"--help\"}"));
    }

    [TestMethod]
    public void LaunchesOnTheSelectedSourceWithoutLocalFallback()
    {
        var local = DockerCliClient.CreateStartInfo(Source(), ["container", "ls", "--format", "{{json .}}"]);
        Assert.IsTrue(local.FileName.EndsWith("docker.exe", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("{{json .}}", local.ArgumentList.Last());
        Assert.IsTrue(local.CreateNoWindow);
        var wsl = DockerCliClient.CreateStartInfo(Source("wsl"), ["container", "ls"]);
        Assert.AreEqual("wsl.exe", wsl.FileName);
        Assert.IsTrue(wsl.ArgumentList.Contains("test-host"));
        var ssh = DockerCliClient.CreateStartInfo(Source("ssh"), ["container", "ls"]);
        Assert.AreEqual("ssh.exe", ssh.FileName);
        Assert.IsTrue(ssh.ArgumentList.Contains("test-host"));
        Assert.IsFalse(ssh.UseShellExecute);
    }

    [TestMethod]
    public void UnlinkedContainersAppearEverywhereAndLinkedOnesAreScoped()
    {
        var source = Source(); var row = Container(source); var project = Guid.NewGuid();
        var links = new Dictionary<string, Guid[]>();
        Assert.IsTrue(DockerOutput.Visible(row, Guid.NewGuid(), links));
        links[row.Id] = [project];
        Assert.IsTrue(DockerOutput.Visible(row, project, links));
        Assert.IsFalse(DockerOutput.Visible(row, Guid.NewGuid(), links));
        Assert.IsFalse(DockerOutput.Visible(row, null, links));
        Assert.IsFalse(DockerOutput.Visible(Container(Source()), Guid.NewGuid(), links));
        Assert.IsTrue(DockerOutput.Visible(Container(Source(), 'b'), Guid.NewGuid(), links));
    }

    [TestMethod]
    public async Task LinksSurviveReloadAndFilterOnProjectSwitch()
    {
        var source = Source(); var row = Container(source); var project = Guid.NewGuid();
        await Store.WriteAsync(new(true, [source], new Dictionary<string, Guid[]>()));
        var client = new FakeDockerClient(); client.Rows[source.Id] = [row];
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        await model.LinkAsync(row, [project]);
        model.SelectProject(Guid.NewGuid()); Assert.AreEqual(0, model.VisibleContainers.Count);
        model.SelectProject(project); Assert.AreEqual(1, model.VisibleContainers.Count);
        Assert.AreEqual(project, (await Store.ReadAsync()).Links[row.Id].Single());
        await model.LinkAsync(row, []);
        model.SelectProject(Guid.NewGuid()); Assert.AreEqual(1, model.VisibleContainers.Count);
    }

    [TestMethod]
    public async Task FailedSourceDoesNotHideHealthySourceOrAllowStaleActions()
    {
        var source = Source(); var other = Source("ssh");
        await Store.WriteAsync(new(true, [source, other], new Dictionary<string, Guid[]>()));
        var client = new FakeDockerClient(); client.Rows[source.Id] = [Container(source)]; client.Rows[other.Id] = [Container(other)];
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        Assert.AreEqual(2, model.Containers.Count);
        var stale = model.Containers.Single(row => row.Container.SourceId == other.Id);
        client.Failures.Add(other.Id); await model.RefreshAsync();
        Assert.AreEqual(1, model.Containers.Count);
        StringAssert.Contains(model.Sources.Single(row => row.Source.Id == other.Id).Status, "Cannot reach Docker engine");
        await stale.StartCommand.ExecuteAsync(); Assert.AreEqual(0, client.Actions.Count);
    }

    [TestMethod]
    public async Task DisablingCancelsReadWithoutStoppingContainersOrLosingConfiguration()
    {
        var source = Source();
        await Store.WriteAsync(new(true, [source], new Dictionary<string, Guid[]>()));
        var pending = new TaskCompletionSource<IReadOnlyList<DockerContainer>>();
        var client = new FakeDockerClient { ListHandler = (_, _) => pending.Task };
        using var model = Model(client); await model.InitializeAsync(); model.IsOpen = true;
        var read = model.RefreshAsync();
        await model.SetEnabledAsync(false);
        pending.SetResult([Container(source)]); await read;
        Assert.IsFalse(model.Enabled); Assert.IsFalse(model.IsOpen);
        Assert.AreEqual(0, model.Containers.Count); Assert.AreEqual(0, client.Actions.Count);
        Assert.AreEqual(1, (await Store.ReadAsync()).Sources.Count);
    }

    [TestMethod]
    public async Task StartFailureIsVisibleAndNeverAutomaticallyRetried()
    {
        var source = Source();
        await Store.WriteAsync(new(true, [source], new Dictionary<string, Guid[]>()));
        var client = new FakeDockerClient { ActionHandler = () => throw new IOException("Port already in use") };
        client.Rows[source.Id] = [Container(source)];
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        await model.Containers.Single().StartCommand.ExecuteAsync();
        StringAssert.Contains(model.Message, "Port already in use");
        Assert.AreEqual(1, client.Actions.Count);
        Assert.IsFalse(model.Busy);
    }

    [TestMethod]
    public async Task SourceRemovalDeletesOnlyItsLinksAndNeverContainers()
    {
        var source = Source(); var row = Container(source); var other = Source("ssh"); var otherRow = Container(other, 'b');
        var project = new Project { Id = Guid.NewGuid(), Name = "Test", Path = @"C:\test" };
        await Store.WriteAsync(new(true, [source, other], new Dictionary<string, Guid[]> { [row.Id] = [project.Id], [otherRow.Id] = [project.Id] }));
        var client = new FakeDockerClient(); client.Rows[other.Id] = [otherRow];
        using var model = Model(client, project); await model.InitializeAsync(); await model.RemoveSourceAsync(source);
        var saved = await Store.ReadAsync();
        Assert.AreEqual(other.Id, saved.Sources.Single().Id);
        Assert.IsFalse(saved.Links.ContainsKey(row.Id)); Assert.IsTrue(saved.Links.ContainsKey(otherRow.Id));
        Assert.AreEqual(0, client.Actions.Count);
    }

    [TestMethod]
    public async Task PrunesMissingContainerLinksOnlyAfterSuccessfulInventory()
    {
        var source = Source(); var unavailable = Source("ssh"); var row = Container(source); var other = Container(unavailable, 'b');
        await Store.WriteAsync(new(true, [source, unavailable], new Dictionary<string, Guid[]> { [row.Id] = [Guid.NewGuid()], [other.Id] = [Guid.NewGuid()] }));
        var client = new FakeDockerClient(); client.Failures.Add(unavailable.Id);
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        var saved = await Store.ReadAsync();
        Assert.IsTrue(saved.Links.ContainsKey(row.Id)); Assert.IsTrue(saved.Links.ContainsKey(other.Id));
        client.Failures.Clear(); client.Rows[unavailable.Id] = [other]; await model.RefreshAsync();
        saved = await Store.ReadAsync();
        Assert.IsFalse(saved.Links.ContainsKey(row.Id)); Assert.IsTrue(saved.Links.ContainsKey(other.Id));
    }

    [TestMethod]
    public async Task DetectsLocalAndWslButDoesNotConnectSavedSshAutomatically()
    {
        var ssh = Source("ssh");
        var project = new Project { Id = Guid.NewGuid(), Name = "Remote", Path = @"C:\test", Targets = [ssh.Target] };
        var client = new FakeDockerClient();
        var discovery = new DockerSourceDiscovery(new InMemoryProjectRepository(project),
            new WslDistributionCache(() => Task.FromResult(new WslDistributionSnapshot(["Ubuntu", "docker-desktop"], "Ubuntu"))), client);
        var found = await discovery.DetectAsync(default);
        Assert.AreEqual(2, found.Count); Assert.IsTrue(found.All(source => source.Target.Kind != "ssh"));
        Assert.AreEqual(1, (await discovery.SshAsync()).Count);
        Assert.AreEqual(0, client.Lists);
    }

    [TestMethod]
    public async Task InvalidSettingsAreNotSilentlyOverwritten()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "docker.json"); await File.WriteAllTextAsync(path, "broken");
        using var model = Model(new FakeDockerClient()); await model.InitializeAsync();
        await Assert.ThrowsExceptionAsync<IOException>(() => model.SetEnabledAsync(true));
        Assert.AreEqual("broken", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task SharedEngineContainersAppearOnceAndLinksApplyThroughEverySource()
    {
        var source = Source(); var wsl = Source("wsl"); var project = Guid.NewGuid();
        await Store.WriteAsync(new(true, [source, wsl], new Dictionary<string, Guid[]>()));
        var client = new FakeDockerClient(); client.Rows[source.Id] = [Container(source)]; client.Rows[wsl.Id] = [Container(wsl)];
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        Assert.AreEqual(1, model.VisibleContainers.Count);
        await model.LinkAsync(Container(source), [project]); model.SelectProject(Guid.NewGuid());
        Assert.AreEqual(0, model.VisibleContainers.Count);
        client.Failures.Add(source.Id); await model.RefreshAsync(); model.SelectProject(project);
        Assert.AreEqual(wsl.Id, model.VisibleContainers.Single().Container.SourceId);
    }

    [TestMethod]
    public async Task DuplicateClicksDoNotRepeatContainerActions()
    {
        var source = Source(); await Store.WriteAsync(new(true, [source], new Dictionary<string, Guid[]>()));
        var pending = new TaskCompletionSource();
        var client = new FakeDockerClient { ActionHandler = () => pending.Task }; client.Rows[source.Id] = [Container(source)];
        using var model = Model(client); await model.InitializeAsync(); await model.RefreshAsync();
        var row = model.Containers.Single(); var action = row.StartCommand.ExecuteAsync();
        Assert.IsFalse(row.CanStart); Assert.IsTrue(row.Busy);
        await row.StartCommand.ExecuteAsync(); Assert.AreEqual(1, client.Actions.Count);
        pending.SetResult(); await action;
    }
}
