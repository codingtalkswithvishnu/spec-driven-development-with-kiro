import * as cdk from 'aws-cdk-lib';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import * as events from 'aws-cdk-lib/aws-events';
import * as targets from 'aws-cdk-lib/aws-events-targets';
import * as sources from 'aws-cdk-lib/aws-lambda-event-sources';
import * as cognito from 'aws-cdk-lib/aws-cognito';
import * as apigw from 'aws-cdk-lib/aws-apigatewayv2';
import { HttpLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { HttpJwtAuthorizer } from 'aws-cdk-lib/aws-apigatewayv2-authorizers';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as cloudwatch from 'aws-cdk-lib/aws-cloudwatch';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const app = new cdk.App();
const stack = new cdk.Stack(app,'ConsentWorkshop');
cdk.Tags.of(stack).add('Project','consent-workshop');
const table = new dynamodb.Table(stack,'Records',{
  partitionKey:{name:'PK',type:dynamodb.AttributeType.STRING},
  sortKey:{name:'SK',type:dynamodb.AttributeType.STRING},
  billingMode:dynamodb.BillingMode.PAY_PER_REQUEST,
  encryption:dynamodb.TableEncryption.AWS_MANAGED,
  pointInTimeRecoverySpecification:{pointInTimeRecoveryEnabled:true},
  stream:dynamodb.StreamViewType.NEW_IMAGE,
  removalPolicy:cdk.RemovalPolicy.DESTROY // disposable fictional lab data only
});
const bus=new events.EventBus(stack,'Bus');
const consumerDlq=new sqs.Queue(stack,'ConsumerFailures',{enforceSSL:true,retentionPeriod:cdk.Duration.days(14)});
const work=new sqs.Queue(stack,'Work',{enforceSSL:true,visibilityTimeout:cdk.Duration.seconds(180),deadLetterQueue:{queue:consumerDlq,maxReceiveCount:3}});
const streamDlq=new sqs.Queue(stack,'StreamFailures',{enforceSSL:true,retentionPeriod:cdk.Duration.days(14)});
const busDlq=new sqs.Queue(stack,'BusFailures',{enforceSSL:true,retentionPeriod:cdk.Duration.days(14)});
const asset=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../function/publish');
function fn(id,handler,extra={}) {
  const logGroup=new logs.LogGroup(stack,id+'Logs',{retention:logs.RetentionDays.ONE_WEEK,removalPolicy:cdk.RemovalPolicy.DESTROY});
  return new lambda.Function(stack,id,{runtime:new lambda.Runtime('dotnet10',lambda.RuntimeFamily.DOTNET),
    architecture:lambda.Architecture.X86_64,handler:'Consent.Cloud::Consent.Cloud.'+handler+'::Handle',
    code:lambda.Code.fromAsset(asset),memorySize:512,timeout:cdk.Duration.seconds(30),logGroup,
    environment:{TABLE_NAME:table.tableName,...extra}});
}
const apiFn=fn('Api','ApiFunction');
const publisher=fn('Publisher','PublisherFunction',{BUS_NAME:bus.eventBusName});
const consumer=fn('Consumer','ConsumerFunction');
apiFn.addToRolePolicy(new iam.PolicyStatement({actions:['dynamodb:GetItem','dynamodb:UpdateItem'],resources:[table.tableArn],conditions:{'ForAllValues:StringLike':{'dynamodb:LeadingKeys':['CONSENT#*']}}}));
apiFn.addToRolePolicy(new iam.PolicyStatement({actions:['dynamodb:PutItem'],resources:[table.tableArn],conditions:{'ForAllValues:StringLike':{'dynamodb:LeadingKeys':['AUDIT#*','OUTBOX#*']},StringEquals:{'dynamodb:EnclosingOperation':'TransactWriteItems'}}}));
publisher.addToRolePolicy(new iam.PolicyStatement({actions:['dynamodb:GetItem','dynamodb:UpdateItem'],resources:[table.tableArn],conditions:{'ForAllValues:StringLike':{'dynamodb:LeadingKeys':['OUTBOX#*']}}}));
consumer.addToRolePolicy(new iam.PolicyStatement({actions:['dynamodb:PutItem','dynamodb:UpdateItem'],resources:[table.tableArn],conditions:{'ForAllValues:StringLike':{'dynamodb:LeadingKeys':['PROCESSED#*','VIEW#*']}}}));
publisher.addEventSource(new sources.DynamoEventSource(table,{startingPosition:lambda.StartingPosition.TRIM_HORIZON,
  batchSize:10,retryAttempts:3,bisectBatchOnError:true,reportBatchItemFailures:true,onFailure:new sources.SqsDlq(streamDlq)}));
bus.grantPutEventsTo(publisher);
const rule=new events.Rule(stack,'ConsentEvents',{eventBus:bus,eventPattern:{source:['consent.workshop'],detailType:['ConsentWithdrawn']}});
rule.addTarget(new targets.SqsQueue(work,{deadLetterQueue:busDlq,retryAttempts:3}));
consumer.addEventSource(new sources.SqsEventSource(work,{batchSize:10,reportBatchItemFailures:true}));
const pool=new cognito.UserPool(stack,'Patients',{selfSignUpEnabled:false,signInAliases:{username:true},
  passwordPolicy:{minLength:14,requireDigits:true,requireLowercase:true,requireUppercase:true,requireSymbols:true},
  removalPolicy:cdk.RemovalPolicy.DESTROY});
const client=pool.addClient('PracticeClient',{generateSecret:false,authFlows:{userPassword:true},preventUserExistenceErrors:true});
const api=new apigw.HttpApi(stack,'HttpApi',{createDefaultStage:true});
const issuer=cdk.Fn.join('', ['https://cognito-idp.',stack.region,'.',stack.urlSuffix,'/',pool.userPoolId]);
const auth=new HttpJwtAuthorizer('PatientTokens',issuer,{jwtAudience:[client.userPoolClientId]});
api.addRoutes({path:'/consents/{id}/withdraw',methods:[apigw.HttpMethod.POST],
  integration:new HttpLambdaIntegration('Withdrawal',apiFn),authorizer:auth,authorizationScopes:['aws.cognito.signin.user.admin']});
for(const [id,q] of [['Consumer',consumerDlq],['Stream',streamDlq],['Bus',busDlq]]){
  new cloudwatch.Alarm(stack,id+'FailureAlarm',{metric:q.metricApproximateNumberOfMessagesVisible(),threshold:1,evaluationPeriods:1,treatMissingData:cloudwatch.TreatMissingData.NOT_BREACHING});
}
new cloudwatch.Alarm(stack,'ApiErrorAlarm',{metric:apiFn.metricErrors(),threshold:1,evaluationPeriods:1,treatMissingData:cloudwatch.TreatMissingData.NOT_BREACHING});
for(const [name,value] of Object.entries({ApiUrl:api.apiEndpoint,TableName:table.tableName,UserPoolId:pool.userPoolId,
  ClientId:client.userPoolClientId,ConsumerQueueUrl:work.queueUrl,ConsumerDlqUrl:consumerDlq.queueUrl,StreamDlqUrl:streamDlq.queueUrl,
  BusDlqUrl:busDlq.queueUrl,PublisherName:publisher.functionName,ConsumerName:consumer.functionName,ApiFunctionName:apiFn.functionName})){
  new cdk.CfnOutput(stack,name,{value});
}
app.synth();
