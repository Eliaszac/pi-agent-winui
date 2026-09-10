# Architecture and MVP

## Local installer

Windows x64 distribution uses an unpackaged per-user Inno Setup installer. `Installer/Build-Installer.ps1` builds an untrimmed self-contained Release payload through `Properties/PublishProfiles/LocalInstaller.pubxml`. The stable install identity is `PiAgentGui.Desktop`, with files under `%LOCALAPPDATA%/Programs/Pi Agent`; user data stays separately under `%LOCALAPPDATA%/PiAgentGui` and Pi's own user directory. Increasing installer versions update in place; same-version repair and uninstall preserve user data, and downgrade is blocked. WebView2 is detected and bootstrapped only if absent. Generated assets stay under ignored `artifacts/installer`. This first local installer is unsigned and has no automatic update feed. See `Installer/README.md` for build, upgrade and verification details.

## Opt-in background research

- The conversation header's More actions menu opens Background research in the same resizable sidepanel area as Terminal. The global opt-in defaults off. Tasks/results are filtered to the selected conversation; the app owns their lifetime independently of main-agent turns.
- App composes ResearchCoordinator, ResearchStore, and PiResearchRunner. At most two separate Pi processes run concurrently, with a total active/queued cap of twelve and a twenty-minute execution timeout. Disabling cancels active/queued work. App shutdown disposes owned processes; saved unfinished tasks become Interrupted after restart and are never automatically replayed.
- Storage is `%LOCALAPPDATA%/PiAgentGui/research/research-enabled.json` and `research-tasks.json`, using atomic file replacement. Completed answers and their questions are persisted by the GUI because these are app-owned one-shot workers, not catalog copies of normal Pi conversations. Worker processes use --no-session and their own lease path. Results are bounded to 100,000 characters.
- The bundled research-dispatch.ts registers background_research but removes it from active tools when disabled. It checks the preference on session_start and before_agent_start, so enabling affects the next parent turn. A private extension UI input envelope carries title/question/model/effort to ConversationSession; the app acknowledges acceptance without creating a user question. Completion, failure, cancellation, and answers are never forwarded to the parent. The model receives no collect, steer, stop, or nesting tools.
- Each worker inherits the requested parent provider/model/effort; the effective model is checked before prompting to reject fallback. Workers start without general extensions, skills, templates, or context files. CLI tool allowlisting and a bundled tool_call guard permit read, grep, find, ls, read_web_page, and optionally websearch. Page reading performs bounded HTTPS GETs with a 20-page limit. This is a tool capability restriction, not an OS sandbox.
- Pi Search (`@heyhuynhgiabuu/pi-search@0.3.0`) is installed globally through Pi and listed in Extensions. Main conversations load its configured tools normally. Workers explicitly load only the verified global package entrypoint; their process disables its other seven tools without changing global configuration. The guard rejects includeContent background fetching and enforces four queries per call/twelve per task. No installed supported package means file/page research remains available without search. Provider settings remain upstream-owned at `~/.pi/pi-search.json` (or PI_SEARCH_CONFIG_PATH); basic search uses public Exa MCP without a key. Search provider availability and main-agent approval remain separate from package detection. Set PI_SEARCH_TEST_EXTENSION to the installed dist/index.js when running the RPC inventory test to verify search integration.
- A self-contained question is the entire worker task context. Its system prompt prefers assumptions and a final uncertainty statement over clarification or follow-up. Results are shown as Markdown in the panel. Explicit Share to composer appends a draft for user review; it never sends. Copy and cancellation are available there.
- Verification: C# fake-transport tests cover opt-in, queue bounds, cancellation, restart recovery, dispatch isolation, exact launch flags and settlement. `Tests/PiExtensions/ResearchRuntime.test.mjs`, with PI_RESEARCH_TEST_CLI pointing to Pi's CLI, verifies active tools on installed Pi without model calls. Tests/PiExtensions/ResearchProbe.ts is test-only and is never loaded in production.

## Markdown streaming crash diagnosis

The intermittent crash observed around automatic naming was traced with a full local dump to `MarkdownMessage.Render`: `String.Substring` received a Markdig block span outside the current streamed text. The exception escaped a `DispatcherQueueTimer` callback as native `0xc000027b` / `0x8000000B`, bypassing the application's managed unhandled-exception report. `MarkdownBlockSignature` validates span bounds and falls back to the complete text snapshot for invalid spans, preserving cache invalidation without unsafe slicing. Regression tests cover malformed spans and all streaming prefixes of representative Markdown. Ordinary Windows minidumps omitted the original exception's stack memory; a full dump plus SOS retained the managed exception stack.

## Terminal side panel

The TabView holds tab headers only; terminal surfaces live in a separate star-sized Grid below it. Keep each surface mounted while switching visibility between tabs. The TabView content presenter collapsed WebView2 to zero height even though scripts and shell output continued working. Live diagnostic measurements confirmed 400×0 before this separation and 400×653 afterward. Do not rely on a shell-output acknowledgement as proof that native layout is visible.

Builds use a concrete platform target and matching Windows runtime identifier, defaulting to x64 when an IDE selects AnyCPU. WebView2's WinRT native library otherwise defaults to x86 and can fail during environment creation with `0x8007007E` in a 64-bit process. Explicit x86 and ARM64 targets remain supported. Verify native dependencies in the ordinary build output, not only a separately configured verification build.

The conversation header toggles a right-side terminal panel with native TabView tabs. Tabs start in the selected project's working folder and retain that folder/session across conversation selection. Hiding preserves tabs; closing a tab ends its shell; closing the last tab closes the panel. The panel resizes with the native grip and overlays the conversation on narrow windows.

Services/Terminal owns Windows ConPTY handles, pipes, UTF-8 I/O, resize and shell lifetime. It starts PowerShell 7 from the standard installation when available, otherwise Windows PowerShell, only when a terminal tab is opened. Each tab owns one session. On app shutdown the renderer stops awaiting acknowledgements before ConPTY closes, allowing its final output to drain. ConPTY closes attached child processes. No Pi process or RPC state is reused for terminals.

The ANSI terminal surface uses bundled xterm.js 6.0.0 and fit-addon 0.11.0 in the existing WebView2 dependency. Native WinUI handles panel/tab chrome; this is a renderer, not a Node bridge. Renderer resources are local and pinned under Assets/Terminal with licenses; WebView2 profile data is under `%LOCALAPPDATA%/PiAgentGui/WebView2/Terminal`. No terminal history or output is deliberately persisted by the GUI (the shell may maintain its own history). Scrollback is capped at 5,000 lines. Output uses acknowledged chunks; the bridge disallows remote navigation, popups and output-driven clipboard writes. Ctrl+Shift+C/V supports explicit copy/paste.


## GitHub PR discovery

The header offers Connect to GitHub when signed out, and Open PR only when connected and an open PR matches the working folder's current branch. Sidebar conversation badges and context menus share that PR. Conversations currently share project working directories, so branch/PR state is project-level presentation data, not persisted conversation metadata.

GitBranchReader reads local Git metadata without fetching or mutating the repository. GitHubApi queries github.com with GitHub App user access tokens and read-only Pull requests/Metadata permissions. GitHubAuthentication implements cancellable device flow and secret-free refresh-token rotation; WindowsGitHubCredentialStore uses Windows Credential Locker. Public build configuration lives in GitHubApp.json. See GITHUB-SETUP.md for registration, install, troubleshooting and credential locations. No GitHub client secret, private key, server or gh dependency is used. Git reads poll every 15 seconds and API lookups are cached for one minute; selection and branch changes update the UI without restarting Pi.


## Opening projects in external applications

The conversation header provides an Open in split button. A read-only Windows discovery service checks App Paths, uninstall registrations, PATH, standard install locations and JetBrains product metadata. Supported detected editors, Windows Terminal and File Explorer use bundled publisher logos (sources in Assets/OpenIn/README.md). Portable or custom installations outside those locations may not be discovered.

The preferred editor ID is stored globally in `%LOCALAPPDATA%/PiAgentGui/open-in.json`. Selection falls back to the unique root solution's Windows association, then an installed editor, then Explorer. Explorer and Terminal do not replace the preferred editor. Solution-aware editors receive an unambiguous root solution; other targets receive the project folder. Launches use structured arguments without a command shell. Terminal uses `wt -d .` with the project's working directory. Discovery never launches applications; opening happens only on a user action. The dropdown includes an explicit refresh action.

## Status

The native project shell and first Pi conversation pass are implemented. Conversations run concurrently with isolated process/session ownership, streamed text, tool output, extension questions, and per-conversation Stop. The user reports the basic runtime works. Opening a conversation now prepares it automatically behind a local loading state; manual connection controls are removed. Builds and fake-transport unit tests are verified; interactive verification of the new loading/recovery UI remains pending. No GUI or Pi process was launched by the agent.

## Agreed product model

- A project has a stable internal ID, a user-chosen display name, and a path to an existing local working directory.
- A project also has an extensible JSON object named `metadata`, defaulting to `{}`. Keep required domain fields typed rather than hiding them in metadata.
- Creating a project registers that directory in the application. It does not generate source code, initialize Git, or require a running Pi process.
- Projects contain conversations. Their stable IDs map to dedicated Pi session files independently of the selected sidebar item.
- Project metadata persists locally across application restarts.
- The application checks directory availability before starting Pi and shows a recoverable error if the directory is unavailable.
- The application owns project metadata and project-to-conversation associations. Pi owns session contents, model interaction, tool execution, context, and compaction.

## Implemented project persistence

- Storage: `%LOCALAPPDATA%\PiAgentGui\projects.json`, outside project working directories. `Configuration/ProjectStorageOptions.cs` provides the default and allows isolated test locations.
- One versioned JSON catalog stores project records with `id`, `name`, `path`, an inline `metadata` object, and typed `conversations` entries. There are no per-project metadata file references or database dependencies.
- Conversation drafts have a local `id`, `title`, and `createdAt`. They contain no messages or Pi runtime state. Existing version 1 catalogs without `conversations` load with an empty list; no migration is needed for this additive early-development field.
- JSON fits the initial small local catalog. `IProjectRepository` separates callers from the format so storage can change later if querying or scale warrants it.
- Metadata supports nested JSON values; unknown metadata keys round-trip unchanged. Metadata updates replace the complete object, so callers must merge existing keys when changing only part of it. Credentials must never be stored here.
- The catalog currently uses schema version 1. Unsupported versions, unknown structural fields, malformed JSON, invalid records, and duplicate records fail explicitly without replacing the original file. A missing catalog starts empty.
- Writes reread the catalog under an exclusive `.lock` file, serialize to a unique adjacent temporary file, and replace the catalog by a same-volume rename. Competing operations fail with an I/O error rather than overwriting each other; the UI should allow retry. The lock file remains on disk, but its OS lock is released when the operation ends.
- Names are trimmed. Paths must be absolute, are normalized with `Path.GetFullPath`, and lose unnecessary trailing separators. Duplicate normalized paths use case-insensitive comparison; duplicate IDs are rejected. Junctions, symlinks, and other filesystem aliases are not resolved for deduplication.
- Creation requires an existing accessible directory. Reading saved projects does not require their directories to remain available; unavailable projects remain visible for recovery.
- `App.xaml.cs` composes the repository, project service, shell view model, modal view model factory, and window-owned folder picker. The app remains unpackaged and uses per-user filesystem storage rather than package-identity APIs.
- `Tests/PiAgentGui.Tests.csproj` links the UI-independent production files so MSTest can exercise them without loading WinUI or adding a production assembly solely for testing.
- Verification commands: `dotnet test Tests/PiAgentGui.Tests.csproj --no-restore --verbosity minimal`; `dotnet build PiAgentGui.csproj --no-restore -p:Platform=x64 --verbosity minimal`. No GUI or Pi process has been launched during this implementation.

## Implemented shell behavior

- Sidebar context menus support inline rename for conversations and projects, deletion, and conversation settle/restore. Enter or focus loss saves inline names; Escape cancels. Empty names and save failures keep the editor open. Persisted names update sidebar labels, the selected conversation heading, and the native window title.
- Each project shows active conversations first and a subtle, initially collapsed `Settled — (count)` group below them when nonempty. `ConversationDraft.IsSettled` is a typed catalog field (default false for older catalogs), independent of Pi's `agent_settled` event. Settling is organization, not cancellation; it preserves the runtime and session. Settling the selected conversation returns to the project view. Settled conversations can be opened or restored from their context menu.
- Delete confirmations explain that catalog entries are removed and affected owned runtimes are closed. Project working folders and Pi session files remain on disk. Removing a project includes all its active and settled conversation entries. Failed catalog writes leave visible entries unchanged; deleting the current selection returns to a valid project or the empty workspace.
- Rename, settle, restore, and delete reread under the existing catalog lock and commit atomically. Mutations preserve unrelated metadata, IDs, session paths, and records. Group updates preserve surviving row instances instead of clearing the entire visible list.
- The Projects heading is a plain label; the user preferred removing its duplicate plus action. The existing top New project button opens the modal. Context menus use native keyboard/mouse support; inline editors use native TextBox controls.
- `Views/MainPage.xaml` contains the native SplitView shell, project groups, draft entries, and contextual empty states. `ShellViewModel` owns loading, selection, and errors; views do not access persistence.
- The first saved project starts expanded. Newly created projects open immediately. Collapsing the active project's group preserves its selected conversation.
- The modal uses ContentDialog with XamlRoot, asynchronous save deferral, field validation, and preserved input on failure. FolderPickerService binds the Windows picker to the main window. Cancelling the dialog or picker does not create a project.
- Successful creation updates the shell before completing the modal's close deferral. Updating only after ShowAsync completes exposed the old workspace during dismissal. In narrow layouts, creation also closes the sidebar overlay to reveal the destination.
- Initial workspace content stays hidden until the catalog is resolved. Reload builds a complete project collection before publishing it and restores project/conversation selection in one step, avoiding intermediate empty-list and project-only states.
- New conversation persists a local entry, selects it, and prepares its own Pi runtime automatically. Startup errors remain local to that conversation; Retry reopens it without resending prompts. Switching away retains the runtime and composer state.
- Sidebar defaults to 280 effective pixels, resizes between 220 and 420, and collapses below a 140-pixel drag threshold into a 48-pixel rail. A root-relative pointer grip stays reachable when collapsed. Arrow keys resize; Home collapses; End expands. The last expanded width is remembered within the window lifetime.
- Below 760 effective pixels, SplitView uses CompactOverlay and closes on conversation selection; wider layouts use CompactInline. Width is capped against available content space.
- Native theme resources follow the system light/dark/high-contrast theme. Shared component styles are in App.xaml; design intent and resource conventions are in `.ui-craft/brief.md` and `.ui-craft/tokens.md`.
- The native title bar opts into `TitleBarTheme.UseDefaultAppMode` at window creation. Its default `Legacy` mode does not follow the XAML content's dark theme. Windows continues to own caption buttons, dragging, and system theme changes.
- Window branding uses `Pi Agent`, changing to `Pi Agent — <conversation title>` when a conversation is selected. Shell selection notifies `WindowTitle`; App updates the native title. `Assets/Pi.ico` is embedded in the executable and copied for `AppWindow.SetIcon`, derived from Pi's official compact badge (source and attribution in `Assets/README.md`).
- The user approved the Install Pi screen; its temporary forced-missing preview has been removed, restoring actual installation detection.
- Persistence operations run off the UI thread because directory validation and file-lock acquisition can block on unavailable drives. Presentation changes resume on the UI context.
- Persisted models are not exposed as public XAML view properties. WinUI's generated activators otherwise try to construct required-member records without their required fields.
- Pending interactive checks: window startup, modal/folder picker, keyboard focus, actual theme switching, pointer dragging across collapse/expand, and restart persistence through the UI. Unit tests cover storage and presentation state but cannot replace these checks.
- Transition-fix verification: 31 tests pass. x64 build succeeds with zero warnings/errors using `-p:OutDir=artifacts/verification/` to avoid disturbing the user's running app. No process was stopped or launched by the agent.

## Agreed initial UI

- Place projects in a left sidebar and the selected conversation in the main content area.
- Render each project as a collapsible group header with its conversations listed underneath. The first project is expanded by default.
- Place the New project action at the top of the sidebar. It opens a modal with a project name and existing-directory picker, plus create/cancel actions and validation feedback.
- Place a New conversation action on each project header. Activating it must not also toggle the group's expansion state.
- Support both light and dark styling, following the Windows system theme automatically. A manual theme selector is outside the initial scope.
- Make the sidebar resizable and collapsible, with an explicit collapse/expand control.
- Support dragging the sidebar width past a collapse threshold and dragging an accessible edge outward to expand it again. This is the initial interpretation of resize-to-collapse/expand; exact widths and thresholds are implementation details.
- Provide keyboard-accessible alternatives for resizing and collapse/expand, visible focus states, and accessible labels for icon actions.

## MVP flow

1. Create a project by entering a name and selecting an existing directory.
2. See saved projects and open one.
3. Start a conversation within that project.
4. Send a prompt and display the streamed response.
5. Display tool activity and results.
6. Cancel an active response and show clear startup, protocol, and process errors.

The project shell and first runtime slice are implemented. The user explicitly included concurrent conversations in the runtime pass. Advanced session browsing, authentication settings, and extensive visual polish remain deferred.

### Conversation composer

- Composer selectors share a horizontal row with inline labels, such as Model: name, Effort: low, and Approval: auto. Model selection resolves a provider/ID-matched list index. The view synchronizes the native ComboBox explicitly: assign the stable ModelOptions text snapshot first, then SelectedIndex, on presentation changes and control load. A guard prevents these programmatic updates from issuing model commands. Separate ItemsSource/selection bindings and native-text rendering alone both left the startup placeholder visible in user testing; the user confirmed ordered control synchronization fixes startup selection. A single circular icon button sits inside the input's bottom-right corner; it sends while idle and stops while running, with matching tooltip/accessibility text. Input padding reserves space so text cannot overlap the action. On initial extension discovery without a saved `modes` entry, explicitly request `/mode auto` and verify the persisted result before exposing controls. Existing saved modes, including unknown future values, are never replaced by the default. Refresh Pi model state after defaulting because extension profiles can select a model.

- Enter sends; Ctrl+Enter inserts a newline at the selection. Shortcuts are scoped to the composer, with no visible hint. IME composition is left to the native text control.
- Keep the title/path header compact and remove the separate Ready/provider/model header.
- A native model selector lives below the message input. Startup loads `get_available_models`; `get_state` supplies the current model. `set_model` changes only this conversation and its successful response supplies the confirmed selection. Switching is disabled while busy, failures preserve the confirmed model, and Pi owns persistence.
- A native Effort dropdown uses `get_available_thinking_levels`, `set_thinking_level`, and `get_state.thinkingLevel`. Pi owns per-conversation persistence. New session files default to low when supported, after approval profile initialization; existing sessions retain their saved choice. Model changes refresh supported levels and Pi's clamped selection. Editing is disabled while busy or when only one level is supported. Fast mode remains deferred.
- Approval modes use the optional global Permission Modes extension; Pi has no built-in tool-approval modes in RPC. Project-trust CLI flags are not tool permissions.

### Global supported extensions

- Windows discovery follows Pi/Node's `USERPROFILE` home (falling back to the account profile only if unset), and expands `~` in `PI_CODING_AGENT_DIR`. The Windows known-folder profile can differ from inherited `USERPROFILE` under alternate-account launches, causing false Not configured results. Installation status verifies the global npm manifest separately from runtime loading. GridView cards explicitly bind their data context to the page view model.

- Extension entries use native cards in a scroll-owning GridView: three columns from 960 effective content pixels, two from 640, otherwise one. The page has no back button; selecting a sidebar conversation restores the workspace.

- Extensions opens from the sidebar footer (also accessible in the collapsed rail) and returns to the retained workspace. Opening it does not dispose or stop conversations. Setup is a shared native modal reachable from this page or the unavailable-looking, keyboard-accessible composer button.
- First integration: `@georgedong32/permission-modes@2.6.3`, explicitly labeled preview pending live Windows validation. Installation instructions use `pi install npm:@georgedong32/permission-modes@2.6.3` and an app restart; the GUI does not execute an installer or edit Pi settings.
- The page checks only global npm package configuration in `settings.json` under `PI_CODING_AGENT_DIR` or the default user agent directory. It does not claim that configuration proves successful loading. Local/Git installations are not detected in this first pass.
- Each `ConversationSession` owns a `PermissionModesIntegration`. It checks `get_commands` for the extension's `mode` command, verifies its package name/version from the manifest, and accepts only user-scoped sources (legacy RPC uses known global paths). Unsupported versions, missing commands, and failed entry queries keep the setup control visible.
- `get_entries` reads the latest `custom` / `modes` entry's `data.currentMode`, matching this extension's append-order restore behavior. No terminal footer parsing, extra bridge, Pi session writes, or guessed default mode. A session without a mode record requests Auto and waits for confirmation. Commands use `/mode <mode>` through RPC, recheck the command before invocation, and query state after completion. Normal slash commands and settled runs also refresh extension state. Modes remain per conversation despite global installation.
- The extension may apply its configured model profiles on a mode switch; the GUI rereads `get_state` to show the actual session model. GUI model selection otherwise uses only the selected session's `set_model`; current Pi RPC does not persist these changes as global defaults. This is covered by two-process isolation/rejection tests.
- Upstream references: https://github.com/GeorgeDong32/pi-permission-modes ; https://pi.dev/docs/latest/rpc ; https://github.com/earendil-works/pi/blob/main/packages/coding-agent/src/core/agent-session.ts .

## Implemented Pi runtime and concurrency

- Before opening the project shell, `StartupViewModel` checks installation files asynchronously through `PiInstallationLocator`, also used by the process factory. Missing Pi or a missing Node.js runtime for npm installs shows a full-window Install Pi screen linking to `https://pi.dev/` and asking the user to close/restart after installation. Detection does not execute Pi or verify authentication/protocol compatibility. Closing during the check does not open the shell afterward.
- `App.xaml.cs` composes `ConversationWorkspaceStore`, a factory for `ConversationSession`, `PiRpcClient`, and `ProcessPiTransport`, and a WinUI dispatcher adapter. One store per window retains a `ConversationViewModel` per `(projectId, conversationId)`.
- Listing projects creates lightweight workspace objects, not processes. First selecting a conversation connects it. Switching selection does not disconnect any conversation. Several conversations can stream simultaneously, including within one project; they share that project's working directory, not separate Git worktrees.
- Each connected conversation owns a separate hidden process and RPC client. There is no shared `switch_session` process. Internal disconnect closes only that process; window shutdown disposes every workspace concurrently. Cleanup closes stdin and uses bounded exit waits, terminating only the owned process tree when necessary.
- Connection management is an implementation detail: the normal interface has no Connect/Disconnect controls. The conversation area shows Loading conversation until startup and history are ready, then reveals the transcript and composer together. The sidebar and extension questions remain usable during startup. Already prepared conversations do not reload on selection; startup or runtime failures show a local recovery surface with Retry. Active turns stay in the chat view with status and Stop. Model dropdown work is explicitly deferred until the basic loop is complete.
- Session path: `%LOCALAPPDATA%\PiAgentGui\sessions\<projectId:N>\<conversationId:N>.jsonl`, derived by `PiSessionPaths`. Launch arguments pass `--mode rpc --session <absolute-file> --session-dir <parent>`, with the project's folder as the working directory. Pi creates and maintains the JSONL file; an empty conversation may have no session file until Pi persists its first assistant turn.
- The same path is reused after restart or reconnect. The catalog schema is unchanged; no session paths or messages are stored in arbitrary metadata. A lifetime `.jsonl.lock` file opened with `FileShare.None` prevents competing app windows from opening the same session. This is an application lock; external Pi invocations do not honor it automatically.
- Startup queries state and history, checks the returned session path, and displays Pi's active-context messages. `get_messages` is not a browser for pre-compaction history or abandoned branches. The GUI never edits Pi's session files.
- `PiProcessStartInfoFactory` resolves a native `pi.exe`, or the npm package's `dist/cli.js` with `node.exe`, from PATH and the usual per-user npm location. Absolute overrides: `PI_GUI_PI_EXECUTABLE` and `PI_GUI_NODE_EXECUTABLE`. Arguments use `ProcessStartInfo.ArgumentList`, never a shell command string. Setup instructions and official references are in `PI-SETUP.md`.
- Protocol framing splits only on LF, strips one trailing CR, and rejects incomplete/oversized records. Stderr is drained separately without persisting potentially sensitive content. RPC request IDs correlate replies; a single reader orders snapshots and events before UI dispatch.
- `PiTranscript` projects indexed text deltas and authoritative final messages. Accumulated tool output replaces the previous partial result. Tool arguments, results, and failures are visible; text is selectable. Basic Markdown and fenced code panels are rendered natively. Syntax highlighting, thinking blocks, and attachments remain outside this pass.
- `agent_end` alone does not mark a conversation idle: automatic retry or compaction may follow. Busy state survives until `agent_settled` or a completed explicit abort. Accepted extension commands that do not start a turn reconcile through `get_state`.
- Stop sends `clear_queue` then `abort` to the selected conversation only. Prompt rejection preserves the draft; uncertain acceptance never causes automatic replay. A successfully acknowledged prompt clears its submitted draft even if subsequent state reconciliation fails.
- Extension confirm/select/input/editor requests appear inline in their owning conversation and require explicit replies. Timed-out questions expire, and disconnection removes pending questions. Notifications/errors are surfaced; optional TUI widgets/status customization are not implemented.
- View models retain each conversation's composer and transcript while background updates are marshalled in bounded batches through the UI dispatcher. The sidebar shows running activity and pending-question/error indicators. The virtualized transcript follows the tail while the user is near the bottom.
- Verification covers framing, request correlation, rejection/exit/timeout/disposal, text/tool projection, concurrent session isolation, stop targeting, reconnect history, handshake cancellation, extension questions, and workspace/draft isolation through fake transports. These tests never start Pi or contact a model provider.
- Current verification: 69 unit tests pass, including sidebar persistence, settle/restore, deletion scope/runtime cleanup, inline rename recovery, installation discovery, and loading/retry/selection behavior; x64 build succeeds with zero warnings/errors using `-p:OutDir=artifacts/verification/` so the user's running build is untouched. The new sidebar interactions still need live UI verification.

## Proposed code boundaries

Start with one application project and one unit-test project. Introduce additional assemblies only when a concrete need justifies them.

| Area | Responsibility |
| --- | --- |
| Views and controls | XAML layout, rendering, and view-specific interaction |
| View models | Presentation state and user commands |
| Project service | Project creation rules and directory validation |
| Project repository | Local project metadata persistence |
| Conversation service | Conversation lifecycle and translation of Pi events into application state |
| Pi RPC client | Protocol serialization, request correlation, responses, and events |
| Pi process transport | Owned process lifecycle, asynchronous stdin/stdout, stderr, and exit handling |
| Models | Separate application models and Pi protocol DTOs |

Use constructor injection and compose dependencies at application startup. Create folders and interfaces when their implementation or test boundary is needed, rather than scaffolding empty layers. Keep reusable utilities in focused, separate files.

## Runtime boundaries

- Starting a conversation may start Pi; opening the project list must not.
- Give each conversation a dedicated runtime owner retained by the workspace store. Selection changes only the visible workspace.
- Process events through an ordered path and marshal UI-bound changes onto the UI dispatcher.
- Never block the UI thread on process communication.
- Keep stderr diagnostics separate from stdout protocol traffic.
- Distinguish local request cancellation from cancelling agent execution.
- Do not automatically replay side-effecting requests after a failure.
- Unit-test protocol behavior through a fake transport without launching Pi or contacting model providers.

## Implementation order

1. Define the project model, validation rules, persistence format/location, and failure handling. Unit-test meaningful project behavior.
2. Build the native sidebar and modal create-project flow using MVVM, including grouped conversations, system light/dark themes, and sidebar resizing/collapse; verify persistence across repository instances.
3. Consult current Pi RPC documentation and resolve process startup, session identity/resumption, cancellation, and event semantics.
4. Implement transport and RPC behavior with focused unit tests.
5. Connect a minimal conversation interface and verify streaming, tool activity, cancellation, and process failures.

Launching the GUI or Pi requires user permission under `AGENTS.md`; unit tests and ordinary builds are permitted.

## Decisions to resolve during implementation

- Supported extensions will be managed globally, per user, rather than through project-level UI. A curated Extensions page should explain supported features and installation. Missing-feature visibility is decided per integration; an unavailable-looking approval control may open installation guidance while remaining keyboard accessible.
- Feasibility checked 2026-09-10: Pi installs packages globally by default (`pi install`, without `-l`). RPC exposes registered extension commands via `get_commands`, invokes them via `prompt`, and transports standard extension dialogs/status updates. Global installation still requires runtime availability checks because a package can be disabled, overridden, or fail to load. Keep that check internal without adding project configuration UI.
- The preview integration now uses `get_entries` to read the extension's persisted mode; its terminal-only footer is not used. Live Windows/RPC verification remains outstanding. No extension was installed by the agent.
- References: https://pi.dev/docs/latest/packages ; https://pi.dev/docs/latest/rpc ; https://github.com/GeorgeDong32/pi-permission-modes/blob/master/index.ts .

- UI recovery flow for renamed or unavailable directories, including any future change-path action.
- Pin and verify a concrete installed Pi release after live testing. The current client targets the official RPC documentation checked on 2026-09-10, including delta-only message updates and `agent_settled`.
- Future authentication settings UI; model and effort selection use Pi's capabilities and existing credentials.

Protocol and CLI behavior were checked against current official Pi documentation and upstream session-manager source on 2026-09-10. See `PI-SETUP.md` for links and the remaining live smoke test.

## Transcript presentation

- User messages use right-aligned theme-aware bubbles; assistant text remains unboxed on the left. Hide You/Pi labels but preserve tool/session headings and error status.
- ChatEntry carries explicit user/assistant and completion flags from Pi message events. Copy prompt is available immediately; Copy response appears after message_end (including stopped/error messages with text) and for restored responses. Copy uses the full plain-text message, without speaker/status/tool arguments.


## Header editing and path privacy

- Shell HeaderRename has separate edit state from sidebar Rename and reuses RenameConversationAsync persistence, keeping sidebar/header/window titles synchronized.
- ProjectPathDisplay formats redacted drive/folder locations, including UNC paths. ProjectPathButton reveals only after activation, resets after five seconds, on blur/unload, and on shell navigation. Sidebar tooltip uses ProjectItemViewModel.RedactedPath. The underlying project working directory is unchanged.


## Sidebar runtime indicators

- Far-right dots: red for pending extension prompts, blue for running, green for an unseen completed turn. Selected conversations hide all dots; selection acknowledges completion. Pending approval/running state reappears on leaving if still active.
- ConversationUpdate.TurnCompleted is emitted only for Pi agent_settled; ordinary idle/startup/disconnect snapshots do not imply completion. ConversationViewModel owns transient completion acknowledgement; no catalog persistence. Errors suppress green. This is separate from the user-managed Settled group.


- Effort selector also synchronizes ItemsSource before SelectedItem on presentation updates and load, with programmatic-change guarding. For capabilities containing only off, display off and disable editing even if Pi retains a reasoning preference in session state. Other selections remain sourced from get_state. Tests cover off and separately arriving state/capability updates.


- Approval uses the same explicit options-before-selection synchronization on load, selected conversation changes, and approval availability/mode updates. Programmatic synchronization never sends approval commands; the extension's confirmed mode remains authoritative.


## Automatic conversation titles

- Extensions page also lists other global extensions from configured npm/Git/local packages and auto-discovered extension files/directories. Inventory reads manifests only, deduplicates paths, and omits supported packages and packages without extension resources. It reports disk presence, not enabled/loaded status; project-local installations remain outside this global view.

- SupportedExtensions defines third-party author attribution, supported version, descriptions, source/documentation links, install/configuration examples, and extension-specific detection. ExtensionCardViewModel and a single card template provide shared refresh/setup behavior. Details is always available; setup hides after installation. Both use the same scrollable ExtensionSetupDialog, including copyable command/JSON panels. The page and dialog explicitly distinguish independent extension ownership from Pi Agent's integration support.

- Recommend the existing global pi-auto-session-name 0.1.1 extension through the Extensions page. No custom naming model call or bundled naming extension is added. Its separate provider/model config lives in ~/.pi/agent/extensions/auto-session-name.json; the upstream default is openai-codex/gpt-5.4-mini, so users should select an authenticated available model there.
- Pi 0.85.1 forwards session_info_changed through RPC; the GUI consumes its name and get_state.sessionName and persists generated titles in the project catalog. New drafts opt into naming. IsTitleManual defaults true when absent to protect legacy titles; explicit user rename persists that flag, sends set_session_name to an existing process, and supplies --name on future launches. Generated-title writes check the manual flag under the catalog lock. Manual and automatic title updates are serialized in the shell.
- The upstream extension skips named sessions, rechecks before applying its result, and aborts on session_info_changed. Source checked for version 0.1.1. Installing or executing it remains a user setup step; live naming has not been tested.

## Fork and clone

- Fork and Clone appear beside Copy only under the latest successful completed assistant response. They hide while a run is active and disable during an in-flight copy. Restored completed conversations support the same actions.
- Both create an independent conversation in the same project containing the saved history, tool results, compaction, and persisted settings. Fork opens it; Clone leaves the original selected. If selection changes while copying, completion does not steal focus. Draft text remains on the original. These actions copy conversation state, not project files or Git working trees.
- `PiExtensions/session-copy.ts` is registered through the existing bundled `write-diff.ts` entry point. Its internal `pi-gui-copy-session` command uses Pi's public `SessionManager.forkFrom`, selects the current leaf on the new manager, and writes an explicit copy title. It never switches or mutates the live source manager. `SessionCopy.ts` atomically publishes the Pi-generated file to the new stable conversation path using a same-volume hard link and removes its private temporary directory. Existing destinations cannot be overwritten.
- The C# session checks command availability before dispatch so a missing integration cannot become an ordinary model prompt. Both C# and the extension restrict copies to a new GUID filename beside the source. Source operation guards prevent concurrent send/model changes. No extra Pi process or provider request is used for copying; the copied process starts when that conversation is opened.
- The shell registers the new catalog entry only after the session file exists. Catalog validation rechecks the source and new identity. On registration failure, the sidebar remains unchanged and the file is retained for recovery; no retry or deletion follows an uncertain commit. New titles use `original · fork` / `original · clone`, with numeric suffixes as needed, and are protected from automatic naming.
- Fork at arbitrary earlier messages, import, session settings, and branch navigation are outside the agreed scope. Providers will be designed separately. SDK behavior was checked against Pi 0.85.1; fake-RPC, persistence, and helper unit tests cover this implementation, but live Pi/GUI verification remains pending.

## Global Providers page

- Providers opens above Extensions in both sidebar states, using a responsive one/two/three-column native card grid. Configured providers sort first; cards show status/source, model count, Details, and Set up/Manage. Model details include context/output limits, reasoning, and image support.
- `Services/Pi/ProviderService` owns a short-lived, separate RPC process per serialized operation. Its neutral working directory is `%LOCALAPPDATA%\PiAgentGui\management`; it uses `--no-session`, `--no-tools`, `--no-extensions`, `--no-skills`, `--no-prompt-templates`, and `--no-context-files`, with only the explicit bundled `PiExtensions/providers.ts`. No project, transcript, session lock, or model call is created. Cleanup covers success, failure, cancellation, deadlines, and app close.
- The integration uses Pi's public ModelRuntime for discovery/login/logout. `ProviderManagement.ts` selects non-secret metadata; credentials remain in Pi's existing global store, respecting its agent-directory override. SDK credential results and raw provider errors never reach the GUI. This pass supports built-in providers and models.json configuration; providers registered exclusively by third-party extensions are not loaded in the isolated management process.
- The native setup modal offers supported sign-in/API-key methods, sign-out only for a saved credential, and documentation for ambient-only setup. Secret/manual-code fields use PasswordBox. Browser links, device codes, text/select prompts, progress, and per-prompt abort are supported. Values travel through transient extension UI replies, never slash arguments or chat messages. Browser callbacks can dismiss manual input without blocking the event reader.
- The service verifies its internal command with get_commands before invocation. Missing integrations cannot fall through to a model prompt. No retries occur after uncertain mutations; a credential committed before failed synchronization gets an explicit refresh notice. Closing setup cancels its operation; refresh after uncertain cancellation/failure.
- Credential changes invalidate all app-owned model snapshots. Idle conversations refresh through internal `pi-gui-refresh-models`; running/busy conversations defer until idle. Refresh does not change selected models or drafts. Restart after upgrading to load the new bundled commands in existing runtimes.
- Configured is not verified access. Sign-out removes Pi's saved credential; environment/configuration may still provide access. Pi supports one saved credential per provider and has no generic account identity/subscription allowance interface.
- Verified using C# fake-transport/presentation tests and Node helper unit tests. No GUI or real Pi/auth flow was launched by the agent; live browser/device-code/API-key verification remains pending. API source checked against Pi v0.85.1.

## Command picker

- The command picker recognizes a slash token at the caret anywhere in a draft, after whitespace or at the beginning. Embedded path/URL separators and selections do not trigger it. It filters names/descriptions, labels command origins, supports arrows, Enter/Tab, and Escape, and scrolls a bounded 240px list without dropping commands.
- Native command mappings cover session details, compact (optional instructions), HTML export through the Windows save picker, model, thinking, copy, new conversation, header rename, fork, clone, and Extensions. Native TUI-only commands are intercepted with an unavailable notice rather than passed to the model. Providers are deferred to a dedicated design; import, session settings, and branch navigation are not currently planned.
- `get_commands` discovers extensions, skills, and templates from the current conversation's Pi process. Results are refreshed when reopening the picker and discarded on conversation/reconnection changes. Source filesystem paths are not shown. Native names win collisions. Extension commands get an argument dialog and run through `prompt`; Pi's existing confirm/select/input handling remains active. Terminal custom UI still needs a supported native integration.
- Selecting a native or extension action removes only its slash token after success and preserves surrounding draft text. Skills/templates are moved to the start of the draft for Pi's prefix expansion and are not automatically sent. Typed leading native commands use the same handler as picker selection, including the Send button.
- `/auto-name` is offered only when discovered from Pi. Explicit regeneration can replace a manual name and saves the result as an explicit title; automatic background naming still cannot overwrite manual titles. The existing third-party naming extension remains responsible for generation.
- Conversation operations are allowlisted behind `IConversationSession`; none switches the workspace's Pi session. Manual compaction reconciles `get_state` after success/rejection, permits Stop while waiting, and has a ten-minute response timeout. No operation is automatically replayed. Session details format unavailable values honestly and hide absolute session paths.

## Markdown presentation

- Compaction is a typed presentation state (`ConversationUpdate.IsCompacting`). The existing processing row, below the latest user prompt, shows a native small spinner and `Compacting context…`; compaction_end returns to ordinary processing, and idle/disconnect clears the real state. Changes refresh the stable row even when IsRunning stays true. Empty conversations can display the status without overlapping the empty-state text.
- The user approved the compaction UI; the forced preview has been removed from App composition. Normal Processing and Compacting context share the same small native spinner. Only real runtime activity shows the row; preview injection remains available to presentation tests and defaults to false.
- Tool rows append tokens only when Pi reports `toolResult.usage` or final tool-result usage. Shared PiTokenUsage parses input/output/cache-read/cache-write totals; no estimate is inferred from tool output size. Usage survives final tool event ordering and Pi history reload. Ordinary filesystem/shell tools generally have no reported model usage and show no token count.

- Completed live runs display reported token consumption and elapsed wall time beside the final assistant response's Copy/Fork/Clone buttons. `RunUsageTracker` starts at agent_start, sums final assistant usage and optional tool/compaction usage, and finalizes at agent_settled. It ignores streaming snapshots and deduplicates message identities. Total includes input, output, cache reads/writes; missing assistant usage is unavailable rather than estimated from text. Time includes tools and approval waits, formatted as seconds below a minute or minutes/seconds above it.
- The usage label is attached only on completion and remains on its response during later runs/sidebar navigation. These are live presentation measurements, not duplicated Pi message persistence: reconnect/history reload or app restart clears them. Historical timings are not inferred from message timestamps. Fault/disconnect resets unfinished tracking.

- PromptNavigationRail displays one marker per user entry beside the transcript. It renders at most 50 markers per page, defaults to the newest page, and offers older/newer paging without discarding prompts. The rail scrolls independently on short windows; new messages preserve an older page being browsed. Native tooltips preview up to 600 characters; accessible buttons jump via ListView.ScrollIntoView and pause tail-following. Marker hover/focus expands the line over 160 ms, respecting system reduced-motion settings.

- Streaming retains the Markdown host and unchanged top-level blocks, updates growing paragraphs/code controls in place, and scrolls directly to the tail after layout (including delayed Markdown size changes). Summary diffs are created on first expansion, retained across collapse/reopen, and use a 360px maximum viewport to bound expanded layout. Closed error rows no longer accumulate large header-to-transcript gaps.

- Markdig 1.3.2 parses CommonMark. Controls/MarkdownMessage coalesces streaming updates at 100 ms; MarkdownRenderer creates native headings, paragraphs, emphasis, links, lists, quotes, dividers, inline code, and CodeBlockView panels. HTML is inert text; images are not fetched. Only HTTP(S)/mailto links are navigable.
- Code panels show fence info (language/filename), horizontally scroll long code lines, and copy code without fences using the existing confirmation button. Whole-response copy retains original Markdown.
- CodeBlockView renders TextMateSharp.Grammars 2.0.4 tokens as native text runs, with light/dark syntax colors and system text in high contrast. The official Svelte grammar from svelte.svelte-vscode 110.3.1 is bundled under Assets/Syntax/Svelte with its MIT license; no extension code executes. Language IDs, common fence aliases, and filename extensions select grammars. Highlighting is debounced and runs off the UI thread; unchanged lines retain tokenizer state. Limits (64,000 characters, 2,000 lines, 8,000 characters per line, 6,000 runs, and a processing budget) fall back to exact plain text. Copy always uses the original code.
- Do not subscribe to AccessibilitySettings.HighContrastChanged from individual code boxes: on this unpackaged desktop configuration its WinRT event registration throws COMException 0x80070490 during Loaded. Read HighContrast inside the guarded rendering path and refresh through the existing WinUI Loaded/ActualThemeChanged lifecycle instead.
- TranscriptPresentation projects DisplayEntries separately from raw Entries. More than three consecutive tools become a stable expandable group (Called N tools); empty assistant tool-only messages do not split it, visible messages do. Expansion survives new tools/results. Individual tools use compact disclosure rows; arguments and output are collapsed initially.
- Permission Modes 2.6.3 config.ts defaults auto classifier to anthropic/claude-haiku-4-5, enabled and fail-closed. The user has no global permission-modes.json, so this explains the reported separate Haiku request. It is independent of the conversation model; no configuration was changed in this pass.


## Composer screenshots and file references

- Pasting a clipboard bitmap attaches a removable preview to that conversation's in-memory draft. Image-only prompts are allowed. Successful prompt acceptance clears submitted attachments; a failed send retains them. Unsent screenshots are not persisted across application restarts.
- ClipboardScreenshotReader normalizes screenshots to PNG, scales the longest edge to at most 2,560 pixels, and limits each encoded image to 2 MiB, with four images per prompt. PiImageContent sends the documented RPC `images` array. Pi owns image persistence in its session JSONL; PiTranscript restores image blocks from history. ScreenshotPreview decodes images only while mounted. The RPC record limit is 128 MiB to accommodate image-bearing history responses.
- Typing an `@` token anywhere in the composer opens a debounced file picker. It searches up to 8,000 project entries, returns up to 30 matches, skips common generated directories and directory symlinks, and supports arrow keys, Enter/Tab, and Escape. Browse uses the native file picker and permits files outside the project.
- Accepted references become quoted absolute paths in the prompt. They ask Pi to read the referenced local file through its tools; the GUI does not upload or embed file contents. Screenshot attachments are kept separate from slash-command execution.

## File-change preview implementation

- The latest live run gets an edited-files card in the transcript footer only on TurnCompleted (Pi agent_settled). It clears when the next run starts or history reloads. It groups successful captured write/edit results by file, totals additions/removals across operations, initially shows three files, and expands each file to review its captured patches in order. Counts are edit totals rather than a net working-tree diff; missing patches are flagged. Shell-driven changes are not included. Completion boundaries are not persisted or inferred from old message history.

- Pi's write result has no baseline or diff. PiExtensions/write-diff.ts is bundled and passed explicitly through --extension for each GUI-owned process. It delegates schema/prompt metadata/execution to Pi's public createWriteToolDefinition. WriteDiffCapture reads the baseline inside Pi's serialized writeFile operation, writes with the same Node API, then uses Pi's generateUnifiedPatch. Approval tool hooks continue to apply; no global extension installation is performed.
- Result details.piGuiFileChange persists path/patch/availability in Pi-owned sessions. The model-visible success content is unchanged. Capture is limited to UTF-8 text up to 256 KiB and 5,000 lines per side; unavailable or failed preview generation does not block an otherwise successful write. Writes still propagate their real errors.
- FileChangeParser also accepts native edit details.patch; PiTranscript retains assistant tool-call arguments for saved-history filenames. FileDiffView shows a selectable colored unified patch, and tool summaries include file plus added/removed counts. Legacy writes lacking baselines report unavailable rather than infer a diff from current disk state.
- Restart the GUI to load the bundled extension into new Pi processes after this upgrade. Pi 0.85.1 public extension exports were checked; no Pi process was launched during verification. Node capture unit tests: node --test Tests/PiExtensions/WriteDiffCapture.test.ts.

