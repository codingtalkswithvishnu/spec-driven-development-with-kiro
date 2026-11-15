import { spawn } from 'node:child_process';
import readline from 'node:readline';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';

const child = spawn(process.execPath, [fileURLToPath(new URL('../03-mcp-context/server.mjs', import.meta.url))], { stdio: ['pipe', 'pipe', 'inherit'] });
const pending = new Map();
let nextId = 1;
const rejectPending = error => { for (const entry of pending.values()) entry.reject(error); pending.clear(); };
const deadline = setTimeout(() => { rejectPending(new Error('MCP smoke check timed out')); child.kill(); }, 5000);
child.on('error', rejectPending);
child.on('exit', () => rejectPending(new Error('MCP server exited before replying')));
readline.createInterface({ input: child.stdout }).on('line', line => {
  try {
    const response = JSON.parse(line);
    const entry = pending.get(response.id);
    assert.ok(entry, 'Unexpected JSON-RPC response');
    pending.delete(response.id);
    entry.resolve(response);
  } catch (error) { rejectPending(error); child.kill(); }
});
const frame = (id, text) => new Promise((resolve, reject) => {
  pending.set(id, { resolve, reject });
  child.stdin.write(text + '\n');
});
const call = (method, params) => {
  const id = nextId++;
  return frame(id, JSON.stringify({ jsonrpc: '2.0', id, method, params }));
};

try {
  const initialized = await call('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'check', version: '1' } });
  assert.equal(initialized.result.protocolVersion, '2025-06-18');
  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
  assert.equal((await call('tools/list')).result.tools.length, 2);
  const issue = await call('tools/call', { name: 'read_demo_issue', arguments: {} });
  assert.equal(JSON.parse(issue.result.content[0].text).key, 'DEMO-101');
  const design = await call('tools/call', { name: 'read_demo_design', arguments: {} });
  assert.equal(JSON.parse(design.result.content[0].text).page, 'consent-authorization');
  assert.equal((await call('tools/call', { name: 'not_a_tool', arguments: {} })).error.code, -32602);
  for (const args of [{ unexpected: true }, null, []])
    assert.equal((await call('tools/call', { name: 'read_demo_issue', arguments: args })).error.code, -32602);
  assert.equal((await frame(null, 'null')).error.code, -32600);
  assert.equal((await frame(null, 'not-json')).error.code, -32700);
  const exited = once(child, 'exit');
  child.stdin.end();
  const [code] = await exited;
  assert.equal(code, 0);
  console.log('PASS MCP handshake, both fixtures, unknown tools, argument validation, and invalid-request handling');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
  child.kill();
} finally { clearTimeout(deadline); }
