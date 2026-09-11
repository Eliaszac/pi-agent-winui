import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { test } from "node:test";
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import registerInstructions from "../../PiExtensions/instructions.ts";

test("reports loaded hashes without file contents, prompt mutations or filesystem discovery", () => {
    let command: { handler: (args: string, ctx: unknown) => unknown } | undefined;
    let before: ((event: unknown, ctx: unknown) => unknown) | undefined;
    const results: string[] = [];
    const pi = {
        registerCommand: (name: string, handler: typeof command) => { assert.equal(name, "pi-gui-instructions"); command = handler; },
        on: (name: string, handler: typeof before) => { assert.equal(name, "before_agent_start"); before = handler; }
    };
    registerInstructions(pi as unknown as ExtensionAPI);
    const files = [{ path: "C:/project/AGENTS.md", content: "Private instructions" }];
    const ctx = { getSystemPromptOptions: () => ({ contextFiles: files }), ui: { setStatus: (key: string, text: string) => {
        assert.equal(key, "pi-gui-instructions-v1"); results.push(text);
    } } };
    assert.equal(command!.handler("", ctx), undefined);
    assert.equal(before!({ systemPromptOptions: { contextFiles: files } }, ctx), undefined);
    assert.deepEqual(JSON.parse(results[0]), { version: 1, available: true, files: [{ path: files[0].path, hash: createHash("sha256").update(files[0].content).digest("hex") }] });
    assert.equal(results[0].includes("Private instructions"), false);
    command!.handler("", { ui: ctx.ui });
    assert.equal(JSON.parse(results[2]).available, false);
});
