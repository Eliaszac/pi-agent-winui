using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PermissionModesSetupTests
{
    private string root = null!;
    private string package = null!;
    private const string Original = "function buildClassifierCompletionOptions(model, stage, base) {\n const options = {\n  ...base,\n  temperature: 0,\n };\n return options;\n}\n";
    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "pi setup tests " + Guid.NewGuid().ToString("N"));
        package = Path.Combine(root, "npm", "node_modules", "@georgedong32", "permission-modes");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(root, "settings.json"), """{"packages":["npm:@georgedong32/permission-modes@2.6.3"]}""");
        File.WriteAllText(Path.Combine(package, "package.json"), """{"name":"@georgedong32/permission-modes","version":"2.6.3"}""");
        File.WriteAllText(Path.Combine(package, "classifier-client.ts"), Original);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public async Task SetupRepairsFreshInstallAndIsRepeatable()
    {
        Assert.IsTrue(PermissionModesSupport.GetInstallationState(root).NeedsSetup);
        await ExecuteSetupAsync(true);
        var source = File.ReadAllText(Path.Combine(package, "classifier-client.ts"));
        Assert.AreEqual(Original.Replace("temperature: 0,", PermissionModesCompatibility.FixedOptions), source);
        Assert.IsFalse(PermissionModesSupport.GetInstallationState(root).NeedsSetup);
        Assert.AreEqual(Original, File.ReadAllText(Path.Combine(package, "classifier-client.ts.before-pi-gui-codex-fix")));
        await ExecuteSetupAsync(true);
        Assert.AreEqual(source, File.ReadAllText(Path.Combine(package, "classifier-client.ts")));
        File.WriteAllText(Path.Combine(package, "classifier-client.ts"), Original);
        Assert.IsTrue(PermissionModesSupport.GetInstallationState(root).NeedsSetup);
    }

    [TestMethod]
    public async Task FailedInstallAndUnknownSourceAreNotPatched()
    {
        await ExecuteSetupAsync(false, installExitCode: 1);
        Assert.AreEqual(Original, File.ReadAllText(Path.Combine(package, "classifier-client.ts")));
        var unknown = Original.Replace("temperature: 0,", "temperature: 1,");
        File.WriteAllText(Path.Combine(package, "classifier-client.ts"), unknown);
        await ExecuteSetupAsync(false);
        Assert.AreEqual(unknown, File.ReadAllText(Path.Combine(package, "classifier-client.ts")));
    }

    [TestMethod]
    public async Task UnexpectedPackageVersionIsNotPatched()
    {
        File.WriteAllText(Path.Combine(package, "package.json"), """{"name":"@georgedong32/permission-modes","version":"99.0.0"}""");
        await ExecuteSetupAsync(false);
        Assert.AreEqual(Original, File.ReadAllText(Path.Combine(package, "classifier-client.ts")));
    }

    private async Task ExecuteSetupAsync(bool success, int installExitCode = 0)
    {
        // The real installation command is shadowed inside this isolated test process. No Pi, npm, or model is run.
        var script = "function pi { $global:LASTEXITCODE = " + installExitCode + " }\ntry {\n" + PermissionModesSupport.InstallCommand + "\n} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        start.Environment["PI_CODING_AGENT_DIR"] = root;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.AreEqual(success, process.ExitCode == 0, (await stderr) + (await stdout));
    }
}
