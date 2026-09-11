## Product purpose

- 2026-09-11: Keep approval, model and effort adjacent in a compact left-aligned group; put flexible space after effort, before the far-right Send/Stop actions.

- 2026-09-11: Composer toolbar has no plus/attachment action or approval glyph. Selectors read `Approval: <mode>` and `Effort: <level>`. Selector widths must remain stable when dropdowns open.

- 2026-09-11: Composer follows the supplied compact chat reference: one rounded neutral surface, borderless multiline draft above an internal toolbar, plus/file action and approval left, model/effort and neutral circular Send/Stop right. Narrow widths wrap the toolbar. Speech input is explicitly deferred.

A native Windows frontend for Pi that organizes coding conversations by named working directory.

## Primary user

A developer working with local source folders on a Windows desktop or laptop.

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
- Rich Markdown/code editors, attachments, advanced session browsing, and multiple-window coordination beyond session locking.

## Learned constraints

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

- 2026-09-10: PR UI starts as an explicitly hardcoded styling preview: Open PR beside Open in in the conversation header, subtle PR number beneath sidebar conversation titles, and an Open PR context-menu entry. Preview uses PR #123 and has no lookup, navigation, authentication or persisted metadata. Future scope is branch-based open-PR detection plus a possible Git CLI diff/commit panel.

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

- 2026-09-10: Fork and Clone are compact icon buttons beside Copy under the latest completed agent response only; hide them during runs. Fork opens an independent continuation, Clone adds the copy to the same project and keeps the original selected. Keep hover cursors, tooltips, accessible names, and theme-aware native styling. Providers will have a separate design; omit import, session settings, and branch navigation.

- 2026-09-10: Providers is a global page matching the Extensions card grid, reachable above Extensions in both sidebar states. Show configured providers first, configuration/source, model details, and Set up/Manage. Use native modals with masked secrets for sign-in, keys, device codes, and setup links. No back button or forced conversation selection; omit account/quota values Pi does not expose.

- 2026-09-10: Show whole-run reported tokens and elapsed time beside the final response action buttons only once the run finishes. Use muted 12px text, seconds below one minute, then minutes and seconds. Preserve existing response controls and avoid a live counter or extra response header.

