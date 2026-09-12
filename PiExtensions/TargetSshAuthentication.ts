import type { RemoteTarget } from "./TargetConnection.ts";

export class TargetSshAuthentication {
    static arguments(target: RemoteTarget): string[] {
        const args = ["-o", target.hasSshSecret ? "BatchMode=no" : "BatchMode=yes", "-o", "NumberOfPasswordPrompts=1",
            "-o", "StrictHostKeyChecking=yes", "-o", "ConnectTimeout=10", "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=2",
            "-o", "KbdInteractiveAuthentication=no", "-o", "AddKeysToAgent=no"];
        args.push(...(target.sshAuthentication === "password" ? ["-o", "PreferredAuthentications=password", "-o", "PubkeyAuthentication=no", "-o", "PasswordAuthentication=yes"]
            : ["-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no"]));
        if (target.sshAuthentication !== "password" && target.sshKeyPath) args.push("-o", "IdentitiesOnly=yes", "-i", target.sshKeyPath);
        return args;
    }
    static environment(target: RemoteTarget): NodeJS.ProcessEnv {
        if (target.kind !== "ssh" || !target.hasSshSecret) return process.env;
        const helper = process.env.PI_GUI_SSH_ASKPASS_PATH;
        if (!helper) throw new Error("The desktop SSH credential helper is unavailable. Restart the conversation from Pi desktop.");
        return { ...process.env, SSH_ASKPASS: helper, SSH_ASKPASS_REQUIRE: "force", DISPLAY: "pi-desktop", PI_DESKTOP_SSH_CREDENTIAL: target.id.replaceAll("-", "") };
    }
}
