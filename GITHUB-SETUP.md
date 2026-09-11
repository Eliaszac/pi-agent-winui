# GitHub connection setup

Pi desktop uses a GitHub App's device flow directly from the Windows app. Users click **Connect to GitHub** in the conversation header, copy the displayed code, open GitHub, and authorize it. No server, `gh`, client secret, private key, or manually pasted user token is needed.

## Register the app

1. Open https://github.com/settings/apps/new (Settings → Developer settings → GitHub Apps → New GitHub App).
2. Choose a unique app name. For Homepage URL, use the project website, public repository URL, or the owning GitHub account URL.
3. Leave Callback URL and Setup URL blank. Leave **Request user authorization (OAuth) during installation** unchecked; sign-in happens through device flow.
4. Enable **Device flow**. Keep **Expire user authorization tokens** enabled.
5. Under Webhook, uncheck **Active**. No webhook URL or secret is needed.
6. Repository permissions: **Pull requests → Read-only**. **Metadata → Read-only** is included automatically. Leave other repository, account, and organization permissions at No access. No event subscriptions are needed.
7. Choose **Any account** if other users will use this integration; **Only on this account** limits installation to your own account.
8. Create the GitHub App. Copy its **Client ID** (not App ID). Copy its slug from `https://github.com/apps/<slug>`.

## Configure this build

Set the public values in the repository-root `GitHubApp.json`:

```json
{
  "ClientId": "your GitHub App client ID",
  "AppSlug": "your-app-slug"
}
```

These are public application identifiers and may be committed. Do not add a client secret or private key. The file is copied into builds and publish output. Rebuild and restart after changing it. End users receive the configured build; they do not register their own app.

In the GitHub App settings, choose **Install App**, choose the account, then grant access to the repositories you want to use. Organization repositories may need an organization owner to approve installation. For fork PRs, the target/parent repository must be accessible too.

## Use and verify

- Open a conversation and click **Connect to GitHub**. Copy the code, click **Open GitHub**, and approve that exact code. The dialog closes when authorization completes.
- On a branch with an open PR, the header shows **Open PR** and conversations under that project show the actual PR number. Both header and conversation context menu open the PR in your browser.
- When connected with no matching open PR, neither Connect nor Open PR is displayed.
- Right-click the conversation header for **Refresh pull request**, **GitHub repository access**, or **Disconnect GitHub**. Disconnect removes the local credential; revoke the authorization in GitHub settings if you also want to revoke it server-side.
- Local branch metadata is checked every 15 seconds while the page is loaded. GitHub results are checked at most once per minute per project, except branch changes or an explicit refresh.
- All conversations currently use their project's one working folder and therefore share its checked-out branch. This does not create conversation-specific branches or worktrees, or persist branch associations in the conversation catalog.
- Supports github.com SSH/HTTPS remotes, upstream tracking branches (origin/current branch fallback), and fork PRs against an accessible parent repository. Detached HEAD, non-Git folders and other Git hosts do not show a PR. Multiple matches choose the most recently updated PR in the parent repository first, then the source repository.

## Credential lifecycle

Access and rotating refresh tokens are stored in Windows Credential Locker under `PiAgentGui.GitHub.<ClientId>` / `user`. They are not written to `GitHubApp.json`, the project catalog, logs, or Obsidian. Expiring tokens refresh automatically before expiry; GitHub explicitly allows device-flow refresh without a client secret. Revocation or refresh-token expiry returns the UI to Connect to GitHub. Network failures do not deliberately erase the saved login.

## Troubleshooting

- Sign-in cannot start: check Client ID and Device flow. The error remains in the sign-in dialog; close it and retry.
- Repository unavailable: use Repository access to install the app or add the repository. Check organization approval and Pull requests read permission.
- Badge absent: ensure the folder is on a branch whose configured upstream matches the PR source branch, and the PR is open. Right-click the header and refresh after changing GitHub installation access.
- Git missing, offline, denied or rate-limited: errors are shown for the selected project; an error is not presented as confirmed absence of a PR.

## Primary references

- https://docs.github.com/en/apps/creating-github-apps/registering-a-github-app/registering-a-github-app
- https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app
- https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/refreshing-user-access-tokens
- https://docs.github.com/en/rest/pulls/pulls

Implementation verified with builds and fake HTTP/Git/credential unit tests. Live browser login, Windows Credential Locker and real private-repository access still need testing after app registration.
