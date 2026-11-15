# Check the same application at different boundaries

From the repository root:

```text
dotnet run --project 01-local-consent/tests/Consent.Checks
dotnet run --project 06-testing-and-review/Cloud.Checks
node tools/check-mcp.mjs
```

The first command checks local behavior, including failure and concurrency cases.
The second reports 32 cloud adapter checks. It checks API input and ownership
decisions, transactional request construction, publisher retries, and consumer
duplicate/stale-event and partial-batch handling with fake SDK clients; it makes
no AWS calls. The third checks the fixture server's JSON-RPC handshake, both
documents, and rejected invalid requests. They verify different boundaries.

For cloud deployment, follow folder 05 and collect separate evidence. A compiled
handler and synthesized template cannot verify Cognito permissions, deployed
transactions, IAM effectiveness, event recovery, or costs in your account.

For an independent review, provide the accepted spec, relevant diff, and check
results. Ask the reviewer to identify missing evidence and likely failure cases.
Review changes to assertions separately from changes to the implementation.
Record cloud checks not executed as pending rather than complete.
