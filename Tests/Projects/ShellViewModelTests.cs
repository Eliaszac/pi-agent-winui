using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ShellViewModelTests
{
    [TestMethod]
    public async Task WelcomeShowsFourRecentUnsettledConversationsAndOpensSelectedItem()
    {
        var now = DateTimeOffset.UtcNow;
        var conversations = Enumerable.Range(0, 7).Select(index => new ConversationDraft
        {
            Id = Guid.NewGuid(), Title = "Conversation " + index, CreatedAt = now.AddDays(-10),
            LastUsedAt = now.AddMinutes(index), IsSettled = index == 6
        }).ToArray();
        var shell = new ShellViewModel(new InMemoryProjectRepository(new Project
        {
            Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example", Conversations = conversations
        }));
        await shell.LoadAsync();
        Assert.IsTrue(shell.ShowWelcome);
        Assert.AreEqual("Example", shell.WelcomeTitle);
        CollectionAssert.AreEqual(new[] { "Conversation 5", "Conversation 4", "Conversation 3", "Conversation 2" }, shell.RecentConversations.Select(item => item.Title).ToArray());
        shell.RecentConversations[0].OpenCommand.Execute(null);
        Assert.IsTrue(shell.HasConversation);
        Assert.AreEqual("Conversation 5", shell.WorkspaceTitle);
        Assert.IsFalse(shell.ShowWelcome);
    }

    [TestMethod]
    public async Task HeaderRenameUpdatesSidebarAndWindowWithoutStartingSidebarEditor()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example" }));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var conversation = shell.Projects[0].Conversations[0];
        shell.HeaderRename.BeginCommand.Execute(null);
        Assert.IsTrue(shell.HeaderRename.IsEditing);
        Assert.IsFalse(conversation.Rename.IsEditing);
        shell.HeaderRename.Draft = "Renamed from header";
        await shell.HeaderRename.SaveCommand.ExecuteAsync();
        Assert.AreEqual("Renamed from header", conversation.Title);
        Assert.AreEqual(conversation.Title, shell.WorkspaceTitle);
        StringAssert.Contains(shell.WindowTitle, conversation.Title);
        shell.HeaderRename.BeginCommand.Execute(null);
        shell.HeaderRename.Draft = "Discard";
        shell.HeaderRename.CancelCommand.Execute(null);
        Assert.AreEqual("Renamed from header", shell.WorkspaceTitle);
    }

    [TestMethod]
    public async Task ExtensionsNavigationRetainsConversationSelection()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example" }));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var title = shell.WorkspaceTitle;
        shell.OpenExtensions();
        Assert.IsTrue(shell.ShowExtensions);
        Assert.IsFalse(shell.ShowWorkspace);
        Assert.IsTrue(shell.HasConversation);
        shell.CloseExtensions();
        Assert.IsTrue(shell.ShowWorkspace);
        Assert.AreEqual(title, shell.WorkspaceTitle);
        Assert.IsTrue(shell.Projects[0].Conversations[0].IsSelected);
    }

    [TestMethod]
    public async Task InitialLoadDoesNotExposeAnEmptyWorkspaceBeforeSavedProjectsArrive()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Saved", Path = @"C:\Saved" }) { ReadBarrier = completion.Task };
        var shell = new ShellViewModel(repository);
        Assert.IsFalse(shell.HasLoaded);
        Assert.IsFalse(shell.ShowEmptyProjects);
        var loading = shell.LoadAsync();
        Assert.IsTrue(shell.ShowInitialLoading);
        Assert.IsFalse(shell.HasLoaded);
        Assert.IsFalse(shell.ShowEmptyProjects);
        completion.SetResult();
        await loading;
        Assert.IsTrue(shell.HasLoaded);
        Assert.IsFalse(shell.ShowInitialLoading);
        Assert.AreEqual("Saved", shell.WorkspaceTitle);
    }

    [TestMethod]
    public async Task ReloadPublishesFinalConversationSelectionWithoutAnIntermediateProjectScreen()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example" }));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var titles = new List<string>();
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.WorkspaceTitle)) titles.Add(shell.WorkspaceTitle);
        };
        var oldProjects = shell.Projects;
        await shell.LoadAsync();
        Assert.AreEqual(1, oldProjects.Count);
        Assert.AreNotSame(oldProjects, shell.Projects);
        CollectionAssert.AreEqual(new[] { "Conversation 1" }, titles);
    }

    [TestMethod]
    public async Task CreatingProjectDoesNotCoverItsWorkspaceWithANarrowSidebar()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository());
        await shell.LoadAsync();
        shell.Sidebar.SetAvailableWidth(600);
        shell.Sidebar.IsOpen = true;
        shell.AddSavedProject(new Project { Id = Guid.NewGuid(), Name = "Created", Path = @"C:\Created" });
        Assert.IsFalse(shell.Sidebar.IsOpen);
        Assert.IsTrue(shell.Projects[0].IsExpanded);
        Assert.AreEqual("Created", shell.WorkspaceTitle);
        Assert.IsFalse(shell.ShowEmptyProjects);
    }

    [TestMethod]
    public async Task FirstProjectIsExpandedAndOtherProjectsStartCollapsed()
    {
        var repository = new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "First", Path = @"C:\First" },
            new Project { Id = Guid.NewGuid(), Name = "Second", Path = @"C:\Second" });
        var shell = new ShellViewModel(repository);
        await shell.LoadAsync();
        Assert.IsTrue(shell.Projects[0].IsExpanded);
        Assert.IsFalse(shell.Projects[1].IsExpanded);
        Assert.AreEqual("Second", shell.WorkspaceTitle);
        Assert.IsTrue(shell.IsReady);
    }

    [TestMethod]
    public async Task CreatingConversationExpandsItsProjectAndSelectionSurvivesCollapsingAndReloading()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example" };
        var repository = new InMemoryProjectRepository(project);
        var shell = new ShellViewModel(repository);
        await shell.LoadAsync();
        shell.Projects[0].IsExpanded = false;
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        Assert.IsTrue(shell.Projects[0].IsExpanded);
        Assert.IsTrue(shell.HasConversation);
        Assert.AreEqual("Conversation 1", shell.WorkspaceTitle);

        shell.Projects[0].ToggleCommand.Execute(null);
        Assert.IsFalse(shell.Projects[0].IsExpanded);
        Assert.IsTrue(shell.HasConversation);
        await shell.LoadAsync();
        Assert.AreEqual("Conversation 1", shell.WorkspaceTitle);
        Assert.IsFalse(shell.Projects[0].IsExpanded);
        Assert.IsTrue(shell.Projects[0].Conversations[0].IsSelected);
    }

    [TestMethod]
    public async Task TogglingProjectsPreservesConversationPageAndNarrowSidebar()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "First", Path = @"C:\First" },
            new Project { Id = Guid.NewGuid(), Name = "Second", Path = @"C:\Second" }));
        await shell.LoadAsync();
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        var conversation = shell.Projects[0].Conversations[0];
        var title = shell.WorkspaceTitle;
        shell.Sidebar.SetAvailableWidth(600);
        shell.Sidebar.IsOpen = true;

        foreach (var showExtensions in new[] { false, true })
        {
            if (showExtensions) shell.OpenExtensions();
            shell.Sidebar.IsOpen = true;
            foreach (var project in shell.Projects)
            {
                var expanded = project.IsExpanded;
                for (var toggle = 0; toggle < 2; toggle++)
                {
                    project.ToggleCommand.Execute(null);
                    Assert.AreEqual(toggle == 0 ? !expanded : expanded, project.IsExpanded);
                    Assert.AreEqual(showExtensions, shell.ShowExtensions);
                    Assert.AreEqual(title, shell.WorkspaceTitle);
                    Assert.IsTrue(shell.HasConversation);
                    Assert.IsTrue(conversation.IsSelected);
                    Assert.IsTrue(shell.Sidebar.IsOpen);
                }
            }
        }
    }

    [TestMethod]
    public async Task FailedLoadIsRecoverableAndDoesNotLookLikeAnEmptyCatalog()
    {
        var repository = new InMemoryProjectRepository { ReadError = new InvalidDataException() };
        var shell = new ShellViewModel(repository);
        await shell.LoadAsync();
        Assert.IsTrue(shell.HasError);
        Assert.IsFalse(shell.ShowEmptyProjects);
        Assert.IsFalse(shell.IsReady);
        repository.ReadError = null;
        await shell.LoadAsync();
        Assert.IsFalse(shell.HasError);
        Assert.IsTrue(shell.ShowEmptyProjects);
        Assert.IsTrue(shell.IsReady);
    }

    [TestMethod]
    public async Task SelectingAConversationDismissesNarrowSidebar()
    {
        var shell = new ShellViewModel(new InMemoryProjectRepository(
            new Project { Id = Guid.NewGuid(), Name = "Example", Path = @"C:\Example" }));
        await shell.LoadAsync();
        shell.Sidebar.SetAvailableWidth(600);
        shell.Sidebar.IsOpen = true;
        await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
        Assert.IsFalse(shell.Sidebar.IsOpen);
        Assert.IsTrue(shell.HasConversation);
    }
}
