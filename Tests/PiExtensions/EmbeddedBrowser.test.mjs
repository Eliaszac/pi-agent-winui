import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import adapter from '../../PiExtensions/embedded-browser.ts';

test('embedded adapter delegates page tools but owns native tabs and translates preview URLs', async () => {
    const root = await mkdtemp(join(tmpdir(), 'pi-browser-unit-'));
    const previous = process.env.PI_GUI_BROWSER_PACKAGE;
    process.env.PI_GUI_BROWSER_PACKAGE = root;
    try {
        await writeFile(join(root, 'index.ts'), `export default function(pi) {
            pi.registerCommand('browser', {handler: async (args,ctx) => {if (!args.startsWith('connect ')) throw Error('unexpected command');}});
            for(const name of ['browser_navigate','browser_tabs','browser_close','browser_file_upload','browser_storage_state','browser_set_storage_state'])
                pi.registerTool({name, description:name, parameters:{}, execute:async(id,params)=>({content:[{type:'text',text:JSON.stringify(params)}],details:{}})});
        }`);
        const tools = new Map();
        await adapter({registerTool: tool => tools.set(tool.name,tool), on() {}});
        const requests = [];
        const ctx = { ui: { input: async (title,data) => {
            assert.equal(title,'pi-gui-browser-v1');
            const request=JSON.parse(data); requests.push(request);
            return JSON.stringify(request.action==='ready'?{port:12345}:request.action==='resolve'?{url:'http://127.0.0.1:54321/'}:{tabs:[]});
        }}};
        const result=await tools.get('embedded_browser_navigate').execute('a',{url:'http://localhost:3000'},undefined,undefined,ctx);
        assert.equal(JSON.parse(result.content[0].text).url,'http://127.0.0.1:54321/');
        await tools.get('embedded_browser_tabs').execute('b',{action:'new'},undefined,undefined,ctx);
        assert.deepEqual(requests.at(-1),{action:'tabs',operation:'new'});
        await tools.get('embedded_browser_close').execute('c',{},undefined,undefined,ctx);
        assert.deepEqual(requests.at(-1),{action:'tabs',operation:'close'});
        await assert.rejects(tools.get('embedded_browser_navigate').execute('d',{},AbortSignal.abort(),undefined,ctx),/cancelled/);
        await assert.rejects(tools.get('embedded_browser_navigate').execute('e',{},undefined,undefined,{ui:{input:async()=>JSON.stringify({error:'Conversation closed'})}}),/Conversation closed/);
        const previousTarget=process.env.PI_GUI_EXECUTION_TARGET;
        try {
            process.env.PI_GUI_EXECUTION_TARGET='{}';
            const remoteTools=new Map();
            await adapter({registerTool:tool=>remoteTools.set(tool.name,tool),on(){}});
            assert.ok(remoteTools.has('embedded_browser_navigate'));
            for(const name of ['browser_file_upload','browser_storage_state','browser_set_storage_state']) assert.equal(remoteTools.has('embedded_'+name),false);
        } finally {
            if(previousTarget===undefined) delete process.env.PI_GUI_EXECUTION_TARGET; else process.env.PI_GUI_EXECUTION_TARGET=previousTarget;
        }
    } finally {
        if (previous === undefined) delete process.env.PI_GUI_BROWSER_PACKAGE; else process.env.PI_GUI_BROWSER_PACKAGE=previous;
        await rm(root,{recursive:true,force:true});
    }
});
