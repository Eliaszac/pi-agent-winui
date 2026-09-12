# Optional workspace checkpoints and safe revert

Status: done, 2026-09-12, as requested by the user. Windows and WSL live extension probes pass. Live SSH verification was declined by the user, and native dialog automation could not complete because the computer-use helper rejected input. These verification limitations remain recorded; completion does not imply those checks passed. See [CHECKPOINTS.md](../../CHECKPOINTS.md) for actual implementation decisions and verification scope. The sections below retain the original implementation and acceptance plan.

## Intended behavior

Users enable Workspace checkpoints from Extensions. Local Windows, WSL, and supported Linux SSH targets are required in the first release. With no configuration-file editing, conversations on all three target types capture file recovery data and show Created, Modified, and Deleted rows. A completed card offers Revert changes with a preview and explicit application. Reverting changes files, not conversation history. Undo revert is available when its recovery data and current file states still permit it.

Remove Ask agent to revert entirely when implementing this feature, including its prompt-generation path and fallback UI. Without a ready extension, offer setup guidance; for requests without usable checkpoints, explain why deterministic revert is unavailable. Conflicts remain explicit and cannot trigger an agent-assisted fallback. Improve created/modified reporting from direct tool evidence independently of extension availability; complete shell/deletion coverage requires capture. Never fabricate historical changes from current Git status.

## 1. Prove the extension boundary before building the UI

- Evaluate a pinned revision of pi-workspace-history as the first reuse candidate; CHECKPOINT-RESEARCH.md records why neither reviewed package is drop-in.
- Validate that its snapshot/storage core can be reused through supported exports or a small, maintained adaptation. Preserve required licenses and attribution. Prefer an upstream API contribution when practical, but do not depend on its acceptance or contact maintainers without authorization.
- Do not install the full rewind extension unchanged: its conversation navigation and whole-workspace restore do not implement selective card revert.
- Define a versioned extension contract: capability/version discovery, per-request change manifest, prepare-revert, apply-revert, undo-revert, and recovery status. Bind every operation to project, conversation, request, execution target, and canonical workspace identity.
- Prove capture and restore through Local Windows, WSL, and SSH backends in this phase. A local-only engine without a viable target abstraction does not satisfy reuse feasibility.
- Use public Pi extension/RPC facilities and the existing integration conventions. Do not require a separate Node bridge or edit Pi-owned session JSONL. Store compact references in Pi-supported metadata, with snapshot bytes outside transcripts and model context.
- Deliverable: an API/data design and focused tests proving created/modified/deleted capture plus guarded restoration. If reuse requires effectively maintaining the whole upstream extension, reassess before committing to it.

## 2. Capture usable recovery data

- Capture baseline before an accepted user operation can mutate files; finalize after agent_settled. Match actual prompt/follow-up/steering behavior to our card boundaries. Preserve evidence from cancelled or failed operations with partial-coverage status.
- Store exact bytes, existence, file kind, relevant metadata, and hashes independently of bounded text previews. Distinguish absent files from existing empty files.
- Combine direct tool-operation evidence with workspace snapshots to cover shell modifications. Do not infer deletion or write paths by parsing arbitrary shell strings.
- Show net before/after changes: absent to present = Created; present to absent = Deleted; changed contents = Modified; identical final state = omitted. Initially represent rename as deletion plus creation rather than guess.
- Retain target-native case sensitivity. Refuse unsupported links/reparse points and unstable file reads rather than traverse or record misleading snapshots.
- Use private content-addressed storage without changing the user's branch, index, commits, or stash. Support non-Git projects through the private store when prerequisites are available.
- Establish default exclusions, maximum file size, total storage quota, and retention before enabling capture. Proposed starting retention: 30 days and 1 GiB per user, validated against representative projects. Exclude generated/dependency directories and sensitive files by default; expose incomplete coverage. Protect active operations and pending recovery from pruning; stop capture visibly if no safe space can be reclaimed.
- Expired data leaves historical summaries readable but removes deterministic revert availability. Disabling capture preserves existing recovery data; deleting history is a separate explicit action.

## 3. Define concurrency and attribution honestly

- Coordinate app-owned operations by canonical workspace identity across conversations and app windows, including overlapping folders. Restoring requires no other app-owned agent run, Git mutation, script, or terminal process that could write to that workspace; do not kill them automatically.
- Preserve concurrent conversations as a supported feature. Record overlap during capture and mark affected snapshot-derived changes ambiguous. Do not automatically attribute every before/after difference to the selected conversation.
- External editors and arbitrary detached processes cannot be fully locked out by an extension. Revalidate files before each mutation, detect unstable state, and refuse ambiguous cases. Do not promise absolute race-free attribution without filesystem/OS isolation.
- Initial deterministic revert uses conservative whole-file matching; same-file later changes block that file even if the edits appear unrelated. Three-way text merging is a later enhancement, not required for the first release.

## 4. Implement selective revert and recovery

- Prepare a bounded immutable preview with safe files, conflicts, unavailable files, and reasons. An old card must identify its exact operation, never the latest operation by accident.
- Modified file: restore the original bytes only if current bytes/kind match the captured after-state.
- Created file: remove only if it still matches the captured creation; preserve its bytes for Undo revert.
- Deleted file: recreate only if the path is still absent and parent/path boundaries remain valid.
- Before any writes, persist a recovery journal and the current contents of every selected file. Backup failure cancels application entirely.
- Revalidate at application time; use temporary sibling files and atomic replacement where supported. Apply only selected paths. No workspace-wide checkout/clean/reset or shell-command replay.
- Multi-file restoration is not atomic. Record progress durably, report partial outcomes accurately, and recover after interruption without overwriting intervening edits. Never automatically replay an uncertain restore.
- Undo revert uses the same checks, preview, and journal rules. Add a compact Pi-visible record of successful file restoration so the model knows its earlier output no longer matches the workspace; retain the original conversation history.

## 5. Integrate Extensions and summary cards

- Add a Workspace checkpoints card using existing global extension discovery/setup conventions. Explain storage and scope; provide Enable/Disable and meaningful states: missing, disabled, incompatible, ready, error.
- Decide distribution after phase 1: a supported pinned external package if it exposes the required contract, otherwise an app-maintained optional extension. Runtime loading and capture must remain explicitly opt-in. No mandatory JSON editing.
- Version/capability handshake per conversation; installation alone does not mean ready. Activating requires idle reload or next connection, with no interruption of running work.
- Update FileChange, parser, ChangedFileViewModel, and RunChangesViewModel to consume explicit change kinds and complete per-request manifests. Use Changed rather than Edited for mixed summaries, retain diff access and diagnostic/test rows, and show binary or incomplete counts accurately.
- Add a native revert preview with selectable safe files, conflict explanations, and an explicit Revert selected action. Clearly report partial revert and recovery availability. Remove the old Ask agent to revert control, command wiring, prompt builder, and obsolete tests; replace them with deterministic revert and unavailable-state coverage.
- Retain current keyboard, focus, theme, accessibility, and virtualization conventions. No new settings page is required.

## 6. Execution targets required at launch

- Local Windows, WSL, and supported Linux SSH hosts must support capture, change summaries, preview, selective revert, Undo revert, retention, and recovery from the first release. Do not defer target parity.
- Build one target-aware contract with local and POSIX backends, reusing existing WSL/OpenSSH execution and authentication services. Pi and its sessions remain on Windows; workspace inspection and mutation occur on the selected target. Never snapshot the Windows Pi runtime directory as if it were the remote workspace, and never silently fall back to local execution.
- Keep snapshot content and durable restore journals on the workspace target outside the project, with stable local references and cached summary metadata. Resolve per-user storage locations and quotas for each target; cleanup runs on that target and protects pending recovery. Workspace snapshots do not replace the authoritative Windows catalog or Pi sessions.
- Detect prerequisites and storage permissions per target and surface failures through Extensions and conversation state without mandatory configuration-file editing. Do not silently install target software. Any required helper deployment must use the explicit extension enable/setup flow and version verification.
- Use target-native path, case, file-kind, permission, hashing, locking, and atomic replacement semantics. Resolve workspace aliases where possible so overlapping paths cannot bypass coordination.
- On disconnect or cancellation during a write, mark the operation uncertain. Reconnect to inspect the target-side journal and current file hashes before offering recovery; never blindly retry a restore or assume a remote process stopped. No persistent background job supervisor is introduced.

## 7. Verification and release criteria

- Unit tests: create/modify/delete; empty/binary/large/excluded files; multiple changes to one file; case-sensitive targets; pre-existing staged/unstaged/untracked work; cancelled/failed runs; restarted history; retention and disablement.
- Revert tests: later edits, recreated deleted paths, changed created files, old-card targeting, concurrent runs, path links, backup failure, disk full, locked files, mid-operation crash, partial restore, and Undo revert conflicts. Confirm Git index/history and unrelated files remain unchanged.
- Transport/view-model tests: missing/incompatible extension, malformed manifests, stale preview IDs, timeout/disconnect, summary change kinds, setup guidance, and unavailable/conflict states. Verify no revert action generates or sends an agent prompt. Existing fake transports avoid provider requests.
- Ordinary builds and unit tests are permitted. Obtain explicit permission before live Pi/GUI tests or setup scripts. With that permission, validate in disposable Local Windows, WSL, and real Linux SSH projects, including multiple app windows, concurrent conversations, external edits, disconnect/reconnect, and interrupted restore recovery. Fake transports alone do not establish SSH acceptance.
- Ship only after all three execution target types pass the installed opt-in path, zero-config defaults, persistence, truthful failure reporting, and later-work protection. Snapshot engine reuse or Local Windows validation alone is not acceptance.

## Implementation order

1. Reuse feasibility and versioned contract proven across Local Windows, WSL, and SSH.
2. Target-aware capture/storage and complete change manifests on all three backends.
3. Deterministic revert/recovery, disconnect handling, and concurrency guards on all three backends.
4. Extensions onboarding and native summary/revert UI.
5. End-to-end Local Windows, WSL, and real Linux SSH validation before release.
