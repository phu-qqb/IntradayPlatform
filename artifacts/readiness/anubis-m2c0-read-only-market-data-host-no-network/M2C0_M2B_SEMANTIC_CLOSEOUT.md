# M2C0 M2B Semantic Closeout

Status: PASS

M2C0 closes the M2B semantic gaps without creating a parallel pipeline.

- Parity is non-tautological: pre-record source payload hash, recorded envelope payload hash, and replayed payload hash are compared independently.
- Source event ids and source event sequences are consumed by the parity mapper.
- Derived events carry derived_event_id, derived_from_source_event_id, and derived_event_sequence.
- Nominal target mapping no longer emits TARGET_REVISED.
- True revision is represented by MapRevision with target_version=2 and supersedes_target_version=1.
- Temporal activation is pure and fail-closed before effective_from and after deadline.
- Shadow risk is explicitly non-authoritative; no silent RiskApproved/Approved is emitted.
- Current position fixture is nonzero and authoritative=false; drift is calculated from it instead of hard-coded zero.
- Sizing market snapshot and execution BBO are recorded for each target instrument.

Validation: targeted M2A/M2B/M2C0 suite passed 103/103.
