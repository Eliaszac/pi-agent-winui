import test from 'node:test';
import assert from 'node:assert/strict';
import { StartupHandlerTiming } from '../../PiExtensions/StartupHandlerTiming.ts';

test('records handlers without changing order, error handling or retained handlers', async () => {
    const seen = [], timings = [];
    const failure = new Error('expected');
    const handlers = [async () => { seen.push(1); }, () => { throw failure; }, () => { seen.push(3); }];
    const saved = [...handlers];
    const prototype = { async emit(event) {
        for (const handler of this.extensions[0].handlers.get(event.type) ?? []) {
            try { await handler(event); } catch (error) { assert.equal(error, failure); seen.push(2); }
        }
        return 'original result';
    } };
    StartupHandlerTiming.install(prototype, (label, ms) => timings.push([label, ms]));
    const runner = Object.assign(Object.create(prototype), { extensions: [{ path: 'C:/private/checkpoints.ts', handlers: new Map([['session_start', handlers]]) }] });
    assert.equal(await runner.emit({ type: 'session_start' }), 'original result');
    assert.deepEqual(seen, [1, 2, 3]);
    assert.deepEqual(handlers, saved);
    assert.equal(timings.length, 4);
    assert.ok(timings.every(([label, ms]) => !label.includes('private') && ms >= 0));
});

test('restores handlers when dispatch fails and ignores diagnostic failures', async () => {
    const handler = () => undefined;
    const handlers = [handler];
    const prototype = { async emit() { throw new Error('dispatch'); } };
    StartupHandlerTiming.install(prototype, () => { throw new Error('logger'); });
    const runner = Object.assign(Object.create(prototype), { extensions: [{ handlers: new Map([['session_start', handlers]]) }] });
    await assert.rejects(runner.emit({ type: 'session_start' }), /dispatch/);
    assert.equal(handlers[0], handler);
});
