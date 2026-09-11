import { createHash } from "node:crypto";
import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";

/** Reports loaded context identities and hashes, never instruction contents or prompt mutations. */
export default function registerInstructions(pi: ExtensionAPI): void {
    const publish = (ctx: ExtensionContext, files: readonly { path: string; content: string }[] | undefined) => {
        const snapshot = files === undefined ? { version: 1, available: false, files: [] } : {
            version: 1, available: true,
            files: files.slice(0, 256).map(file => ({ path: file.path, hash: createHash("sha256").update(file.content, "utf8").digest("hex") }))
        };
        ctx.ui.setStatus("pi-gui-instructions-v1", JSON.stringify(snapshot));
    };
    pi.registerCommand("pi-gui-instructions", {
        description: "Internal loaded instruction inventory for Pi desktop",
        handler(_args, ctx) {
            publish(ctx, typeof ctx.getSystemPromptOptions === "function" ? ctx.getSystemPromptOptions().contextFiles ?? [] : undefined);
        }
    });
    pi.on("before_agent_start", (event, ctx) => { publish(ctx, event.systemPromptOptions?.contextFiles); });
}
