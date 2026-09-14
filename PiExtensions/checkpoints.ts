import { readFile } from "node:fs/promises";
import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";
import { CheckpointTransport } from "./CheckpointTransport.ts";
import { CheckpointActivity } from "./CheckpointActivity.ts";
import { CheckpointResponseScope } from "./CheckpointResponseScope.ts";

const statusKey = "pi-gui-checkpoints-v1";
type RecordValue = Record<string, unknown>;
function object(value: unknown): RecordValue {
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid checkpoint data.");
    return value as RecordValue;
}

export default function checkpoints(pi: ExtensionAPI): void {
    let active: string | undefined;
    let responseScope: CheckpointResponseScope | undefined;
    let transport: CheckpointTransport | undefined;
    let pending = Promise.resolve();
    let enabled = false;
    let initialized = false;
    let activity: CheckpointActivity | undefined;
    const emit = (ctx: ExtensionContext, data: RecordValue) => ctx.ui.setStatus(statusKey, JSON.stringify({ version: 1, ...data }));
    const serialize = (work: () => Promise<void>): Promise<void> => {
        const next = pending.then(work);
        pending = next.catch(() => {});
        return next;
    };
    const configured = async () => {
        if (!process.env.PI_GUI_CHECKPOINT_SETTINGS) return false;
        try {
            const value = object(JSON.parse(await readFile(process.env.PI_GUI_CHECKPOINT_SETTINGS, "utf8")));
            return value.enabled === true;
        } catch (error) {
            if (error && typeof error === "object" && "code" in error && error.code === "ENOENT") return false;
            throw error;
        }
    };
    const initialize = async (ctx: ExtensionContext) => {
        initialized = false;
        enabled = await configured();
        for (const entry of ctx.sessionManager.getBranch()) {
            if (entry.type === "custom" && entry.customType === "pi-gui-checkpoint") {
                const data = entry.data;
                if (data && typeof data === "object" && "manifest" in data) emit(ctx, { manifest: data.manifest });
            }
        }
        if (!enabled) { emit(ctx, { status: "disabled" }); return; }
        transport = new CheckpointTransport(ctx.cwd, ctx.sessionManager.getSessionFile() ?? ctx.sessionManager.getSessionId());
        const inventory = object(await transport.request({ action: "list" }));
        emit(ctx, { status: "ready" });
        if (Array.isArray(inventory.records)) {
            for (const value of inventory.records.slice(-100)) {
                const record = object(value);
                if (record.state === "active" || record.state === "applying" || record.state === "interrupted") {
                    emit(ctx, { status: "recovery", recoveryId: record.id });
                } else {
                    emit(ctx, { manifest: await transport.request({ action: "manifest", id: record.id }) });
                }
            }
        }
        initialized = true;
    };
    pi.on("session_start", async (_event, ctx) => {
        try { await serialize(() => initialize(ctx)); }
        catch (error) { emit(ctx, { status: "error", error: String(error) }); }
    });
    pi.on("before_agent_start", async (_event, ctx) => {
        await serialize(async () => {
            try {
                enabled = await configured();
                if (!enabled || active) return;
                if (!initialized) await initialize(ctx);
                const scope = new CheckpointResponseScope(ctx.sessionManager.getBranch());
                transport ??= new CheckpointTransport(ctx.cwd, ctx.sessionManager.getSessionFile() ?? ctx.sessionManager.getSessionId());
                activity = new CheckpointActivity(ctx.sessionManager.getSessionFile() ?? ctx.sessionManager.getSessionId());
                const begin = object(await transport.request({ action: "begin", overlap: await activity.begin() }));
                if (typeof begin.id !== "string") throw new Error("Checkpoint identity is missing.");
                active = begin.id;
                responseScope = scope;
                emit(ctx, { status: "capturing" });
            } catch (error) { emit(ctx, { status: "error", error: String(error) }); }
        });
    });
    pi.on("agent_settled", async (_event, ctx) => {
        await serialize(async () => {
            if (!active || !transport) return;
            const responseId = responseScope?.settle(ctx.sessionManager.getBranch()) ?? "";
            try {
                const manifest = await transport.request({ action: "finish", id: active, response: responseId, overlap: await activity?.changed() ?? true });
                pi.appendEntry("pi-gui-checkpoint", { version: 1, id: active, response: responseId, manifest });
                active = undefined;
                responseScope = undefined;
                emit(ctx, { status: "ready", manifest });
            } catch (error) { emit(ctx, { status: "recovery", recoveryId: active, error: String(error) }); }
        });
    });
    pi.registerCommand("pi-gui-checkpoint", {
        description: "Manage optional workspace checkpoints from Pi desktop",
        handler: async (args, ctx) => {
            let requestId = "";
            try {
                const request = object(JSON.parse(args));
                if (typeof request.requestId !== "string" || !/^[a-f0-9]{32}$/.test(request.requestId)) throw new Error("Invalid request identity.");
                requestId = request.requestId;
                if (!["preview", "apply", "recover", "manifest", "refresh"].includes(String(request.action))) throw new Error("Unsupported checkpoint action.");
                if (!ctx.isIdle()) throw new Error("Finish this conversation before restoring files.");
                await serialize(async () => {
                    if (request.action === "refresh") {
                        await initialize(ctx);
                        emit(ctx, { requestId, result: {} });
                        return;
                    }
                    transport ??= new CheckpointTransport(ctx.cwd, ctx.sessionManager.getSessionFile() ?? ctx.sessionManager.getSessionId());
                    const result = await transport.request(request);
                    if (request.action === "recover" && request.id === active) active = undefined;
                    if (request.action === "apply" || request.action === "recover") {
                        const manifest = request.action === "apply" ? object(result).manifest : result;
                        pi.appendEntry("pi-gui-checkpoint", { version: 1, id: request.id, manifest });
                        emit(ctx, { manifest });
                        if (request.action === "apply") {
                            pi.sendMessage({ customType: "pi-gui-file-restoration", content: `Workspace restoration result: ${JSON.stringify({ checkpoint: request.id, undo: request.undo === true, done: object(result).done, error: object(result).error })}`, display: false }, { triggerTurn: false });
                        }
                    }
                    emit(ctx, { requestId, result });
                });
            } catch (error) { emit(ctx, { requestId, error: String(error) }); }
        },
    });
    pi.on("session_shutdown", async () => { await pending; });
}
