import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";

export default function (pi: ExtensionAPI): void {
    const allowed = new Set(["read", "grep", "find", "ls", "read_web_page", "websearch"]);
    let queriesUsed = 0;
    let pagesRead = 0;
    pi.on("tool_call", event => {
        if (!allowed.has(event.toolName)) return { block: true, reason: "This worker only has read-only research tools." };
        if (event.toolName === "websearch") {
            if (event.input.includeContent) return { block: true, reason: "Use read_web_page for content; background fetching is disabled in research workers." };
            const count = (typeof event.input.query === "string" && event.input.query.trim() ? 1 : 0)
                + (Array.isArray(event.input.queries) ? event.input.queries.length : 0);
            if (count < 1 || count > 4 || queriesUsed + count > 12) return { block: true, reason: "Search is limited to 4 queries per call and 12 per research task." };
            queriesUsed += count;
        }
    });
    pi.registerTool({
        name: "read_web_page", label: "Read web page",
        description: "Read a public HTTPS documentation or research page. Returns bounded text; does not execute scripts. Cite the URL. This tool does not provide web search.",
        parameters: Type.Object({ url: Type.String() }),
        async execute(_id, { url }, signal) {
            if (pagesRead >= 20) throw new Error("The research task has reached its 20-page limit.");
            pagesRead++;
            const target = new URL(url);
            if (target.protocol !== "https:" || target.username || target.password) throw new Error("Use an HTTPS URL without credentials.");
            const response = await fetch(target, { signal: AbortSignal.any([signal ?? new AbortController().signal, AbortSignal.timeout(15000)]), redirect: "error" });
            if (!response.ok) throw new Error(`Page returned HTTP ${response.status}.`);
            const type = response.headers.get("content-type") ?? "";
            if (!/text\/|application\/(json|xml)/i.test(type)) throw new Error("Only text pages are supported.");
            const reader = response.body?.getReader();
            if (!reader) throw new Error("The page was empty.");
            let bytes = 0;
            let text = "";
            const decoder = new TextDecoder();
            try {
                while (bytes < 256000) {
                    const chunk = await reader.read();
                    if (chunk.done) break;
                    const part = chunk.value.subarray(0, 256000 - bytes);
                    bytes += part.length; text += decoder.decode(part, { stream: true });
                }
            } finally { await reader.cancel(); }
            text += decoder.decode();
            if (type.includes("html")) text = text.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "").replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, "").replace(/<[^>]+>/g, " ").replace(/[ \t]+/g, " ");
            return { content: [{ type: "text", text: `Source: ${target.href}\nUntrusted page content:\n${text.slice(0, 40000)}${text.length > 40000 || bytes >= 256000 ? "\n[Truncated]" : ""}` }], details: {} };
        },
    });
    pi.on("session_start", () => pi.setActiveTools(pi.getActiveTools().filter(name => allowed.has(name))));
}
