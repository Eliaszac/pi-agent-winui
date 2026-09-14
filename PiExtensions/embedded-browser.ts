import type { ExtensionAPI, ExtensionContext, ExtensionCommandContext, ToolDefinition } from "@earendil-works/pi-coding-agent";
import { pathToFileURL } from "node:url";
import { join } from "node:path";

/** Adapts installed Pi Browser tools to native conversation-owned WebView2 tabs. */
export default async function (pi: ExtensionAPI): Promise<void> {
    const root = process.env.PI_GUI_BROWSER_PACKAGE;
    if (!root) return;
    const { default: browser } = await import(pathToFileURL(join(root, "index.ts")).href);
    const tools: ToolDefinition[] = [];
    let command: ((args: string, ctx: ExtensionCommandContext) => Promise<void> | void) | undefined;
    await browser({ ...pi,
        registerTool(tool: ToolDefinition) { tools.push(tool); },
        registerCommand(name: string, options: { handler: typeof command }) { if (name === "browser") command = options.handler; },
    });
    let port = 0;
    let pending: Promise<unknown> = Promise.resolve();
    pi.on("before_agent_start", async event => ({ systemPrompt: event.systemPrompt +
        "\nUse embedded_browser_* tools for websites and previews in Pi desktop. These control this conversation's visible browser tabs. Use embedded_browser_tabs to create, select, or close them. Start development servers separately on the conversation's execution target; navigate to their localhost URL and Pi desktop forwards the port. Browser control and cookies live on Windows even for remote workspaces. Do not use browser_launch or external browser tools to show an embedded preview." }));
    const request = async (ctx: ExtensionContext, data: Record<string, unknown>): Promise<Record<string, unknown>> => {
        const response = await ctx.ui.input("pi-gui-browser-v1", JSON.stringify(data));
        if (!response) throw new Error("Browser request cancelled.");
        const value: unknown = JSON.parse(response);
        if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid browser response.");
        const result = value as Record<string, unknown>;
        if (typeof result.error === "string") throw new Error(result.error);
        return result;
    };
    for (const tool of tools) {
        // These tools operate on Windows files, not the remote workspace.
        if (process.env.PI_GUI_EXECUTION_TARGET && ["browser_file_upload", "browser_storage_state", "browser_set_storage_state"].includes(tool.name)) continue;
        pi.registerTool({ ...tool, name: "embedded_" + tool.name,
            description: tool.description + " Uses this conversation's embedded browser panel. Prefer these tools for showing previews to the user.",
            execute(id, params, signal, onUpdate, ctx) {
                const work = pending.then(async () => {
                if (signal?.aborted) throw new Error("Browser operation cancelled.");
                if (tool.name === "browser_tabs" || tool.name === "browser_close") {
                    const result = await request(ctx, { ...params, action: "tabs", operation: tool.name === "browser_close" ? "close" : params.action });
                    return { content: [{ type: "text" as const, text: JSON.stringify(result) }], details: result };
                }
                const target = await request(ctx, { action: "ready" });
                if (typeof target.port !== "number" || !command) throw new Error("Browser connection is unavailable.");
                if (port !== target.port) {
                    let failure: string | undefined;
                    await command(`connect ${target.port}`, { ...ctx, ui: { ...ctx.ui,
                        notify(message: string, level?: string) { if (level === "error") failure = message; },
                    } } as ExtensionCommandContext);
                    if (failure) { await request(ctx, { action: "error", message: failure }); throw new Error(failure); }
                    port = target.port;
                }
                let input = params;
                if (tool.name === "browser_navigate") {
                    const resolved = await request(ctx, { action: "resolve", url: params.url });
                    input = { ...params, url: resolved.url };
                }
                if (signal?.aborted) throw new Error("Browser operation cancelled.");
                try { return await tool.execute(id, input, signal, onUpdate, ctx); }
                catch (error) { port = 0; throw error; }
                });
                pending = work.catch(() => {});
                return work;
            },
        });
    }
}
