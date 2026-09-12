import { test } from 'node:test';
import assert from 'node:assert/strict';
import register from '../../PiExtensions/mcp-status.ts';

test('MCP errors reach the panel without bearer credentials', () => {
    const handlers = new Map();
    let status;
    register({ events: { on: (name, callback) => handlers.set(name, callback) }, on: (name, callback) => handlers.set(name, callback) });
    handlers.get('session_start')({}, { ui: { setStatus: (_, value) => status = JSON.parse(value) } });
    handlers.get('pi-mcp-adapter/status/v1')({ version: 1, servers: [{ name: 'notes', status: 'failed', toolCount: 0 }] });
    handlers.get('tool_result')({ toolName: 'mcp', isError: true, details: { server: 'notes', error: 'connection_failed', message: 'HTTP 401 Bearer secret-value' } });
    assert.match(status.servers[0].error, /HTTP 401/);
    assert.doesNotMatch(status.servers[0].error, /secret-value/);
    handlers.get('pi-mcp-adapter/status/v1')({ version: 1, servers: [{ name: 'notes', status: 'connected', toolCount: 2 }] });
    assert.equal(status.servers[0].error, undefined);
});
