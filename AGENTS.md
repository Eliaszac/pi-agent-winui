# Project Context

This project is a native Windows GUI for the Pi coding agent, built with WinUI 3 and C#.

The goal is to use Pi as the underlying coding-agent harness while replacing its terminal user interface with a fully native Windows experience.

Pi remains responsible for the agent runtime and harness functionality, including model interaction, tool execution, sessions, context management, compaction, and related agent behavior. This project builds the product and user interface around that existing runtime rather than reimplementing the harness.

## Architecture

The intended integration is:

`WinUI 3 / C# -> Pi RPC -> Pi`

Pi runs as a separate process in RPC mode. The C# application communicates with it directly using Pi's RPC protocol over stdin/stdout.

A separate Node.js or TypeScript bridge is not currently intended. C# can implement the RPC client directly.

The GUI is not intended to be a Pi extension or a modification of Pi's TUI. It is a separate native frontend using Pi as its underlying agent engine.

Pi extensions may still be used where appropriate to extend the capabilities or behavior of the underlying agent.

Forking Pi is not currently intended. The preference is to use Pi's public RPC and extension interfaces and remain compatible with upstream Pi.

## Pi

Pi is an extensible coding-agent harness designed to support different models, tools, extensions, interfaces, and embedding scenarios.

Official Pi documentation:

https://pi.dev/docs

RPC documentation:

https://pi.dev/docs/latest/rpc

When working on Pi integration, consult the current Pi documentation rather than assuming protocol details, available commands, events, or extension behavior. The documentation may be looked up as needed.
