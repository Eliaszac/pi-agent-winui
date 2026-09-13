# Future work

Remaining work and optional ideas. Listing an item does not authorize implementation.

## Remaining work

1. **Storage overview and targeted cleanup.** Show session, research, checkpoint and diagnostic storage separately. Add Open app data folder, Clear diagnostics, Clear completed research, and pending-cleanup retry. Scan off the UI thread, protect active work and recovery data, and respect Windows/WSL/SSH ownership. Do not delete project files, shared Pi credentials or unrelated data.
2. **Public-release identity and packaging.** Confirm the publisher identity and whether to retain “Pi desktop” or choose an independent product name. Check upgrade cleanup for removed artwork; a fresh build excludes it, but ordinary installer replacement can leave old files behind. See [branding](BRANDING.md) and [installer guidance](../Installer/README.md).

## Optional Settings ideas

These are suggestions from the completed review, not an agreed implementation scope:

- Terminal font size and scrollback limit; explain that reducing scrollback removes terminal history.
- Remember deliberate sidebar width/collapse choices across restarts, separately from responsive layout changes.
- Copy a diagnostic summary after preview, excluding credentials and conversation content.
- Completion/approval notifications with privacy-conscious content and deduplication.
- Restore defaults by category, without deleting user data.
- Match individual settings in search and distinguish success/error feedback.

Completed features belong in [ARCHITECTURE.md](ARCHITECTURE.md), not this backlog.
