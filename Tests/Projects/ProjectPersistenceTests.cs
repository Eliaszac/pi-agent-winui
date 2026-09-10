using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Projects;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ProjectPersistenceTests
{
    private string testDirectory = null!;
    private string workingDirectory = null!;
    private ProjectStorageOptions options = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"));
        workingDirectory = Directory.CreateDirectory(System.IO.Path.Combine(testDirectory, "workspace")).FullName;
        options = new ProjectStorageOptions { CatalogPath = System.IO.Path.Combine(testDirectory, "storage", "projects.json") };
    }

    [TestCleanup]
    public void Cleanup()
    {
        var fullPath = System.IO.Path.GetFullPath(testDirectory);
        var allowedRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PiAgentGui.Tests") + System.IO.Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup path is outside the test root.");
        Directory.Delete(fullPath, recursive: true);
    }

    [TestMethod]
    public async Task GeneratedTitlesPersistButCannotOverwriteManualRenames()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var conversation = await repository.AddConversationAsync(project.Id);
        Assert.IsFalse(conversation.IsTitleManual);
        Assert.IsTrue(await repository.SetGeneratedTitleAsync(project.Id, conversation.Id, "Generated title"));
        var reopened = new JsonProjectRepository(options);
        Assert.AreEqual("Generated title", (await reopened.GetAllAsync()).Single().Conversations.Single().Title);
        await reopened.RenameConversationAsync(project.Id, conversation.Id, "My title");
        Assert.IsFalse(await repository.SetGeneratedTitleAsync(project.Id, conversation.Id, "Late generated title"));
        var saved = (await new JsonProjectRepository(options).GetAllAsync()).Single().Conversations.Single();
        Assert.AreEqual("My title", saved.Title);
        Assert.IsTrue(saved.IsTitleManual);
    }

    [TestMethod]
    public void LegacyConversationTitlesAreProtected()
    {
        var conversation = JsonSerializer.Deserialize<PiAgentGui.Models.Projects.ConversationDraft>(
            """{"Id":"a5d07b1d-cdf4-4b0f-bc6d-b564c6c67355","Title":"Existing title","CreatedAt":"2026-09-10T00:00:00Z"}""")!;
        Assert.IsTrue(conversation.IsTitleManual);
    }

    [TestMethod]
    public async Task CopiedConversationPersistsAndRejectsDuplicateIdentityOrMissingSource()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var source = await repository.AddConversationAsync(project.Id);
        var copy = source with { Id = Guid.NewGuid(), Title = "Example · clone", IsTitleManual = true };
        await repository.AddConversationCopyAsync(project.Id, source.Id, copy);
        var saved = (await new JsonProjectRepository(options).GetAllAsync()).Single().Conversations;
        Assert.AreEqual(2, saved.Count);
        Assert.AreEqual(copy, saved[1]);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => repository.AddConversationCopyAsync(project.Id, source.Id, copy));
        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => repository.AddConversationCopyAsync(project.Id, Guid.NewGuid(), copy with { Id = Guid.NewGuid() }));
        Assert.AreEqual(2, (await repository.GetAllAsync()).Single().Conversations.Count);
    }

    [TestMethod]
    public async Task ProjectAndNestedMetadataSurviveReopeningAndDocumentDisposal()
    {
        var repository = new JsonProjectRepository(options);
        Guid id;
        using (var metadata = JsonDocument.Parse("""{"custom":{"labels":["work",42],"enabled":true},"future":null}"""))
        {
            var project = await new ProjectService(repository).CreateAsync("  Example  ", workingDirectory + "\\", metadata.RootElement);
            id = project.Id;
        }

        var saved = (await new JsonProjectRepository(options).GetAllAsync()).Single();
        Assert.AreEqual(id, saved.Id);
        Assert.AreEqual("Example", saved.Name);
        Assert.AreEqual(workingDirectory, saved.Path);
        Assert.AreEqual(42, saved.Metadata.GetProperty("custom").GetProperty("labels")[1].GetInt32());
        Assert.IsTrue(saved.Metadata.GetProperty("custom").GetProperty("enabled").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, saved.Metadata.GetProperty("future").ValueKind);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(workingDirectory).Length);
    }

    [TestMethod]
    public async Task NewCatalogIsEmptyAndMetadataDefaultsToObject()
    {
        var repository = new JsonProjectRepository(options);
        Assert.AreEqual(0, (await repository.GetAllAsync()).Count);
        await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var saved = (await repository.GetAllAsync()).Single();
        Assert.AreEqual(JsonValueKind.Object, saved.Metadata.ValueKind);
        Assert.AreEqual(0, saved.Metadata.EnumerateObject().Count());
    }

    [TestMethod]
    public async Task RenamingAndSettlingRoundTripWithoutChangingIdentityOrMetadata()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Original", workingDirectory,
            JsonSerializer.SerializeToElement(new { custom = "keep" }));
        var conversation = await repository.AddConversationAsync(project.Id);
        await repository.RenameProjectAsync(project.Id, "  Renamed project  ");
        await repository.RenameConversationAsync(project.Id, conversation.Id, "  Renamed conversation  ");
        await repository.SetConversationSettledAsync(project.Id, conversation.Id, true);
        var saved = (await new JsonProjectRepository(options).GetAllAsync()).Single();
        Assert.AreEqual("Renamed project", saved.Name);
        Assert.AreEqual(project.Path, saved.Path);
        Assert.AreEqual("keep", saved.Metadata.GetProperty("custom").GetString());
        Assert.AreEqual(conversation.Id, saved.Conversations[0].Id);
        Assert.AreEqual(conversation.CreatedAt, saved.Conversations[0].CreatedAt);
        Assert.AreEqual("Renamed conversation", saved.Conversations[0].Title);
        Assert.IsTrue(saved.Conversations[0].IsSettled);
        await repository.SetConversationSettledAsync(project.Id, conversation.Id, false);
        Assert.IsFalse((await repository.GetAllAsync()).Single().Conversations[0].IsSettled);
    }

    [TestMethod]
    public async Task DeleteRemovesOnlyRequestedCatalogEntriesAndPreservesFiles()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("First", workingDirectory);
        var other = await new ProjectService(repository).CreateAsync("Other", Directory.CreateDirectory(System.IO.Path.Combine(testDirectory, "other")).FullName);
        var first = await repository.AddConversationAsync(project.Id);
        var second = await repository.AddConversationAsync(project.Id);
        var userFile = System.IO.Path.Combine(workingDirectory, "keep.txt");
        await File.WriteAllTextAsync(userFile, "project data");
        await repository.DeleteConversationAsync(project.Id, first.Id);
        Assert.AreEqual(second.Id, (await repository.GetAllAsync())[0].Conversations.Single().Id);
        await repository.DeleteProjectAsync(project.Id);
        Assert.AreEqual(other.Id, (await new JsonProjectRepository(options).GetAllAsync()).Single().Id);
        Assert.AreEqual("project data", await File.ReadAllTextAsync(userFile));
    }

    [TestMethod]
    public async Task InvalidRenameOrMissingConversationLeavesCatalogUntouched()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("First", workingDirectory);
        var conversation = await repository.AddConversationAsync(project.Id);
        var before = await File.ReadAllTextAsync(options.CatalogPath);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => repository.RenameProjectAsync(project.Id, " "));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => repository.RenameConversationAsync(project.Id, conversation.Id, " "));
        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => repository.DeleteConversationAsync(project.Id, Guid.NewGuid()));
        Assert.AreEqual(before, await File.ReadAllTextAsync(options.CatalogPath));
    }

    [TestMethod]
    public async Task LegacyConversationWithoutSettledFieldLoadsAsActive()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("First", workingDirectory);
        await repository.AddConversationAsync(project.Id);
        var document = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(options.CatalogPath))!;
        document["projects"]![0]!["conversations"]![0]!.AsObject().Remove("isSettled");
        await File.WriteAllTextAsync(options.CatalogPath, document.ToJsonString());
        Assert.IsFalse((await repository.GetAllAsync()).Single().Conversations.Single().IsSettled);
    }

    [TestMethod]
    public async Task DuplicatePathsAreRejectedAcrossRepositoryInstances()
    {
        var first = await new ProjectService(new JsonProjectRepository(options)).CreateAsync("First", workingDirectory);
        var duplicate = first with { Id = Guid.NewGuid(), Name = "Second", Path = workingDirectory.ToUpperInvariant() + "\\." };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new JsonProjectRepository(options).AddAsync(duplicate));
        Assert.AreEqual(1, (await new JsonProjectRepository(options).GetAllAsync()).Count);
    }

    [TestMethod]
    public async Task MetadataUpdatePreservesIdentityAndOtherProjects()
    {
        var repository = new JsonProjectRepository(options);
        var first = await new ProjectService(repository).CreateAsync("First", workingDirectory);
        var secondDirectory = Directory.CreateDirectory(System.IO.Path.Combine(testDirectory, "second")).FullName;
        var second = await new ProjectService(repository).CreateAsync("Second", secondDirectory);
        await repository.UpdateMetadataAsync(first.Id, JsonSerializer.SerializeToElement(new { color = "green" }));

        var saved = await new JsonProjectRepository(options).GetAllAsync();
        Assert.AreEqual(first.Id, saved[0].Id);
        Assert.AreEqual(first.Name, saved[0].Name);
        Assert.AreEqual(first.Path, saved[0].Path);
        Assert.AreEqual("green", saved[0].Metadata.GetProperty("color").GetString());
        Assert.AreEqual(second.Id, saved[1].Id);
        Assert.AreEqual("{}", saved[1].Metadata.GetRawText());
    }

    [DataTestMethod]
    [DataRow("broken json")]
    [DataRow("null")]
    [DataRow("{\"schemaVersion\":2,\"projects\":[]}")]
    [DataRow("{\"schemaVersion\":1,\"projects\":null}")]
    [DataRow("{\"schemaVersion\":1,\"projects\":[null]}")]
    [DataRow("{\"schemaVersion\":1}")]
    public async Task InvalidCatalogIsNeverSilentlyReplaced(string content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(options.CatalogPath)!);
        await File.WriteAllTextAsync(options.CatalogPath, content);
        var repository = new JsonProjectRepository(options);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => repository.GetAllAsync());
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new ProjectService(repository).CreateAsync("Example", workingDirectory));
        Assert.AreEqual(content, await File.ReadAllTextAsync(options.CatalogPath));
    }

    [TestMethod]
    public async Task UnavailableDirectoryStillAppearsInSavedProjects()
    {
        var repository = new JsonProjectRepository(options);
        await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        Directory.Delete(workingDirectory);
        Assert.AreEqual(1, (await repository.GetAllAsync()).Count);
        await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => new ProjectService(repository).CreateAsync("Missing", workingDirectory));
    }

    [TestMethod]
    public async Task CancelledWritePreservesExistingCatalog()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var before = await File.ReadAllTextAsync(options.CatalogPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => repository.UpdateMetadataAsync(
            project.Id, JsonSerializer.SerializeToElement(new { changed = true }), cancellation.Token));
        Assert.AreEqual(before, await File.ReadAllTextAsync(options.CatalogPath));
    }

    [TestMethod]
    public async Task CompetingWriterFailsWithoutChangingCatalog()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var before = await File.ReadAllTextAsync(options.CatalogPath);
        using (var catalogLock = new FileStream(options.CatalogPath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsExceptionAsync<IOException>(() => new JsonProjectRepository(options).UpdateMetadataAsync(
                project.Id, JsonSerializer.SerializeToElement(new { changed = true })));
        }
        Assert.AreEqual(before, await File.ReadAllTextAsync(options.CatalogPath));
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("42")]
    public async Task NonObjectMetadataIsRejected(string content)
    {
        using var metadata = JsonDocument.Parse(content);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => new ProjectService(new JsonProjectRepository(options))
            .CreateAsync("Example", workingDirectory, metadata.RootElement));
        Assert.IsFalse(File.Exists(options.CatalogPath));
    }

    [TestMethod]
    public async Task BlankNameAndRelativePathAreRejected()
    {
        var service = new ProjectService(new JsonProjectRepository(options));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.CreateAsync(" ", workingDirectory));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.CreateAsync("Example", "relative"));
        Assert.IsFalse(File.Exists(options.CatalogPath));
    }

    [TestMethod]
    public async Task ConversationDraftsSurviveReopeningAndMetadataUpdates()
    {
        var repository = new JsonProjectRepository(options);
        var project = await new ProjectService(repository).CreateAsync("Example", workingDirectory);
        var first = await repository.AddConversationAsync(project.Id);
        var second = await new JsonProjectRepository(options).AddConversationAsync(project.Id);
        await repository.UpdateMetadataAsync(project.Id, JsonSerializer.SerializeToElement(new { custom = true }));
        var saved = (await new JsonProjectRepository(options).GetAllAsync()).Single();
        Assert.AreEqual(first, saved.Conversations[0]);
        Assert.AreEqual(second, saved.Conversations[1]);
        Assert.AreEqual("Conversation 2", second.Title);
        Assert.IsTrue(saved.Metadata.GetProperty("custom").GetBoolean());
    }

    [TestMethod]
    public async Task OriginalCatalogWithoutConversationsLoadsAndCanCreateDraft()
    {
        var id = Guid.NewGuid();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(options.CatalogPath)!);
        await File.WriteAllTextAsync(options.CatalogPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            projects = new[] { new { id, name = "Example", path = workingDirectory, metadata = new { } } }
        }));
        var repository = new JsonProjectRepository(options);
        Assert.AreEqual(0, (await repository.GetAllAsync()).Single().Conversations.Count);
        await repository.AddConversationAsync(id);
        Assert.AreEqual(1, (await repository.GetAllAsync()).Single().Conversations.Count);
    }

    [TestMethod]
    public async Task CreateFormPreservesInputAfterFailureAndCanRetry()
    {
        var repository = new JsonProjectRepository(options);
        var service = new ProjectService(repository);
        await service.CreateAsync("Existing", workingDirectory);
        var form = new PiAgentGui.ViewModels.Projects.CreateProjectViewModel(service)
        {
            Name = "My project",
            Path = workingDirectory
        };
        Assert.IsNull(await form.TryCreateAsync());
        Assert.IsTrue(form.HasError);
        Assert.AreEqual("My project", form.Name);
        Assert.AreEqual(workingDirectory, form.Path);
        Assert.IsTrue(form.CanSubmit);
        Assert.IsFalse(form.IsBusy);

        form.Path = Directory.CreateDirectory(System.IO.Path.Combine(testDirectory, "another-workspace")).FullName;
        Assert.IsNotNull(await form.TryCreateAsync());
        Assert.IsFalse(form.HasError);
        Assert.AreEqual(2, (await repository.GetAllAsync()).Count);
    }
}
