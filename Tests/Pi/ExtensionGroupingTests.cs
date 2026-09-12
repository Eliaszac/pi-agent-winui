using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ExtensionGroupingTests
{
    [TestMethod]
    public void CuratedExtensionsAppearOnceInTheRequestedSections()
    {
        var model = new ExtensionsViewModel();
        CollectionAssert.AreEqual(new[] { "Essentials", "Recommended & supported", "Our extensions" }, model.Groups.Select(group => group.Name).ToArray());
        CollectionAssert.AreEqual(new[] { SupportedExtensions.Permissions, SupportedExtensions.AutomaticTitles }, model.Groups[0].Cards.Select(card => card.Definition).ToArray());
        Assert.AreEqual(SupportedExtensions.Checkpoints, model.Groups[2].Cards.Single().Definition);
        Assert.AreEqual(model.Cards.Count, model.Groups.Sum(group => group.Cards.Count));
        Assert.AreEqual(model.Cards.Count, model.Groups.SelectMany(group => group.Cards).Distinct().Count());
        Assert.IsTrue(model.Groups[1].Cards.All(card => card.Attribution.StartsWith("Third-party")));
    }
}
