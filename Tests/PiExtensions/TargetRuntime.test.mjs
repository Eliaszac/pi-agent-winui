import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { resolve } from "node:path";
import { randomUUID } from "node:crypto";

test("installed Pi loads target tools with builtins disabled and no model requests", { skip: !process.env.PI_TARGET_TEST_CLI, timeout: 25000 }, async () => {
    const child = spawn(process.execPath, [process.env.PI_TARGET_TEST_CLI, "--mode", "rpc", "--no-session", "--no-extensions", "--no-builtin-tools", "--no-skills", "--no-context-files", "--no-prompt-templates",
        ...(process.env.PI_TARGET_TEST_APPROVAL ? ["--extension", process.env.PI_TARGET_TEST_APPROVAL] : []),
        "--extension", resolve(process.env.PI_TARGET_TEST_WSL ? "Tests/PiExtensions/TargetToolsProbe.ts" : "PiExtensions/execution-target.ts")], {
        windowsHide: true, stdio: ["pipe", "pipe", "pipe"], env: { ...process.env, PI_OFFLINE: "1", PI_GUI_EXECUTION_TARGET: JSON.stringify({
            id: "0be8a01a-1234-4321-a123-123456789abc", name: "Test target", kind: process.env.PI_TARGET_TEST_WSL ? "wsl" : "ssh",
            host: process.env.PI_TARGET_TEST_WSL ?? "example.invalid", path: process.env.PI_TARGET_TEST_WSL ? "/tmp/pi-desktop-test-" + randomUUID() : "/test" }) }
    });
    child.stderr.resume();
    let pending = "";
    const packets = [];
    let wake;
    child.stdout.setEncoding("utf8");
    child.stdout.on("data", text => {
        pending += text;
        for (;;) { const at = pending.indexOf("\n"); if (at < 0) break; packets.push(JSON.parse(pending.slice(0, at))); pending = pending.slice(at + 1); }
        wake?.();
    });
    const timer = setTimeout(() => child.kill(), 20000);
    let exited = false;
    const exit = new Promise(resolveExit => child.on("exit", () => { exited = true; wake?.(); resolveExit(); }));
    async function response(id) {
        for (;;) {
            const error = packets.find(packet => packet.type === "extension_error");
            if (error) throw new Error(JSON.stringify(error));
            const packet = packets.find(packet => packet.id === id && packet.type === "response");
            if (packet) return packet;
            if (exited) throw new Error("Pi exited before replying.");
            await new Promise(resolveWake => { wake = resolveWake; });
        }
    }
    try {
        child.stdin.write(JSON.stringify({ type: "get_commands", id: "commands" }) + "\n");
        const commands = await response("commands");
        assert.equal(commands.success, true);
        assert.ok(commands.data.commands.some(command => command.name === "pi-gui-target-check"), "Target registration must succeed.");
        if (process.env.PI_TARGET_TEST_APPROVAL) {
            const mode = commands.data.commands.find(command => command.name === "mode");
            assert.ok(mode, "Approval command must load.");
            console.log("Approval command source:", JSON.stringify(mode.sourceInfo));
        }
        child.stdin.write(JSON.stringify({ type: "get_state", id: "state" }) + "\n");
        assert.equal((await response("state")).success, true);
        if (process.env.PI_TARGET_TEST_WSL) {
            child.stdin.write(JSON.stringify({ type: "prompt", message: "/pi-target-test-tools", id: "tools" }) + "\n");
            assert.equal((await response("tools")).success, true);
            assert.ok(packets.some(packet => packet.method === "notify" && packet.message === "TARGET_TOOL_TEST_PASSED"), JSON.stringify(packets.filter(p => p.method === "notify")));
        }
    } finally {
        clearTimeout(timer); child.stdin.end();
        const kill = setTimeout(() => child.kill(), 2000);
        await exit; clearTimeout(kill);
    }
});
