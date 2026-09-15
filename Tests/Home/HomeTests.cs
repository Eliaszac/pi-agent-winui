using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Home;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Home;
using PiAgentGui.Tests.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Home;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Home;

[TestClass]
public sealed class HomeTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PiHomeTests-" + Guid.NewGuid().ToString("N"));
    private PiSessionPaths Paths => new(new ProjectStorageOptions { CatalogPath = Path.Combine(directory, "projects.json") });
    private static ConversationDraft Conversation(string title, int minutes = 0) => new() { Id = Guid.NewGuid(), Title = title, CreatedAt = DateTimeOffset.Now.AddMinutes(minutes) };
    private static Project Project(params ConversationDraft[] conversations) => new() { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\example", Conversations = conversations };
    private static string Entry(string id = "entry1", string model = "model-a", long tokens = 10) => JsonSerializer.Serialize(new
    {
        type = "message", id, timestamp = DateTimeOffset.Now.ToString("O"),
        message = new { role = "assistant", model, provider = "provider", timestamp = 1789214400000L,
            content = new[] { new { type = "text", text = "This content must not enter analytics" } },
            usage = new { input = tokens, output = 2, cacheRead = 3, cacheWrite = 4 }, stopReason = "stop" }
    });
    private async Task WriteAsync(Project project, ConversationDraft conversation, string data)
    {
        var path = Paths.GetSessionFile(project.Id, conversation.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, data);
    }
    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    public async Task UsageReaderCarriesEffortMetadataAndRefreshesAfterNewResponses()
    {
        var conversation = Conversation("Default learning"); var project = Project(conversation);
        var prefix = """{"type":"thinking_level_change","id":"effort","parentId":null,"thinkingLevel":"high"}""";
        var data = prefix + "\n" + Entry();
        await WriteAsync(project, conversation, data);
        var reader = new SessionUsageReader(Paths);
        var history = await reader.ReadAsync([project]);
        Assert.AreEqual("high", history.Samples.Single().Effort);
        await WriteAsync(project, conversation, data + "\n" + """{"type":"thinking_level_change","id":"effort2","thinkingLevel":"low"}""" + "\n" + Entry("new"));
        history = await reader.ReadAsync([project]);
        Assert.AreEqual(2, history.Samples.Count);
        Assert.AreEqual("low", history.Samples.Last().Effort);
    }

    [TestMethod]
    public async Task StartsOnHomeAndPreservesConversationWhenReturningHome()
    {
        var draft = Conversation("Existing"); var project = Project(draft);
        var shell = new ShellViewModel(new InMemoryProjectRepository(project));
        await shell.LoadAsync();
        Assert.IsTrue(shell.ShowHome); Assert.IsFalse(shell.ShowWorkspace);
        shell.Projects[0].Conversations[0].SelectCommand.Execute(null);
        Assert.IsFalse(shell.ShowHome); Assert.IsTrue(shell.HasConversation);
        var selected = shell.SelectedConversation;
        shell.OpenHome();
        Assert.AreSame(selected, shell.SelectedConversation); Assert.IsTrue(shell.ShowHome);
        shell.SelectProject(shell.Projects[0]);
        Assert.IsFalse(shell.HasConversation); Assert.IsTrue(shell.ShowWorkspace);
        Assert.AreEqual("Example", shell.WelcomeTitle);
    }

    [TestMethod]
    public async Task UsageResetExcludesOldResponsesWithoutEditingSessions()
    {
        var conversation = Conversation("Usage"); var project = Project(conversation);
        var original = Entry() + "\n";
        await WriteAsync(project, conversation, original);
        var settings = new Services.Settings.AppSettingsStore(Path.Combine(directory, "settings.json"));
        var shell = new ShellViewModel(new InMemoryProjectRepository(project));
        await shell.LoadAsync();
        using var home = new HomeViewModel(shell, new SessionUsageReader(Paths), settings);
        await home.RefreshAsync();
        Assert.AreEqual("1", home.ResponseTotal);
        await settings.SaveAsync(new(UsageResetAt: DateTimeOffset.UtcNow.AddMinutes(1)));
        await home.RefreshAsync();
        Assert.AreEqual("0", home.ResponseTotal);
        Assert.AreEqual(original, await File.ReadAllTextAsync(Paths.GetSessionFile(project.Id, conversation.Id)));
        await settings.SaveAsync(settings.Current with { ShowLocalUsage = false });
        await home.RefreshAsync();
        Assert.IsFalse(home.ShowLocalUsage);
        Assert.AreEqual("—", home.ResponseTotal);
    }

    [TestMethod]
    public async Task GlobalPagesAreExclusiveAndHomeSurvivesCatalogReload()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(Project())); await shell.LoadAsync();
        shell.OpenProviders(); Assert.IsFalse(shell.ShowHome); Assert.IsTrue(shell.ShowProviders);
        shell.OpenHome(); Assert.IsFalse(shell.ShowProviders); Assert.IsFalse(shell.ShowExtensions);
        await shell.LoadAsync(); Assert.IsTrue(shell.ShowHome);
    }

    [TestMethod]
    public void UsageParserReadsActualModelAndCacheTokensWithoutRetainingContent()
    {
        var row = SessionUsageParser.Parse(Entry(model: "actual-model"), Guid.NewGuid(), Guid.NewGuid(), 1)!;
        Assert.AreEqual("actual-model", row.Model); Assert.AreEqual(19L, row.Tokens);
        Assert.IsFalse(JsonSerializer.Serialize(row).Contains("This content"));
        Assert.IsNull(SessionUsageParser.Parse("{\"type\":\"message\",\"message\":{\"role\":\"user\"}}", Guid.NewGuid(), Guid.NewGuid(), 1));
    }

    [TestMethod]
    public void MissingAndInvalidTokenCountsRemainUnknown()
    {
        var row = SessionUsageParser.Parse(Entry(tokens: -1), Guid.NewGuid(), Guid.NewGuid(), 1)!;
        Assert.IsNull(row.Tokens);
        var missing = "{\"type\":\"message\",\"id\":\"x\",\"timestamp\":\"2026-09-12T12:00:00Z\",\"message\":{\"role\":\"assistant\"}}";
        Assert.IsNull(SessionUsageParser.Parse(missing, Guid.NewGuid(), Guid.NewGuid(), 1)!.Tokens);
    }

    [TestMethod]
    public async Task SharedForkHistoryIsCountedOnceAndSurvivesDeletionAndRestart()
    {
        var original = Conversation("Original", -2); var fork = Conversation("Fork", -1); var project = Project(original, fork);
        var shared = Entry();
        await WriteAsync(project, original, shared + "\n");
        await WriteAsync(project, fork, shared + "\n" + Entry("new-entry") + "\n");
        var reader = new SessionUsageReader(Paths);
        var inventory = await reader.ReadAsync([project]);
        Assert.AreEqual(2, inventory.Samples.Count);
        Assert.AreEqual(original.Id, inventory.Samples.First().ConversationId);
        var remaining = await reader.ReadAsync([project with { Conversations = [fork] }]);
        Assert.AreEqual(2, remaining.Samples.Count);
        var empty = await new SessionUsageReader(Paths).ReadAsync([]); Assert.AreEqual(2, empty.Samples.Count);
        Assert.AreEqual(original.Id, empty.Samples.First().ConversationId);
        Assert.IsFalse((await File.ReadAllTextAsync(Paths.UsageArchiveFile)).Contains("This content must not enter analytics"));
    }

    [TestMethod]
    public async Task ResetPhysicallyRemovesOldUsageAndDoesNotReimportIt()
    {
        var conversation = Conversation("Reset"); var project = Project(conversation);
        await WriteAsync(project, conversation, Entry());
        var reader = new SessionUsageReader(Paths);
        await reader.ReadAsync([project]);
        var settings = new Services.Settings.AppSettingsStore(Path.Combine(directory, "settings.json"));
        var model = new ViewModels.Settings.SettingsViewModel(settings, reader);
        await model.ResetUsageAsync();
        Assert.IsFalse(model.HasMessage);
        var archive = JsonSerializer.Deserialize<UsageArchive>(await File.ReadAllTextAsync(Paths.UsageArchiveFile))!;
        Assert.AreEqual(0, archive.Samples.Count);
        Assert.IsNotNull(archive.ResetAt);
        Assert.AreEqual(0, (await new SessionUsageReader(Paths).ReadAsync([project])).Samples.Count);
        Assert.IsTrue(File.Exists(Paths.GetSessionFile(project.Id, conversation.Id)));
    }

    [TestMethod]
    public async Task CleanupPreservesUsageWithoutAnyPriorHomeVisit()
    {
        var conversation = Conversation("Delete"); var project = Project(conversation);
        await WriteAsync(project, conversation, Entry());
        var repository = new InMemoryProjectRepository(project);
        var cleanup = new Services.Conversations.ConversationDataCleanup(Paths, repository, (_, _) => Task.FromResult(0), new SessionUsageReader(Paths));
        await cleanup.ScheduleAsync(project.Id, conversation.Id, ProjectTargets.All(project)[0]);
        await repository.DeleteConversationAsync(project.Id, conversation.Id);
        Assert.IsNull(await cleanup.RunPendingAsync());
        Assert.IsFalse(File.Exists(Paths.GetSessionFile(project.Id, conversation.Id)));
        Assert.AreEqual(1, (await new SessionUsageReader(Paths).ReadAsync([])).Samples.Count);
    }

    [TestMethod]
    public async Task ConcurrentReadersMergeWithoutDroppingUsage()
    {
        var first = Conversation("First"); var second = Conversation("Second");
        var project = Project(first, second);
        await WriteAsync(project, first, Entry("first"));
        await WriteAsync(project, second, Entry("second"));
        await Task.WhenAll(new SessionUsageReader(Paths).ReadAsync([project with { Conversations = [first] }]),
            new SessionUsageReader(Paths).ReadAsync([project with { Conversations = [second] }]));
        Assert.AreEqual(2, (await new SessionUsageReader(Paths).ReadAsync([])).Samples.Count);
    }

    [TestMethod]
    public void ResetRejectsOldSamplesButKeepsNewUsage()
    {
        var cutoff = DateTimeOffset.UtcNow;
        var store = new LocalUsageStore(Paths.UsageArchiveFile);
        var old = new UsageSample("old", Guid.NewGuid(), Guid.NewGuid(), cutoff.AddSeconds(-1), "provider", "model", 10);
        var fresh = old with { Key = "fresh", At = cutoff.AddSeconds(1), Tokens = 20 };
        store.Merge([old], null, default);
        store.Merge([], cutoff, default);
        var samples = store.Merge([old, fresh], null, default);
        Assert.AreEqual("fresh", samples.Single().Key);
        Assert.AreEqual(20L, samples.Single().Tokens);
    }

    [TestMethod]
    public async Task FailedUsagePreservationLeavesSessionForCleanupRetry()
    {
        var conversation = Conversation("Retry"); var project = Project(conversation);
        await WriteAsync(project, conversation, Entry());
        await File.WriteAllTextAsync(Paths.UsageArchiveFile, "invalid json");
        var repository = new InMemoryProjectRepository(project);
        var cleanup = new Services.Conversations.ConversationDataCleanup(Paths, repository, (_, _) => Task.FromResult(0), new SessionUsageReader(Paths));
        await cleanup.ScheduleAsync(project.Id, conversation.Id, ProjectTargets.All(project)[0]);
        await repository.DeleteConversationAsync(project.Id, conversation.Id);
        Assert.IsNotNull(await cleanup.RunPendingAsync());
        Assert.IsTrue(File.Exists(Paths.GetSessionFile(project.Id, conversation.Id)));
        Assert.AreEqual("invalid json", await File.ReadAllTextAsync(Paths.UsageArchiveFile));
    }

    [TestMethod]
    public async Task ChangedFilesRefreshWhileIncompleteWritesAreReportedAndRetried()
    {
        var conversation = Conversation("Test"); var project = Project(conversation); var reader = new SessionUsageReader(Paths);
        await WriteAsync(project, conversation, Entry() + "\n{\"type\":");
        var partial = await reader.ReadAsync([project]);
        Assert.AreEqual(1, partial.Samples.Count); Assert.AreEqual(1, partial.SkippedRecords);
        await WriteAsync(project, conversation, Entry() + "\n" + Entry("second") + "\n");
        var complete = await reader.ReadAsync([project]);
        Assert.AreEqual(2, complete.Samples.Count); Assert.AreEqual(0, complete.SkippedRecords);
    }

    [TestMethod]
    public void BoundedLinesSkipOversizedRowsAndResumeAtNextLine()
    {
        using var reader = new StringReader("short\n" + new string('x', 100) + "\nnext");
        var rows = BoundedJsonLines.Read(reader, default, 20).ToArray();
        CollectionAssert.AreEqual(new string?[] { "short", null, "next" }, rows);
    }

    [TestMethod]
    public void DateRangesIncludeEmptyDaysAndSeparateModelsAndProjects()
    {
        var date = DateOnly.FromDateTime(DateTime.Today); var at = DateTimeOffset.Now; var project = Guid.NewGuid(); var other = Guid.NewGuid();
        var samples = new UsageSample[]
        {
            new("a", project, Guid.NewGuid(), at, "one", "model", 10),
            new("b", project, Guid.NewGuid(), at.AddDays(-1), "two", "model", 20),
            new("c", other, Guid.NewGuid(), at, "one", "another", null),
            new("old", project, Guid.NewGuid(), at.AddDays(-30), "one", "model", 1000)
        };
        var summary = HomeUsageAggregator.Summarize(new(samples, 0, 0), new Dictionary<Guid, string> { [project] = "A", [other] = "B" }, date, 7);
        Assert.AreEqual(30m, summary.Tokens); Assert.AreEqual(3, summary.Responses); Assert.AreEqual(1, summary.MissingUsage);
        Assert.AreEqual(7, summary.Days.Count); Assert.AreEqual(3, summary.Models.Count); Assert.AreEqual(2, summary.ActiveProjects);
        Assert.AreEqual(0m, summary.Days[0].Tokens);
    }

    [TestMethod]
    public async Task RecentConversationsSpanProjectsAndExcludeSettledItems()
    {
        var first = Project(Conversation("Earlier", -10)); var second = Project(Conversation("Latest"), Conversation("Settled", 1) with { IsSettled = true });
        var shell = new ShellViewModel(new InMemoryProjectRepository(first, second)); await shell.LoadAsync();
        using var home = new HomeViewModel(shell, new SessionUsageReader(Paths)); await home.RefreshAsync();
        Assert.AreEqual(2, home.Recent.Count); Assert.AreEqual("Latest", home.Recent[0].Title);
        Assert.AreEqual("0", home.TokenTotal);
        home.Recent[1].OpenCommand.Execute(null);
        Assert.AreEqual("Earlier", shell.SelectedConversation!.Title); Assert.IsFalse(shell.ShowHome);
    }
}
