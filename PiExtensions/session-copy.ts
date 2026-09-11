import { SessionManager, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { SessionCopy } from "./SessionCopy.ts";

export default function registerSessionCopy(pi: ExtensionAPI): void {
    pi.registerCommand("pi-gui-copy-session", {
        description: "Internal Pi desktop conversation copy",
        handler: async (args, ctx) => {
            if (!ctx.isIdle()) throw new Error("Wait for the conversation to finish before copying it.");
            const request: unknown = JSON.parse(args);
            if (!request || typeof request !== "object" || !("target" in request) || !("title" in request) ||
                typeof request.target !== "string" || typeof request.title !== "string")
                throw new Error("Invalid conversation copy request.");
            const source = ctx.sessionManager.getSessionFile();
            const leaf = ctx.sessionManager.getLeafId();
            if (!source || !leaf) throw new Error("There is no saved conversation to copy yet.");
            SessionCopy.create(source, request.target, request.title, leaf, (path, directory) => {
                const copy = SessionManager.forkFrom(path, ctx.cwd, directory);
                return copy;
            });
        },
    });
}
