import type { ExtensionAPI, ExtensionCommandContext } from "@earendil-works/pi-coding-agent";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { mcpErrorText } from "./McpErrorText.ts";

/** A single-server adapter instance. No chat, model requests, or ambient workspace configuration. */
export default async function (pi: ExtensionAPI): Promise<void> {
    const root = process.env.PI_GUI_MCP_AGENT_DIR;
    const name = process.env.PI_GUI_MCP_SERVER;
    if (!root || !name) throw new Error("Missing MCP management target.");
    const packageRoot = process.env.PI_GUI_MCP_PACKAGE_DIR ?? join(root, "npm", "node_modules", "pi-mcp-adapter");
    const { loadMcpConfig } = await import(pathToFileURL(join(packageRoot, "dist", "config.js")).href);
    const config = loadMcpConfig(join(root, "mcp.json"), process.cwd());
    const definition = config.mcpServers[name];
    if (!definition) throw new Error("MCP server is not configured.");
    const { createMcpAdapter } = await import(pathToFileURL(join(packageRoot, "index.ts")).href);
    type Handler = (args: string, ctx: ExtensionCommandContext) => Promise<void> | void;
    const commands = new Map<string, Handler>();
    let status: Record<string, unknown> | undefined;
    pi.events.on("pi-mcp-adapter/status/v1", (value: unknown) => {
        if (!value || typeof value !== "object" || !("servers" in value) || !Array.isArray(value.servers)) return;
        status = value.servers.find((row: Record<string, unknown>) => row.name === name);
    });
    await createMcpAdapter({ config: { mcpServers: { [name]: { ...definition, lifecycle: "lazy", directTools: false } }, settings: { scriptMode: false } } })({
        ...pi,
        registerCommand(command: string, options: { handler: Handler }) { commands.set(command, options.handler); },
    });
    pi.registerCommand("pi-gui-mcp-manage", {
        description: "Internal MCP connection management",
        async handler(action, ctx) {
            const emit = (payload: object) => ctx.ui.notify(JSON.stringify({ piGuiMcp: 1, ...payload }), "info");
            if (action !== "connect" && action !== "login") { emit({ kind: "error", message: "Unsupported MCP action." }); return; }
            let failure: string | undefined;
            let warning: string | undefined;
            const ui = {
                ...ctx.ui,
                notify(message: string, level?: string) {
                    const safe = mcpErrorText(message) ?? "";
                    if (level === "error") failure = safe;
                    if (level === "warning") warning = safe;
                    emit({ kind: "progress", message: safe });
                },
            };
            try {
                const handler = commands.get(action === "login" ? "mcp-auth" : "mcp");
                if (!handler) throw new Error("The adapter does not support this operation.");
                await handler(action === "login" ? name : `reconnect ${name}`, { ...ctx, ui } as ExtensionCommandContext);
                emit({ kind: "result", connected: status?.status === "connected", state: status?.status ?? "not-connected",
                    toolCount: status?.toolCount ?? 0, message: failure ?? (status?.status === "connected" ? "Connection verified." : warning ?? `The adapter reported ${String(status?.status ?? "no connection status")} for ${name}. Check the saved command, vault path, and whether the server is disabled.`) });
            } catch (error) { emit({ kind: "error", message: mcpErrorText(error instanceof Error ? error.message : String(error)) ?? "Connection failed." }); }
        },
    });
}
