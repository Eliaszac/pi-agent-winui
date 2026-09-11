import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";

/** Observes the optional adapter's public event bus; never imports it or connects servers. */
export default function registerMcpStatus(pi: ExtensionAPI): void {
    let context: ExtensionContext | undefined;
    let latest: string | undefined;
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
                return { name: row.name, status: row.status, toolCount: row.toolCount, resourceCount: row.resourceCount };
            });
            const json = JSON.stringify({ version: 1, servers });
            if (json.length > 262144) return;
            latest = json;
            publish();
        } catch { /* Optional status must never interrupt the adapter or the agent. */ }
    });
    pi.on("session_start", (_event, ctx) => { context = ctx; publish(); });
    pi.on("session_shutdown", () => { context = undefined; latest = undefined; });
}
