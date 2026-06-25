# M2C0 Parity Spec

Parity row inputs are independent:

1. Source payload hash computed before record from mapper output.
2. Recorded payload hash read from the envelope snapshot.
3. Replayed payload hash computed from replayed payload JSON after ReplaySnapshotAsync validation.

A row is PASS only if all three hashes match.

The parity key includes source contract, event type, source_event_id, source_entity_id and symbol to avoid accidental source id reuse across incompatible event shapes.

Tamper behavior:

- Source tamper => parity FAIL and shadow gate NO_GO.
- Recorded payload/chunk tamper => replay FAIL before parity is trusted.
- Replay semantic mutation => deterministic replay hash changes.
