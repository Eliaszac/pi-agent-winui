import { readFileSync } from "node:fs";
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";

export default function (pi: ExtensionAPI): void {
    const enabled = (): boolean => {
        try { return JSON.parse(readFileSync(process.env.PI_GUI_RESEARCH_PREFERENCE ?? "", "utf8")) === true; }
        catch { return false; }
    };
    const update = (): void => {
        const tools = pi.getActiveTools().filter(name => name !== "background_research");
        pi.setActiveTools(enabled() ? [...tools, "background_research"] : tools);
    };
    pi.registerTool({
        name: "background_research", label: "Background research",
        description: "Dispatch a small, independent read-only question or research task to the user's sidepanel. Supply all relevant context in the question. Returns only an acknowledgement; you cannot collect results or manage the worker. Continue your own work without waiting. The user chooses whether to share results. Do not delegate work whose answer you need to finish your current task, or any file changes.",
        parameters: Type.Object({ title: Type.String({ maxLength: 120 }), question: Type.String({ maxLength: 24000 }) }),
        async execute(_id, parameters, signal, _update, ctx) {
            if (!enabled()) throw new Error("Background research is disabled.");
            const payload = JSON.stringify({ ...parameters, provider: ctx.model?.provider, model: ctx.model?.id, effort: pi.getThinkingLevel() });
            const result = await ctx.ui.input("pi-gui-background-research-v1", payload, { signal, timeout: 15000 });
            if (!result) throw new Error("The app did not accept this research task.");
            const reply: { accepted?: boolean; error?: string } = JSON.parse(result);
            if (!reply.accepted) throw new Error(reply.error ?? "Dispatch failed.");
            return { content: [{ type: "text", text: "Research accepted by the app. Results will appear only in the user's sidepanel. Continue your task; do not wait or poll for this work." }], details: {} };
        },
    });
    pi.on("session_start", update);
    pi.on("before_agent_start", update);
}
