# Take the same consent feature to AWS

This completed cloud adapter and CDK definition extend the local example. They
are a development learning checkpoint, not a certified healthcare system. The
handlers compile and the template synthesizes locally. Live account tests remain
your next verification step; see [VALIDATION.md](../VALIDATION.md).

## Understand why these services are here

| Service | What it does here | Why this feature needs it |
| --- | --- | --- |
| API Gateway HTTP API | Exposes one protected withdrawal route | Gives the client a defined request boundary |
| Cognito | Supplies application-user access tokens | Establishes a caller identity; the handler still checks record ownership |
| Lambda | Runs the API, publisher, and consumer code | Keeps the lab focused on the request and event work rather than server management |
| DynamoDB | Stores consent, audit, outbox, and simulated consumer records | Supports conditional, all-or-nothing writes for the agreed state change |
| DynamoDB Streams | Starts processing newly created outbox records | Moves event publication out of the request path |
| EventBridge | Routes ConsentWithdrawn events | Separates the publisher from the downstream consumer |
| SQS | Buffers work and holds failed processing attempts | Makes retries and failure investigation visible |
| CloudWatch | Stores sanitized logs and evaluates alarms | Lets you inspect runtime errors and nonempty failure queues |
| IAM | Limits each function's AWS access | Separates API, publisher, and consumer permissions |
| CDK | Defines the resources in code | Makes the architecture and permissions reviewable before deployment |

See the official [serverless service guide](https://docs.aws.amazon.com/decision-guides/latest/decision-guides/choosing-aws-serverless-service.html).
The design uses an [outbox](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html):
the database transaction saves the change, audit, and pending work together;
publication happens afterward. Delivery can repeat, so the simulated consumer
handles duplicates and stale versions. This does not stop every real downstream
system instantly.

## Prepare

Use .NET 10, a CDK-supported Node.js version, AWS CLI v2, and an authorized
development account and Region. Authenticate using your organization's approved
temporary-credential process. Verify the intended account with
`aws sts get-caller-identity`. Do not print or commit credentials. Follow the
[AWS CLI setup guide](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-quickstart.html).

The CDK bootstrap and deployment require an appropriately authorized deployment
identity. Get that setup from your account administrator; this example does not
grant itself administrator access. Application sign-in uses different identities.

## Build without deploying

From this folder:

```text
dotnet publish function/Consent.Cloud.csproj -c Release -o function/publish --no-self-contained
cd infra
npm ci --ignore-scripts
npm run synth
```

Expected result: a CloudFormation template under cdk.out with a DynamoDB table,
three .NET 10 Lambda functions, four SQS queues, an event bus, a Cognito pool,
and a JWT-protected API route. Synthesis is a structural check, not a live test.

On Windows, use `npm.cmd` with the same arguments if PowerShell blocks the
`npm.ps1` wrapper. The PowerShell helpers below require a shell policy that
permits reviewed local scripts; follow your organization's policy. See
[the known dependency finding](../VALIDATION.md#known-dependency-finding)
before using the infrastructure toolchain.

## Review and deploy deliberately

Still in infra, inspect the template and your deployment identity. If bootstrap
is needed, use the [official CDK bootstrap guidance](https://docs.aws.amazon.com/cdk/v2/guide/bootstrapping.html)
for your account and Region. Bootstrap resources can remain after lab cleanup.

```text
npm run diff
npm run deploy -- --outputs-file outputs.json
```

Review the proposed IAM and resource changes before accepting deployment. The
stream integration includes `dynamodb:ListStreams` on Resource `*`, because that
discovery action has no resource type in the official
[service authorization reference](https://docs.aws.amazon.com/service-authorization/latest/reference/list_dynamodb.html).
This narrow discovery exception does not authorize reading every table or stream.
Other permissions need their own resource and condition review. The
template intentionally uses DESTROY policies for disposable fictional lab data.
Do not apply that policy to records with a real retention obligation.

## Exercise the protected route

The helper commands use PowerShell and AWS CLI v2. Return to this folder from
infra with `cd ..`. Create two fictional users, choosing unique lab passwords
when prompted. Account creation requires the deployment administrator's Cognito
permissions. Skip a creation command if that user already exists.

```powershell
.\scripts\New-DemoUser.ps1 -Username p001
.\scripts\New-DemoUser.ps1 -Username p002
.\scripts\Invoke-DemoRequest.ps1 -Username p001 -ConsentId C-001 -Seed -ExpectedStatus 200
.\scripts\Invoke-DemoRequest.ps1 -Username p002 -ConsentId C-001 -ExpectedStatus 404
.\scripts\Invoke-DemoRequest.ps1 -Username p001 -ConsentId C-001 -ExpectedStatus 200
```

The first accepted call should report Withdrawn and version 2. The repeated call
reports AlreadyWithdrawn. Another owner receives 404, the same external response
as an unknown record. Seeding creates a missing fixture only; it does not reset
an existing consent or erase history. Use a new consent ID for another transition.

The helpers write short-lived CLI input files to the operating system's temp
directory and remove them in finally blocks. Do not run them with production
credentials or patient data. Tokens are kept in the script process and are not
printed. A local machine administrator may still inspect process or file data;
these scripts are a lab convenience, not a secret-management product.

Use DynamoDB's console or CLI to inspect these keys for C-001:

- `CONSENT#C-001` / `STATE`: Withdrawn, version 2.
- `AUDIT#C-001` / `VERSION#2`: the accepted transition.
- `OUTBOX#C-001:2` / `EVENT`: Pending, then Sent after publication.
- `VIEW#C-001` / `STATE`: the simulated consumer's eventual Withdrawn result.

The source record is the authority for the committed change. Sent only means the
bus accepted publication. It does not prove the consumer completed its update.

## Investigate a delivery failure

Inspect the publisher and consumer log groups and the three failure queues from
the outputs. A consumer dead-letter queue does not cover stream or bus-target
failures. Alarms are created but no email/SNS action is configured: inspect their
state in CloudWatch or deliberately add an action after review.

For a controlled development test, temporarily disable the publisher's stream
event-source mapping, withdraw a new fictional consent, and confirm its outbox
remains Pending. Re-enable the mapping promptly and observe recovery. Record
the mapping ID and original enabled state before changing it.

Stream retention is finite. The durable Pending record remains the basis for
manual recovery if processing did not finish in that window. The publisher can
be invoked administratively after you inspect the record. From this folder:

```powershell
.\scripts\Replay-Outbox.ps1 -ConsentId C-001 -Version 2
```

This requires DynamoDB read and Lambda invoke permissions. The helper reads the
existing record and passes an INSERT-shaped notification to the publisher, which
retrieves its stored detail. It refuses a missing or unexpected record and skips
an already Sent record. It checks function and batch failures; then you must
inspect the outbox and eventual consumer view. The helper has been parsed
locally; live replay is a pending account check. Production use needs a reviewed reconciliation job and retention
policy; this checkpoint does not provide an unattended recovery service.

## Costs and cleanup

Budget for function invocations, database operations and recovery settings,
logs, queues, events, Cognito use, and any bootstrap storage. No free-tier promise
is made. Consult each service's official pricing before deployment.

After collecting your results, return to infra and run `npm run destroy`. Review
the stack name before confirming. This lab deletes its fictional records and
user pool. Verify the stack, log groups, queues, and other resources are gone.
Check retained backups and CDK bootstrap resources separately; destroy does not
mean every account resource or charge has been removed.

For runtime or token errors, check the current [Lambda runtime table](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html)
and [API Gateway JWT authorizer guide](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-jwt-authorizer.html).
