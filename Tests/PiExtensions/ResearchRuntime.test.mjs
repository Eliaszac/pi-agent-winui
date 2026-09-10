import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { once } from "node:events";

const cli = process.env.PI_RESEARCH_TEST_CLI;
async function inventory(extension, tools, enabled, searchExtension) {
    const directory = await mkdtemp(join(tmpdir(), "pi-research-test-"));
    const preference = join(directory, "enabled.json");
    await writeFile(preference, JSON.stringify(enabled));
    const child = spawn(process.execPath, [cli, "--mode", "rpc", "--no-session", "--no-extensions", "--no-skills", "--no-context-files", "--no-prompt-templates",
        "--tools", tools, ...(searchExtension ? ["--extension", searchExtension] : []), "--extension", resolve("PiExtensions", extension), "--extension", resolve("Tests/PiExtensions/ResearchProbe.ts")],
        { windowsHide: true, stdio: ["pipe", "pipe", "pipe"], env: { ...process.env, PI_OFFLINE: "1", PI_GUI_RESEARCH_PREFERENCE: preference } });
    child.stderr.resume();
    const lines = createInterface({ input: child.stdout });
    const packets = lines[Symbol.asyncIterator]();
    const exit = once(child, "exit");
    const timeout = setTimeout(() => child.kill(), 15000);
    async function until(predicate) {
        for (;;) {
            const next = await packets.next();
            if (next.done) throw new Error("Pi ended before the probe completed.");
            const packet = JSON.parse(next.value);
            if (packet.type === "extension_error") throw new Error(packet.error);
            if (predicate(packet)) return packet;
        }
    }
    try {
        child.stdin.write(JSON.stringify({ id: "commands", type: "get_commands" }) + "\n");
        const commands = await until(packet => packet.id === "commands");
        assert.ok(commands.data.commands.some(command => command.name === "research-test-probe"));
        child.stdin.write(JSON.stringify({ id: "probe", type: "prompt", message: "/research-test-probe" }) + "\n");
        return JSON.parse((await until(packet => packet.method === "notify")).message).active;
    } finally {
        clearTimeout(timeout); lines.close(); child.stdin.end();
        const kill = setTimeout(() => child.kill(), 2000);
        await exit; clearTimeout(kill);
        await rm(directory, { recursive: true, force: true });
    }
}

test("worker exposes exactly the read-only tools", { skip: !cli }, async () => {
    const allowed = ["read", "grep", "find", "ls", "read_web_page"];
    assert.deepEqual((await inventory("research-worker.ts", allowed.join(","), false)).sort(), allowed.sort());
});
test("dispatch tool is absent while disabled and available after opting in", { skip: !cli }, async () => {
    assert.ok(!(await inventory("research-dispatch.ts", "read,background_research", false)).includes("background_research"));
    assert.ok((await inventory("research-dispatch.ts", "read,background_research", true)).includes("background_research"));
});

test("installed search loads in RPC and workers expose only websearch", { skip: !cli || !process.env.PI_SEARCH_TEST_EXTENSION }, async () => {
    const allowed = ["read", "grep", "find", "ls", "read_web_page", "websearch"];
    assert.deepEqual((await inventory("research-worker.ts", allowed.join(","), false, process.env.PI_SEARCH_TEST_EXTENSION)).sort(), allowed.sort());
});
