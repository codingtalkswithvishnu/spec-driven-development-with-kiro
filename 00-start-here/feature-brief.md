# Patient-consent withdrawal

Goal: a fictional patient withdraws an existing consent for one purpose.

Use C-001 owned by P-001 for research-demo, initially Granted at version 1.
Use P-002 to check rejection, and C-002 for another purpose that must not change.

- R1: only an authorized caller may change the selected consent; initially its owner.
- R2: accepted withdrawal changes Granted to Withdrawn.
- R3: unrelated consent records remain unchanged.
- R4: repeating a completed withdrawal creates no additional state transition.
- R5: the state change, required audit record, and pending-event record are saved together.
- R6: an unknown consent is not created by a withdrawal request.

The first checkpoint is in memory and uses a test identity. No real patient data,
external notifications, cloud deployment, or production authentication is in scope.
Open decisions: trusted caller identity in the API, persistent transaction boundary,
asynchronous delivery and recovery, audit access and retention, measured targets.
