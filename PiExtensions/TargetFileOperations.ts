import path from "node:path";
import { createHash } from "node:crypto";
import { TargetConnection } from "./TargetConnection.ts";

/** Bounded reads and atomic target-side replacement, with optional optimistic edit validation. */
export class TargetFileOperations {
    private readonly connection: TargetConnection;
    constructor(connection: TargetConnection) { this.connection = connection; }
    async read(file: string, signal?: AbortSignal): Promise<Buffer> {
        return this.connection.checked(`head -c 8388609 -- ${TargetConnection.quote(file)}`, signal);
    }
    async write(file: string, content: string, signal?: AbortSignal, expected?: Buffer): Promise<void> {
        if (Buffer.byteLength(content) > 8 * 1024 * 1024) throw new Error("Write exceeds 8 MB.");
        const quote = TargetConnection.quote;
        const check = expected === undefined ? "" :
            `test "$(sha256sum -- ${quote(file)} | cut -d ' ' -f 1)" = ${quote(createHash("sha256").update(expected).digest("hex"))} || exit 73`;
        await this.connection.checked(`set -e
test ! -L ${quote(file)}
tmp=$(mktemp -- ${quote(path.posix.join(path.posix.dirname(file), ".pi-desktop-write.XXXXXXXX"))})
trap 'rm -f -- "$tmp"' EXIT
base64 -d > "$tmp"
${check}
if test -f ${quote(file)}; then chmod --reference=${quote(file)} "$tmp"; fi
mv -f -- "$tmp" ${quote(file)}`, signal, Buffer.from(content).toString("base64"));
    }
}
