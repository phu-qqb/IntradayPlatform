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

The package inventory is closed: every top-level entry except `manifest.json`
must be declared, undeclared entries and reparse points are rejected, and each
declared byte stream is checked against its SHA-256 and optional declared
size. `normalized-position-lines.csv` is parsed as 19 typed columns and must
equal `snapshot.Lines` record for record.

## Runtime Selection

Contract `arch7b_position_snapshot_for_slot_selection_v1` replaces the former
single-row assumption. `LoadSourceAsync` receives `SlotStartUtc`, selects the
unique snapshot having the maximum `AsOfUtc <= SlotStartUtc`, and never falls
back if that latest candidate is invalid. A tie, future-only set, missing set,
or age above 300 seconds fails closed.

The selected snapshot must be broker authoritative, complete, explicit,
non-reconstructed, SHA-valid, classified exactly as
`LMAX_PORTAL_BRACKETED_CURRENT_GLOBAL_FLAT_V1`, and contain 99 distinct lines
matching the ingestion's 99 security mappings. The selected classification,
not `AccountSnapshot.Authority`, becomes `PmsShadowEconomicSource` and
economic-revision `PositionAuthority`.

## Modes

`arm-import` runs before broker acquisition. It resolves the real clean Git
root/HEAD/build commit, validates the exact TEST target, writer privileges,
latest completed source ingestion and PostgreSQL UTC time, creates the
exclusive owner file, and atomically publishes `importer.armed.json`.

`publish-ready` runs after package generation. It revalidates the package and
database source read-only, checks the same owner, authorization, repository,
target and ingestion, reads PostgreSQL time, and atomically publishes a marker
binding both armed evidence SHA-256 and package-manifest SHA-256.

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
- atomically published armed state, owner lease and ready marker;
- exact Core evidence, consumer evidence, universe and line-set hashes;
- exact package-manifest and armed-evidence hashes;
- exact snapshot, ingestion and account IDs;
- clean real Git root with `HEAD == BuildCommit == armed == ready`;
- exact target profile/fingerprint and explicit authorization/owner CLI values;
- chronology `armed <= bracket lower <= P2 <= ready <= apply`;
- PostgreSQL `clock_timestamp()` in UTC as the freshness authority;
- latest ingestion, four ModelRuns, four Qubes inputs, 288 TargetWeights,
  99 mappings, model counts and required-universe SHA rebuilt from the database;
- a principal having source SELECT and INSERT only on the two append tables,
  with no UPDATE, DELETE, order, Fill, ledger, target or drift INSERT.

The 300-second limit reuses
`PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds`.

## Atomicity And Replay

The future apply path uses one serializable transaction and a transaction
advisory lock derived from contract, target, account and source ingestion. A
`NEW` candidate inserts one snapshot plus exactly 99 lines.
An `ALREADY_APPLIED_IDENTICAL` candidate writes zero rows. Any identity,
header, line, lineage, target, marker, or hash mismatch is a `CONFLICT` and
fails before commit. Any exception rolls the transaction back.

Before commit, the importer reads back the header and all 99 lines and checks
their IDs, quantities, evidence SHA, authority and normalized line-set binding.
After commit it opens a separate repeatable-read, read-only transaction and
repeats the readback while proving that ingestion, account, model, weight,
mapping, target, drift and ARCH7B lifecycle counts did not change. The only
permitted delta is one `position_snapshots` row plus 99
`position_snapshot_lines` rows.

## Reporting

The persisted snapshot uses the existing tables consumed by the read-only
institutional reporting projection. This prepares position coverage without
changing reporting authority: working-order authority remains `INCONNU`.

This contract contains no LMAX acquisition, Account API, Databento, FIX
logon, broker send, order creation, Fill creation, PositionLedgerEvent
creation, real account access, or production mutation.
