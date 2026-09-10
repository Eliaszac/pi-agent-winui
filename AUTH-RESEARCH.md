# Provider authentication research

Checked 2026-09-10 against current Pi documentation and upstream v0.85.1 source. The first Providers implementation now follows this research; see ARCHITECTURE.md for implemented scope and verification limits. No user credentials were read or changed during development.

## Standard RPC

- `get_available_models` supplies full model objects; `get_state` supplies the conversation's selected model. Metadata includes provider/model IDs, name, API, endpoint, reasoning capability, input types, context window, output limit, and catalog cost rates. The GUI currently retains only provider, ID, and name.
- `get_session_stats` reports session token/cost totals and context estimates, not account balance or subscription allowance.
- There are no standard provider-list/auth-status/login/logout RPC commands. `/login` and `/logout` belong to the TUI; sending them as prompts is not authentication management.

Source: https://pi.dev/docs/latest/rpc

## SDK capabilities

- `ModelRuntime.getProviders`, `checkAuth`, `listCredentials`, `getProviderAuthStatus`, `isUsingOAuth`, and `isUsingSubscription` support provider discovery and status. Status can identify stored, runtime, environment, or configured auth sources. Availability is configuration evidence, not proof that a provider will accept the next request.
- `login(providerId, type, interaction)` runs the provider-owned flow and persists credentials; `logout` removes stored credentials. Runtime key overrides are temporary. Credential operations synchronize the local model snapshot; remote catalog refresh is separate. `CredentialSynchronizationError` can mean the credential was already committed: do not retry a mutation blindly.
- `ModelRegistry`, exposed to extensions, offers provider lookup/status and refresh but does not expose login/logout. A bundled integration would need its own public `ModelRuntime`, then explicitly refresh conversation registries after changes.

Sources:
- https://pi.dev/docs/latest/sdk
- https://raw.githubusercontent.com/earendil-works/pi/v0.85.1/packages/coding-agent/src/core/model-runtime.ts
- https://raw.githubusercontent.com/earendil-works/pi/v0.85.1/packages/coding-agent/src/core/model-registry.ts

## Native login flow and account limits

- `AuthInteraction` supports text/secret/select/manual-code prompts, authorization URLs, device codes, progress, and cancellation. These can map to native controls plus the system browser.
- Non-secret credential enumeration returns provider ID and credential type. There is no standardized email/avatar/plan/quota/account-profile interface. OAuth records contain expiry and provider-specific extra fields, but expiry alone does not mean disconnected because refresh is supported.
- Default storage has one credential per provider ID. Multiple accounts for the same provider would require additional account-profile/storage design.
- Logout removes local stored credentials; it does not universally revoke tokens remotely or remove environment/config credentials that may still provide access.

Sources:
- https://raw.githubusercontent.com/earendil-works/pi/v0.85.1/packages/ai/src/auth/types.ts
- https://raw.githubusercontent.com/earendil-works/pi/v0.85.1/packages/ai/src/models.ts
- https://pi.dev/docs/latest/providers

## Suggested integration

- Global Providers surface, independent of having a project/conversation open. One app-owned authentication coordinator can use Pi's SDK through a bundled management integration; this transport/lifecycle still needs implementation design.
- Keep Pi's shared global auth storage as the source of truth rather than making a competing C# credential file. Return only allowlisted status metadata to the GUI. Keep secrets out of prompts, transcripts, project metadata, diagnostic logs, and generic exception serialization; add a dedicated masked-input/reply path for login.
- After changes, refresh idle conversation registries and model selectors; defer changes affecting active runs. AuthStorage detects file revisions and uses file locking, but separate runtime catalog snapshots still need explicit refresh. Do not assume a global restart is necessary or that every process immediately updates its UI.
- First UI: provider name, supported login methods, configured/auth-required/error status, credential source, model count/list; sign in, add/replace key, sign out, and refresh. Show provider-specific setup for ambient/cloud credentials. Account quota dashboards and multi-account switching are outside this initial proposal.

Storage source: https://raw.githubusercontent.com/earendil-works/pi/v0.85.1/packages/coding-agent/src/core/auth-storage.ts
