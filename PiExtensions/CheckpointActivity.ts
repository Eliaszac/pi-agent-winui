import { readFile, readdir } from "node:fs/promises";
import path from "node:path";

/** Conservative cross-window overlap detection, including app work without checkpoints enabled. */
export class CheckpointActivity {
    private generation = "";
    constructor(private readonly session: string) { }
    async begin(): Promise<boolean> {
        this.generation = await this.current();
        const directory = process.env.PI_GUI_ACTIVITY_DIRECTORY;
        if (!directory) return false;
        try {
            for (const name of await readdir(directory)) {
                if (!name.endsWith(".json")) continue;
                try {
                    const value: unknown = JSON.parse(await readFile(path.join(directory, name), "utf8"));
                    if (!value || typeof value !== "object" || !("pid" in value) || typeof value.pid !== "number") return true;
                    try { process.kill(value.pid, 0); } catch { continue; }
                    if (!("session" in value) || value.session !== this.session) return true;
                } catch { return true; }
            }
        } catch { return true; }
        return false;
    }
    async changed(): Promise<boolean> { return this.generation !== await this.current(); }
    private async current(): Promise<string> {
        const directory = process.env.PI_GUI_ACTIVITY_DIRECTORY;
        if (!directory) return "";
        try { return await readFile(path.join(directory, "generation"), "utf8"); }
        catch { return "unavailable"; }
    }
}
