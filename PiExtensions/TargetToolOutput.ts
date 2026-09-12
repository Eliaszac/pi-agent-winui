import { truncateHead } from "@earendil-works/pi-coding-agent";

export class TargetToolOutput {
    static text(buffer: Buffer, maximum = 2000) {
        const truncated = truncateHead(buffer.toString("utf8"), { maxLines: maximum });
        return { content: [{ type: "text" as const, text: truncated.content + (truncated.truncated ? "\n[Output truncated; narrow the query.]" : "") }], details: { truncation: truncated } };
    }
}
