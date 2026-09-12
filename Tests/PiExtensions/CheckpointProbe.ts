import assert from "node:assert/strict";
import { readFile, writeFile, unlink } from "node:fs/promises";
import path from "node:path";
import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";
import checkpoints from "../../PiExtensions/checkpoints.ts";
import { TargetConnection } from "../../PiExtensions/TargetConnection.ts";

/** Test-only lifecycle driver: real Pi extension/RPC, no model calls. */
export default function probe(pi: ExtensionAPI): void {
    const hooks = new Map<string, (event: unknown, ctx: ExtensionContext) => Promise<void>>();
    let checkpointCommand: { handler: (args: string, ctx: unknown) => Promise<void> };
    const packets: Record<string, unknown>[] = [];
    const wrapper = new Proxy(pi, { get(target, property) {
        if (property === "on") return (event: string, handler: (event: unknown, ctx: ExtensionContext) => Promise<void>) => {
            hooks.set(event, handler);
            if (event === "session_start") pi.on("session_start", handler);
        };
        if (property === "registerCommand") return (name: string, command: typeof checkpointCommand) => { checkpointCommand = command; pi.registerCommand(name, command); };
        return Reflect.get(target, property);
    } });
    checkpoints(wrapper);
    pi.registerCommand("pi-checkpoint-probe", { description: "Disposable checkpoint verification", handler: async (_args, ctx) => {
        const remote = process.env.PI_GUI_EXECUTION_TARGET ? new TargetConnection(process.env.PI_GUI_EXECUTION_TARGET) : undefined;
        const root = remote?.target.path ?? ctx.cwd;
        assert.match(root, /pi-checkpoint-test-[a-f0-9-]+$/i);
        const write = async (name: string, content: string) => {
            if (remote) await remote.checked(`printf %s ${TargetConnection.quote(content)} > ${TargetConnection.quote(root + "/" + name)}`);
            else await writeFile(path.join(root, name), content);
        };
        const read = async (name: string) => remote
            ? (await remote.checked(`cat -- ${TargetConnection.quote(root + "/" + name)}`)).toString("utf8")
            : await readFile(path.join(root, name), "utf8");
        const remove = async (name: string) => remote ? await remote.checked(`rm -- ${TargetConnection.quote(root + "/" + name)}`) : await unlink(path.join(root, name));
        const context = new Proxy(ctx, { get(target, property) {
            if (property === "ui") return new Proxy(ctx.ui, { get(ui, key) {
                if (key === "setStatus") return (status: string, value: string) => { if (status === "pi-gui-checkpoints-v1") packets.push(JSON.parse(value)); ui.setStatus(status, value); };
                return Reflect.get(ui, key);
            } });
            if (property === "sessionManager") return new Proxy(ctx.sessionManager, { get(manager, key) {
                if (key === "getBranch") return () => [...manager.getBranch().filter(entry => entry.type === "custom"), { type: "message", message: { role: "assistant", timestamp: 123 } }];
                const value = Reflect.get(manager, key);
                return typeof value === "function" ? value.bind(manager) : value;
            } });
            return Reflect.get(target, property);
        } });
        await write("modified.txt", "before\n");
        await write("deleted.txt", "removed\n");
        await hooks.get("before_agent_start")!({}, context);
        assert.ok(packets.some(p => p.status === "capturing"), JSON.stringify(packets));
        await write("modified.txt", "after\n");
        await write("created-λ.txt", "new\n");
        await remove("deleted.txt");
        await hooks.get("agent_settled")!({}, context);
        const manifest = packets.findLast(p => p.manifest)?.manifest as { id: string; files: { path: string; kind: string }[] };
        assert.ok(manifest, JSON.stringify(packets));
        assert.deepEqual(manifest.files.map(f => f.kind).sort(), ["created", "deleted", "modified"]);
        const command = async (action: string, extra: object = {}) => {
            const requestId = (await import("node:crypto")).randomUUID().replaceAll("-", "");
            await checkpointCommand.handler(JSON.stringify({ requestId, action, id: manifest.id, ...extra }), context);
            const reply = packets.find(p => p.requestId === requestId);
            assert.ok(reply && !reply.error, JSON.stringify(reply));
            return reply.result as { files?: { safe: boolean }[]; error?: string };
        };
        assert.ok((await command("preview")).files!.every(f => f.safe));
        const paths = manifest.files.map(f => f.path);
        assert.equal((await command("apply", { paths })).error, "");
        assert.equal(await read("modified.txt"), "before\n");
        assert.equal(await read("deleted.txt"), "removed\n");
        assert.equal((await command("apply", { paths, undo: true })).error, "");
        assert.equal(await read("created-λ.txt"), "new\n");
        await write("modified.txt", "later user edit\n");
        const conflict = await command("preview");
        assert.ok(conflict.files!.some(f => !f.safe));
        await writeFile(process.env.PI_GUI_CHECKPOINT_SETTINGS!, JSON.stringify({ enabled: false }));
        packets.length = 0;
        await hooks.get("session_start")!({}, context);
        assert.ok(packets.some(p => p.status === "disabled"));
        assert.ok(packets.some(p => (p.manifest as { id?: string } | undefined)?.id === manifest.id), "Disabled capture must preserve cached summaries.");
        ctx.ui.notify("CHECKPOINT_PROBE_PASSED", "info");
    } });
}
