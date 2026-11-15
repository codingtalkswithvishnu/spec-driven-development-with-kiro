# Reference design

The local Consent.Core boundary is defined in 00-start-here/contracts.md.
ConsentStore validates the actor and version, recognizes a completed withdrawal,
then commits the consent, audit, and event objects under one in-memory lock.
A pre-commit callback allows a failure check before any collection is changed.
This models the rule without claiming crash durability.

The cloud checkpoint is an adapter for the same rules. API Gateway validates
access tokens and the handler checks ownership. DynamoDB saves related items
transactionally. A stream processor publishes outbox events to EventBridge;
SQS buffers consumer work. A deduplication item and simulated view update are
transactional. Separate failure queues and a documented manual recovery path
support the learning exercise. A production reconciliation service, full domain
policy, and live-account validation are additional work.
