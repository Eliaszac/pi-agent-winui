# Future work

Remaining work and optional ideas. Listing an item does not authorize implementation.

## Current priority

Focus on making the conversation UI excellent for everyday use. The user considers most of the app well tested; the conversation UI is now the main factor limiting their use of it. Prioritize responsiveness, stable and smooth scrolling, readability, and interaction polish over adding more features. Assess improvements in real conversations and preserve the resolved input responsiveness and scroll stability.

## Ideas to explore

- **Tag projects and Git work in conversations.** Consider searchable mentions/tags for projects, Git repositories, pull requests across all states (open, draft, closed, merged), and Git issues across open/closed and other supported states. Selecting an item should attach useful context and a link so the agent can review it or work on it without the user manually copying everything. Explore discovery, state filters, permissions, and how referenced context stays current. This is an idea, not an approved implementation.
- **Full integrations with work-management tools.** Consider native Jira, Notion, Linear, and similar integrations beyond the current MCP setup shortcuts. Let users find and tag their issues, tasks, pages, and other relevant records in conversations for review and implementation work. Explore authentication, browsing/search, linked context, and any explicitly requested write-back actions as part of a complete workflow. Scope and supported services remain undecided.

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
