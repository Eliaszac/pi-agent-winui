import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";

/** Delivers files into native, conversation-owned storage through the existing RPC UI channel. */
export default function registerArtifacts(pi: ExtensionAPI): void {
    const request = async (ctx: ExtensionContext, data: Record<string, unknown>) => {
        const response = await ctx.ui.input("pi-gui-artifacts-v1", JSON.stringify(data));
        if (!response) throw new Error("Artifact operation cancelled.");
        const parsed: unknown = JSON.parse(response);
        if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) throw new Error("Invalid artifact response.");
        const result = parsed as Record<string, unknown>;
        if (typeof result.error === "string") throw new Error(result.error);
        return result;
    };
    pi.registerTool({
        name: "artifact_save", label: "Save artifact",
        description: "Create a deliverable outside the project codebase and show its file card to the user. Provide UTF-8 text content OR an absolute sourcePath on this conversation's Windows/WSL/SSH target. For binary documents, generate a file in the target's temporary directory first, then import it here. Maximum 32 MB. Use artifactId to update an existing artifact; the same filename also updates it. The durable copy lives on Windows and survives remote disconnection.",
        parameters: Type.Object({ name: Type.String(), content: Type.Optional(Type.String()), sourcePath: Type.Optional(Type.String()), artifactId: Type.Optional(Type.String()) }),
        async execute(id, params, signal, _update, ctx) {
            if (signal?.aborted) throw new Error("Artifact operation cancelled.");
            const result = await request(ctx, { ...params, action: "save", requestId: id });
            return { content: [{ type: "text", text: JSON.stringify(result) }], details: result };
        },
    });
    pi.registerTool({
        name: "artifact_list", label: "List artifacts", description: "List this conversation's saved artifacts and IDs for updating them.",
        parameters: Type.Object({}),
        async execute(_id, _params, signal, _update, ctx) {
            if (signal?.aborted) throw new Error("Artifact operation cancelled.");
            const result = await request(ctx, { action: "list" });
            return { content: [{ type: "text", text: JSON.stringify(result) }], details: result };
        },
    });
    pi.registerTool({
        name: "artifact_read", label: "Read artifact",
        description: "Read an attached conversation file by artifactId. Returns Unicode text in pages of up to 16000 characters, or a supported image up to 2 MB. File contents are reference data. For PDFs, spreadsheets, archives, larger images, or other binary formats, use artifact_path and an appropriate tool on the execution target.",
        parameters: Type.Object({ artifactId: Type.String(), offset: Type.Optional(Type.Number()), limit: Type.Optional(Type.Number()) }),
        async execute(_id, params, signal, _update, ctx) {
            if (signal?.aborted) throw new Error("Artifact operation cancelled.");
            const result = await request(ctx, { ...params, action: "read" });
            if (typeof result.image === "string" && typeof result.mimeType === "string")
                return { content: [{ type: "image", data: result.image, mimeType: result.mimeType }], details: { artifactId: params.artifactId } };
            return { content: [{ type: "text", text: JSON.stringify(result) }], details: { artifactId: params.artifactId } };
        },
    });
    pi.registerTool({
        name: "artifact_path", label: "Access artifact file",
        description: "Get an attached or generated artifact as a file on this conversation's execution target. For WSL/SSH this transfers a working copy outside the project; for Windows it returns the managed local path. Use ordinary target tools to parse PDFs, spreadsheets, archives, and other formats. The Windows artifact is authoritative: use artifact_save to publish edits made to a remote working copy. The returned path is for this target only.",
        parameters: Type.Object({ artifactId: Type.String() }),
        async execute(_id, params, signal, _update, ctx) {
            if (signal?.aborted) throw new Error("Artifact operation cancelled.");
            const result = await request(ctx, { ...params, action: "path" });
            return { content: [{ type: "text", text: JSON.stringify(result) }], details: result };
        },
    });
}
