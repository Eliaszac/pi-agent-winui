## Product purpose

A native Windows frontend for Pi that organizes coding conversations by named working directory.

## Primary user

A developer working with local source folders on a Windows desktop or laptop.

## Three to five principles

1. Projects provide conversation context: group conversations beneath their working directory in the left sidebar.
2. Creating a project is a focused modal task; preserve the surrounding workspace.
3. Give space back to the conversation: allow sidebar resizing, collapse, and drag-to-expand.
4. Follow Windows: native controls, system typography and accent, automatic light/dark themes, and keyboard access.

## Success metric for the surface

The user can register a named folder, reopen it after restarting, and work in multiple conversations simultaneously. Switching conversations preserves their drafts and running agents. The first project starts expanded; sidebar controls remain reachable when collapsed or narrow.

## Out of scope

- Manual theme selection.
- Project scaffolding or Git initialization.
- Rich Markdown/code editors, attachments, advanced session browsing, and multiple-window coordination beyond session locking.

## Learned constraints

- 2026-09-10: Model and approval selectors share one row with Send/Stop below the composer. Auto is the default approval mode when the supported extension has no saved mode; preserve existing conversation choices.

- 2026-09-10: Hide extension setup once the supported version is installed. Refresh is a tooltip-labeled icon at the top right of each card.

- 2026-09-10: Extensions use cards in a responsive two-to-three-column grid, falling back to one column in narrow windows. No Back to workspace button; sidebar conversation selection returns to chat.

- 2026-09-10: Build a global Extensions page for supported integrations, with installation guidance in a modal. Missing approval modes stay discoverable as a muted, clickable composer control; other integrations choose missing-feature visibility individually. Global installation must not make model or mode selections shared across conversations. First approval integration is explicitly a preview until live Windows verification.

- 2026-09-10: Conversation composer uses Enter to send and Ctrl+Enter for a newline without instructional copy. Keep the title/path header compact without Ready/provider/model text above the transcript. Model, Effort, and Approval share one row below the input with labels inside each dropdown, such as Effort: medium; no separate label row. A single icon action inside the input switches from an upward send arrow to a stop square while running; never show Send alongside Stop. Reserve text space and provide tooltips/accessibility names. Effort is native Pi functionality, conversation-specific, and defaults to low for new sessions when supported; preserve saved choices and list only the selected model's supported levels. Fast mode remains deferred.

- 2026-09-10: User approved sidebar management but rejected the duplicate + beside Projects. Keep that heading plain and use the existing New project button.
- 2026-09-10: Complete sidebar actions via context menus: conversation inline rename/delete/settle/restore and project rename/delete. Use a subtle expandable Settled — (count) group below each project's active conversations, initially collapsed. Settled is a user organization flag, separate from runtime completion. Add a new-project + beside the Projects heading. Inline rename uses Enter/blur to save and Escape to cancel.
- 2026-09-10: Use Pi's official compact badge for the native window icon. App title is Pi Agent; append the selected conversation title with an em dash. Keep native window controls. Install Pi screen was approved and its forced preview removed.
- 2026-09-10: Check for Pi when the app starts. If unavailable, replace the workspace with an Install Pi screen linking to the official website and explaining that the app must be restarted after installation. Use native system-theme styling.
- 2026-09-10: Connection management should be invisible in the normal workflow. Automatically prepare a selected conversation behind a local loading state; reveal transcript/composer together, keep sidebar and startup extension questions usable, and offer Retry only on failure. Keep active runs inline with Stop. Defer the model dropdown until the basic conversation loop is complete.
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

