import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";

// Used only by the no-model-call runtime compatibility check.
export default function (pi: ExtensionAPI): void {
    pi.registerCommand("research-test-probe", {
        description: "Test-only research tool inventory",
        handler: async (_args, ctx) => { ctx.ui.notify(JSON.stringify({ active: pi.getActiveTools() }), "info"); },
    });
}
