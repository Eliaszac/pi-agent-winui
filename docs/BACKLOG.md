# Future work

Remaining work and optional ideas. Listing an item does not authorize implementation.

## Ideas to explore

- **Native integrations replacing the removed MCP shortcuts.** Plan future integrations for Atlassian (Jira and Confluence), Linear, Notion, and Supabase (projects and database context). These entries have been removed from Quick integrations; Obsidian remains MCP-only. Explore a shared connection for native search/tagging/context attachment and agent tools, with authentication and permissions appropriate to each service. Start with read-only workflows; write actions require a separately agreed scope. Implementation order and detailed scope remain undecided.

## Low priority / nonessential

### Public-release identity and packaging

Confirm the publisher identity and whether to retain “Pi desktop” or choose an independent product name. Check upgrade cleanup for removed artwork; a fresh build excludes it, but ordinary installer replacement can leave old files behind. See [branding](BRANDING.md) and [installer guidance](../Installer/README.md). This is nonessential and not a current priority.

### Optional Settings improvements

These are suggestions from the completed review, not an agreed implementation scope:

- Copy a diagnostic summary after preview, excluding credentials and conversation content.
- Restore defaults by category, without deleting user data.
- Match individual settings in search and distinguish success/error feedback.

Completed features belong in [ARCHITECTURE.md](../ARCHITECTURE.md), not this backlog.
