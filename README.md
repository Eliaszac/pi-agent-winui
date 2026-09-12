# Pi desktop

Native Windows frontend for the Pi coding agent, built with WinUI 3 and C#. Pi runs locally through RPC; conversations can work in Local Windows, WSL, or Linux SSH workspaces.

## Documentation

- [Future work](BACKLOG.md) — ideas and issues to revisit.
- [MIT license](LICENSE), [terms](Legal/TERMS.md), [privacy and data](Legal/PRIVACY.md), and [third-party notices](Legal/THIRD-PARTY-NOTICES.md) — also available offline in Settings → Legal & privacy.
- [Architecture and current scope](ARCHITECTURE.md) — ownership, implemented features, storage, decisions, and verification limits.
- [Pi setup](PI-SETUP.md) — runtime discovery, providers, and optional integrations.
- [Execution targets](EXECUTION-TARGETS.md) — Windows/WSL/SSH setup and capability limits.
- [Workspace checkpoints](CHECKPOINTS.md) — opt-in revert, Undo, storage clearing, and automatic cleanup. Feature status: done.
- [GitHub setup](GITHUB-SETUP.md) — build configuration, device login, and PR discovery.
- [Installer](Installer/README.md) — local build/install/update workflow.
- [Development instructions](AGENTS.md) — repository rules and execution permissions.
- [Design brief](.ui-craft/brief.md) and [native resource contract](.ui-craft/tokens.md).

Asset README and license files under `Assets/` document bundled artwork, grammars, and terminal dependencies; preserve their attribution.

## Development

Use Windows with the .NET 10 SDK and the project's Windows build tooling. Dependencies are declared in `PiAgentGui.csproj`. Ordinary verification commands:

```powershell
dotnet build PiAgentGui.csproj -p:Platform=x64
dotnet test Tests/PiAgentGui.Tests.csproj
```

Application startup, Pi runtime probes, and installer scripts are separate from unit tests and require authorization under AGENTS.md. See the setup guides before launching. No database or separate Node bridge is required.

## Keeping documentation current

Update the relevant guide when behavior changes, and ARCHITECTURE.md when ownership or product scope changes. Keep documentation focused on current behavior and operational guidance. Remove superseded implementation plans instead of retaining a development log, distinguish implemented behavior from verification, and avoid machine-specific installed-version claims in setup instructions.
