using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.Services.Projects;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class TargetLocationFormTests
{
    [TestMethod]
    public void LocationSwitchingKeepsSshAndWslSelectionsIndependent()
    {
        var form = new TargetLocationViewModel { Path = "/home/user/app", Host = "dev-server", WslDistribution = "Ubuntu", TargetKindIndex = 1 };
        Assert.AreEqual("Ubuntu", form.CreateTarget(Guid.NewGuid(), "Linux").Host);
        Assert.AreEqual("wsl", form.CreateTarget(Guid.NewGuid(), "Linux").Kind);
        form.TargetKindIndex = 2;
        Assert.AreEqual("dev-server", form.CreateTarget(Guid.NewGuid(), "Server").Host);
        Assert.IsTrue(form.IsLocationComplete);
        form.TargetKindIndex = 1;
        Assert.AreEqual("Ubuntu", form.SelectedHost);
    }

    [TestMethod]
    public void CloneChoiceRequiresUrlAndUpdatesCreateAction()
    {
        var form = new CreateProjectViewModel(new ProjectService(new InMemoryProjectRepository())) { Name = "App", Path = @"C:\Projects\app" };
        var notifications = new List<string?>();
        form.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        Assert.IsTrue(form.CanSubmit);
        form.SourceIndex = 1;
        Assert.IsFalse(form.CanSubmit);
        Assert.AreEqual("Clone destination", form.FolderLabel);
        Assert.IsTrue(notifications.Contains(nameof(form.CanSubmit)));
        form.RepositoryUrl = "https://github.com/owner/app.git";
        Assert.IsTrue(form.CanSubmit);
        form.SourceIndex = 0;
        Assert.AreEqual("Workspace folder", form.FolderLabel);
        Assert.IsTrue(form.CanSubmit);
    }
}
