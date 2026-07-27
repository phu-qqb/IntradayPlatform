# ARCH7B Append-Only Global-Flat Position Import

## Scope

Contract `arch7b_bracketed_global_flat_position_import_v1` prepares a future,
separately authorized import of a fresh, bracketed LMAX Demo global-flat
position snapshot. It does not acquire broker data and does not create a
second PMS.

The July 27, 2026 P2 package is a historical structural fixture only. Its
result is always `HISTORICAL_FIXTURE_NOT_IMPORT_ELIGIBLE`; it can be planned
read-only but cannot be passed to `apply-import`.

## Persistence Forensic

The existing `pms_shadow` schema already supports the representation:

- `position_snapshots` is keyed by `position_snapshot_id`;
- `(ingestion_id, snapshot_sha256)` is the idempotency constraint;
- `position_snapshot_lines` is keyed by
  `(position_snapshot_id, instrument_id)`;
- the new snapshot references the existing source ingestion and its existing
  authoritative account snapshot;
- all 99 lines reference the new position snapshot;
- the consumer evidence SHA-256 is persisted as `snapshot_sha256`.

No migration is required. The import does not add or copy an ingestion,
account snapshot, ModelRun, TargetWeight, SecurityMapping, TargetPosition,
PositionOnlyDrift, Fill, or PositionLedgerEvent. Existing rows are never
updated or deleted.

## Lineage

The package reader verifies the consumer manifest and every declared file
hash. It then requires:

- 99 required PMS instruments;
- 99 normalized lines;
- 99 explicit zero quantities;
- zero unknown lines;
- exact source ingestion and source session bindings;
- exact required-universe and normalized-line-set SHA-256 bindings;
- broker send disabled and `no_order`, `no_fix`, `no_database_write`.

The consumer-generated account identity remains evidence metadata. The
database foreign key uses `required-pms-universe.json` field
`source_account_snapshot_id`, which identifies the existing PMS account
snapshot.

## Modes

`plan-import` starts a repeatable-read transaction, executes
`SET TRANSACTION READ ONLY`, verifies `SHOW transaction_read_only`, validates
the exact `ARCH7B_RDS_TEST` target, checks collisions and row deltas, and
rolls back. It emits:

- `append-only-import-contract.json`;
- `persistence-schema-forensic.json`;
- `import-plan.json`;
- `collision-check.json`;
- `expected-row-deltas.json`;
- `ready-marker-schema.json`;
- `dry-run-report.md`;
- `manifest.json`.

`apply-import` is present for a future packet but is fail-closed. It requires
all of:

- explicit `--apply` and `--no-order`;
- a non-historical package no more than 300 seconds old;
- a broker P2 timestamp not in the future and not before the PMS source;
- an atomically published prearmed marker;
- exact Core evidence, consumer evidence, universe and line-set hashes;
- exact snapshot, ingestion and account IDs;
- exact repository commit, target profile and target fingerprint;
- a non-empty future authorization ID and exclusive owner lock.

The 300-second limit reuses
`PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds`.

## Atomicity And Replay

The future apply path uses one serializable transaction and a transaction
advisory lock. A `NEW` candidate inserts one snapshot plus exactly 99 lines.
An `ALREADY_APPLIED_IDENTICAL` candidate writes zero rows. Any identity,
header, line, lineage, target, marker, or hash mismatch is a `CONFLICT` and
fails before commit. Any exception rolls the transaction back.

## Reporting

The persisted snapshot uses the existing tables consumed by the read-only
institutional reporting projection. This prepares position coverage without
changing reporting authority: working-order authority remains `INCONNU`.

This contract contains no LMAX acquisition, Account API, Databento, FIX
logon, broker send, order creation, Fill creation, PositionLedgerEvent
creation, real account access, or production mutation.
