# One application throughout

## The user need

A fictional patient can withdraw permission to use their data for a stated
purpose. The application records that decision and, in a later stage, notifies
a simulated authorized consumer. It does not send messages to real healthcare
organizations or use real patient data.

## Shared vocabulary

| Term | Meaning |
| --- | --- |
| Patient | An application user; P-001 and P-002 are fictional labels |
| Consent | Permission for one defined purpose; C-001 is the main example |
| Purpose | Why data use was allowed; research-demo and care-demo are fictional examples |
| Status | Granted or Withdrawn in this limited model |
| Version | A number used to detect a conflicting or obsolete change |
| Audit record | A record of the accepted business change for an authorized reviewer |
| Outbox record | Notification work saved with the state change, to publish asynchronously |

The local exercise supplies a trusted test identity. In the cloud checkpoint,
the owner field is a Cognito subject identifier, not the P-001 label entered in
a request. P-001 is the human-readable alias used in the teaching scenario.

## Follow one request

1. Identify the caller and selected consent.
2. Check authorization and the current version/state.
3. Save the accepted change, required audit record, and pending event together.
4. Return the source-of-truth result.
5. Publish pending notification work and process it safely after retries.
6. Observe failures and recover work without inventing another consent change.

The local reference models the first steps in memory. The cloud adapter uses
DynamoDB transactions, an outbox processor, EventBridge, SQS, and a simulated
consumer. The numbered folders are exercises around this same application.

## Keep the scope honest

This is a learning reference, not a complete clinical consent product. Delegated
consent, guardianship, clinical emergency access, retention policy, geographic
restrictions, multi-tenant isolation, and enforcement in real downstream systems
need additional requirements. A notification does not instantly stop all data
processing. A successful local check does not certify security or compliance.
