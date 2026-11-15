# What has been checked

Checked on 15 November 2026. These results apply to this companion revision.
They describe local execution, not a live AWS deployment or healthcare certification.

## Executed results

| Check | Result | What it establishes |
| --- | --- | --- |
| Local consent suite | 12/12 passed | Selected ownership, state, repeat, failure, concurrency, and record-consistency rules in the in-memory model |
| Cloud adapter suite | 32/32 passed, no AWS calls | API decisions and transaction construction; publisher failure handling; consumer duplicate, stale-event, and partial-batch decisions with fake SDK clients |
| MCP protocol check | Passed | Initialization handshake, both fixed documents, rejected unknown tools/arguments, and invalid JSON/request responses |
| .NET Release publish | Passed | The three cloud handlers compile into the deployment artifact |
| CDK clean install and synthesis | Passed | Locked dependencies install and produce a CloudFormation template without account lookups |
| Template assertions | Passed | Runtime, JWT-protected route, queues, partial-batch responses, and selected IAM constraints are present |
| PowerShell helper parsing | Passed | Helper syntax is accepted; this does not exercise AWS CLI or a deployed API |
| Starter exercise | Checked | The starter compiles, initially fails its unfinished behavior checks, preserves existing brief/notes, and refuses an existing code destination |
| Repository boundary | Passed | Only permitted companion files are tracked; basic credential-pattern scan passed |

Local tools: .NET SDK 10.0.404 and Node.js 26.13.0 on Windows. The workflow
uses .NET 10 and Node.js 24 on Linux; its status is separate evidence, not
implied by these Windows results. Infrastructure dependencies are locked;
the library is aws-cdk-lib 2.277.0 and the CDK CLI is 2.1148.1.

## Reproduce the checks

From the repository root:

```text
dotnet run --project 01-local-consent/tests/Consent.Checks
dotnet run --project 06-testing-and-review/Cloud.Checks
node tools/check-mcp.mjs
dotnet publish 05-aws-consent/function/Consent.Cloud.csproj -c Release -o 05-aws-consent/function/publish --no-self-contained
node tools/check-repo.mjs
```

Then, from `05-aws-consent/infra`:

```text
npm ci --ignore-scripts
npm run synth -- --quiet
node check-template.mjs
npm audit
```

Stop and investigate a failed command. On Windows, use `npm.cmd` if PowerShell
blocks the `npm.ps1` wrapper; the arguments remain the same.

## Known dependency finding

The npm audit reports **two moderate findings**: bundled `minimatch` and the
parent `aws-cdk-lib` dependency path, from
[GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249).
The report is not clean. This dependency is in the local infrastructure toolchain,
not the published .NET handler artifact. The example uses a fixed local asset
directory; that does not establish that every possible CDK use is unaffected.

Do not apply the suggested older CDK baseline blindly: it introduced additional
findings during review. Review an upstream fix, update the lockfile, and rerun
audit and synthesis before treating the issue as resolved. Audit data changes;
read your current report. The NuGet vulnerability scan reported no vulnerable
packages for the cloud project at the review date; this is not a security guarantee.

## Checks still required in the target environment

- Observe the hook firing and the MCP server connecting in a signed-in Kiro client.
- Deploy in an authorized development account and verify effective IAM permissions.
- Exercise valid, wrong-owner, invalid/expired-token, unknown-record, repeated,
  and conflicting requests against the protected route.
- Inspect committed consent, audit, and outbox records after accepted changes
  and confirm rejected changes leave no partial state.
- Observe the complete stream-to-bus-to-queue-to-consumer path, delayed delivery,
  duplicates, obsolete versions, failure destinations, and recovery.
- Run the PowerShell sign-in, fixture, request, and administrative replay helpers
  against that deployment. Their live behavior has not been verified here.
- Validate monitoring actions, backup restoration, measured load/cost, retention,
  and resource cleanup for the intended environment.

No AWS resources were deployed during this review. Offline fake-client checks
do not execute DynamoDB expressions, transactions, JWT verification, or IAM.
The runtime path still needs live evidence even when its template synthesizes.
