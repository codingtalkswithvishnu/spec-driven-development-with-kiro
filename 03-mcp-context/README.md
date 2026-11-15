# Read business context through MCP

This read-only server supplies fictional Jira-like and Confluence-like documents
for the same consent feature. It does not connect to a real Atlassian account.
Node.js is the only runtime dependency.

From the repository root, run `node tools/check-mcp.mjs` for the protocol smoke
check. To use Kiro, merge the consent-demo entry from mcp.example.json into your
workspace `.kiro/settings/mcp.json`; do not overwrite existing entries. Open the
repository root so the supplied relative server path resolves correctly. For
another workspace, replace args with the absolute path to server.mjs.

Reconnect the server through Kiro's MCP interface. Confirm that it exposes
read_demo_issue and read_demo_design. Ask it to retrieve both and compare their
criteria with the consent spec. Expected sources are the two fixed fictional
fixtures, with an unresolved audit-retention question preserved as a question.
No write tool is exposed.

For real Jira and Confluence Cloud, follow the official
[Atlassian Rovo MCP guide](https://developer.atlassian.com/cloud/rovo-mcp/) and
[Kiro configuration guide](https://kiro.dev/docs/mcp/configuration/). The real
connection requires authorization and organization permissions. Retrieval does
not make every document authoritative or keep local specs automatically synced.

Remove the demo entry to disconnect. Do not publish tokens or copied internal
pages. A page is reference data, not authority to expand tool permissions.
