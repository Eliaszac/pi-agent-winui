import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";

/** Native host owns GitHub credentials, repository validation, and explicit approval. */
export default function registerGitHubWrites(pi: ExtensionAPI): void {
    for (const action of ["update_issue", "comment_pr"] as const) {
        pi.registerTool({
            name: "github_" + action,
            label: action === "update_issue" ? "Update GitHub issue" : "Comment on GitHub PR",
            description: action === "update_issue"
                ? "Propose changes to an existing issue in this project's GitHub origin repository. Only title, body and open/closed state are supported. Omitted fields stay unchanged; an empty body clears it. The native app fetches current values and asks the user to approve exact changes before writing. Never retry an unknown write outcome without checking GitHub."
                : "Propose a general conversation comment on a pull request in this project's GitHub origin repository. The native app asks the user to approve the exact comment before posting. No inline reviews or merges. Never retry an unknown write outcome without checking GitHub.",
            parameters: action === "update_issue"
                ? Type.Object({ number: Type.Integer({ minimum: 1 }), title: Type.Optional(Type.String({ minLength: 1, maxLength: 256 })), body: Type.Optional(Type.String({ maxLength: 60000 })), state: Type.Optional(Type.Union([Type.Literal("open"), Type.Literal("closed")])) })
                : Type.Object({ number: Type.Integer({ minimum: 1 }), body: Type.String({ minLength: 1, maxLength: 60000 }) }),
            async execute(_id, params, signal, _update, ctx) {
                if (signal?.aborted) throw new Error("GitHub operation cancelled.");
                const response = await ctx.ui.input("pi-gui-github-write-v1", JSON.stringify({ ...params, action }));
                if (!response) throw new Error("GitHub operation cancelled.");
                const result: unknown = JSON.parse(response);
                if (!result || typeof result !== "object" || Array.isArray(result)) throw new Error("Invalid GitHub response.");
                if ("error" in result && typeof result.error === "string") throw new Error(result.error);
                return { content: [{ type: "text", text: JSON.stringify(result) }], details: result };
            },
        });
    }
}
