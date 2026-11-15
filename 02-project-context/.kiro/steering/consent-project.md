---
inclusion: always
---

# Project context

Use C# and .NET 10. Work on the fictional patient-consent application only.
Read feature-brief.md and the current spec before changing behavior.
Preserve the public contract and accepted checks. Explain a requirement change
before changing the expected result. Keep credentials and real patient data out.
From the reference repository root, verify with:
`dotnet run --project 01-local-consent/tests/Consent.Checks`.
From a generated practice workspace, use:
`dotnet run --project tests/Consent.Checks`.
Choose the command that matches the current root; do not guess the folder.
Report changed files, executed checks, failures, and work not verified.
Cloud deployment, external writes, and changed permissions need explicit scope.
