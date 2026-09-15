using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ConversationDataCleanupTests
{
    [TestMethod]
    public async Task DeletionRemovesSessionImagesAndSettingsButPreservesCopiesAndRetriesTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-cleanup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var conversation = new ConversationDraft { Id = Guid.NewGuid(), Title = "Test", CreatedAt = DateTimeOffset.UtcNow };
            var project = new Project { Id = Guid.NewGuid(), Name = "Test", Path = directory, Conversations = [conversation] };
            var repository = new InMemoryProjectRepository(project);
            var paths = new PiSessionPaths(new ProjectStorageOptions { CatalogPath = Path.Combine(directory, "projects.json") });
            var session = paths.GetSessionFile(project.Id, conversation.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(session)!);
            await File.WriteAllTextAsync(session, "saved messages and embedded screenshot bytes");
            await File.WriteAllTextAsync(session + ".settings.json", "{}");
            var artifacts = new ArtifactStore(ArtifactStore.ForSession(session));
            await artifacts.SaveAsync("document.txt", [1, 2, 3], "text/plain", "Agent");
            var copy = paths.GetSessionFile(project.Id, Guid.NewGuid());
            File.Copy(session, copy);
            var calls = 0;
            var cleanup = new ConversationDataCleanup(paths, repository, (_, exactSession) =>
            {
                Assert.AreEqual(session, exactSession);
                return ++calls == 1 ? Task.FromException<int>(new IOException("offline")) : Task.FromResult(1);
            });
            await cleanup.ScheduleAsync(project.Id, conversation.Id, ProjectTargets.All(project)[0]);
            await cleanup.RunPendingAsync();
            Assert.IsTrue(File.Exists(session), "Scheduling alone must not delete a saved conversation.");
            Assert.AreEqual(0, calls);
            await repository.DeleteConversationAsync(project.Id, conversation.Id);
            Assert.IsNotNull(await cleanup.RunPendingAsync());
            Assert.IsFalse(File.Exists(session));
            Assert.IsFalse(File.Exists(session + ".settings.json"));
            Assert.IsFalse(Directory.Exists(artifacts.DirectoryPath));
            Assert.IsTrue(File.Exists(copy));
            Assert.AreEqual(1, Directory.GetFiles(paths.CleanupDirectory, "*.json").Length);
            Assert.IsNull(await cleanup.RunPendingAsync());
            Assert.AreEqual(0, Directory.GetFiles(paths.CleanupDirectory, "*.json").Length);
        }
        finally { Directory.Delete(directory, true); }
    }
}
