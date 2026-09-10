import { mkdir, open, writeFile } from "node:fs/promises";

const maxBytes = 256 * 1024;

/** Captures the baseline when Pi invokes its serialized write operation. */
export class WriteDiffCapture {
    patch: string | null = null;
    private readonly displayPath: string;
    private readonly generatePatch: (path: string, before: string, after: string) => string;

    constructor(displayPath: string, generatePatch: (path: string, before: string, after: string) => string) {
        this.displayPath = displayPath;
        this.generatePatch = generatePatch;
    }

    readonly operations = {
        mkdir: async (path: string): Promise<void> => { await mkdir(path, { recursive: true }); },
        writeFile: async (path: string, content: string): Promise<void> => {
            const before = await this.readBefore(path);
            await writeFile(path, content, "utf-8");
            if (before !== null && Buffer.byteLength(content, "utf-8") <= maxBytes
                && before.split("\n").length <= 5000 && content.split("\n").length <= 5000) {
                try { this.patch = this.generatePatch(this.displayPath, before, content); }
                catch { /* Presentation failure must not change a successful write result. */ }
            }
        },
    };

    private async readBefore(path: string): Promise<string | null> {
        try {
            const file = await open(path, "r");
            try {
                const buffer = Buffer.alloc(maxBytes + 1);
                let bytesRead = 0;
                while (bytesRead < buffer.length) {
                    const read = await file.read(buffer, bytesRead, buffer.length - bytesRead, bytesRead);
                    if (read.bytesRead === 0) break;
                    bytesRead += read.bytesRead;
                }
                if (bytesRead > maxBytes || buffer.subarray(0, bytesRead).includes(0)) return null;
                return new TextDecoder("utf-8", { fatal: true }).decode(buffer.subarray(0, bytesRead));
            } finally { await file.close(); }
        } catch (error: unknown) {
            if (typeof error === "object" && error !== null && "code" in error && error.code === "ENOENT") return "";
            return null;
        }
    }
}
