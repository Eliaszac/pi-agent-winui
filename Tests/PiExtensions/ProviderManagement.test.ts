import assert from "node:assert/strict";
import test from "node:test";
import { ProviderManagement } from "../../PiExtensions/ProviderManagement.ts";

test("discovery publishes only safe fields and distinguishes stored credentials from ambient access", async () => {
    const messages: string[] = [];
    const runtime = {
        listCredentials: async () => [{ providerId: "test", type: "oauth", access: "never-expose" }],
        getProviders: () => [{ id: "test", name: "Test", auth: { oauth: { login: () => {}, loginLabel: "Sign in" } } },
            { id: "ambient", name: "Ambient", auth: { apiKey: { resolve: () => {} } } }],
        getProviderAuthStatus: () => ({ configured: true, source: "environment" }),
        isUsingOAuth: () => false,
        getModels: () => [{ id: "m", name: "Model", contextWindow: 100, maxTokens: 10, reasoning: true, input: ["text"], headers: { Authorization: "never-expose" } }],
    };
    await ProviderManagement.run('{"action":"list"}', { ui: { notify: (message: string) => messages.push(message) } } as never, async () => runtime as never);
    const result = JSON.parse(messages.at(-1)!);
    assert.equal(result.kind, "result");
    assert.equal(result.providers[0].stored, true);
    assert.equal(result.providers[1].stored, false);
    assert.equal(result.providers[1].apiKey, false);
    assert.equal(messages.join("").includes("never-expose"), false);
});

test("a saved credential followed by synchronization failure is reported without exposing credentials or retrying", async () => {
    const messages: string[] = [];
    let calls = 0;
    const runtime = {
        getProvider: () => ({}),
        login: async () => {
            calls++;
            const error = new Error("sensitive-value");
            error.name = "CredentialSynchronizationError";
            Object.assign(error, { credential: { access: "sensitive-value" } });
            throw error;
        },
    };
    await ProviderManagement.run('{"action":"login","provider":"test","method":"oauth"}',
        { ui: { notify: (message: string) => messages.push(message) } } as never, async () => runtime as never);
    assert.equal(calls, 1);
    assert.equal(JSON.parse(messages[0]).kind, "changed");
    assert.equal(JSON.parse(messages[1]).kind, "error");
    assert.equal(messages.join("").includes("sensitive-value"), false);
});

test("browser callback abort dismisses the manual input prompt", async () => {
    const messages: string[] = [];
    const controller = new AbortController();
    const runtime = {
        getProvider: () => ({}),
        login: async (_provider: string, _method: string, interaction: { prompt: (prompt: object) => Promise<string> }) =>
            interaction.prompt({ type: "manual_code", message: "Code", signal: controller.signal }),
    };
    await ProviderManagement.run('{"action":"login","provider":"test","method":"oauth"}', { ui: {
        notify: (message: string) => messages.push(message),
        input: async (title: string) => {
            const prompt = JSON.parse(title);
            assert.equal(prompt.type, "manual_code");
            assert.equal("signal" in prompt, false);
            controller.abort();
            return undefined;
        },
    } } as never, async () => runtime as never);
    assert.equal(JSON.parse(messages[0]).kind, "dismiss");
});
