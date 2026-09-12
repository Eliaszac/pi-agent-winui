import assert from "node:assert/strict";
import { test } from "node:test";
import { TargetConnection } from "../../PiExtensions/TargetConnection.ts";
import { TargetSshAuthentication } from "../../PiExtensions/TargetSshAuthentication.ts";

const target = { id: "0be8a01a-1234-4321-a123-123456789abc", name: "Build host", kind: "ssh", host: "user@build", path: "/home/user/project" };
test("SSH arguments cannot inject options and use POSIX literal quoting", () => {
    const connection = new TargetConnection(JSON.stringify(target));
    const launch = connection.startArguments("echo 'hello'");
    assert.equal(launch.executable, "ssh.exe");
    assert.ok(launch.args.includes("StrictHostKeyChecking=yes"));
    assert.ok(launch.args.includes("BatchMode=yes"));
    assert.equal(launch.args.at(-2), "user@build");
    assert.equal(launch.args.at(-1), `bash -lc 'echo '\"'\"'hello'\"'\"''`);
    assert.throws(() => new TargetConnection(JSON.stringify({ ...target, host: "-oProxyCommand=evil" })));
    assert.throws(() => new TargetConnection(JSON.stringify({ ...target, host: "host; echo evil" })));
});
test("WSL keeps distribution and command as separate argv entries", () => {
    const connection = new TargetConnection(JSON.stringify({ ...target, kind: "wsl", host: "Ubuntu Test" }));
    assert.deepEqual(connection.startArguments("pwd").args, ["--distribution", "Ubuntu Test", "--exec", "bash", "-lc", "pwd"]);
});
test("paths resolve on Linux and never become desktop paths", () => {
    const connection = new TargetConnection(JSON.stringify(target));
    assert.equal(connection.resolve("src/../README.md"), "/home/user/project/README.md");
    assert.equal(connection.resolve("/tmp/example"), "/tmp/example");
    assert.throws(() => connection.resolve("C:\\Windows\\test"));
    assert.throws(() => connection.resolve("~/file"));
    assert.throws(() => connection.resolve("bad\0name"));
    assert.equal(TargetConnection.quote("$(touch nope)`x`"), "'$(touch nope)`x`'");
});
test("invalid target data fails closed", () => {
    for (const value of [null, {}, { ...target, kind: "local" }, { ...target, path: "relative" }, { ...target, path: "/repo/../other" }])
        assert.throws(() => new TargetConnection(JSON.stringify(value)));
});
test("password and explicit key authentication use distinct SSH policies", () => {
    const password = new TargetConnection(JSON.stringify({ ...target, sshAuthentication: "password", hasSshSecret: true }));
    assert.ok(password.startArguments("pwd").args.includes("PreferredAuthentications=password"));
    assert.ok(password.startArguments("pwd").args.includes("BatchMode=no"));
    assert.ok(password.startArguments("pwd").args.includes("StrictHostKeyChecking=yes"));
    assert.throws(() => new TargetConnection(JSON.stringify({ ...target, sshAuthentication: "password" })));
    const key = new TargetConnection(JSON.stringify({ ...target, sshKeyPath: "C:\\Keys\\work key" }));
    assert.ok(key.startArguments("pwd").args.includes("C:\\Keys\\work key"));
    assert.ok(key.startArguments("pwd").args.includes("PasswordAuthentication=no"));
    assert.ok(TargetSshAuthentication.arguments(password.target).includes("NumberOfPasswordPrompts=1"));
});
