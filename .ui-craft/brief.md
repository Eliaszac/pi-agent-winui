## Product purpose

- 2026-09-11: Keep approval, model and effort adjacent in a compact left-aligned group; put flexible space after effort, before the far-right Send/Stop actions.

- 2026-09-11: Composer toolbar has no plus/attachment action or approval glyph. Selectors read `Approval: <mode>` and `Effort: <level>`. Selector widths must remain stable when dropdowns open.

- Composer uses one rounded neutral surface with a borderless multiline draft and internal toolbar. Approval, model, and effort stay grouped on the left; Send/Stop stays on the right. There is no plus/attachment toolbar action. Speech input is deferred.

A native Windows frontend for Pi that organizes coding conversations by named working directory.

## Primary user

A developer using a Windows desktop with Local Windows, WSL, or Linux SSH workspaces.

## Three to five principles

1. Projects provide conversation context: group conversations beneath their working directory in the left sidebar.
2. Creating a project is a focused modal task; preserve the surrounding workspace.
3. Give space back to the conversation: allow sidebar resizing, collapse, and drag-to-expand.
4. Follow Windows: native controls, system typography, automatic light/dark themes, and keyboard access. Reserve the system accent for action buttons; keep disclosure and selection surfaces neutral.

## Success metric for the surface

The user can register a named folder, reopen it after restarting, and work in multiple conversations simultaneously. Switching conversations preserves their drafts and running agents. The first project starts expanded; sidebar controls remain reachable when collapsed or narrow.

## Out of scope

- Manual theme selection.
- Project scaffolding or Git initialization.
- Advanced session-tree browsing and a full standalone code editor. Markdown rendering, screenshot attachments, file previews, and coordinated app-owned workspace operations are implemented.

## Learned constraints

- 2026-09-12: Approved: the expanded sidebar uses a compact outlined folder-plus at the right of the Projects heading to open the existing New project dialog. It replaces the separate New project button and stays visually distinct from the plain plus used for new conversations. This supersedes the earlier preference against a heading-level add control.

- 2026-09-12: Home has no introductory “Pick up where you left off” sentence. Place the app version below About local usage. Center the complete content vertically when it fits the viewport; preserve scrolling and equal top/bottom padding when it does not.

- 2026-09-12: Global Home is accepted as the startup destination, with recent conversations across projects first, compact new/open/add project actions, then local usage summaries. Preserve project overviews and background conversations. Use native theme resources, a neutral daily usage chart, responsive model/project breakdowns, and explicit empty/partial-data states. Put Home navigation at the top of both sidebar layouts, omit the Home page heading, and show a quiet app version from build metadata. No separate analytics database or uploads; Settings (including the app version) and legal/privacy content remain deferred.

- 2026-09-12: Providers includes an Ollama card in the existing grid with automatic import of local tool-capable models. Manage connection accepts a remote server once, then syncs it automatically (including initially empty servers). Check automatically only at app startup; keep explicit Connect and sync for manual checks; preserve existing model settings and favorites. Show quiet counts/errors and read-only model statuses, without per-model import controls. Use loaded/saved context when available with a labeled 4,096 fallback. Provider/model search and Configured only never change onboarding inventory. Retain native card/dialog/theme resources.

- 2026-09-12: Model selection uses a two-pane dropdown: vertical provider navigation on the left with Favorites first and recognizable provider brand icons, searchable models on the right, and a separate favorite star per model. Keep the compact composer trigger and existing native system-theme styling. Favorites persist globally; selection remains per conversation.

- Run summaries sit directly beneath the assistant response, not in a viewport-separated footer. Show more/fewer files and tool disclosures stay neutral in checked, hover, and pressed states; retain high-contrast system colors.

- Project expand/collapse is disclosure only: preserve the current page, selected conversation, and open sidebar. Browsing groups must not navigate.

- 2026-09-10: Extensions has one page-header refresh icon for all recommended/supported cards and other installed extensions; no per-card or section refresh buttons.

- 2026-09-10: Use a traditional toggle switch for the research opt-in, rather than a text button labeled On/Off.

- 2026-09-10: Keep the research panel compact: On/Off in the header, explanation in an info flyout, a short empty state, and result actions beside the selected result. Show Cancel only for queued/running tasks; show Copy and Add to prompt only for completed results.

- 2026-09-10: Individual and grouped tool-call disclosures must stay neutral when expanded, hovered, or pressed. Do not use the Windows accent on disclosure surfaces or resize handles. Preserve system high-contrast accessibility colors.

- 2026-09-10: Terminal panel spans the full chat workspace height, including the conversation header. It must not begin beneath that header.

- 2026-09-10: Add a terminal icon button to the chat header, opening a right-side terminal panel with native tabs. Closing the last tab closes the panel. Hiding preserves sessions, new tabs use the selected project folder, and tab sessions remain independent when switching conversations. Support system theme, resizing and a narrow-window overlay.

- 2026-09-10: The existing completed-run file-change summary may show successful verification only: Lint passed and Tests x/x passed. Never create a card for checks alone or show failed/not-run/unknown check rows. Clear verification after later edits; use recognized tool commands and explicit test totals rather than assistant prose. No coverage claim for files outside the check's scope.

- 2026-09-10: Only show the GitHub header action after confirming that the selected project folder is inside an initialized Git working tree. Check locally even while signed out; an initial commit or remote is not required. Hide immediately when switching to an unverified folder.

- 2026-09-10: PR preview is now live integration. Show GitHub-logo Connect to GitHub in the chat header only when signed out; when connected, show Open PR only for a detected open PR. Use actual PR numbers in sidebar conversation metadata and context-menu navigation. Local project working-folder branch is shared by conversations. GitHub login uses a code-and-browser dialog, credential storage and automatic token refresh. Header context menu exposes refresh, repository access and disconnect without adding a full source-control panel.

- 2026-09-10: Extensions use cards in a responsive two-to-three-column grid, falling back to one column in narrow windows. No Back to workspace button; sidebar conversation selection returns to chat.

- 2026-09-10: Build a global Extensions page for supported integrations, with installation guidance in a modal. Missing approval modes stay discoverable as a muted, clickable composer control; other integrations choose missing-feature visibility individually. Global installation must not make model or mode selections shared across conversations. First approval integration is explicitly a preview until live Windows verification.

- 2026-09-10: Conversation composer uses Enter to send and Ctrl+Enter for a newline without instructional copy. Keep the title/path header compact without Ready/provider/model text above the transcript. Model, Effort, and Approval share one row below the input with labels inside each dropdown, such as Effort: medium; no separate label row. A single icon action inside the input switches from an upward send arrow to a stop square while running; never show Send alongside Stop. Reserve text space and provide tooltips/accessibility names. Effort is native Pi functionality, conversation-specific, and defaults to low for new sessions when supported; preserve saved choices and list only the selected model's supported levels. Fast mode remains deferred.

- 2026-09-10: User approved sidebar management but rejected the duplicate + beside Projects. Keep that heading plain and use the existing New project button.
- 2026-09-10: Complete sidebar actions via context menus: conversation inline rename/delete/settle/restore and project rename/delete. Use a subtle expandable Settled — (count) group below each project's active conversations, initially collapsed. Settled is a user organization flag, separate from runtime completion. Inline rename uses Enter/blur to save and Escape to cancel.
- 2026-09-10: Use Pi's official compact badge for the native window icon. App title is Pi desktop; append the selected conversation title with an em dash. Keep native window controls. Install Pi screen was approved and its forced preview removed.
- 2026-09-10: Check for Pi when the app starts. If unavailable, replace the workspace with an Install Pi screen linking to the official website and explaining that the app must be restarted after installation. Use native system-theme styling.
- 2026-09-10: Connection management should be invisible in the normal workflow. Automatically prepare a selected conversation behind a local loading state; reveal transcript/composer together, keep sidebar and startup extension questions usable, and offer Retry only on failure. Keep active runs inline with Stop.
- 2026-09-10: User explicitly requires more than one active conversation in the first runtime pass. Keep background runs alive on selection changes; each conversation owns its composer, messages, questions, and Stop action. Show background activity in the sidebar.
- 2026-09-10: Native window chrome must follow the system app theme as well as the XAML content; a light title bar over a dark workspace is inconsistent.

- 2026-09-10: User specified project creation in a modal, New project at the sidebar top, and New conversation on each collapsible project header.
- 2026-09-10: User specified system light/dark themes and sidebar resizing with drag-to-collapse/expand. Existing conversation approval supplies the design brief; native resources supply styling defaults.
- 2026-09-10: User reported stale-state flashes during project creation and other transitions. Commit the destination workspace before dismissing a modal, show loading instead of premature empty states, and publish restored selections directly without intermediate screens.

- 2026-09-10: User confirmed the model startup selection fix. Chat messages have no You/Pi labels: user prompts are right-aligned bubbles, assistant replies plain text on the left. Add a copy icon beneath each; show the response copy control only once the message is complete. Preserve system light/dark styling.

- 2026-09-10: Successful message copying briefly replaces the copy icon with a checkmark. Crossfade over 180 ms, restore after 1.6 seconds, restart the confirmation on repeated clicks, and skip animation when Windows animations are disabled. Reset feedback when a virtualized message control is reused.

- 2026-09-10: Clicking the conversation header title starts inline rename; Enter or blur saves, Escape cancels. Use a separate editor state from sidebar rename. Project paths default to drive + ellipsis + folder (network paths hide server/share parents). Click to reveal for five seconds; hide on blur, navigation, or a second click. Sidebar path tooltips are also redacted.

- 2026-09-10: Sidebar conversation rows use far-right red (approval needed), blue (running), or green (unseen completion) dots with descriptive tooltips. Hide dots on the selected conversation. Opening acknowledges green permanently until the next background completion; red takes precedence over blue.

- 2026-09-10: Render basic Markdown in assistant responses. Fenced code has a themed box, language/filename header, and code-only copy confirmation. Keep tool calls muted and compact; more than three consecutive calls collapse into Called N tools, expandable to inspect each. Preserve message ordering and group expansion during streaming.

- 2026-09-10: Write/edit tool summaries show the affected file and +added / minus-removed line counts. Expanding displays a colored unified diff instead of only Successfully wrote. Never fabricate removed-line counts without a baseline; retain real error/output details when no patch is available.

- 2026-09-10: Slash commands open at a word boundary anywhere in the composer, not only at its beginning. Show a compact themed list above the input with command descriptions, origin labels, keyboard selection, and filtering. Preserve surrounding draft text when running actions. Pi skills/templates use a leading command in the draft because Pi requires that for expansion.

- 2026-09-10: Fork and Clone are compact icon buttons beside Copy under the latest completed agent response only; hide them during runs. Fork opens an independent continuation, Clone adds the copy to the same project and keeps the original selected. Keep hover cursors, tooltips, accessible names, and theme-aware native styling. Providers has its own global page; omit import, session settings, and branch navigation.

- 2026-09-10: Providers is a global page matching the Extensions card grid, reachable above Extensions in both sidebar states. Show configured providers first, configuration/source, model details, and Set up/Manage. Use native modals with masked secrets for sign-in, keys, device codes, and setup links. No back button or forced conversation selection; omit account/quota values Pi does not expose.

- 2026-09-10: Show whole-run reported tokens and elapsed time beside the final response action buttons only once the run finishes. Use muted 12px text, seconds below one minute, then minutes and seconds. Preserve existing response controls and avoid an extra response header. Processing has its own live elapsed label.

- 2026-09-12: Both provider navigation in the model dropdown and Providers cards prioritize Anthropic, OpenAI, OpenAI Codex, then Ollama. Favorites stays first in the dropdown; other providers retain their existing relative ordering.
- 2026-09-12: Clear the composer immediately on Send, before RPC acknowledgement or state refresh. Restore failed submissions without overwriting a newer draft.
- 2026-09-12 correction: Inherited composer icon Foreground did not resolve contrast in the live app. Explicitly use black send/stop shapes in dark mode (white button), and white shapes in light mode (dark button); update on ActualThemeChanged.

## Current completion and data controls

- Workspace checkpoints is done and opt-in. Its Extensions button reads Manage; management includes Clear stored data with confirmation.
- Revert and Undo are embedded actions in completed change-summary cards. Show created, modified, and deleted files. Ask agent to revert is removed.
- Hide the empty-conversation prompt while processing. Show elapsed seconds, then minutes/seconds after one minute.
- Conversation deletion explains removal of saved messages/screenshots and restore data while preserving project files. Snapshot retention and unavailable recovery must remain truthful.

- MCP Quick integrations opens a native card list from the MCP panel. Each card includes a logo, name, description, publisher, Documentation, and Add. Add opens native authentication setup with Save only and an isolated connection check. Configured cards offer Manage; MCP inventory cards group connection, JSON editing, and confirmed removal in a Manage menu. Saving alone never claims connectivity. Editing/removal preserve unrelated definitions and reject concurrent changes.
- MCP inventory includes saved global definitions before runtime loading and while disconnected. Show configured/disabled/live states truthfully and make available error details selectable. Obsidian quick setup chooses a vault for a local stdio server.

## Response Markdown and tables

- Keep response typography native: 15px body text with 24px line spacing, distinct headings, and quieter inline code. Preserve surrounding response buttons, summaries, and tool-call controls.
- Conversation responses use 70% of available transcript width, with a 680px reading width where space permits and available width below that. Other Markdown previews retain their existing width.
- Preserve nested-list hierarchy with an additional 16px indent beyond the parent gutter, hollow second-level bullets, and square deeper bullets. Markers share the text baseline with compact spacing.
- Tables show up to ten measured data rows with a sticky header. The vertical scrollbar overlays the left edge; the horizontal scrollbar sits below the header and starts at the first column. Header and body scroll together horizontally.
- Header clicks toggle ascending/descending with a direction arrow. Sort numbers or unambiguous ISO/named-month dates when the whole populated column matches; otherwise use case-insensitive text. Keep equal values stable and empty cells last.
- Export CSV, Export JSON, and Original order belong in the vertical-dots menu at the header's far right. Match the header background and height, with native hover feedback and no extra toolbar row.
- Exports include every row in current visual order as readable text. JSON values remain strings; blank or duplicate headers get unique keys. Sorting never changes the saved transcript.
- Export feedback must not resize the transcript. Tail-follow responds to new Markdown content, not presentation changes such as sorting or resizing.
- Approval request redesign is complete and user-approved. Keep the inline card with a distinct heading, separate selectable command preview, target and working-folder context, visible response choices, and Cancel/Submit actions. Preserve Pi's option wording and response values; require an explicit choice before submission.
- Do not launch previews or perform manual UI checks unless the user requests it again.
- The expanded sidebar uses one virtualized list for project headers, conversations and settled-group headers. Keep a small viewport cache rather than paging or hiding conversations after a fixed count. Preserve the original view models for selection, live status and inline rename state, and scroll a selected conversation into view after navigation.
- Command palette opens and closes with two short unmodified Shift taps (hint: “Shift + Shift”), including from terminals. Escape closes it. Until the provider inventory has a configured provider, show only the provider setup state. Otherwise show searchable, context-aware commands with keyboard selection and nested choices. Rank empty searches by persisted use count, then recency; search relevance wins over usage. Close the palette before opening another dialog, and reuse existing confirmations.
- Side panels use a shared reorderable tab strip while preserving their existing content. Each conversation retains tabs, selected tab and order until app shutdown. Only terminals allow multiple tabs; rename through the tab context menu or F2. Closing a terminal ends its process, while hiding the panel preserves it. Use compact icon-and-label launcher rows when no tabs are open, showing only available panels.
- Code blocks: completed assistant snippets support Save to project and Run for PowerShell/Bash/Python as compact header icons beside Copy, with the target in accessible tooltips. Show inline status/output and elapsed time, with Stop in the header. Output expansion, Copy output, Add output to prompt, and Dismiss are compact icons beside the result status. Dismiss clears completed results; idle blocks have no empty result area. No save picker, overwrite, extra confirmation, dependency install, or automatic agent turn. Keep surrounding response controls.
- Settings uses native collapsible categories and a search field. Search reveals matching categories without losing their prior expansion state. Keep destructive actions explicit and confirmed, distinguish project registrations from source folders, and put the full legal/privacy documents on a separate page linked from About. Version uses the same build metadata as Home.
