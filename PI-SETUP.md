# Pi runtime setup and verification

## Prerequisites

Install and authenticate Pi separately before using the chat view. The GUI uses Pi's existing provider configuration and authentication; it does not collect or store API keys. Use a current Pi release matching the [RPC reference](https://pi.dev/docs/latest/rpc), including `agent_settled` and delta-only streaming events. An exact release has not yet been validated locally.

The current [official quickstart](https://pi.dev/docs/latest/quickstart) documents:

```powershell
npm install -g --ignore-scripts @earendil-works/pi-coding-agent
```

Follow the [Windows setup guide](https://pi.dev/docs/latest/windows) for Node.js and Git Bash requirements. Authenticate through Pi's terminal `/login` flow or a provider's supported environment configuration. See [Pi usage](https://pi.dev/docs/latest/usage) for configuration and model selection. Restart Rider/the GUI after changing inherited environment variables.

No installation, authentication, GUI launch, or Pi launch was performed during this implementation.

## Executable discovery

Startup checks for Pi before showing the project workspace. If it is missing, an Install Pi screen links to `https://pi.dev/` and asks you to close and restart this app after installation. An npm installation without Node.js shows setup guidance too. The check inspects files only; authentication and protocol compatibility are checked later when using a conversation.

The GUI searches PATH and `%APPDATA%\npm` for:

- `pi.exe` (native standalone executable).
- `node_modules\@earendil-works\pi-coding-agent\dist\cli.js`, launched directly with `node.exe`.
- The legacy npm package directory `node_modules\@mariozechner\pi-coding-agent\dist\cli.js` as a discovery fallback; discovery does not guarantee protocol compatibility.

For a custom installation, set these environment variables before launching the app:

```text
PI_GUI_PI_EXECUTABLE=C:\absolute\path\to\pi.exe
```

Or for an npm installation:

```text
PI_GUI_PI_EXECUTABLE=C:\absolute\path\to\pi-coding-agent\dist\cli.js
PI_GUI_NODE_EXECUTABLE=C:\Program Files\nodejs\node.exe
```

Use an actual `.exe` or `dist\cli.js`, not `pi.cmd`/PowerShell wrappers. The application uses argument arrays and never invokes a command shell to start Pi. Opening a conversation starts Pi automatically behind a local loading state. A missing executable, unavailable project directory, locked session, or runtime failure appears in the affected conversation with a Retry action.

## Storage and ownership

- GUI catalog: `%LOCALAPPDATA%\PiAgentGui\projects.json`.
- Pi session: `%LOCALAPPDATA%\PiAgentGui\sessions\<project-guid-without-hyphens>\<conversation-guid-without-hyphens>.jsonl`.
- Session lease: the same session path with `.lock` appended. The file can remain after shutdown; the OS lock, not its presence, indicates ownership.
- Pi is the only writer of session contents. See the [upstream session manager](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/src/core/session-manager.ts) for explicit-path session creation/resumption. An empty conversation can remain catalog-only until Pi persists a turn.
- Conversation processes in the same project use the same working folder. They do not get isolated checkouts automatically.
- Selecting another conversation leaves the previous process running. The interface does not expose manual connection management. Closing the main window closes all processes owned by that window.

## Live smoke test still required

The Extensions page offers global setup for `@georgedong32/permission-modes@2.6.3` as a preview integration. Follow its modal instructions, finish active work, then restart the GUI. No installation is performed automatically. Only this pinned npm version is recognized initially. Existing extension model profiles and rules still apply; these modes are not an OS sandbox.

Check missing, configured-but-disabled, and incompatible-version states. Open the setup modal both from Extensions and from the chat control using mouse and keyboard. With the extension loaded, test Ask approval/cancel, Plan, Auto, and Bypass only in a disposable project. Confirm the selector follows the extension's saved mode after switching and reopening. A fresh session should default to Auto; an existing saved choice should survive reopening. Verify two conversations retain independent models and modes, including after navigating to Extensions and back. The current Pi version must support `get_entries` for mode reporting.

1. Open a project and conversation; confirm a local loading state resolves into the chat automatically. Selecting an already loaded conversation should be immediate. Check that the composer model dropdown reflects Pi's current model; select another, then reopen after restarting to verify Pi's persistence.
2. Send a small prompt with Enter; verify streaming, final text, and that Send becomes available again. Ctrl+Enter should insert a newline at the cursor or replace selected text, without sending. Check Enter in the model dropdown does not submit a message.
3. Ask for a simple tool operation in a disposable project; verify arguments and results.
4. Start two conversations, switch between them while running, and stop one. Confirm the other continues and both drafts/transcripts remain separate.
5. Restart the GUI and reopen a completed conversation. Confirm saved history appears.
6. Verify extension questions, missing Pi, unavailable folder, and unexpected process exit feedback.
7. Check chat scrolling, keyboard send, and both system themes in the actual WinUI app.

Ordinary builds and fake-transport unit tests do not validate these live behaviors. Under `AGENTS.md`, the agent must obtain permission before launching the GUI or Pi.

## Automatic titles

Open Extensions → Automatic titles → Setup instructions. Install globally with `pi install npm:pi-auto-session-name@0.1.1`, configure `~/.pi/agent/extensions/auto-session-name.json` to an available authenticated model (for this setup: `{"provider":"openai-codex","model":"gpt-5.5"}`), then restart. The extension's default naming model is separate from the chat model. New conversations can be automatically named; legacy and manually renamed titles are protected. Test naming after a first run, rename during naming, and restart to verify protection. No install or provider request was run during implementation.

## Approval classifier model

Permission Modes 2.6.3 enables a separate auto-approval classifier by default, using anthropic/claude-haiku-4-5. Its config.ts is the source of truth; the README shows a disabled example, not the actual default. Without a classifier override in ~/.pi/agent/permission-modes.json, Auto may request Haiku even while the conversation uses another provider. Classifier errors default to denying the action.

Use Ask for manual approvals, or configure classifier.model in that file to an appropriate model available through Pi credentials. Review that model's approval behavior; changing the chat dropdown does not change the classifier. Do not disable failClosed merely to hide authentication errors. The GUI does not alter this configuration automatically.


## Bundled write diff capture

Restart the GUI after upgrading to load PiExtensions/write-diff.ts into each Pi process. It wraps Pi's native local write operation to capture a unified patch and persist it in tool-result details; approval hooks still run. Test both a new file and an overwrite in a disposable project, then reopen the conversation to verify the same diff survives. Old sessions without captured write metadata cannot recover the previous file contents. Binary/unreadable files and previews over 256 KiB or 5,000 lines per side show a diff-unavailable notice.

