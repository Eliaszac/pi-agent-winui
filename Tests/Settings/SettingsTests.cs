using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Settings;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Settings;
using PiAgentGui.Tests.Projects;
using PiAgentGui.ViewModels.Settings;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    public async Task IntegrationsIsExclusiveAndSurvivesCatalogReload()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository());
        await shell.LoadAsync();
        shell.OpenIntegrations();
        Assert.IsTrue(shell.ShowIntegrations);
        Assert.IsFalse(shell.ShowHome || shell.ShowSettings || shell.ShowWorkspace || shell.ShowProviders || shell.ShowExtensions);
        await shell.LoadAsync();
        Assert.IsTrue(shell.ShowIntegrations);
        shell.OpenSettings(); Assert.IsFalse(shell.ShowIntegrations);
        shell.OpenIntegrations(); shell.OpenHome(); Assert.IsFalse(shell.ShowIntegrations);
        shell.OpenIntegrations(); shell.CloseExtensions(); Assert.IsFalse(shell.ShowIntegrations);
    }
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PiSettingsTests-" + Guid.NewGuid().ToString("N"));
    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    public async Task PreferencesRoundTripAndDefaultsAreLocalOnly()
    {
        var path = Path.Combine(directory, "settings.json");
        var store = new AppSettingsStore(path);
        await store.LoadAsync();
        Assert.AreEqual(new AppPreferences(), store.Current);
        var expected = new AppPreferences(true, false, DateTimeOffset.UtcNow);
        await store.SaveAsync(expected);
        var reloaded = new AppSettingsStore(path);
        await reloaded.LoadAsync();
        Assert.AreEqual(expected, reloaded.Current);
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public async Task FailedSaveKeepsPreviousPreferencesAndShowsError()
    {
        Directory.CreateDirectory(directory);
        var invalidPath = Path.Combine(directory, "folder");
        Directory.CreateDirectory(invalidPath);
        var store = new AppSettingsStore(invalidPath);
        var model = new SettingsViewModel(store);
        await model.SetUsageAsync(false);
        Assert.IsTrue(model.HasMessage);
        Assert.IsTrue(model.ShowLocalUsage);
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    public void SearchExpandsMatchesAndRestoresPreviousExpansion()
    {
        var model = new SettingsViewModel(new AppSettingsStore(Path.Combine(directory, "settings.json")));
        model.Data.Expanded = false;
        model.Query = "delete screenshots";
        Assert.IsTrue(model.Data.Visible); Assert.IsTrue(model.Data.Expanded);
        Assert.IsFalse(model.General.Visible);
        model.Query = "no such preference";
        Assert.IsTrue(model.NoResults);
        model.Query = "";
        Assert.IsFalse(model.Data.Expanded);
        Assert.IsTrue(model.Categories.All(category => category.Visible));
    }

    [TestMethod]
    public async Task ResumeChoosesMostRecentAcrossProjectsAndSettingsPreservesSelection()
    {
        var old = new ConversationDraft { Id = Guid.NewGuid(), Title = "Old", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
        var recent = old with { Id = Guid.NewGuid(), Title = "Recent", LastUsedAt = DateTimeOffset.UtcNow };
        var repository = new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "A", Path = @"C:\A", Conversations = [recent] },
            new Project { Id = Guid.NewGuid(), Name = "B", Path = @"C:\B", Conversations = [old] });
        var shell = new ShellViewModel(repository) { ResumeConversationOnStartup = true };
        await shell.LoadAsync();
        Assert.AreEqual("Recent", shell.SelectedConversation?.Title);
        Assert.IsTrue(shell.ShowWorkspace);
        shell.OpenSettings(true);
        Assert.IsTrue(shell.ShowLegal); Assert.IsFalse(shell.ShowWorkspace); Assert.IsFalse(shell.ShowHome);
        await shell.LoadAsync();
        Assert.IsTrue(shell.ShowLegal);
        Assert.AreEqual("Recent", shell.SelectedConversation?.Title);
        shell.OpenHome();
        Assert.IsFalse(shell.ShowLegal); Assert.IsTrue(shell.ShowHome);
    }

    [TestMethod]
    public async Task BulkDeletionOnlyRemovesCatalogRecordsAndCanKeepProjects()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.txt");
        await File.WriteAllTextAsync(source, "keep me");
        var repository = new InMemoryProjectRepository(new Project
        {
            Id = Guid.NewGuid(), Name = "Source", Path = directory,
            Conversations = [new() { Id = Guid.NewGuid(), Title = "Draft", CreatedAt = DateTimeOffset.UtcNow }]
        });
        var shell = new ShellViewModel(repository);
        await shell.LoadAsync(); shell.OpenSettings();
        await shell.ClearCatalogAsync(false);
        Assert.AreEqual(1, shell.Projects.Count);
        Assert.AreEqual(0, shell.Projects[0].Conversations.Count);
        Assert.AreEqual(0, (await repository.GetAllAsync())[0].Conversations.Count);
        await shell.ClearCatalogAsync(true);
        Assert.AreEqual(0, shell.Projects.Count);
        Assert.AreEqual("keep me", await File.ReadAllTextAsync(source));
    }

    [TestMethod]
    public async Task ActiveConversationPreventsBulkDeletionBeforeCatalogChanges()
    {
        var draft = new ConversationDraft { Id = Guid.NewGuid(), Title = "Running", CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Path = directory, Conversations = [draft] };
        var repository = new InMemoryProjectRepository(project);
        var session = new Pi.FakeConversationSession();
        var dispatcher = new Pi.QueuedUiDispatcher();
        await using var workspaces = new Services.Conversations.ConversationWorkspaceStore((_, _) => session, dispatcher);
        workspaces.GetOrCreate(project, draft);
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        var shell = new ShellViewModel(repository, workspaces);
        await shell.LoadAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => shell.ClearCatalogAsync(true));
        Assert.AreEqual(1, (await repository.GetAllAsync()).Count);
        Assert.AreEqual(1, shell.Projects[0].Conversations.Count);
        Assert.IsTrue(shell.CanManageSidebar);
    }
}
