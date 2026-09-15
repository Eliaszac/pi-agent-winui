import test from 'node:test';
import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';

const hooks = registerHooks({ resolve(specifier, context, next) {
    if (specifier === 'typebox') return { shortCircuit: true, url: 'data:text/javascript,export const Type={Object:p=>p,String:()=>({}),Integer:()=>({}),Optional:p=>p,Union:p=>p,Literal:p=>p};' };
    return next(specifier, context);
}});
const { default: register } = await import('../../PiExtensions/github-write.ts');
hooks.deregister();

test('GitHub tools delegate exact proposals to native approval and expose results', async () => {
    const tools = new Map();
    register({ registerTool: tool => tools.set(tool.name, tool) });
    assert.deepEqual([...tools.keys()], ['github_update_issue', 'github_comment_pr']);
    for (const action of ['update_issue', 'comment_pr']) {
        const result = await tools.get('github_' + action).execute('call', { number: 12, body: 'Exact text' }, undefined, undefined,
            { ui: { input: async (title, payload) => {
                assert.equal(title, 'pi-gui-github-write-v1');
                assert.deepEqual(JSON.parse(payload), { action, number: 12, body: 'Exact text' });
                return '{"success":true,"url":"https://github.com/owner/repo/issues/12"}';
            } } });
        assert.equal(result.details.success, true);
    }
});

test('cancellation and host failures never retry or report success', async () => {
    const tools = new Map(); register({ registerTool: tool => tools.set(tool.name, tool) });
    const tool = tools.get('github_comment_pr');
    await assert.rejects(tool.execute('a', {}, AbortSignal.abort(), undefined, {}), /cancelled/);
    let calls = 0;
    await assert.rejects(tool.execute('b', {}, undefined, undefined, { ui: { input: async () => { calls++; return '{"error":"Outcome unknown"}'; } } }), /Outcome unknown/);
    assert.equal(calls, 1);
    const blocked = await tool.execute('c', {}, undefined, undefined, { ui: { input: async () => '{"cancelled":true}' } });
    assert.equal(blocked.details.cancelled, true);
    await assert.rejects(tool.execute('d', {}, undefined, undefined, { ui: { input: async () => 'null' } }), /Invalid/);
});
