import assert from "node:assert/strict";
import { test } from "node:test";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { WriteDiffCapture } from "../../PiExtensions/WriteDiffCapture.ts";

test("new files and overwrites capture the contents immediately before each write", async () => {
    const dir = await mkdtemp(join(tmpdir(), "pi-diff-test-"));
    try {
        const file = join(dir, "file.txt");
        const generate = (path: string, before: string, after: string) => JSON.stringify({ path, before, after });
        const first = new WriteDiffCapture("file.txt", generate);
        await first.operations.writeFile(file, "one\n");
        assert.deepEqual(JSON.parse(first.patch!), { path: "file.txt", before: "", after: "one\n" });
        const second = new WriteDiffCapture("file.txt", generate);
        await second.operations.writeFile(file, "two\n");
        assert.deepEqual(JSON.parse(second.patch!), { path: "file.txt", before: "one\n", after: "two\n" });
        assert.equal(await readFile(file, "utf8"), "two\n");
    } finally { await rm(dir, { recursive: true, force: true }); }
});

test("binary or oversized baselines do not fabricate a diff or block the write", async () => {
    const dir = await mkdtemp(join(tmpdir(), "pi-diff-test-"));
    try {
        const file = join(dir, "file.txt");
        for (const before of [Buffer.from([0, 1, 2]), Buffer.alloc(300 * 1024, 65)]) {
            await writeFile(file, before);
            const capture = new WriteDiffCapture("file.txt", () => { throw new Error("must not calculate"); });
            await capture.operations.writeFile(file, "new");
            assert.equal(capture.patch, null);
            assert.equal(await readFile(file, "utf8"), "new");
        }
    } finally { await rm(dir, { recursive: true, force: true }); }
});

test("failed writes propagate and diff generation errors do not undo successful writes", async () => {
    const dir = await mkdtemp(join(tmpdir(), "pi-diff-test-"));
    try {
        const capture = new WriteDiffCapture("file.txt", () => { throw new Error("diff failure"); });
        await assert.rejects(capture.operations.writeFile(dir, "new"));
        assert.equal(capture.patch, null);
        const file = join(dir, "file.txt");
        await capture.operations.writeFile(file, "new");
        assert.equal(capture.patch, null);
        assert.equal(await readFile(file, "utf8"), "new");
    } finally { await rm(dir, { recursive: true, force: true }); }
});
