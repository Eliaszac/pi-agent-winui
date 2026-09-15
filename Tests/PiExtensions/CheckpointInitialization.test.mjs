import test from 'node:test';
import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';

const hooks = registerHooks({ resolve(specifier, context, next) {
    if (specifier === 'node:fs/promises') return { shortCircuit: true, url: 'data:text/javascript,export const readFile=async()=>JSON.stringify({enabled:true});' };
    if (specifier === './CheckpointTransport.ts') return { shortCircuit: true, url: 'data:text/javascript,export class CheckpointTransport { request(value) { return globalThis.checkpointRequest(value); } }' };
    if (specifier === './CheckpointActivity.ts') return { shortCircuit: true, url: 'data:text/javascript,export class CheckpointActivity { async begin() { return false; } }' };
    return next(specifier, context);
} });
const { default: register } = await import('../../PiExtensions/checkpoints.ts');
hooks.deregister();

test('startup returns immediately but agent capture waits for the complete inventory', async () => {
    const previous = process.env.PI_GUI_CHECKPOINT_SETTINGS;
    process.env.PI_GUI_CHECKPOINT_SETTINGS = 'mock-settings';
    const handlers = new Map(), statuses = [], requests = [];
    let release;
    const inventory = new Promise(resolve => { release = resolve; });
    globalThis.checkpointRequest = async request => {
        requests.push(request.action);
        if (request.action === 'list') return inventory;
        if (request.action === 'manifest') return {};
        return { id: 'capture' };
    };
    try {
        register({ on: (name, fn) => handlers.set(name, fn), registerCommand() {} });
        const ctx = { cwd: '.', sessionManager: { getBranch: () => [], getSessionFile: () => 'session' },
            ui: { setStatus: (_, data) => statuses.push(JSON.parse(data)) } };
        assert.equal(handlers.get('session_start')({}, ctx), undefined);
        const agent = handlers.get('before_agent_start')({}, ctx);
        await new Promise(resolve => setImmediate(resolve));
        assert.deepEqual(requests, ['list']);
        assert.ok(!statuses.some(value => value.status === 'ready'));
        release({ records: [{ id: 'old', state: 'finished' }] });
        await agent;
        assert.deepEqual(requests, ['list', 'manifest', 'begin']);
        assert.deepEqual(statuses.filter(value => value.status).map(value => value.status), ['initializing', 'initializing', 'ready', 'capturing']);
        await handlers.get('session_shutdown')();
    } finally {
        if (previous === undefined) delete process.env.PI_GUI_CHECKPOINT_SETTINGS;
        else process.env.PI_GUI_CHECKPOINT_SETTINGS = previous;
        delete globalThis.checkpointRequest;
    }
});
