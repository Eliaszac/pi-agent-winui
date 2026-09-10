using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Services.Pi;
using PiAgentGui.ViewModels.Startup;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiInstallationTests
{
    private string folder = null!;

    [TestInitialize]
    public void Setup()
    {
        folder = Path.Combine(Path.GetTempPath(), "PiInstallationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public async Task MissingPiShowsInstallScreen()
    {
        var startup = new StartupViewModel(new PiInstallationLocator(new PiRuntimeOptions(), [folder]));
        Assert.IsTrue(startup.IsChecking);
        Assert.IsFalse(await startup.CheckAsync());
        Assert.IsFalse(startup.IsChecking);
        Assert.IsTrue(startup.ShowInstall);
        StringAssert.Contains(startup.Message, "Pi couldn't be found");
    }

    [TestMethod]
    public async Task NativeInstallationPassesWithoutNodeOrLaunchingExecutable()
    {
        var executable = Path.Combine(folder, "pi.exe");
        File.WriteAllText(executable, "Test placeholder; never executed.");
        var locator = new PiInstallationLocator(new PiRuntimeOptions(), [folder]);
        Assert.AreEqual(executable, locator.Resolve().ExecutablePath);
        Assert.IsNull(locator.Resolve().CliPath);
        var startup = new StartupViewModel(locator);
        Assert.IsTrue(await startup.CheckAsync());
        Assert.IsFalse(startup.ShowInstall);
    }

    [TestMethod]
    public async Task NpmInstallationRequiresNodeAndSharesResolutionWithLauncher()
    {
        var cli = Path.Combine(folder, "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js");
        Directory.CreateDirectory(Path.GetDirectoryName(cli)!);
        File.WriteAllText(cli, "Test placeholder; never executed.");
        var locator = new PiInstallationLocator(new PiRuntimeOptions(), [folder]);
        var startup = new StartupViewModel(locator);
        Assert.IsFalse(await startup.CheckAsync());
        StringAssert.Contains(startup.Message, "Node.js");
        var node = Path.Combine(folder, "node.exe");
        File.WriteAllText(node, "Test placeholder; never executed.");
        var installation = locator.Resolve();
        Assert.AreEqual(node, installation.ExecutablePath);
        Assert.AreEqual(cli, installation.CliPath);
        var info = new PiProcessStartInfoFactory(locator).Create(new(folder, Path.Combine(folder, "session.jsonl")));
        Assert.AreEqual(node, info.FileName);
        Assert.AreEqual(cli, info.ArgumentList[0]);
    }

    [TestMethod]
    public void ExplicitOverrideIsHonoredAndInvalidOverrideDoesNotSilentlyFallBack()
    {
        var executable = Path.Combine(folder, "pi.exe");
        File.WriteAllText(executable, "Test placeholder; never executed.");
        var custom = Path.Combine(folder, "custom.exe");
        File.WriteAllText(custom, "Test placeholder; never executed.");
        Assert.AreEqual(custom, new PiInstallationLocator(new PiRuntimeOptions { ExecutablePath = custom }, [folder]).Resolve().ExecutablePath);
        Assert.ThrowsException<FileNotFoundException>(() => new PiInstallationLocator(
            new PiRuntimeOptions { ExecutablePath = Path.Combine(folder, "missing.exe") }, [folder]).Resolve());
    }
}
