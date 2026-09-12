import { spawn } from "node:child_process";
import path from "node:path";
import { TargetSshAuthentication } from "./TargetSshAuthentication.ts";

export type RemoteTarget = { id: string; kind: "wsl" | "ssh"; name: string; host: string; path: string; sshAuthentication?: "key" | "password"; sshKeyPath?: string; hasSshSecret?: boolean };

/** Validates the desktop-owned target and transports commands without a Windows shell. */
export class TargetConnection {
    readonly target: RemoteTarget;
    constructor(serialized: string) {
        const value: unknown = JSON.parse(serialized);
        if (!value || typeof value !== "object" || !("id" in value) || typeof value.id !== "string" || !("kind" in value) || !("host" in value) || !("path" in value) || !("name" in value)
            || (value.kind !== "wsl" && value.kind !== "ssh") || typeof value.host !== "string" || typeof value.path !== "string" || typeof value.name !== "string"
            || !value.path.startsWith("/") || /[\\\x00-\x1f]/.test(value.path) || value.path.split("/").some(p => p === "." || p === "..")
            || !(value.kind === "ssh" ? /^[A-Za-z0-9_][A-Za-z0-9_.@-]*$/ : /^[A-Za-z0-9_][A-Za-z0-9_. -]*$/).test(value.host))
            throw new Error("Invalid execution target. Local execution is disabled.");
        this.target = value as RemoteTarget;
        if (!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(this.target.id)
            || (this.target.sshAuthentication !== undefined && !["key", "password"].includes(this.target.sshAuthentication))
            || (this.target.hasSshSecret !== undefined && typeof this.target.hasSshSecret !== "boolean")
            || (this.target.sshKeyPath !== undefined && (typeof this.target.sshKeyPath !== "string" || /[\x00-\x1f]/.test(this.target.sshKeyPath)))
            || (this.target.kind === "ssh" && this.target.sshAuthentication === "password" && !this.target.hasSshSecret))
            throw new Error("Invalid SSH authentication settings.");
    }

    static quote(value: string): string {
        if (value.includes("\0")) throw new Error("NUL is not allowed in a shell argument.");
        return "'" + value.replaceAll("'", "'\"'\"'") + "'";
    }

    resolve(value = "."): string {
        if (value.includes("\\") || /^[A-Za-z]:/.test(value) || value.includes("\0") || value.startsWith("~"))
            throw new Error("Use Linux paths on the execution target; local Windows paths and ~ are not supported.");
        return path.posix.resolve(this.target.path, value);
    }

    startArguments(command: string): { executable: string; args: string[] } {
        return this.target.kind === "wsl" ? { executable: "wsl.exe", args: ["--distribution", this.target.host, "--exec", "bash", "-lc", command] }
            : { executable: "ssh.exe", args: ["-T", ...TargetSshAuthentication.arguments(this.target), "--", this.target.host, "bash -lc " + TargetConnection.quote(command)] };
    }

    async run(command: string, signal?: AbortSignal, input?: string, timeoutSeconds = 120,
        onData?: (data: Buffer) => void): Promise<{ output: Buffer; exitCode: number }> {
        signal?.throwIfAborted();
        const launch = this.startArguments(command);
        return await new Promise((resolve, reject) => {
            const child = spawn(launch.executable, launch.args, { windowsHide: true, stdio: ["pipe", "pipe", "pipe"], env: TargetSshAuthentication.environment(this.target) });
            const chunks: Buffer[] = [];
            let size = 0;
            let failure: Error | undefined;
            const stop = (error: Error) => { failure ??= error; child.kill(); };
            const aborted = () => stop(new Error("Target operation cancelled; verify remote command state before retrying."));
            const timer = setTimeout(() => stop(new Error("Target operation timed out; its outcome may be uncertain.")), Math.min(Math.max(timeoutSeconds, 1), 1200) * 1000);
            signal?.addEventListener("abort", aborted, { once: true });
            if (signal?.aborted) aborted();
            child.stdout.on("data", (data: Buffer) => {
                size += data.length;
                if (size > 8 * 1024 * 1024) { stop(new Error("Target output exceeded 8 MB. Narrow the request.")); return; }
                if (onData) onData(data); else chunks.push(data);
            });
            // Command output is forwarded only for shell tools; transport diagnostics never enter persisted logs.
            child.stderr.on("data", (data: Buffer) => { if (onData) { size += data.length; if (size > 8 * 1024 * 1024) stop(new Error("Target output exceeded 8 MB.")); else onData(data); } });
            child.stdin.on("error", () => {});
            if (!onData) child.stdin.end(input ?? "");
            const cleanup = () => { child.stdin.destroy(); clearTimeout(timer); signal?.removeEventListener("abort", aborted); };
            child.on("error", error => { cleanup(); reject(error); });
            child.on("close", code => {
                cleanup();
                if (failure) reject(failure);
                else if (code === null || code === 255) reject(new Error("Target connection failed. Check WSL or SSH credentials and known_hosts. No local fallback was used."));
                else resolve({ output: Buffer.concat(chunks), exitCode: code });
            });
        });
    }

    async checked(command: string, signal?: AbortSignal, input?: string): Promise<Buffer> {
        const result = await this.run(command, signal, input);
        if (result.exitCode !== 0) throw new Error(`Target operation failed (exit ${result.exitCode}). Check the target path, permissions and required commands.`);
        return result.output;
    }
}
