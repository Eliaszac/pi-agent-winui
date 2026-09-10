# Project Context

This project is a native Windows GUI for the Pi coding agent, built with WinUI 3 and C#.

The goal is to use Pi as the underlying coding-agent harness while replacing its terminal user interface with a fully native Windows experience.

Pi remains responsible for the agent runtime and harness functionality, including model interaction, tool execution, sessions, context management, compaction, and related agent behavior. This project builds the product and user interface around that existing runtime rather than reimplementing the harness.

## Architecture

Read `ARCHITECTURE.md` for the agreed project model, MVP scope, implementation order, and decisions still to be resolved. Keep it current when those decisions change.

The intended integration is:

`WinUI 3 / C# -> Pi RPC -> Pi`

Pi runs as a separate process in RPC mode. The C# application communicates with it directly using Pi's RPC protocol over stdin/stdout.

A separate Node.js or TypeScript bridge is not currently intended. C# can implement the RPC client directly.

The GUI is not intended to be a Pi extension or a modification of Pi's TUI. It is a separate native frontend using Pi as its underlying agent engine.

Pi extensions may still be used where appropriate to extend the capabilities or behavior of the underlying agent.

Forking Pi is not currently intended. The preference is to use Pi's public RPC and extension interfaces and remain compatible with upstream Pi.

## Native Application Conventions

- Prefer MVVM. Views and code-behind handle presentation and view-specific interactions; view models expose presentation state and commands.
- Keep Pi process management, RPC transport, and protocol serialization in dedicated services or clients, outside views and view models.
- Pi is the source of truth for agent and session state. The GUI maintains presentation state and reflects Pi's documented responses and events rather than reimplementing agent behavior.
- Prefer constructor injection and compose dependencies at application startup. Keep the structure proportional to the project; add folders and abstractions only when needed.
- Follow WinUI and XAML conventions. General web-specific instructions about Express, frontend folder layouts, CSS, and animation libraries do not apply to this native application.
- Native WinUI icon controls and vector geometry are allowed alongside SVG. Use native theme resources, keyboard interaction, focus feedback, accessibility, and reduced-motion preferences where applicable.

## Pi Process and RPC Lifecycle

- Support concurrent conversations. Each connected conversation owns its own Pi process, RPC client, and session file. Sidebar selection must never switch a shared process or stop background conversations.
- Keep the project catalog separate from Pi-owned JSONL sessions. Stable project/conversation IDs determine session paths; do not duplicate message persistence in the catalog.
- Use asynchronous I/O and never block the UI thread while waiting for Pi. Marshal UI-bound state changes through the WinUI dispatcher when required.
- Handle process startup failures, unexpected exits, cancellation, and application shutdown explicitly. Dispose streams and process resources, and terminate only processes owned by this application when needed.
- Keep stdout protocol traffic separate from stderr diagnostics. Do not log credentials or sensitive prompt/tool content by default.
- Follow the documented RPC framing, request correlation, commands, and events. Tolerate unfamiliar events without crashing, and report malformed messages or unsupported operations clearly.
- Distinguish cancelling a local wait from cancelling an agent operation; use Pi's documented cancellation behavior where appropriate.
- Do not automatically replay requests after a process failure when doing so could repeat tool execution or other side effects.
- Keep the transport replaceable so protocol and presentation behavior can be unit-tested without starting Pi or contacting model providers.

## Development and Verification

- Reading files, inspecting Git state, and editing project files are allowed as part of an authorized task.
- Unit tests, ordinary builds, formatting, and static analysis are allowed without additional permission. This does not authorize unrelated custom scripts or side-effecting build targets.
- Launching the GUI, starting Pi, running dev servers, or executing other application or setup scripts requires explicit user permission. Existing permission for the same action remains valid within the task.
- Keep generated build output, IDE-local state, secrets, and signing private keys out of Git. Keep source assets, manifests, and shared publish profiles tracked.
- Prefer pinned dependency versions once a working baseline is established; do not change dependencies solely to perform documentation or repository housekeeping.

## Pi

Pi is an extensible coding-agent harness designed to support different models, tools, extensions, interfaces, and embedding scenarios.

Official Pi documentation:

https://pi.dev/docs

RPC documentation:

https://pi.dev/docs/latest/rpc

When working on Pi integration, consult the current Pi documentation rather than assuming protocol details, available commands, events, or extension behavior. The documentation may be looked up as needed.
