# Pi desktop architecture

Current implementation and product decisions, reviewed 2026-09-12. Start at [README.md](README.md) for setup and the documentation index.

## Product and runtime ownership

Pi desktop is an unpackaged WinUI 3 / C# Windows frontend for Pi. The integration is `WinUI / C# → Pi RPC → Pi`: each connected conversation owns an independent Windows Pi process, RPC client, and session file. There is no separate Node bridge or Pi fork. Bundled TypeScript extensions use Pi's public interfaces where native RPC alone is insufficient.

Pi owns model interaction, tool execution, transcripts, context, and compaction. The app owns project associations, execution targets, presentation, and optional integrations. Selecting another conversation never switches a shared Pi process or stops another conversation. Opening a conversation prepares its runtime automatically; failed startup offers retry without replaying prompts. Shutdown stops app-owned processes, with confirmation when agent/research work is active.

Use MVVM: views handle native presentation; view models expose state and commands; services own transport, persistence, and integrations. Dependencies are composed in `App.xaml.cs`. Async I/O stays off the UI thread and presentation updates use the dispatcher. `Tests/PiAgentGui.Tests.csproj` links UI-independent code for tests without WinUI startup.

## Projects and execution targets

A logical project has a stable ID, display name, metadata, conversations, and named Local Windows, WSL, or Linux SSH targets. Each target has its own workspace path. Each conversation retains its selected target; changing the project default affects only new conversations. Legacy projects/conversations resolve to their original local target.

Creation supports an existing folder or explicit Git clone into a new destination, including WSL-first and SSH-first projects without a Windows checkout. Failed clones retain partial files. Targets can be added and their default changed; editing/removing targets or moving an existing conversation is not currently offered.

Pi and sessions remain on Windows. The bundled target extension delegates workspace tools to WSL/SSH, loads target ancestor instructions, and requires a matching target acknowledgement. Unavailable targets never fall back to Windows execution. Separate checkouts do not synchronize automatically; conversations on the same target folder share working files. There are no automatic worktrees.

See [EXECUTION-TARGETS.md](EXECUTION-TARGETS.md) for prerequisites, authentication, cancellation, native-panel limits, and verification scope.

## Persistence and data lifecycle

Application data lives under `%LOCALAPPDATA%\PiAgentGui`, separate from source folders and the installation directory.

| Data | Location / ownership |
| --- | --- |
| Project catalog | `projects.json`; stable project/conversation/target IDs and metadata, no transcript copies or secrets |
| Pi sessions | `sessions/<project-id-N>/<conversation-id-N>.jsonl`; Pi writes transcript content, including submitted images |
| Confirmed selectors | `<session>.settings.json`; approval/model/effort restored on reconnect |
| Session ownership | `<session>.lock`; the OS lock determines ownership, not file presence |
| Deferred deletion | `deleted-conversation-data`; retry intents for incomplete conversation cleanup |
| Model favorites / editor / onboarding | `model-favorites.json`, `open-in.json`, `onboarding.json` |
| Background research | `research/`; app-owned opt-in worker tasks/results and preference |
| Terminal WebView2 profile | `WebView2/Terminal`; GUI does not deliberately persist terminal output |

The versioned JSON catalog uses an exclusive lock, reread-before-mutation, and adjacent temporary-file replacement. Lock contention waits up to five seconds with cancellation. Invalid catalogs fail visibly without replacement; unknown metadata is preserved. Target paths follow their native OS rules.

Deleting a conversation stops its runtime and removes its session, settings, and unused checkpoint data. Project deletion applies this to its conversations, preserving source folders and independent copies. Deletion intent is saved before the catalog mutation; cleanup only runs once the identity is absent. Offline target cleanup retries on a later catalog load. Pending restore recovery is protected. No broad user-folder/orphan sweep runs. Settling a conversation only organizes the sidebar and preserves its data/runtime.

Checkpoint bytes live on each target under `$HOME/.pi-desktop-checkpoints`, outside the workspace. Keep the latest five completed checkpoints per conversation; active/recovery/Undo data receives protection, with the documented 30-day age limit for completed/reverted records. See [CHECKPOINTS.md](CHECKPOINTS.md).

## Implemented product surfaces

- Native project/conversation sidebar with rename, settle/restore, delete, target details, resize/collapse, keyboard access, and system light/dark/high-contrast themes.
- Streaming Markdown and tool output, extension questions, Stop, steering/follow-up queue, screenshots/file references, command picker, session details/export/compaction, and conversation fork/clone. Fork/clone copies session history; it does not branch source files.
- Processing elapsed time in seconds, switching to minutes/seconds after one minute. The empty-conversation prompt is hidden while processing.
- Searchable provider/model picker with global favorites and per-conversation confirmed model/effort/approval preferences.
- Providers page with Pi-owned sign-in/API-key flows and non-secret status. Credential changes refresh idle runtimes and defer busy ones. Configured credentials do not prove valid access. Ollama imports tool-capable models from local or explicitly connected endpoints at app startup or explicit Connect and sync; it does not download or run models.
- Global Extensions page for supported packages and bundled opt-ins, plus skills/MCP inventory and supported setup. Remote third-party I/O is restricted as described in the target guide.
- **Workspace checkpoints and deterministic revert: done.** Opt-in Manage dialog, Clear stored data, Created/Modified/Deleted summaries, selective revert, conflict checks, Undo, interrupted-write inspection, and automatic cleanup. Ask agent to revert is removed; unavailable recovery has no agent-assisted fallback.
- Target-aware terminals, run scripts, file browsing and source control, with documented local-only capabilities. Scripts execute only on explicit Run; saved definitions do not schedule or resume work.
- Local GitHub PR discovery/device login and Open in integration. See [GITHUB-SETUP.md](GITHUB-SETUP.md).
- Optional background research: at most two concurrent one-shot workers, twelve active/queued tasks, twenty-minute timeout. Results stay in the panel until the user copies or adds them to a draft. Shutdown interrupts workers; restart never replays them. This is distinct from durable background execution.
- **Docker extension and panel: done, accepted by the user.** Optional integration under Our extensions: Windows/WSL CLI detection, explicit addition of saved SSH targets, and a conversation tab with container status and Start/Stop. Manage owns source removal and project links. Unlinked containers appear in every project; full container IDs share links and avoid duplicate rows when Windows and WSL reach the same engine. This is a native GUI integration, not a Pi tool or npm extension.

## Integration and safety boundaries

**Global Home and local usage analytics: done, accepted by the user.** Home navigation sits at the top of both sidebar layouts. The page omits a redundant Home heading and shows the app version from assembly build metadata; Settings will expose the same version later.

Home is the default workspace landing page, reachable from both sidebar layouts and the command palette. It preserves the selected conversation and its running process; project selection still opens the existing project overview. Recent conversations span projects and omit settled items. Local usage is derived on a background worker from catalog-owned Pi JSONL sessions ([session format](https://pi.dev/docs/latest/session-format)), not from model responses or account billing APIs. The 7/30-day summaries include reported assistant input/output/cache tokens, response counts, and model/project breakdowns. Shared fork message identities are counted once. Research workers, management sessions and other usage outside saved assistant messages are excluded and disclosed in the UI.

Usage caching is memory-only, keyed by session file metadata and pruned against the current catalog. No analytics database, prompt copy, or telemetry upload is added. Deleting a conversation removes its contribution on refresh unless a saved fork retains that history. Reads are bounded to 64 MiB per session, 256 MiB of uncached files per scan, two million characters per JSONL record, and a ten-second budget between files; partial/unreadable records are disclosed. Home refreshes on navigation and every thirty seconds while visible, and cancels reads when hidden. Settings can disable the usage reader or persist a cutoff for displayed totals. Resetting totals does not rewrite Pi sessions or provider records.

## Settings and legal information

The Settings page is complete and user-approved. Possible additional settings and third-party logo permissions remain separate follow-ups in BACKLOG.md.

Settings is a global page available from either sidebar layout and the command palette. Search matches category titles and keywords, expands matching categories temporarily, and restores expansion state after clearing the query. Preferences are saved atomically in `settings.json` under the app data directory. Startup defaults to Home; optionally the most recently used conversation across projects is selected on the first catalog load. Global page navigation preserves conversation runtimes.

Bulk deletion uses the existing catalog/session/checkpoint cleanup boundary. It requires typed confirmation and no active conversations, snippets, research or terminal sessions. Conversation deletion can preserve projects; project removal also removes their conversations and never deletes registered source folders. Offline target cleanup remains pending and visible. Research records, shared Pi credentials, extension packages and unrelated local files are explicitly outside these actions; there is no broad folder wipe.

The separate Legal & privacy page reads bundled offline documents and dependency notices, including Microsoft's original image-library EULA. The project's original code/documentation uses MIT; dependencies and branding retain their own terms. Publisher/contact details are Eliaszac (Denmark), eliaszacho@gmail.com. These notices are implementation documentation, not a certification of legal compliance or third-party trademark permission. Branding/distribution rights need to be confirmed before public release.

Provider authentication remains in Pi's global store, respecting its agent-directory override. Dedicated provider-management processes use the bundled integration with no session or workspace tools. Secrets are not sent as chat prompts or persisted in the catalog. GitHub tokens use Windows Credential Locker; SSH passwords/passphrases use Windows Credential Manager. The setup guides describe their separate ownership.

RPC traffic uses stdout; bounded stderr diagnostics are separate. Errors expose bounded, redacted metadata and actionable recovery. Never automatically replay uncertain side effects. Cancellation of a wait and cancellation of agent execution are separate operations. Workspace activity leases coordinate app-owned writers; checkpoint restoration also validates expected file contents/existence/mode to protect later edits.

The native terminal uses ConPTY and bundled xterm.js through WebView2, not a runtime bridge. Keep terminal surfaces mounted outside the TabView content presenter: the earlier layout collapsed WebView2 to zero height. Build with a concrete architecture matching native WebView2 dependencies. Markdown streaming must validate Markdig spans before slicing; malformed streaming spans previously escaped a native timer callback.

Side panels share a native tab strip. Each conversation owns its in-memory tab order, selection and open/hidden state; switching conversations restores those tabs, while restarting starts fresh. Non-terminal panels have one tab per type. Terminal tabs own independent sessions, can be renamed and reordered, and are scoped by conversation even when projects share a directory. Hiding or switching tabs keeps shells running; closing a terminal tab or deleting its conversation disposes its shell. Project scripts use the same conversation scope. Existing panel content stays outside the tab content presenter. The empty panel offers available panel types, with existing remote-target and research opt-in restrictions.

## Scope decisions

Docker uses each source user's installed CLI and current Docker context. It never installs Docker, starts an engine, creates/deletes containers, or replays failed actions. Detection can start WSL distributions. SSH uses the saved target's authentication and host verification. Preferences are written atomically to `%LOCALAPPDATA%\PiAgentGui\docker.json`; secrets stay in the existing SSH credential store. Removing a source or disabling Docker does not stop containers. Polling runs while its tab is selected, every ten seconds, with bounded CLI calls and independent source errors. Container links are pruned only after every source responds successfully; deleted project links are pruned when Manage loads the project catalog. Docker build and mocked unit verification do not establish live Windows/WSL/SSH connectivity.

The current refined feature list is complete; no additional feature is approved by this document. Durable background execution, scheduled tasks/triggers, automatic worktrees, cross-session memory, product-managed sandboxing, and general-purpose subagents were removed from scope. Users can independently configure sandboxing. The existing opt-in research feature remains.

## Distribution and verification

Display name: Pi desktop. Repository: `pi-agent-winui`. Existing namespaces, executable/resource names, credential keys, `PiAgentGui.Desktop` installer identity, and `%LOCALAPPDATA%\Programs\Pi Agent` install path remain stable for compatibility. Windows x64 distribution uses a self-contained, untrimmed Inno Setup installer. No automatic updater or publishing pipeline is implemented. See [Installer/README.md](Installer/README.md).

Ordinary builds and unit tests are allowed under [AGENTS.md](AGENTS.md). Live Pi/GUI execution and setup scripts require user authorization; respect prior permission and refusals. Pi 0.85.1 Windows/Ubuntu WSL extension probes have passed without model calls. Builds and focused checkpoint/cleanup tests passed during implementation. These are recorded results, not a fresh verification of every surface.

The user marked checkpoints done. Live SSH verification was declined; real SSH/interrupted-connection checks and the complete native checkpoint dialog flow remain unverified. Native automation rejected input, although preview app launches succeeded. Other integration and installer checks are listed in their guides; do not infer full release acceptance from a successful build.

### MCP quick integrations

The MCP panel includes Quick integrations, opening a native scrollable dialog with six curated cards: Obsidian, Atlassian, GitHub, Linear, Notion, and Supabase. Cards show bundled logos, descriptions, publisher attribution, and documentation links. Add opens native setup with authentication selection, masked token entry, browser sign-in, Save only, and an isolated connection check. Existing integrations open Manage. Saving validates definitions and detects concurrent changes. Requires the supported MCP Adapter. Atlassian, Linear, Notion, and Supabase use adapter OAuth; GitHub accepts a token through the adapter credential-store CLI over stdin, without putting it in JSON or command arguments. Obsidian uses the community obsidian-mcp@2 stdio package: choose an existing vault with an .obsidian directory; npx downloads the package on first use. No REST plugin, API key, or running Obsidian app is needed. Existing HTTP definitions are never replaced automatically. Restart loads the saved definitions; configuration is not proof of authentication or connectivity. No service account is connected automatically, and WSL/SSH third-party MCP restrictions remain. Existing Add server remains available.

The MCP panel merges saved global mcp.json names with conversation runtime status, so new entries appear immediately after saving and remain visible offline. Runtime rows take precedence; unloaded definitions say Configured · restart to load. Status events do not include connection error details in adapter 2.33.0; the bundled bridge also captures identified MCP proxy tool failures, bounds/redacts messages, and forwards them to selectable card details. Missing details are stated explicitly. This does not connect servers or inspect adapter internals.

MCP cards expose a Manage menu for connection/authentication, editing the complete server definition as validated JSON, and confirmed removal. Editing preserves adapter-specific options; editing and removal compare the reviewed definition before writing, preserving unrelated configuration. These actions affect global definitions only; external definitions must be changed at their source. Removal leaves stored credentials and remote account access intact. Running conversations retain loaded connections until restart.

Markdown tables use native sticky headers and a ten-row scrolling viewport. Sorting is local presentation state (stable ascending/descending number, unambiguous date, or text order) with an original-order reset. CSV/JSON export uses the native save picker and includes the complete table in current order. Exported values are visible text; JSON preserves values as strings and disambiguates header keys. The Pi transcript is unchanged. Pure parsing/sorting/export tests cover these behaviors; native save-picker interaction is not part of automated verification.

### Code-block actions

Completed assistant code blocks offer Save to project and, for PowerShell/Bash/Python fences, Run. Operations use the conversation's fixed target and working directory, without Pi/model involvement or extra confirmation. Save creates snippet.ext, snippet-2.ext, and so on using exclusive creation; unknown languages use .txt. Windows runs use directly discovered interpreters; Bash never implicitly selects WSL. WSL/SSH use the bundled Python 3 helper and existing target/SSH configuration, requiring the chosen interpreter on that target.

Runs are noninteractive, with stdin closed, bounded inline output, elapsed time, exit status, Stop, output copy/expansion, and an explicit Add output to prompt action. No automatic dependency installation or prompt submission occurs. Snippet state belongs to the conversation and survives view changes; disposal cancels its operations. Stop terminates the owned local process tree or requests remote process-group termination; connection loss cannot guarantee immediate remote cancellation. Snippet writes participate in workspace activity coordination. Temporary run files are removed; Save to project files remain user-owned. New actions stay unavailable while the response is streaming.

Verification: pure C# tests cover names, concurrent saves, interpreter arguments, state, output limits and cancellation. Python helper unit tests mock subprocesses and cover save/stdio/exit/Stop cleanup. No native UI or live WSL/SSH snippet checks have been performed.
