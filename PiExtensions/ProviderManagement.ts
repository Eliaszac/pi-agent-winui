import type { ModelRuntime, ExtensionCommandContext } from "@earendil-works/pi-coding-agent";
import type { AuthInteraction } from "@earendil-works/pi-ai";

type ProviderRuntime = Pick<ModelRuntime, "getProvider" | "login" | "logout" | "listCredentials" | "getProviders" | "getProviderAuthStatus" | "isUsingOAuth" | "getModels">;

/** Adapts Pi authentication to a non-secret metadata channel and transient native prompts. */
export class ProviderManagement {
    static async run(args: string, ctx: Pick<ExtensionCommandContext, "ui">,
        createRuntime: (signal: AbortSignal) => Promise<ProviderRuntime>): Promise<void> {
        const emit = (payload: object): void => ctx.ui.notify(JSON.stringify({ piGuiProviders: 1, ...payload }), "info");
        const signal = AbortSignal.timeout(10 * 60 * 1000);
        try {
            const request: unknown = JSON.parse(args);
            if (!request || typeof request !== "object" || !("action" in request) ||
                !["list", "login", "logout"].includes(String(request.action))) throw new Error("Invalid operation");
            const runtime = await createRuntime(signal);
            if (request.action !== "list") {
                if (!("provider" in request) || typeof request.provider !== "string" || !runtime.getProvider(request.provider))
                    throw new Error("Unknown provider");
                if (request.action === "logout") await runtime.logout(request.provider, { signal });
                else {
                    if (!("method" in request) || (request.method !== "oauth" && request.method !== "api_key")) throw new Error("Invalid auth method");
                    const interaction: AuthInteraction = {
                        signal,
                        notify: event => emit({ kind: "event", event }),
                        async prompt(prompt) {
                            const promptId = crypto.randomUUID();
                            const localSignal = prompt.signal ? AbortSignal.any([signal, prompt.signal]) : signal;
                            const dismiss = (): void => emit({ kind: "dismiss", promptId });
                            localSignal.addEventListener("abort", dismiss, { once: true });
                            try {
                                localSignal.throwIfAborted();
                                const { signal: _signal, ...fields } = prompt;
                                const value = await ctx.ui.input(JSON.stringify({ piGuiProviders: 1, promptId, ...fields }), undefined, { signal: localSignal });
                                if (value === undefined) throw new Error("Authentication cancelled");
                                return value;
                            } finally { localSignal.removeEventListener("abort", dismiss); }
                        },
                    };
                    // The return value contains secrets. Never send it over the metadata channel.
                    await runtime.login(request.provider, request.method, interaction);
                }
                emit({ kind: "changed" });
            }
            const credentials = await runtime.listCredentials({ signal });
            const providers = runtime.getProviders().map(provider => {
                const status = runtime.getProviderAuthStatus(provider.id);
                const credential = credentials.find(item => item.providerId === provider.id);
                return {
                    id: provider.id, name: provider.name || provider.id,
                    configured: status.configured, source: status.source || "",
                    method: credential?.type || (runtime.isUsingOAuth(provider.id) ? "oauth" : ""),
                    stored: credential !== undefined,
                    oauth: !!provider.auth.oauth, apiKey: !!provider.auth.apiKey?.login,
                    loginLabel: provider.auth.oauth?.loginLabel || "Sign in",
                    models: runtime.getModels(provider.id).map(model => ({
                        id: model.id, name: model.name, contextWindow: model.contextWindow,
                        maxTokens: model.maxTokens, reasoning: model.reasoning, input: model.input,
                    })),
                };
            });
            emit({ kind: "result", providers });
        } catch (error) {
            // Provider errors may contain request bodies or credentials; do not serialize them.
            const committed = error instanceof Error && error.name === "CredentialSynchronizationError";
            if (committed) emit({ kind: "changed" });
            emit({ kind: "error", message: committed
                ? "Credentials were saved, but model synchronization failed. Refresh the Providers page before trying again."
                : "Pi couldn't complete this operation. Check your provider setup and try again. No request was retried." });
        }
    }
}

