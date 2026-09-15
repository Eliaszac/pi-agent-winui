import test from 'node:test';
import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';

// Exercise the actual extension without loading Pi, a provider, or a filesystem tool.
const hooks = registerHooks({ resolve(specifier, context, next) {
    if (specifier === 'typebox') return { shortCircuit: true, url: 'data:text/javascript,export const Type={Object:p=>p,String:()=>({type:"string"}),Number:()=>({type:"number"}),Optional:p=>p};' };
    return next(specifier, context);
}});
const { default: register } = await import('../../PiExtensions/artifacts.ts');
hooks.deregister();

test('artifact tools send native requests and preserve the card identity in tool details', async () => {
    const tools = new Map();
    register({ registerTool: tool => tools.set(tool.name, tool) });
    const requests = [];
    const ctx = { ui: { input: async (title, data) => {
        assert.equal(title, 'pi-gui-artifacts-v1');
        const request = JSON.parse(data); requests.push(request);
        return JSON.stringify(request.action === 'list' ? { artifacts: [] } : { artifactId: 'saved-id', name: request.name, size: 5 });
    }}};
    const save = tools.get('artifact_save');
    const result = await save.execute('call-a', { name: 'report.md', content: 'Hello' }, undefined, undefined, ctx);
    assert.equal(result.details.artifactId, 'saved-id');
    assert.deepEqual(JSON.parse(result.content[0].text), result.details);
    assert.deepEqual(requests[0], { name: 'report.md', content: 'Hello', action: 'save', requestId: 'call-a' });
    await save.execute('call-b', { name: 'report.pdf', sourcePath: '/tmp/report.pdf', artifactId: 'saved-id' }, undefined, undefined, ctx);
    assert.equal(requests[1].sourcePath, '/tmp/report.pdf');
    assert.equal(requests[1].artifactId, 'saved-id');
    await tools.get('artifact_list').execute('call-c', {}, undefined, undefined, ctx);
    assert.deepEqual(requests[2], { action: 'list' });
    await assert.rejects(save.execute('call-d', {}, AbortSignal.abort(), undefined, ctx), /cancelled/);
    await assert.rejects(save.execute('call-e', {}, undefined, undefined, { ui: { input: async () => '{"error":"Artifact exceeds limit"}' } }), /exceeds limit/);
    await assert.rejects(save.execute('call-f', {}, undefined, undefined, { ui: { input: async () => 'null' } }), /Invalid artifact/);
});

test('reads return image blocks without copying base64 into details and path requests remain target-owned', async () => {
    const tools = new Map();
    register({ registerTool: tool => tools.set(tool.name, tool) });
    const requests = [];
    const ctx = { ui: { input: async (_title, data) => {
        const request = JSON.parse(data); requests.push(request);
        return JSON.stringify(request.action === 'path' ? { path: '/home/test/.pi-desktop-artifacts/document.pdf' } : { image: 'aGVsbG8=', mimeType: 'image/png' });
    }}};
    const image = await tools.get('artifact_read').execute('a', { artifactId: 'image-id' }, undefined, undefined, ctx);
    assert.deepEqual(image.content, [{ type: 'image', data: 'aGVsbG8=', mimeType: 'image/png' }]);
    assert.deepEqual(image.details, { artifactId: 'image-id' });
    const path = await tools.get('artifact_path').execute('b', { artifactId: 'file-id' }, undefined, undefined, ctx);
    assert.equal(JSON.parse(path.content[0].text).path, '/home/test/.pi-desktop-artifacts/document.pdf');
    assert.deepEqual(requests[1], { action: 'path', artifactId: 'file-id' });
});
