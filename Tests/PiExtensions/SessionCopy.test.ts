import assert from "node:assert/strict";
import { test } from "node:test";
import { appendFileSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { SessionCopy } from "../../PiExtensions/SessionCopy.ts";

test("copies through Pi's session factory, publishes independently, and never changes the source", () => {
    const root = mkdtempSync(join(tmpdir(), "pi-session-copy-test-"));
    try {
        const source = join(root, "source.jsonl");
        const target = join(root, "a".repeat(32) + ".jsonl");
        writeFileSync(source, "original");
        let selectedLeaf: string | undefined;
        SessionCopy.create(source, target, "New name", "leaf", (path, dir) => {
            assert.equal(path, source);
            const file = join(dir, "pi-generated.jsonl");
            writeFileSync(file, "new Pi identity\nhistory including tools and settings\n");
            return {
                getSessionFile: () => file,
                branch: (leaf) => { selectedLeaf = leaf; },
                appendSessionInfo: (name) => { appendFileSync(file, name); return "entry"; },
            };
        });
        assert.equal(selectedLeaf, "leaf");
        assert.equal(readFileSync(source, "utf8"), "original");
        assert.match(readFileSync(target, "utf8"), /New name$/);
        appendFileSync(target, "\nnew response");
        assert.equal(readFileSync(source, "utf8"), "original");
        assert.deepEqual(readdirSync(root).sort(), ["a".repeat(32) + ".jsonl", "source.jsonl"]);
    } finally { rmSync(root, { recursive: true, force: true }); }
});

test("never overwrites a destination or publishes a failed copy", () => {
    const root = mkdtempSync(join(tmpdir(), "pi-session-copy-test-"));
    try {
        const source = join(root, "source.jsonl");
        const target = join(root, "b".repeat(32) + ".jsonl");
        writeFileSync(source, "original");
        writeFileSync(target, "existing");
        assert.throws(() => SessionCopy.create(source, target, "Copy", "leaf", (_, dir) => {
            const file = join(dir, "copy.jsonl");
            writeFileSync(file, "copy");
            return { getSessionFile: () => file, branch: () => {}, appendSessionInfo: () => "entry" };
        }));
        assert.equal(readFileSync(target, "utf8"), "existing");
        assert.throws(() => SessionCopy.create(source, join(root, "c".repeat(32) + ".jsonl"), "Copy", "leaf", () => { throw new Error("SDK failure"); }));
        assert.equal(readdirSync(root).length, 2);
        assert.throws(() => SessionCopy.create(source, join(root, "../outside.jsonl"), "Copy", "leaf", () => { throw new Error("must not run"); }));
    } finally { rmSync(root, { recursive: true, force: true }); }
});
