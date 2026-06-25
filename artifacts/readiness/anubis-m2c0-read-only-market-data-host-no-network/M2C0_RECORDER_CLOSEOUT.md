# M2C0 Recorder Closeout

Status: PASS

Recorder V2 changes:

- Added event types SIZING_MARKET_SNAPSHOT_OBSERVED, EXECUTION_BBO_OBSERVED, TARGET_EXPIRED, SHADOW_RISK_EVALUATION_OBSERVED.
- Added derived identity, target version, supersession, sizing snapshot id, and execution BBO id fields to event and envelope contracts.
- Replay now exposes CanonicalRecorderV2ReplaySnapshot with ReplayReport, Events, and InputFileHashes.
- ReadEventsAsync returns the already-validated replay snapshot events; it does not re-read chunk files after validation.
- Deterministic replay hash includes environment, event id, source and derived identities, timestamps, market prices/sizes, session/sequence data, payload hash, and lineage fields.
- Final manifest, run manifest, DQ report, chunk metadata, schema, event ids, sequence contiguity, payload hashes, dimensions, and counts are validated during replay.
- Data quality report now computes manifest_validation_status, run_integrity_status, recorder_health_status, and shadow_readiness_status from actual recorder state.

Volatile metadata intentionally excluded from deterministic replay hash: filesystem path spelling and file modification timestamps. File contents are covered by InputFileHashes and manifest/chunk hashes.
