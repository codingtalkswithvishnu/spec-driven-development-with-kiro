import fs from 'node:fs';
import { execFileSync } from 'node:child_process';
const allowed=new Set(['00-start-here','01-local-consent','02-project-context','03-mcp-context','04-git-workflow','05-aws-consent','06-testing-and-review','.github','.kiro','tools','README.md','PROJECT_OVERVIEW.md','VALIDATION.md','global.json','.gitignore','.gitattributes']);
const names=[...new Set(execFileSync('git',['ls-files','--cached','--others','--exclude-standard','-z'],{encoding:'utf8'}).split('\0').filter(Boolean))];
for(const name of names){
 if(!allowed.has(name.split('/')[0]))throw Error('Unexpected file outside the examples boundary: '+name);
 if(/\.(epub|pdf|jpg|png)$/i.test(name)||/Amazon_Kiro|front-matter|EDITORIAL_REVIEW|ARCHITECTURE_REVIEW|manuscript|archive\//i.test(name))throw Error('Book or private artifact cannot be uploaded: '+name);
 const text=fs.readFileSync(name,'utf8');
 if(/AKIA[A-Z0-9]{16}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----/.test(text))throw Error('Possible credential material: '+name);
}
console.log('PASS examples-only boundary and basic credential-pattern check: '+names.length+' files');
