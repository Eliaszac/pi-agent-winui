import { linkSync, mkdtempSync, realpathSync, rmSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";

export interface CopiedSession {
    getSessionFile(): string | undefined;
    branch(leafId: string): void;
    appendSessionInfo(name: string): string;
}

/** Publishes a Pi-created session beside its source without editing the active session. */
export class SessionCopy {
    static create(source: string, target: string, title: string, leafId: string,
        fork: (source: string, directory: string) => CopiedSession): void {
        const sourcePath = resolve(source);
        const targetPath = resolve(target);
        if (!/^[a-f0-9]{32}\.jsonl$/i.test(basename(targetPath)) ||
            realpathSync(dirname(targetPath)) !== realpathSync(dirname(sourcePath)) || targetPath === sourcePath)
            throw new Error("Copies must use a new conversation ID in the same project session directory.");
        if (!title.trim() || !leafId) throw new Error("The conversation is not ready to copy.");
        const temporary = mkdtempSync(join(dirname(sourcePath), ".pi-copy-"));
        try {
            const copy = fork(sourcePath, temporary);
            copy.branch(leafId);
            copy.appendSessionInfo(title);
            const file = copy.getSessionFile();
            if (!file || dirname(resolve(file)) !== temporary) throw new Error("Pi did not create the copied session.");
            // Same-volume hard-link publication is atomic and refuses to replace an existing target.
            linkSync(file, targetPath);
        } finally {
            rmSync(temporary, { recursive: true, force: true });
        }
    }
}
