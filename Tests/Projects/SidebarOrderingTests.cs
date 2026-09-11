using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class SidebarOrderingTests
{
    [TestMethod]
    public async Task OpeningConversationMovesItFirstAndSurvivesReload()
    {
        var older = new ConversationDraft { Id = Guid.NewGuid(), Title = "Older", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
        var newer = new ConversationDraft { Id = Guid.NewGuid(), Title = "Newer", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var repository = new InMemoryProjectRepository(new Project { Id = Guid.NewGuid(), Name = "Project", Path = @"C:\Project", Conversations = [older, newer] });
        var shell = new ShellViewModel(repository);
        await shell.LoadAsync();
        Assert.AreEqual("Newer", shell.Projects[0].ActiveConversations[0].Title);
        shell.Projects[0].Conversations[0].SelectCommand.Execute(null);
        Assert.AreEqual("Older", shell.Projects[0].ActiveConversations[0].Title);
        await shell.LoadAsync();
        Assert.AreEqual("Older", shell.Projects[0].ActiveConversations[0].Title);
        Assert.IsTrue(shell.Projects[0].ActiveConversations[0].IsSelected);
    }

    [TestMethod]
    public async Task ProjectsLoadNewestFirstAndNewProjectInsertsAtTop()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Old", Path = @"C:\Old" },
            new Project { Id = Guid.NewGuid(), Name = "New", Path = @"C:\New" }));
        await shell.LoadAsync();
        Assert.AreEqual("New", shell.Projects[0].Name);
        shell.AddSavedProject(new Project { Id = Guid.NewGuid(), Name = "Newest", Path = @"C:\Newest" });
        Assert.AreEqual("Newest", shell.Projects[0].Name);
    }
}
