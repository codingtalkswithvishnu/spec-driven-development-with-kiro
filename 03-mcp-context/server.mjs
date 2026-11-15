import readline from 'node:readline';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
const issue=JSON.parse(fs.readFileSync(fileURLToPath(new URL('./fixtures/issue.json',import.meta.url)),'utf8'));
const note=JSON.parse(fs.readFileSync(fileURLToPath(new URL('./fixtures/design-note.json',import.meta.url)),'utf8'));
const versions=['2024-11-05','2025-03-26','2025-06-18'];
const tools=[{name:'read_demo_issue',description:'Read the fixed fictional consent issue.',inputSchema:{type:'object',properties:{},additionalProperties:false}},
 {name:'read_demo_design',description:'Read the fixed fictional consent design note.',inputSchema:{type:'object',properties:{},additionalProperties:false}}];
function handle(request){
 if(!request || typeof request!=='object' || Array.isArray(request) || request.jsonrpc!=='2.0' || typeof request.method!=='string' ||
    ('id' in request && typeof request.id!=='string' && typeof request.id!=='number'))
  return {jsonrpc:'2.0',id:null,error:{code:-32600,message:'Invalid JSON-RPC request'}};
 if(request.id===undefined)return null;
 let result;
 switch(request.method){
  case 'initialize': result={protocolVersion:versions.includes(request.params?.protocolVersion)?request.params.protocolVersion:'2025-06-18',capabilities:{tools:{}},serverInfo:{name:'consent-context-demo',version:'1.0.0'}};break;
  case 'ping':result={};break;
  case 'tools/list':result={tools};break;
  case 'tools/call': {
   const name=request.params?.name;
   if(!tools.some(t=>t.name===name))return {jsonrpc:'2.0',id:request.id,error:{code:-32602,message:'Unknown demo tool'}};
   const args=request.params?.arguments === undefined ? {} : request.params.arguments;
   if(args===null || typeof args!=='object' || Array.isArray(args) || Object.keys(args).length!==0)
    return {jsonrpc:'2.0',id:request.id,error:{code:-32602,message:'Demo tools accept an empty arguments object only'}};
   result={content:[{type:'text',text:JSON.stringify(name==='read_demo_issue'?issue:note)}]};break;
  }
  default:return {jsonrpc:'2.0',id:request.id,error:{code:-32601,message:'Method not found'}};
 }
 return {jsonrpc:'2.0',id:request.id,result};
}
readline.createInterface({input:process.stdin,crlfDelay:Infinity}).on('line',line=>{
 let request;
 try { request=JSON.parse(line); }
 catch { process.stdout.write(JSON.stringify({jsonrpc:'2.0',id:null,error:{code:-32700,message:'Invalid JSON'}})+'\n');return; }
 const response=handle(request);
 if(response)process.stdout.write(JSON.stringify(response)+'\n');
});
