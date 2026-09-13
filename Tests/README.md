# Tests

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet test Tests/PiAgentGui.Tests.csproj
```

The project compiles UI-independent production files directly, so these tests do not need WinUI startup, Pi or provider credentials. Tests use disposable files and mocked boundaries where appropriate. The three environment-dependent checks below report **Skipped** unless their explicit opt-in variables are set. Do not set those variables in an ordinary unit-test job.

To exclude integration checks entirely, including when your shell already has opt-in variables set:

```powershell
dotnet test Tests/PiAgentGui.Tests.csproj --filter 'TestCategory!=WindowsIntegration&TestCategory!=LiveIntegration'
```

## Windows Credential Manager

`CredentialStoreRoundTripsOnlyItsOwnUniqueTestEntry` is categorized as `WindowsIntegration`. It writes, reads and removes one uniquely named dummy credential. It never needs real SSH credentials or a remote host. Run only in a Windows user session that permits Credential Manager access:

```powershell
$env:PI_TEST_WINDOWS_CREDENTIALS = '1'
try {
    dotnet test Tests/PiAgentGui.Tests.csproj --filter 'FullyQualifiedName~CredentialStoreRoundTripsOnlyItsOwnUniqueTestEntry'
} finally {
    Remove-Item Env:PI_TEST_WINDOWS_CREDENTIALS -ErrorAction SilentlyContinue
}
```

The opt-in is checked before accessing the store. Once enabled, credential-access errors remain test failures; they are not caught and disguised as skips. Restricted sandboxes and some build agents cannot run this check.

## Headless SSH helper

`HeadlessHelperReturnsOnlyTheExpectedCredentialAndRejectsOtherPrompts` is also `WindowsIntegration`. Set `PI_TEST_SSH_HELPER_EXE` to an absolute path to a built `PiAgentGui.exe`, then select that test by name. This opt-in launches the executable's headless helper path and creates a temporary dummy credential. It verifies accepted/rejected prompts without connecting to SSH or opening WinUI. Remove the environment variable afterward.

## Live Obsidian MCP connection

`ObsidianConnectsThroughProductionSetupAndTransport` is categorized as `LiveIntegration`. Set `PI_MCP_TEST_OBSIDIAN=1` and select that test by name only when Pi, the supported MCP adapter and the Obsidian package are available. It starts real processes against a disposable vault and temporarily overrides the Pi agent directory. Package setup may access the network; it is not an offline unit test. Remove the environment variable afterward.

Repository execution rules in [AGENTS.md](../AGENTS.md) still apply to agent-driven live verification. Unit-test success does not establish GUI, real SSH or installer coverage; see the corresponding setup guides for verification limits.
