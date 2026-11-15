# Reference requirements

- R1: A caller may withdraw only a consent they are authorized to change; this example uses the owner.
- R2: An accepted withdrawal changes the selected Granted consent to Withdrawn.
- R3: Consent records for other purposes remain unchanged.
- R4: Repeating a completed withdrawal creates no additional state transition.
- R5: The state change, required audit entry, and pending notification are saved together.
- R6: A withdrawal request for an unknown consent creates no consent record.

The local checkpoint uses a test identity and memory. The cloud adapter uses
the validated application subject and a database transaction. An external API
may hide record existence with a common NotFound response. Event delivery is a
separate asynchronous milestone and may repeat. Use fictional data only.
