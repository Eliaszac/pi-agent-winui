# Checkpoint extension investigation

Read-only source review on 2026-09-12. No extension installed or executed. These findings describe upstream main as retrieved on that date, not a pinned or locally verified release. This historical investigation predates the completed bundled implementation; see [CHECKPOINTS.md](../../CHECKPOINTS.md).

## Product requirement

Replace the summary card's agent-assisted revert with deterministic file restoration where safe; report created, modified, and deleted files. Preserve unrelated work, later changes, and conversation history. Concurrent conversations share their target workspace. WSL/SSH workspace operations happen on the selected target while Pi stays on Windows.

## pi-workspace-history

Sources:
- https://pi.dev/packages/pi-workspace-history
- https://github.com/wcldyx/pi-workspace-history
- https://raw.githubusercontent.com/wcldyx/pi-workspace-history/main/.pi/extensions/workspace-history.ts

Uses private per-session Git snapshot storage and before/after operation records. Defaults enable automatically in recognized projects, exclude ignored/generated/sensitive files, and retain a target of three inactive/active session histories per workspace and ten workspaces (active entries are protected from cleanup).

The registered commands are undo, redo, and checkpoint. Navigation modes are conversation-and-workspace or conversation-only; no files-only mode is exposed. Undo calls navigateTree. No dedicated structured change-list or selective per-turn file-revert command was found. Snapshot records include before/after commit IDs and message anchors, so deriving file status is feasible but requires integration with internal data or a new upstream API.

Restoration checks for unsnapshotted changes, then restores the managed snapshot through the private Git index and working directory. This is historical workspace restoration, not selective reversal of one request while preserving later work. Session-specific storage and retention leases do not provide ownership attribution or exclusive access to a shared workspace. A concurrent writer can race checking/restoration; changes from another conversation during snapshot capture can be included.

Source performs local filesystem access and local Git execution rooted at ctx.cwd. It cannot simply be enabled for this application's Windows-hosted Pi processes to snapshot their WSL/SSH workspaces.

## pi-rewind comparison

Sources:
- https://github.com/arpagon/pi-rewind
- https://raw.githubusercontent.com/arpagon/pi-rewind/main/src/commands.ts

Provides a files-only mode and separately organized core helpers. Still restores checkpoints rather than selectively reversing a historical card. In the reviewed commands.ts, performRestore continues if the pre-restore backup fails, catches restore failure without propagating it, and callers can subsequently announce success. Do not adopt unchanged without addressing these behaviors and checking current Pi/Windows compatibility.

## Recommendation

Neither reviewed extension is a drop-in implementation of the requested card behavior. Prefer investigating a small upstream API addition or a pinned, adapted extension before writing a checkpoint engine. Required integration: structured per-operation changes, files-only selective restore, current-state validation, shared-workspace coordination, persistent availability reporting, and execution-target support. Native summary rendering remains application work. Do not label whole-workspace rewind as selective revert.
