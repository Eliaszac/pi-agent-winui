import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { TargetConnection } from "./TargetConnection.ts";

/** Runs the same bounded checkpoint engine locally or through the selected target. */
export class CheckpointTransport {
    private readonly target = process.env.PI_GUI_EXECUTION_TARGET ? new TargetConnection(process.env.PI_GUI_EXECUTION_TARGET) : undefined;
    private readonly loader = "import sys,base64; code=base64.b64decode(sys.stdin.readline()); exec(compile(code,'checkpoint_engine.py','exec'))";
    constructor(private readonly root: string, private readonly session: string) { }

    async request(request: Record<string, unknown>): Promise<unknown> {
        const code = await readFile(new URL("./checkpoint_engine.py", import.meta.url));
        const input = `${code.toString("base64")}\n${JSON.stringify({ ...request, root: this.target?.target.path ?? this.root, session: this.session })}`;
        let output: string;
        if (this.target) {
            output = (await this.target.checked(`python3 -X utf8 -c ${TargetConnection.quote(this.loader)}`, undefined, input)).toString("utf8");
        } else {
            output = await this.local(input);
        }
        const result: unknown = JSON.parse(output);
        if (!result || typeof result !== "object" || !("ok" in result)) throw new Error("Invalid checkpoint engine response.");
        if (!result.ok) throw new Error("error" in result && typeof result.error === "string" ? result.error : "Checkpoint operation failed.");
        return "data" in result ? result.data : null;
    }

    private async local(input: string): Promise<string> {
        return await new Promise((resolve, reject) => {
            const child = spawn("python", ["-X", "utf8", "-c", this.loader], { windowsHide: true, stdio: ["pipe", "pipe", "pipe"] });
            let text = "", failed = false;
            const timer = setTimeout(() => { failed = true; child.kill(); reject(new Error("Checkpoint operation timed out. Inspect recovery before retrying.")); }, 120000);
            child.stdout.on("data", (data: Buffer) => {
                text += data.toString("utf8");
                if (text.length > 4_000_000) { failed = true; child.kill(); reject(new Error("Checkpoint output exceeded its limit.")); }
            });
            child.stderr.resume();
            child.stdin.on("error", () => {});
            child.stdin.end(input);
            child.on("error", () => { failed = true; clearTimeout(timer); reject(new Error("Checkpoints require Python 3 and Git on PATH. Install them, then reconnect.")); });
            child.on("close", code => {
                clearTimeout(timer);
                if (failed) return;
                if (code !== 0) reject(new Error("Checkpoint engine failed. Verify Python 3 and Git are available on this target."));
                else resolve(text);
            });
        });
    }
}
