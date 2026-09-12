import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";
import { mcpErrorText } from "./McpErrorText.ts";

/** Observes the optional adapter's public event bus; never imports it or connects servers. */
export default function registerMcpStatus(pi: ExtensionAPI): void {
    let context: ExtensionContext | undefined;
    let latest: string | undefined;
    const errors = new Map<string, string>();
    const publish = () => {
        if (context && latest) context.ui.setStatus("pi-gui-mcp-status-v1", latest);
    };
    pi.events.on("pi-mcp-adapter/status/v1", (value: unknown) => {
        try {
            if (!value || typeof value !== "object" || !("version" in value) || value.version !== 1 ||
                !("servers" in value) || !Array.isArray(value.servers) || value.servers.length > 256) return;
            const servers = value.servers.map((server: unknown) => {
                if (!server || typeof server !== "object") return null;
                const row = server as Record<string, unknown>;
                const name = typeof row.name === "string" ? row.name : "";
                if (row.status === "connected" && row.toolCount !== undefined) errors.delete(name);
                return { name: row.name, status: row.status, toolCount: row.toolCount, resourceCount: row.resourceCount,
                    error: mcpErrorText(row.error) ?? errors.get(name) };
            });
            const json = JSON.stringify({ version: 1, servers });
            if (json.length > 262144) return;
            latest = json;
            publish();
        } catch { /* Optional status must never interrupt the adapter or the agent. */ }
    });
    pi.on("tool_result", (event) => {
        if (event.toolName !== "mcp" || !event.details || typeof event.details !== "object") return;
        const details = event.details as Record<string, unknown>;
        if (typeof details.server !== "string" || details.server.length > 256) return;
        const error = mcpErrorText(details.message);
        if (!error || !(event.isError || details.error)) return;
        if (errors.size >= 256 && !errors.has(details.server)) return;
        errors.set(details.server, error);
        if (!latest) return;
        try {
            const snapshot = JSON.parse(latest);
            for (const row of snapshot.servers) if (row.name === details.server) row.error = error;
            latest = JSON.stringify(snapshot);
            publish();
        } catch { /* A malformed optional snapshot must not affect tool execution. */ }
    });
    pi.on("session_start", (_event, ctx) => { context = ctx; publish(); });
    pi.on("session_shutdown", () => { context = undefined; latest = undefined; errors.clear(); });
}
