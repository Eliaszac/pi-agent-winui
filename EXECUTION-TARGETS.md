# Execution targets

One project can have local Windows, WSL, and Linux SSH workspaces. Each conversation keeps its selected target. The desktop remains authoritative for the project catalog, Pi runtime, credentials, settings, and session history; workspace files remain on their target. Separate checkouts are not automatically synchronized.

## Setup

1. Create a project and select Local Windows, WSL, or Remote SSH. Supply the target's absolute folder path. For WSL, choose an installed distribution from the dropdown; for SSH, enter an existing SSH config alias or `user@hostname`.
2. Choose an existing folder, or enable **Clone Git repository** and supply a repository URL and a new destination folder. Configure Git credentials where the clone runs. Failed clones retain partial files for inspection.
3. Open the project's sidebar context menu → **Execution targets** to add other workspace locations and choose the default for new conversations.
4. Create a conversation. A project with one target uses it immediately; projects with multiple targets show the picker. Changing the project default does not move existing conversations. Create another conversation to work on a different target.

WSL/SSH targets need Linux with Bash, coreutils (`base64`, `head`, `mktemp`, `chmod`, `sha256sum`), GNU `find`, and `setsid`. Install `rg` for search/file references and Git for source control. Pi itself runs on Windows; it is not required on the target. SSH uses Windows OpenSSH and existing configuration and known-host records. Automatic host-key acceptance is disabled. Custom ports belong in SSH config.

For SSH, choose **Key / SSH agent** or **Password**. Key mode uses your SSH configuration/agent by default, with optional explicit local private-key path and saved passphrase. A saved passphrase requires an explicit key file. Password mode uses the SSH password authentication method; keyboard-interactive/MFA challenges and password-change flows are not automated.

Passwords and key passphrases are saved per target in Windows Credential Manager (`PiAgentGui.Ssh.<target-id>`). They are excluded from the catalog, command arguments, environment variables and Pi transcript. An OpenSSH askpass helper retrieves the secret only for the expected destination password prompt or matching key passphrase prompt; it rejects trust confirmations and unrelated hosts/keys. Setup, agent tools, Git and terminals share this policy. Use **Update password** or **Update key passphrase** in the target manager to replace a saved credential. SSH host trust must already be established through your normal SSH client. Configure jump hosts separately; their prompts cannot receive the destination's password.

Sidebar location icons indicate execution location, not connection health. Hover for target details, or use the conversation context menu → **Execution target**. Available local PR information is included.

The app discovers WSL distributions in the background at startup and keeps a shared in-memory cache. Opening either creation dialog reuses it immediately once discovery finishes. The only installed workspace distribution is selected automatically; otherwise WSL's default is preferred. Docker's internal distributions are excluded. Use **Refresh distributions** after installing another distribution while the app is open. Empty lists and discovery failures display setup guidance; startup never installs or launches a Linux environment.

## Current scope

Agent file and shell tools, terminal sessions, target-specific run scripts, and ordinary Git actions execute on the selected target. Target ancestor instruction files load before each turn. Arbitrary third-party extensions, MCP integrations, skills and prompt templates are disabled for remote conversations because their own I/O may still run locally. The supported local Permission Modes integration remains available.

Remote file browsing/text preview is read-only; use agent tools or the target terminal to edit. Native conflict editing, untracked diff previews/recycling, GitHub PR discovery, Open in, process inspection and background research remain local-only. Target editing/removal and moving existing conversations between targets are not yet offered.

There is no background remote job supervisor. Cancellation attempts to stop the shell process group, but detached jobs and connection failures can have uncertain outcomes. Check the target before retrying a mutating command. No operation silently falls back to a local workspace.

## Manual verification

- Open an existing project and confirm its old conversations still use their original local folder.
- Add a WSL target; create simultaneous local and WSL conversations. Check icons, delayed hover details, keyboard context-menu details, headers and target persistence after restart.
- Change the project's default and confirm only new conversations use it.
- Ask the WSL conversation to report its working directory, read an instruction file, and edit a disposable file. Check that the Windows checkout stays unchanged and approval controls still work.
- Open each target's terminal, run a target-specific script, and switch conversations while terminals remain active. Verify labels and working directories.
- Inspect target files and tracked Git changes. Use only a disposable repository for stage/commit/revert tests.
- Create a WSL-first or SSH-first project without a local checkout; test cloning into a new destination using your existing Git authentication.
- Try an unavailable distribution/SSH host and verify a clear error with no local tool execution.

Implementation verification used builds, unit tests, and Pi against an isolated Ubuntu WSL folder without model calls. SSH tests cover temporary Windows Credential Manager entries and the explicitly headless helper, which exits before WinUI initialization. The GUI was not launched. A real SSH server and the native UI still require manual verification.
