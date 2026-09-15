import { createWriteToolDefinition, generateUnifiedPatch, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { WriteDiffCapture } from "./WriteDiffCapture.ts";
import registerSessionCopy from "./session-copy.ts";
import registerModelRefresh from "./refresh-models.ts";
import registerMcpStatus from "./mcp-status.ts";
import registerInstructions from "./instructions.ts";
import registerCheckpoints from "./checkpoints.ts";
import registerArtifacts from "./artifacts.ts";
import registerGitHubWrites from "./github-write.ts";
import * as piRuntime from "@earendil-works/pi-coding-agent";
import { StartupHandlerTiming } from "./StartupHandlerTiming.ts";

export default function (pi: ExtensionAPI): void {
    if (process.env.PI_TIMING === "1") {
        // Runtime shape is version-dependent; diagnostics must never prevent loading.
        try {
            StartupHandlerTiming.install(piRuntime.ExtensionRunner.prototype as unknown as Parameters<typeof StartupHandlerTiming.install>[0],
                (label, ms) => process.stderr.write(`PI_GUI_HANDLER ${label} ${ms}\n`));
        } catch { /* Unsupported runner versions keep normal startup behavior. */ }
    }
    registerArtifacts(pi);
    registerGitHubWrites(pi);
    registerCheckpoints(pi);
    registerSessionCopy(pi);
    registerModelRefresh(pi);
    registerMcpStatus(pi);
    if (process.env.PI_GUI_EXECUTION_TARGET) return;
    registerInstructions(pi);
    const base = createWriteToolDefinition(process.cwd());
    pi.registerTool({
        name: base.name,
        label: base.label,
        description: base.description,
        parameters: base.parameters,
        promptSnippet: base.promptSnippet,
        promptGuidelines: base.promptGuidelines,
        async execute(id, params, signal, _onUpdate, ctx) {
            const capture = new WriteDiffCapture(params.path, generateUnifiedPatch);
            const tool = createWriteToolDefinition(ctx.cwd, { operations: capture.operations });
            const result = await tool.execute(id, params, signal, undefined, ctx);
            return { ...result, details: { piGuiFileChange: {
                version: 1, path: params.path, patch: capture.patch, kind: capture.kind,
                unavailable: capture.patch === null ? "Diff unavailable: previous text could not be read, or the file exceeds the preview limit." : null,
            } } };
        },
    });
}
