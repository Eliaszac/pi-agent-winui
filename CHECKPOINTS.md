# Workspace checkpoints

Workspace checkpoints is an optional extension bundled with Pi desktop. Enable it in **Extensions → Workspace checkpoints → Manage**. It is disabled by default; the toggle takes effect before the next agent operation. No configuration-file editing or separate extension package installation is required.

Manage also offers **Clear stored data** for This computer or a saved WSL/SSH target. After explicit confirmation it expires all stored checkpoints for the selected target user and removes their snapshot bytes. Project files, Pi conversations, and cached historical file summaries remain. Existing Revert/Undo becomes unavailable; reconnect conversations to refresh their status. Capture remains enabled if its toggle is on. Clearing refuses active captures and pending or partial recovery; finish or inspect those first. Clearing was unit-tested only in disposable storage; no real user data or SSH target was cleared.

## Requirements and targets

- Windows: Python 3 available as `python`, and Git on PATH.
- WSL and Linux SSH: Python 3 available as `python3`, and Git on the selected target.
- The existing WSL/SSH connection and authentication are reused. Snapshot bytes and restore journals remain on that target. There is no fallback to the Windows workspace when a remote connection fails.
- Missing prerequisites produce an error; the app does not install target software automatically.

The extension uses Pi's public lifecycle hooks, custom session entries, commands, and RPC status messages. Its app-maintained Python standard-library engine is shared by all targets; it does not fork Pi or introduce a Node bridge.

## Behavior

Capture begins before an agent operation and finishes at `agent_settled`. The final response card receives net Created, Modified, and Deleted rows, including shell-driven changes inside capture coverage. Renames appear as deletion plus creation. Direct write-tool evidence can show Created/Modified even without checkpoints; old requests do not acquire recovery data retroactively.

**Revert changes** opens a file selection preview. Modified files must still match their captured final bytes and permissions. Created files are removed only if unchanged; deleted files are recreated only if still absent. Later changes to the same file cause a conflict, even when they are on different lines. Unrelated paths are preserved. Git branches, commits, index, stash, and Pi conversation history are untouched.

**Undo revert** restores the pre-revert contents of successfully reverted files with the same conflict checks. A compact restoration result enters Pi context without starting a model turn. The old **Ask agent to revert** button and prompt builder are removed; there is no agent fallback.

Multi-file restore is not atomic. Backups and a journal precede writes; progress is recorded per file. An interrupted or uncertain operation offers **Inspect checkpoint recovery**, which reconciles current contents with the journal without replaying writes. Stop any remote commands still running before inspection. Later conflicting edits remain preserved.

## Defaults and storage

| Setting | Default |
| --- | --- |
| Capture | Disabled until enabled in Extensions |
| Retention | Latest five completed checkpoints per conversation, with a 30-day age limit; evaluated when the engine runs and after capture finishes |
| Snapshot content budget | 1 GiB per target user |
| Individual file limit | 8 MiB |
| Scan bound | 20,000 candidate files / 30 seconds of directory enumeration |
| Text diff previews | Bounded independently of exact snapshot bytes |

Storage is `$HOME/.pi-desktop-checkpoints` on each target (`%USERPROFILE%\.pi-desktop-checkpoints` on Windows), partitioned by canonical workspace and conversation identity. Identical file contents are deduplicated within a workspace. Active operations and pending/partial recovery are protected from pruning. Metadata and cached summaries are additional to the snapshot content budget. Historical summaries survive expiration, while expired bytes cannot be restored. Disabling capture preserves existing data and cached session summaries.

The Windows opt-in setting is `%LOCALAPPDATA%\PiAgentGui\checkpoints.json`. Cross-window activity coordination lives in `%TEMP%\PiAgentGui-workspace-activity`; uncertain-restore guards live in `%LOCALAPPDATA%\PiAgentGui\checkpoint-recovery`. These are implementation locations, not files users need to configure.

Default exclusions include Git metadata, dependency/build/cache folders, `.env` and `.env.*`, and `.pem`/`.key` files. Git ignore rules are honored in Git workspaces. Links, junctions, unsupported files, oversized files, and unstable reads are not captured; incomplete coverage disables deterministic revert. A project folder is required; filesystem roots and the target user's home folder are rejected.

## Concurrency limits

Deleting a conversation now schedules removal of its app-owned Pi JSONL session (including embedded screenshots), selector-settings sidecar, and that conversation's checkpoint records. Independent copies and shared snapshot blobs still referenced by other conversations are preserved. Project deletion schedules the same cleanup for its conversations, without deleting project files. The app stops owned runtimes before cleanup. Pending recovery is protected; local known pending recovery blocks deletion. Failed or offline target cleanup retains a small request in `%LOCALAPPDATA%\PiAgentGui\deleted-conversation-data` and retries on a subsequent project-list load. This is housekeeping, not an agent job scheduler. No broad scan of user folders or retroactive deletion of old unregistered sessions is performed.

The five-checkpoint limit preserves active, interrupted, partial, and already-reverted checkpoints with Undo data; the existing age limit still applies to completed/reverted checkpoints. Earlier change summaries remain readable after snapshot expiry.

The first implementation conservatively coordinates all app-owned agents, Git commands, scripts, and open terminals across app windows. Other activity can disable attribution even in a different project; restoration requires those activities to finish. Concurrent conversations remain available, but overlapping captures are not automatically revertible. Target-side locks and journals protect reconnect/recovery handling.

External editors and detached processes cannot be fully excluded. Files are checked again before each mutation, but this is not filesystem isolation and cannot guarantee freedom from every external write race.

## Verification status

- C# suite: 465 passed, one existing headless credential-helper test skipped. Ordinary WinUI build: zero warnings/errors.
- Engine tests cover exact bytes, create/modify/delete, Undo, conflicts, unrelated work, overlapping sessions, backup failure, partial retry, interrupted-write reconciliation, retention, path/content validation, ignored files, scan failure, and Git index/history preservation.
- Live Pi 0.85.1 probes passed on Windows and Ubuntu WSL, using disposable folders without model requests. The probe drives lifecycle callbacks through a test extension and exercises the real target transports; it is not a provider-driven end-to-end conversation test.
- The native verification build launched and exposed its accessible window tree. Full dialog interaction remains unverified because the computer-use helper rejected input (`SendInput ... GetLastError=87`).
- Linux SSH implementation uses the same engine and existing SSH transport. The user declined live SSH checks; real SSH and interrupted-connection acceptance remain unverified. Do not mark the feature release-ready before these checks and the native dialog flow pass.

The original acceptance plan is in [CHECKPOINT-PLAN.md](CHECKPOINT-PLAN.md); package reuse findings are in [CHECKPOINT-RESEARCH.md](CHECKPOINT-RESEARCH.md).
