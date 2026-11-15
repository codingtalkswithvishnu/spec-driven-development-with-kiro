import fs from 'node:fs';
import assert from 'node:assert/strict';
const t=JSON.parse(fs.readFileSync('cdk.out/ConsentWorkshop.template.json','utf8'));
const resources=Object.values(t.Resources);
const type=name=>resources.filter(x=>x.Type===name);
assert.equal(type('AWS::Lambda::Function').length,3);
assert.ok(type('AWS::Lambda::Function').every(x=>x.Properties.Runtime==='dotnet10'));
assert.equal(type('AWS::SQS::Queue').length,4);
assert.equal(type('AWS::DynamoDB::Table').length,1);
assert.equal(type('AWS::ApiGatewayV2::Authorizer')[0].Properties.AuthorizerType,'JWT');
assert.equal(type('AWS::ApiGatewayV2::Route')[0].Properties.AuthorizationType,'JWT');
assert.ok(type('AWS::ApiGatewayV2::Route')[0].Properties.AuthorizationScopes.length>0);
assert.ok(type('AWS::Lambda::EventSourceMapping').every(x=>x.Properties.FunctionResponseTypes?.includes('ReportBatchItemFailures')));
for(const p of type('AWS::IAM::Policy'))for(const statement of p.Properties.PolicyDocument.Statement){
 assert.ok(![statement.Action].flat().includes('*'),'No universal IAM action');
 if([statement.Resource].flat().includes('*'))assert.deepEqual([statement.Action].flat(),['dynamodb:ListStreams'],'Only the documented stream-discovery exception may use Resource *');
}
console.log('PASS template runtime, protected route, queues, partial-batch responses, and IAM scope (documented ListStreams exception)');
