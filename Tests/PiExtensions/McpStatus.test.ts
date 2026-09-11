import assert from "node:assert/strict";
import { test } from "node:test";
import type { ExtensionAPI, ExtensionContext } from "@earendil-works/pi-coding-agent";
import registerMcpStatus from "../../PiExtensions/mcp-status.ts";

test("forwards only status metadata, caches before startup and stays independent across instances", () => {
    const create = () => {
        let receive: (value: unknown) => void = () => {};
        const lifecycle = new Map<string, (event: unknown, context: ExtensionContext) => void>();
        const output: string[] = [];
        const pi = {
            events: { on: (channel: string, callback: typeof receive) => { assert.equal(channel, "pi-mcp-adapter/status/v1"); receive = callback; } },
            on: (event: string, callback: (event: unknown, context: ExtensionContext) => void) => lifecycle.set(event, callback),
        };
        registerMcpStatus(pi as unknown as ExtensionAPI);
        const ctx = { ui: { setStatus: (key: string, text: string) => { assert.equal(key, "pi-gui-mcp-status-v1"); output.push(text); } } };
        return { receive: (value: unknown) => receive(value), output,
            start: () => lifecycle.get("session_start")?.({}, ctx as unknown as ExtensionContext),
            stop: () => lifecycle.get("session_shutdown")?.({}, ctx as unknown as ExtensionContext) };
    };
    const first = create(); const second = create();
    first.receive({ version: 1, servers: [{ name: "docs", status: "cached", toolCount: 2, headers: { Authorization: "secret" } }], token: "secret" });
    assert.equal(first.output.length, 0);
    first.start(); second.start();
    assert.equal(second.output.length, 0);
    assert.deepEqual(JSON.parse(first.output[0]), { version: 1, servers: [{ name: "docs", status: "cached", toolCount: 2 }] });
    first.receive({ version: 2, servers: [] });
    assert.equal(first.output.length, 1);
    first.stop();
    first.receive({ version: 1, servers: [] });
    assert.equal(first.output.length, 1);
});
