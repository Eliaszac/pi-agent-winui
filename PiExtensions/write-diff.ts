import { createWriteToolDefinition, generateUnifiedPatch, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { WriteDiffCapture } from "./WriteDiffCapture.ts";
import registerSessionCopy from "./session-copy.ts";
import registerModelRefresh from "./refresh-models.ts";

export default function (pi: ExtensionAPI): void {
    registerSessionCopy(pi);
    registerModelRefresh(pi);
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
                version: 1, path: params.path, patch: capture.patch,
                unavailable: capture.patch === null ? "Diff unavailable: previous text could not be read, or the file exceeds the preview limit." : null,
            } } };
        },
    });
}
