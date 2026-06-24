# M1C Test Matrix

Implemented unit coverage:

- valid bundle -> `EvidenceOnly`
- explicit model run -> `CanonicalLinked`
- missing manifest
- hash mismatch
- unknown instrument
- unknown quantity unit
- missing environment
- missing broker account
- exact duplicate idempotence
- duplicate conflict rejection
- fill without child
- fill without ExecID
- partial fill then final fill
- inconsistent cum/leaves
- cancel pending without terminal
- out-of-order events
- same ClOrdID across environments
- manual UI cannot create fill
- EOD report fill evidence
- offline catalog unique match
- no ModelRun -> `EvidenceOnly`
- ambiguous catalog -> no arbitrary selection
- missing deadline remains missing
- deterministic rerun hash
- CLI guardrails
- no DB/network/gateway/R009 source boundary
