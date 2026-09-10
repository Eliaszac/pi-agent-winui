using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ExtensionCardTests
{
    [TestMethod]
    public async Task RefreshUpdatesSetupVisibilityAndKeepsDefinitionAvailableAfterInstall()
    {
        var installed = false;
        var definition = SupportedExtensions.AutomaticTitles with
        {
            CheckInstallation = () => (installed ? "Installed" : "Not installed", !installed)
        };
        var card = new ExtensionCardViewModel(definition);
        await card.RefreshCommand.ExecuteAsync();
        Assert.IsTrue(card.NeedsSetup);
        installed = true;
        await card.RefreshCommand.ExecuteAsync();
        Assert.IsFalse(card.NeedsSetup);
        Assert.AreEqual("Installed", card.Status);
        Assert.AreSame(definition, card.Definition);
    }

    [TestMethod]
    public async Task FailedDetectionIsReportedWithoutBreakingOtherCards()
    {
        var failing = new ExtensionCardViewModel(SupportedExtensions.Permissions with
        {
            CheckInstallation = () => throw new IOException("Cannot read settings")
        });
        var working = new ExtensionCardViewModel(SupportedExtensions.AutomaticTitles with
        {
            CheckInstallation = () => ("Installed", false)
        });
        await Task.WhenAll(failing.RefreshCommand.ExecuteAsync(), working.RefreshCommand.ExecuteAsync());
        Assert.IsTrue(failing.NeedsSetup);
        StringAssert.Contains(failing.Status, "Cannot read settings");
        Assert.IsFalse(working.NeedsSetup);
    }
}
