# Future work

Remaining work and optional ideas. Listing an item does not authorize implementation.

## Current priority

Focus on making the conversation UI excellent for everyday use. The user considers most of the app well tested; the conversation UI is now the main factor limiting their use of it. Prioritize responsiveness, stable and smooth scrolling, readability, and interaction polish over adding more features. Assess improvements in real conversations and preserve the resolved input responsiveness and scroll stability.

## Ideas to explore

- **Tag projects and Git work in conversations.** Extend the implemented PR/issue reference picker with project and Git repository references. Selecting an item should attach useful context and a link so the agent can review it or work on it without the user manually copying everything. Explore discovery, state filters, permissions, and how referenced context stays current. This is an idea, not an approved implementation.
- **Native integrations replacing the removed MCP shortcuts.** Plan future integrations for GitHub (repositories, PRs and issues), Atlassian (Jira and Confluence), Linear, Notion, and Supabase (projects and database context). These entries have been removed from Quick integrations; Obsidian remains MCP-only. Explore a shared connection for native search/tagging/context attachment and agent tools, with authentication and permissions appropriate to each service. Start with read-only workflows; write actions require a separately agreed scope. For GitHub, reuse the existing GitHub App connection. The user reports adding Issues read-only alongside Pull requests read-only; installation approval and API access have not been verified here. Implementation order and detailed scope remain undecided.

- **GitHub write actions.** At minimum, support agent-driven updates to an existing issue and comments on a pull request. Plan the editable issue fields, comment types, and approval behavior before implementation, and request the permissions required by those specific endpoints. The initial integration remains read-only; this records future scope, not authorization to perform writes or expand permissions now.

## Low priority / nonessential

### Public-release identity and packaging

Confirm the publisher identity and whether to retain “Pi desktop” or choose an independent product name. Check upgrade cleanup for removed artwork; a fresh build excludes it, but ordinary installer replacement can leave old files behind. See [branding](BRANDING.md) and [installer guidance](../Installer/README.md). This is nonessential and not a current priority.

### Optional Settings improvements

These are suggestions from the completed review, not an agreed implementation scope:

- Terminal font size and scrollback limit; explain that reducing scrollback removes terminal history.
- Copy a diagnostic summary after preview, excluding credentials and conversation content.
- Completion/approval notifications with privacy-conscious content and deduplication.
- Restore defaults by category, without deleting user data.
- Match individual settings in search and distinguish success/error feedback.

Completed features belong in [ARCHITECTURE.md](../ARCHITECTURE.md), not this backlog.
