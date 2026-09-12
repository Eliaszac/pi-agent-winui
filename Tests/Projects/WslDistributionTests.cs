using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class WslDistributionTests
{
    [TestMethod]
    public void DefaultSelectionSupportsSpacesAndLocalizedStateColumns()
    {
        var result = WslDistributionParser.Parse("Ubuntu\r\nUbuntu Work\r\nDebian\r\n", "  NAME           STATE        VERSION\r\n* Ubuntu Work    Arrêté       2\r\n  Ubuntu         Running      2");
        Assert.AreEqual("Ubuntu Work", result.PreferredName);
        Assert.AreEqual(3, result.Names.Count);
    }

    [TestMethod]
    public void OnlyWorkspaceDistributionIsSelectedAndEmptyInventoryStaysEmpty()
    {
        var result = WslDistributionParser.Parse("docker-desktop\r\nUbuntu\r\ndocker-desktop-data\r\n", "* docker-desktop    Running     2");
        CollectionAssert.AreEqual(new[] { "Ubuntu" }, result.Names.ToArray());
        Assert.AreEqual("Ubuntu", result.PreferredName);
        Assert.IsNull(WslDistributionParser.Parse("", "").PreferredName);
    }

    [TestMethod]
    public async Task ConcurrentDialogsShareStartupDiscoveryAndExplicitRefreshReplacesIt()
    {
        var calls = 0;
        var ready = new TaskCompletionSource<WslDistributionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new WslDistributionCache(() => { Interlocked.Increment(ref calls); return ready.Task; });
        var startup = cache.GetAsync();
        Assert.AreSame(startup, cache.GetAsync());
        Assert.AreSame(startup, cache.GetAsync(refresh: true));
        ready.SetResult(new(["Ubuntu"], "Ubuntu"));
        await startup;
        Assert.AreSame(startup, cache.GetAsync());
        Assert.AreEqual(1, calls);
        await cache.GetAsync(refresh: true);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task DiscoveryFailureIsDisplayedAndRefreshCanRecover()
    {
        var fail = true;
        var cache = new WslDistributionCache(() => fail ? throw new IOException("unavailable") : Task.FromResult(new WslDistributionSnapshot(["Debian"], "Debian")));
        var error = await cache.GetAsync();
        Assert.IsNotNull(error.Error);
        Assert.IsNull(error.PreferredName);
        fail = false;
        Assert.AreEqual("Debian", (await cache.GetAsync(refresh: true)).PreferredName);
    }
}
