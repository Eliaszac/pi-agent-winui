using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class SidebarRowsTests
{
    [TestMethod]
    public void LargeExpandedProjectFlattensRowsAndKeepsConversationIdentity()
    {
        var dispatcher = new QueuedUiDispatcher();
        var source = new Project { Id = Guid.NewGuid(), Name = "Project", Path = @"C:\Project",
            Conversations = Enumerable.Range(0, 150).Select(index => new ConversationDraft { Id = Guid.NewGuid(), Title = "Conversation " + index, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(index), IsSettled = index >= 125 }).ToArray() };
        var project = new ProjectItemViewModel(source, (_, _) => { }, new AsyncRelayCommand(_ => Task.CompletedTask, _ => { }));
        project.IsExpanded = true;
        using var sidebar = new SidebarRows(dispatcher);
        sidebar.SetProjects(new ObservableCollection<ProjectItemViewModel> { project }); dispatcher.Drain();
        Assert.AreEqual(127, sidebar.Rows.Count);
        Assert.AreSame(project, sidebar.Rows[0]);
        Assert.AreSame(project.ActiveConversations[0], sidebar.Rows[1]);
        var first = project.ActiveConversations[0];
        first.Rename.BeginCommand.Execute(null); first.Rename.Draft = "Unfinished rename";
        project.IsSettledExpanded = true; dispatcher.Drain();
        Assert.AreEqual(152, sidebar.Rows.Count);
        project.IsExpanded = false; dispatcher.Drain(); Assert.AreEqual(1, sidebar.Rows.Count);
        project.IsExpanded = true; dispatcher.Drain();
        Assert.AreSame(first, sidebar.Rows[1]); Assert.AreEqual("Unfinished rename", first.Rename.Draft);
        project.Conversations.Remove(first); dispatcher.Drain(); Assert.IsFalse(sidebar.Rows.Contains(first));
    }

    [TestMethod]
    public void ReplacingCatalogDetachesOldGroupsAndEmptyProjectsHaveAnEmptyRow()
    {
        var dispatcher = new QueuedUiDispatcher();
        var project = new ProjectItemViewModel(new Project { Id = Guid.NewGuid(), Name = "Empty", Path = @"C:\Empty" }, (_, _) => { }, new AsyncRelayCommand(_ => Task.CompletedTask, _ => { }));
        project.IsExpanded = true;
        using var sidebar = new SidebarRows(dispatcher);
        sidebar.SetProjects(new ObservableCollection<ProjectItemViewModel> { project }); dispatcher.Drain();
        Assert.IsTrue(sidebar.Rows[1] is SidebarGroupRow { IsEmpty: true });
        sidebar.SetProjects([]); dispatcher.Drain();
        project.IsExpanded = false; dispatcher.Drain();
        Assert.AreEqual(0, sidebar.Rows.Count);
    }
}
