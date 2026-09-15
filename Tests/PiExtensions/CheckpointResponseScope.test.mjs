import test from 'node:test';
import assert from 'node:assert/strict';
import { CheckpointResponseScope } from '../../PiExtensions/CheckpointResponseScope.ts';

const assistant = (id, timestamp) => ({ id, type: 'message', message: { role: 'assistant', timestamp } });
const user = id => ({ id, type: 'message', message: { role: 'user' } });
const old = assistant('old', 100);

test('a run with no new assistant cannot overwrite the previous card', () => {
    const scope = new CheckpointResponseScope([old, user('prompt')]);
    assert.equal(scope.settle([old, user('prompt')]), '');
});
test('the final new assistant owns the current checkpoint', () => {
    const scope = new CheckpointResponseScope([old, user('prompt')]);
    assert.equal(scope.settle([old, user('prompt'), assistant('interim', 200), assistant('final', 300)]), 'assistant:300');
});
test('an initially empty conversation can acquire its first response', () => {
    assert.equal(new CheckpointResponseScope([]).settle([user('prompt'), assistant('first', 200)]), 'assistant:200');
});
test('switching branches does not attach to unrelated responses', () => {
    assert.equal(new CheckpointResponseScope([old, user('prompt')]).settle([old, user('other'), assistant('other-response', 300)]), '');
});
test('retrying finish cannot reassign a checkpoint to a later run', () => {
    const scope = new CheckpointResponseScope([old]);
    assert.equal(scope.settle([old, assistant('first', 200)]), 'assistant:200');
    assert.equal(scope.settle([old, assistant('first', 200), assistant('later', 300)]), 'assistant:200');
});
test('an unassigned checkpoint stays unassigned on retry', () => {
    const scope = new CheckpointResponseScope([old]);
    assert.equal(scope.settle([old]), '');
    assert.equal(scope.settle([old, assistant('later', 300)]), '');
});
test('timestamp collisions cannot overwrite an existing response card', () => {
    assert.equal(new CheckpointResponseScope([old]).settle([old, assistant('new', 100)]), '');
});
