import assert from "node:assert/strict";
import type { ExtensionAPI, ToolDefinition } from "@earendil-works/pi-coding-agent";
import registerExecutionTarget from "../../PiExtensions/execution-target.ts";
import { TargetConnection } from "../../PiExtensions/TargetConnection.ts";
import { TargetFileOperations } from "../../PiExtensions/TargetFileOperations.ts";
import { TargetShellCommand } from "../../PiExtensions/TargetShellCommand.ts";
import { TargetInstructions } from "../../PiExtensions/TargetInstructions.ts";

/** Test-only command. No model request is needed to exercise Pi's registered tool definitions. */
export default function probe(pi: ExtensionAPI): void {
    const tools = new Map<string, ToolDefinition>();
    const capturing = new Proxy(pi, { get(target, property) {
        if (property === "registerTool") return (tool: ToolDefinition) => { tools.set(tool.name, tool); pi.registerTool(tool); };
        return Reflect.get(target, property);
    } });
    registerExecutionTarget(capturing);
    pi.registerCommand("pi-target-test-tools", { description: "Test-only remote operations", handler: async (_args, ctx) => {
        const connection = new TargetConnection(process.env.PI_GUI_EXECUTION_TARGET!);
        const root = connection.target.path;
        assert.match(root, /^\/tmp\/pi-desktop-test-[a-f0-9-]+$/);
        const file = root + "/dollar$(echo nope)' λ.txt";
        const quote = TargetConnection.quote;
        const execute = async (name: string, parameters: Record<string, unknown>) => {
            const tool = tools.get(name); assert.ok(tool, name);
            return tool.execute("target-test-" + name, parameters, ctx.signal, undefined, ctx);
        };
        await connection.checked("mkdir -- " + quote(root), ctx.signal);
        try {
            await execute("write", { path: root + "/AGENTS.md", content: "Target instruction λ\n" });
            const instructions = await TargetInstructions.load(connection, ctx.signal);
            assert.equal(instructions.find(item => item.path === root + "/AGENTS.md")?.content, "Target instruction λ\n");
            await execute("write", { path: file, content: "first line\nsecond line\n" });
            const read = await execute("read", { path: file });
            assert.match(JSON.stringify(read), /first line/);
            await execute("edit", { path: file, edits: [{ oldText: "first line", newText: "updated line" }] });
            const after = await execute("read", { path: file });
            assert.match(JSON.stringify(after), /updated line/);
            const listing = await execute("ls", { path: root });
            assert.match(JSON.stringify(listing), /dollar/);
            const bash = await execute("bash", { command: "pwd", timeout: 10 });
            assert.match(JSON.stringify(bash), new RegExp(root));
            const operations = new TargetFileOperations(connection);
            await assert.rejects(() => operations.write(file, "should not replace", ctx.signal, Buffer.from("stale")));
            assert.match((await operations.read(file, ctx.signal)).toString(), /updated line/);
            const cancellation = new AbortController();
            const timer = setTimeout(() => cancellation.abort(), 200);
            try {
                await assert.rejects(() => connection.run(TargetShellCommand.wrap("sleep 2; touch -- " + quote(root + "/cancelled")), cancellation.signal, undefined, 10, () => {}));
                await new Promise(resolve => setTimeout(resolve, 2300));
                assert.equal((await connection.run("test -e " + quote(root + "/cancelled"))).exitCode, 1, "Cancelled command must not continue mutating the target.");
            } finally { clearTimeout(timer); }
            ctx.ui.notify("TARGET_TOOL_TEST_PASSED", "info");
        } finally {
            await connection.checked("rm -f -- " + quote(file) + " " + quote(root + "/cancelled") + " " + quote(root + "/AGENTS.md") + " && rmdir -- " + quote(root));
        }
    } });
}
