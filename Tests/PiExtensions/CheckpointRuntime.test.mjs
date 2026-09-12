import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';

test('real Pi checkpoint extension restores files without a model request', { skip: !process.env.PI_CHECKPOINT_TEST_CLI, timeout: 120000 }, async () => {
    const scratch = await mkdtemp(join(tmpdir(), 'pi-checkpoint-harness-'));
    const local = join(scratch, 'pi-checkpoint-test-' + randomUUID());
    const { mkdir } = await import('node:fs/promises');
    await mkdir(local);
    const settings = join(scratch, 'settings.json');
    await writeFile(settings, JSON.stringify({ enabled: true }));
    const distribution = process.env.PI_CHECKPOINT_TEST_WSL;
    const remoteRoot = '/tmp/pi-checkpoint-test-' + randomUUID();
    if (distribution) execFileSync('wsl.exe', ['-d', distribution, '--exec', 'mkdir', remoteRoot], { windowsHide: true });
    const env = { ...process.env, PI_OFFLINE: '1', PI_GUI_CHECKPOINT_SETTINGS: settings };
    delete env.PI_GUI_ACTIVITY_DIRECTORY;
    if (distribution) env.PI_GUI_EXECUTION_TARGET = JSON.stringify({ id: randomUUID(), kind: 'wsl', host: distribution, path: remoteRoot, name: 'Checkpoint test' });
    else delete env.PI_GUI_EXECUTION_TARGET;
    const child = spawn(process.execPath, [process.env.PI_CHECKPOINT_TEST_CLI, '--mode', 'rpc', '--no-session', '--no-extensions', '--no-tools', '--no-skills', '--no-context-files', '--no-prompt-templates',
        '--extension', resolve('Tests/PiExtensions/CheckpointProbe.ts')], { cwd: local, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    const packets = [];
    let buffer = '', diagnostics = '', wake;
    const exited = new Promise(done => child.on('exit', code => { wake?.(); done(code); }));
    child.stderr.on('data', b => { diagnostics = (diagnostics + b.toString()).slice(-6000); });
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', text => {
        buffer += text;
        let at;
        while ((at = buffer.indexOf('\n')) >= 0) { packets.push(JSON.parse(buffer.slice(0, at))); buffer = buffer.slice(at + 1); }
        wake?.();
    });
    const timer = setTimeout(() => child.kill(), 100000);
    const response = async id => {
        while (true) {
            const packet = packets.find(p => p.type === 'response' && p.id === id);
            if (packet) return packet;
            if (child.exitCode !== null) throw Error('Pi exited: ' + diagnostics);
            await new Promise(done => { wake = done; });
        }
    };
    try {
        child.stdin.write(JSON.stringify({ type: 'get_commands', id: 'commands' }) + '\n');
        const commands = await response('commands');
        assert.ok(commands.data.commands.some(c => c.name === 'pi-gui-checkpoint'), diagnostics);
        child.stdin.write(JSON.stringify({ type: 'prompt', message: '/pi-checkpoint-probe', id: 'probe' }) + '\n');
        const result = await response('probe');
        assert.equal(result.success, true, JSON.stringify(result));
        assert.ok(packets.some(p => p.message === 'CHECKPOINT_PROBE_PASSED'), JSON.stringify(packets.filter(p => p.method === 'notify' || p.type === 'extension_error')));
    } finally {
        clearTimeout(timer);
        child.stdin.end();
        const kill = setTimeout(() => child.kill(), 2000);
        await exited;
        clearTimeout(kill);
        // Only this newly created harness directory is removed; target-side history is kept for inspection.
        assert.ok(scratch.startsWith(tmpdir()) && scratch.includes('pi-checkpoint-harness-'));
        await rm(scratch, { recursive: true, force: true });
    }
});
