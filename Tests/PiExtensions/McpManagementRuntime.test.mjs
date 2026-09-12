import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';

test('isolated MCP management loads only the chosen server and connects without a model', { skip: !process.env.PI_MCP_TEST_CLI, timeout: 60000 }, async () => {
    const scratch = await mkdtemp(join(tmpdir(), 'pi-mcp-management-test-'));
    const fixture = join(scratch, 'fixture.mjs');
    await writeFile(fixture, `import {createInterface} from 'node:readline';
createInterface({input:process.stdin}).on('line', line => {
const req=JSON.parse(line); if(req.id===undefined)return;
let result={};
if(req.method==='initialize')result={protocolVersion:req.params.protocolVersion,capabilities:{tools:{}},serverInfo:{name:'fixture',version:'1.0'}};
else if(req.method==='tools/list')result={tools:[{name:'fixture_read',description:'Test fixture',inputSchema:{type:'object'}}]};
else if(req.method==='resources/list')result={resources:[]};
else if(req.method==='prompts/list')result={prompts:[]};
else {process.stdout.write(JSON.stringify({jsonrpc:'2.0',id:req.id,error:{code:-32601,message:'Method not found'}})+'\\n');return;}
process.stdout.write(JSON.stringify({jsonrpc:'2.0',id:req.id,result})+'\\n');});`);
    await mkdir(join(scratch, '.obsidian'));
    const definition = process.env.PI_MCP_TEST_FAILURE ? {command:'pi-gui-missing-mcp-fixture'} : process.env.PI_MCP_TEST_OBSIDIAN
        ? {command:'npx.cmd',args:['--offline','-y','obsidian-mcp@2','serve','--vault',`notes=${scratch}`]}
        : {command:process.execPath,args:[fixture]};
    await writeFile(join(scratch, 'mcp.json'), JSON.stringify({mcpServers:{fixture:definition, unrelated:{command:'must-never-run',lifecycle:'eager'}}}));
    const child=spawn(process.execPath,[process.env.PI_MCP_TEST_CLI,'--mode','rpc','--no-session','--no-extensions','--no-tools','--no-skills','--no-context-files','--no-prompt-templates','--extension',resolve('PiExtensions/mcp-management.ts')],{
        cwd:scratch,windowsHide:true,env:{...process.env,PI_OFFLINE:'1',PI_CODING_AGENT_DIR:scratch,PI_GUI_MCP_AGENT_DIR:scratch,PI_GUI_MCP_SERVER:'fixture'},stdio:['pipe','pipe','pipe']});
    let buffer='',stderr=''; const packets=[]; let wake;
    const exited=new Promise(done=>child.on('exit',done));
    child.stderr.on('data',data=>stderr=(stderr+data).slice(-8000));
    child.stdout.on('data',data=>{buffer+=data;let at;while((at=buffer.indexOf('\n'))>=0){packets.push(JSON.parse(buffer.slice(0,at)));buffer=buffer.slice(at+1);}wake?.();});
    async function until(predicate){const deadline=Date.now()+45000;while(Date.now()<deadline){const found=packets.find(predicate);if(found)return found;if(child.exitCode!==null)throw new Error(stderr);await new Promise(done=>{const timer=setTimeout(done,100);wake=()=>{clearTimeout(timer);done();};});}throw new Error('Timed out: '+stderr);}
    try {
        child.stdin.write(JSON.stringify({id:'commands',type:'get_commands'})+'\n');
        const commands=await until(p=>p.id==='commands');
        assert.ok(commands.data.commands.some(c=>c.name==='pi-gui-mcp-manage'),stderr);
        child.stdin.write(JSON.stringify({id:'check',type:'prompt',message:'/pi-gui-mcp-manage connect'})+'\n');
        const packet=await until(p=>p.method==='notify' && p.message?.includes('"kind":"result"'));
        const result=JSON.parse(packet.message);
        if (process.env.PI_MCP_TEST_FAILURE) {
            assert.equal(result.connected, false);
            assert.match(result.message, /ENOENT|not found|not recognized|spawn/i);
            return;
        }
        assert.equal(result.connected,true,JSON.stringify(result)+' '+stderr);
        if (process.env.PI_MCP_TEST_OBSIDIAN) assert.ok(result.toolCount > 0);
        else assert.equal(result.toolCount,1);
        assert.ok(!packets.some(p=>p.type==='agent_start'));
        assert.ok(!stderr.includes('must-never-run'));
    } finally {
        if (process.platform === 'win32' && child.exitCode === null) {
            await new Promise(done => spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], {windowsHide:true,stdio:'ignore'}).on('exit',done));
        } else child.kill();
        await exited;
        await rm(scratch,{recursive:true,force:true,maxRetries:5,retryDelay:200});
    }
});
