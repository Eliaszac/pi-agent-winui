import path from "node:path";
import { TargetConnection } from "./TargetConnection.ts";

/** Loads bounded ancestor instructions in one target round trip. */
export class TargetInstructions {
    static async load(connection: TargetConnection, signal?: AbortSignal): Promise<{ path: string; content: string }[]> {
        const ancestors: string[] = [];
        for (let directory = connection.target.path; ; directory = path.posix.dirname(directory)) {
            ancestors.unshift(directory);
            if (ancestors.length > 32) throw new Error("Target workspace exceeds 32 ancestor directories.");
            if (directory === "/") break;
        }
        const candidates = ancestors.flatMap(directory => ["AGENTS.md", "CLAUDE.md", "AGENTS.override.md"].map(name => path.posix.join(directory, name)));
        const output = await connection.checked(`set -e -o pipefail
test -d ${TargetConnection.quote(connection.target.path)}
for file in ${candidates.map(TargetConnection.quote).join(" ")}; do
    if test -f "$file"; then
        printf '%s\\0' "$file"
        head -c 262145 -- "$file" | base64 -w 0
        printf '\\0'
    fi
done`, signal);
        const fields = output.toString("utf8").split("\0");
        const files: { path: string; content: string }[] = [];
        let total = 0;
        for (let i = 0; i + 1 < fields.length; i += 2) {
            const bytes = Buffer.from(fields[i + 1], "base64");
            total += bytes.length;
            if (bytes.length > 262144 || total > 524288) throw new Error("Target instructions exceed the supported size (256 KB per file, 512 KB total).");
            files.push({ path: fields[i], content: new TextDecoder("utf8", { fatal: true }).decode(bytes) });
        }
        return files;
    }
}
