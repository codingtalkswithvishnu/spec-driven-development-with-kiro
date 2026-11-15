import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
if(!process.argv[2]) throw new Error('Usage: node tools/create-practice.mjs <empty-practice-folder>');
const dest=path.resolve(process.argv[2]);
if(dest===path.parse(dest).root || dest===repo || dest.startsWith(repo+path.sep)) throw new Error('Choose a separate practice folder outside the reference repository.');
const allowed=new Set(['feature-brief.md','learning-notes.md']);
if(fs.existsSync(dest)&&fs.readdirSync(dest).some(name=>!allowed.has(name))) throw new Error('Choose an empty folder or one containing only the feature brief and learning notes. Existing code will not be overwritten.');
fs.mkdirSync(dest,{recursive:true});
const copy=(from,to)=>{const target=path.join(dest,to);if(to==='feature-brief.md'&&fs.existsSync(target))return;fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(repo,from),target);};
for(const file of ['src/Consent.Core/Consent.Core.csproj','src/Consent.Demo/Consent.Demo.csproj','src/Consent.Demo/Program.cs','tests/Consent.Checks/Consent.Checks.csproj','tests/Consent.Checks/Program.cs'])copy('01-local-consent/'+file,file);
copy('global.json','global.json');copy('00-start-here/feature-brief.md','feature-brief.md');copy('00-start-here/contracts.md','contracts.md');
const complete=fs.readFileSync(path.join(repo,'01-local-consent/src/Consent.Core/ConsentStore.cs'),'utf8');
const types=complete.slice(0,complete.indexOf('// A local teaching implementation.'));
const stub=`public sealed class ConsentStore
{
    public ConsentStore(IEnumerable<ConsentRecord> seed, Action? beforeCommit = null) { }
    public ConsentRecord? Find(string id) => throw new NotImplementedException();
    public AuditRecord[] Audit => throw new NotImplementedException();
    public ConsentEvent[] Outbox => throw new NotImplementedException();
    public WithdrawalResult Withdraw(string actor, string consentId, int expectedVersion, string requestId) => throw new NotImplementedException();
}
`;
fs.writeFileSync(path.join(dest,'src/Consent.Core/ConsentStore.cs'),types+stub);
fs.writeFileSync(path.join(dest,'.gitignore'),'**/bin/\n**/obj/\n.env\n');
console.log('Created a starter with unfinished implementation and independent acceptance checks: '+dest);
console.log('Open it in Kiro. Before implementation, the checks are expected to fail.');
