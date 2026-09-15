import path from "node:path";
import { createHash } from "node:crypto";
import { createReadToolDefinition, createWriteToolDefinition, createEditToolDefinition, createBashToolDefinition,
    createLsToolDefinition, createFindToolDefinition, createGrepToolDefinition, generateUnifiedPatch, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { TargetConnection } from "./TargetConnection.ts";
import { TargetFileOperations } from "./TargetFileOperations.ts";
import { TargetToolOutput } from "./TargetToolOutput.ts";
import { TargetShellCommand } from "./TargetShellCommand.ts";
import { TargetInstructions } from "./TargetInstructions.ts";

/** Keeps the Pi harness local while routing all enabled workspace tools to one immutable target. */
export default function registerExecutionTarget(pi: ExtensionAPI): void {
    const connection = new TargetConnection(process.env.PI_GUI_EXECUTION_TARGET ?? "{}");
    const target = connection.target;
    const cwd = process.cwd();
    const quote = TargetConnection.quote;
    const allowed = new Set(["read", "write", "edit", "bash", "ls", "find", "grep"]);
    let instructionsReady = false;
    let instructionInventory = { version: 1, available: false, files: [] as { path: string; hash: string }[] };
    const shell = (command: string) => `cd -- ${quote(target.path)} || exit $?\n${command}`;
    const files = new TargetFileOperations(connection);

    const readBase = createReadToolDefinition(cwd);
    pi.registerTool({ ...readBase, async execute(id, params, signal, update, ctx) {
        const file = connection.resolve(params.path);
        const tool = createReadToolDefinition(cwd, { operations: {
            readFile: () => files.read(file, signal), access: async () => { await connection.checked(`test -r ${quote(file)}`, signal); },
            detectImageMimeType: async () => {
                const mime = (await connection.run(`file --mime-type -b -- ${quote(file)}`, signal)).output.toString().trim();
                return ["image/jpeg", "image/png", "image/gif", "image/webp"].includes(mime) ? mime : null;
            }
        } });
        return tool.execute(id, { ...params, path: "target-file" }, signal, update, ctx);
    } });
    const editBase = createEditToolDefinition(cwd);
    pi.registerTool({ ...editBase, async execute(id, params, signal, update, ctx) {
        const file = connection.resolve(params.path);
        let expected: Buffer | undefined;
        const tool = createEditToolDefinition(cwd, { operations: { readFile: async () => { expected = await files.read(file, signal); return expected; },
            access: async () => { await connection.checked(`test -r ${quote(file)} && test -w ${quote(file)}`, signal); },
            writeFile: (_p, content) => files.write(file, content, signal, expected) } });
        return tool.execute(id, { ...params, path: "target-file" }, signal, update, ctx);
    } });
    const writeBase = createWriteToolDefinition(cwd);
    pi.registerTool({ ...writeBase, async execute(id, params, signal, update, ctx) {
        const file = connection.resolve(params.path);
        let before: string | null = null;
        const existed = await connection.run(`test -e ${quote(file)}`, signal);
        if (existed.exitCode === 1) before = "";
        else if (existed.exitCode === 0) {
            const snapshot = await connection.run(`head -c 262145 -- ${quote(file)}`, signal);
            if (snapshot.exitCode === 0 && snapshot.output.length <= 262144) before = snapshot.output.toString("utf8");
        }
        const tool = createWriteToolDefinition(cwd, { operations: {
            mkdir: async () => { await connection.checked(`mkdir -p -- ${quote(path.posix.dirname(file))}`, signal); },
            writeFile: (_p, content) => files.write(file, content, signal)
        } });
        const result = await tool.execute(id, { ...params, path: "target-file" }, signal, update, ctx);
        let patch: string | null = null;
        if (before !== null && Buffer.byteLength(params.content) <= 262144 && before.split("\n").length <= 5000 && params.content.split("\n").length <= 5000) {
            try { patch = generateUnifiedPatch(params.path, before, params.content); } catch { /* Preview failure must not fail a completed write. */ }
        }
        return { ...result, details: { piGuiFileChange: { version: 1, path: params.path,
            patch,
            unavailable: before === null ? "Previous target contents could not be previewed." : null } } };
    } });
    const bashOperations = { exec: async (command: string, _cwd: string, options: { onData: (data: Buffer) => void; signal?: AbortSignal; timeout?: number }) => {
        const result = await connection.run(TargetShellCommand.wrap(shell(command)), options.signal, undefined, options.timeout ?? 120, options.onData);
        return { exitCode: result.exitCode };
    } };
    pi.registerTool(createBashToolDefinition(cwd, { operations: bashOperations }));
    pi.on("user_bash", () => ({ operations: bashOperations }));
    pi.registerTool({ ...createLsToolDefinition(cwd), async execute(_id, params, signal) {
        const directory = connection.resolve(params.path);
        const output = await connection.checked(`find ${quote(directory)} -mindepth 1 -maxdepth 1 -printf '%f%y\\0'`, signal);
        const entries = output.toString("utf8").split("\0").filter(Boolean).sort().map(entry => entry.slice(0, -1) + (entry.endsWith("d") ? "/" : ""));
        const limit = Math.min(Math.max(params.limit ?? 500, 1), 20000);
        return TargetToolOutput.text(Buffer.from(entries.slice(0, limit).join("\n") + (entries.length > limit ? "\n[Entry limit reached.]" : "")));
    } });
    pi.registerTool({ ...createFindToolDefinition(cwd), async execute(_id, params, signal) {
        const directory = connection.resolve(params.path);
        const output = await connection.run(`cd -- ${quote(directory)} && rg --files --hidden -g '!node_modules' -g '!.git' -g ${quote(params.pattern)}`, signal);
        if (output.exitCode > 1) throw new Error("Target file search failed. Install ripgrep (rg) on this target.");
        return TargetToolOutput.text(output.output, Math.min(Math.max(params.limit ?? 1000, 1), 20000));
    } });
    pi.registerTool({ ...createGrepToolDefinition(cwd), async execute(_id, params, signal) {
        const args = ["rg", "--line-number", "--with-filename", "--color=never", "--hidden", "-g", "!.git", "-g", "!node_modules"];
        if (params.ignoreCase) args.push("--ignore-case");
        if (params.literal) args.push("--fixed-strings");
        if (params.glob) args.push("--glob", params.glob);
        args.push("--context", String(Math.min(Math.max(params.context ?? 0, 0), 100)), "--", params.pattern, connection.resolve(params.path));
        const output = await connection.run(shell(args.map(quote).join(" ")), signal);
        if (output.exitCode > 1) throw new Error("Target search failed. Check the pattern and install ripgrep (rg) on the target.");
        return TargetToolOutput.text(output.output, Math.min(Math.max(params.limit ?? 100, 1), 20000));
    } });
    pi.on("tool_call", event => !instructionsReady ? { block: true, reason: "Target instructions could not be loaded for this turn. Reconnect before executing tools." }
        : allowed.has(event.toolName) ? undefined : { block: true, reason: "This tool has not been enabled for the execution target." });
    pi.on("session_start", () => {
        for (const tool of pi.getAllTools()) if (tool.name.startsWith("embedded_browser_") || ["artifact_save", "artifact_list", "artifact_read", "artifact_path"].includes(tool.name)) allowed.add(tool.name);
        pi.setActiveTools([...allowed]);
    });
    pi.registerCommand("pi-gui-instructions", { description: "Internal loaded instruction inventory for Pi desktop", handler: (_args, ctx) => {
        ctx.ui.setStatus("pi-gui-instructions-v1", JSON.stringify(instructionInventory));
    } });
    pi.registerCommand("pi-gui-target-check", { description: "Verify the desktop execution target", handler: async (_args, ctx) => {
        await connection.checked(shell("test -d . && command -v bash >/dev/null && command -v base64 >/dev/null && command -v setsid >/dev/null"), ctx.signal);
        ctx.ui.setStatus("pi-gui-target-ready", target.id);
    } });
    pi.on("before_agent_start", async (event, ctx) => {
        instructionsReady = false;
        instructionInventory = { version: 1, available: false, files: [] };
        ctx.ui.setStatus("pi-gui-instructions-v1", JSON.stringify(instructionInventory));
        const files = await TargetInstructions.load(connection, ctx.signal);
        instructionInventory = { version: 1, available: true,
            files: [...(event.systemPromptOptions?.contextFiles ?? []), ...files].map(file => ({ path: file.path, hash: createHash("sha256").update(file.content).digest("hex") })) };
        ctx.ui.setStatus("pi-gui-instructions-v1", JSON.stringify(instructionInventory));
        instructionsReady = true;
        return { systemPrompt: event.systemPrompt.replaceAll(cwd, target.path) +
            `\n\nExecution target: ${target.name} (${target.kind}, ${target.host}). All workspace tools run there. Use Linux paths. The agent and sessions remain on the desktop.\n` +
            files.map(file => `\nProject instructions: ${file.path}\n${file.content}`).join("\n") };
    });
}
