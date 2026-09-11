import { ModelRuntime, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { ProviderManagement } from "./ProviderManagement.ts";

/** Loaded only by the dedicated, non-persistent Providers RPC process. */
export default function (pi: ExtensionAPI): void {
    pi.registerCommand("pi-gui-providers", {
        description: "Internal provider management for Pi desktop",
        handler: (args, ctx) => ProviderManagement.run(args, ctx, signal => ModelRuntime.create({ signal })),
    });
}
