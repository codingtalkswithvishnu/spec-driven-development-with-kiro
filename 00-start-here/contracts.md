# Stable boundary, flexible implementation

Generated private code can differ. Keep the public types and members used by the
acceptance checks so every reader can run the same behavior checks.

- Namespace: `Consent.Core`.
- Records: `ConsentRecord`, `AuditRecord`, `ConsentEvent`, `WithdrawalResult`.
- Store constructor: `ConsentStore(IEnumerable<ConsentRecord> seed, Action? beforeCommit = null)`.
- Methods: `Find(string id)` and `Withdraw(string actor, string consentId, int expectedVersion, string requestId)`.
- Read-only snapshots: `Audit` and `Outbox` arrays.
- Status values: `Granted`, `Withdrawn`.
- Result codes: `Unauthenticated`, `InvalidRequest`, `NotFound`, `Forbidden`,
  `Conflict`, `AlreadyWithdrawn`, `Withdrawn`.

The generated starter contains the exact type definitions. The checks exercise
observable behavior, including a failure callback before committing the change.
Do not remove a check to accommodate an incorrect implementation. A justified
contract or rule change needs review and an updated check, not a silent bypass.
