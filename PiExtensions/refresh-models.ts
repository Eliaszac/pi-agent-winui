import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";

export default function (pi: ExtensionAPI): void {
    pi.registerCommand("pi-gui-refresh-models", {
        description: "Internal model availability refresh for Pi Agent",
        async handler(_args, ctx) {
            if (!ctx.isIdle()) throw new Error("Wait for this conversation to finish.");
            const result = await ctx.modelRegistry.refresh({ allowNetwork: false, signal: AbortSignal.timeout(60_000) });
            if (result.aborted || result.errors.size > 0 || ctx.modelRegistry.getError())
                throw new Error("Model availability couldn't be refreshed. Check your provider configuration.");
        },
    });
}
