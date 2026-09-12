using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class SshAuthenticationTests
{
    [TestMethod]
    public void PasswordLaunchUsesCredentialReferenceWithoutASecretInArguments()
    {
        var target = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Server", Kind = "ssh", Host = "user@server", Path = "/srv/app", SshAuthentication = "password", HasSshSecret = true };
        var start = TargetCommandRunner.CreateStartInfo(target, "pwd");
        Assert.IsTrue(start.ArgumentList.Contains("BatchMode=no"));
        Assert.IsTrue(start.ArgumentList.Contains("PreferredAuthentications=password"));
        Assert.IsTrue(start.ArgumentList.Contains("StrictHostKeyChecking=yes"));
        Assert.AreEqual(target.Id.ToString("N"), start.Environment["PI_DESKTOP_SSH_CREDENTIAL"]);
        Assert.AreEqual("force", start.Environment["SSH_ASKPASS_REQUIRE"]);
        Assert.IsTrue(start.ArgumentList.Contains("NumberOfPasswordPrompts=1"));
        var key = target with { SshAuthentication = "key", HasSshSecret = false, SshKeyPath = @"C:\Keys\work key" };
        var args = SshLaunchOptions.Arguments(key);
        Assert.IsTrue(args.Contains(key.SshKeyPath));
        Assert.IsTrue(args.Contains("BatchMode=yes"));
        Assert.IsTrue(args.Contains("PasswordAuthentication=no"));
    }

    [TestMethod]
    public void AskpassRejectsOtherHostsKeysAndConfirmationPrompts()
    {
        Assert.IsTrue(SshAskpassPolicy.Allows("user@server's password:", "user@server's password: ", null));
        Assert.IsFalse(SshAskpassPolicy.Allows("user@server's password:", "user@jump's password: ", null));
        Assert.IsFalse(SshAskpassPolicy.Allows("user@server's password:", "user@server's password:", "confirm"));
        Assert.IsTrue(SshAskpassPolicy.Allows(@"Enter passphrase for key 'C:\Keys\work':", "Enter passphrase for key 'C:/Keys/work': ", null));
        Assert.IsFalse(SshAskpassPolicy.Allows(@"Enter passphrase for key 'C:\Keys\work':", "Enter passphrase for key 'C:/Keys/other': ", null));
    }

    [TestMethod]
    public void CredentialStoreRoundTripsOnlyItsOwnUniqueTestEntry()
    {
        var id = Guid.NewGuid();
        var store = new SshCredentialStore();
        try
        {
            Assert.IsNull(store.Read(id));
            store.Save(id, "test@invalid's password:", "unit-test-only-λ");
            var saved = store.Read(id);
            Assert.AreEqual("test@invalid's password:", saved?.Prompt);
            Assert.AreEqual("unit-test-only-λ", saved?.Secret);
        }
        finally { store.Delete(id); }
        Assert.IsNull(store.Read(id));
    }

    [TestMethod]
    public async Task OneTargetSkipsPickerWhileMultipleTargetsKeepChoice()
    {
        var id = Guid.NewGuid();
        var first = new ExecutionTarget { Id = id, Name = "Local", Path = Path.GetTempPath() };
        var second = first with { Id = Guid.NewGuid(), Name = "Other" };
        foreach (var multiple in new[] { false, true })
        {
            var project = new Project { Id = id, Name = "App", Path = first.Path, Targets = multiple ? [first, second] : [first], DefaultTargetId = first.Id };
            var shell = new ShellViewModel(new InMemoryProjectRepository(project));
            var calls = 0;
            shell.ChooseTargetAsync = _ => { calls++; return Task.FromResult<Guid?>(second.Id); };
            await shell.LoadAsync();
            await shell.NewConversationCommand.ExecuteAsync(shell.Projects[0]);
            Assert.AreEqual(multiple ? 1 : 0, calls);
            Assert.AreEqual(multiple ? second.Id : first.Id, shell.Projects[0].Conversations.Single().Target?.Id);
        }
    }

    [TestMethod]
    public async Task HeadlessHelperReturnsOnlyTheExpectedCredentialAndRejectsOtherPrompts()
    {
        var executable = Environment.GetEnvironmentVariable("PI_TEST_SSH_HELPER_EXE");
        if (string.IsNullOrWhiteSpace(executable)) { Assert.Inconclusive("Set PI_TEST_SSH_HELPER_EXE to a built executable to verify the headless SSH helper."); return; }
        var id = Guid.NewGuid();
        var store = new SshCredentialStore();
        try
        {
            store.Save(id, "unit-test@invalid's password:", "dummy-helper-test-λ");
            foreach (var valid in new[] { true, false })
            {
                var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
                start.Environment["PI_DESKTOP_SSH_CREDENTIAL"] = id.ToString("N");
                start.Environment.Remove("SSH_ASKPASS_PROMPT");
                start.ArgumentList.Add(valid ? "unit-test@invalid's password: " : "other@invalid's password: ");
                using var process = System.Diagnostics.Process.Start(start)!;
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    Assert.AreEqual(valid ? 0 : 1, process.ExitCode);
                    Assert.AreEqual(valid ? "dummy-helper-test-λ\n" : "", await output);
                    Assert.AreEqual("", await error);
                }
                finally { if (!process.HasExited) process.Kill(true); }
            }
        }
        finally { store.Delete(id); }
    }
}
