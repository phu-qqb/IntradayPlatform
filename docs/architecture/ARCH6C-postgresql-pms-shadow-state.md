# ARCH6C PostgreSQL PMS shadow state

## Scope and authority

Contract: `postgresql_pms_shadow_state_contract_v1`
Schema: `pms_shadow`
Migration: `20260721152240_InitialPostgreSqlPmsShadowState`

This contract is an evidence-only, non-accounting projection of the existing
ARCH5B/ARCH6A business pipeline. PostgreSQL is the intended future authority
for PMS shadow state, but ARCH6C neither connects to nor applies a database.
The authoritative business computations remain `ModelRun`, `TargetWeight`,
`TargetPosition`, `DriftSnapshot`, `ManualPaperCycle`, and R009 in the existing
Domain/Application projects.

The requested documents `QUBES-UPSTREAM-ENGINE-AND-DATA-CONTRACT-R001.md` and
`lead_review_m1b_anubis_intraday_oms_pms.md` are absent from the verified
Intraday master and from the searched local repositories. No untracked copy was
used as authority. This is the only documentary divergence found.

## Existing persistence inventory

The verified Intraday master contains one EF Core 10 persistence provider:
`QQ.Production.Intraday.Infrastructure.SqlServer`. Its `IntradayDbContext`
already maps the core domain objects, including `ModelRun`, `TargetWeight`,
`TargetPosition`, `DriftSnapshot`, and `MarketDataSnapshot`, as well as the
pre-existing execution/accounting model. Existing migrations target SQL Server,
use explicit keys and indexes, use decimal types, and apply `Restrict` to facts.

No PostgreSQL provider, Npgsql package, PostgreSQL PMS schema, outbox, or
PostgreSQL migration existed on master. The new provider is therefore a narrow
persistence projection beside the existing provider. It does not define a new
business model or a second lineage pipeline. It references Domain/Application
and consumes `Arch6aOperationalPositionShadowResult` directly.

## Canonical relational model

All 18 tables use the `pms_shadow` schema:

| Table | Purpose | Natural/idempotent key |
| --- | --- | --- |
| `ingestions` | Qualified source session envelope | unique `source_session_id` |
| `source_artifacts` | Content-addressed external references | ingestion + SHA |
| `qubes_input_snapshots` | Qubes input lineage | input SHA; ingestion + strategy |
| `account_snapshots` | Consumed test-account state | ingestion + snapshot SHA |
| `position_snapshots` | Consumed broker position state | ingestion + snapshot SHA |
| `position_snapshot_lines` | Observed current positions | snapshot + instrument |
| `market_data_snapshots` | Consumed operational quote set | ingestion + snapshot SHA |
| `market_data_observations` | Decision-effective quotes | snapshot + instrument |
| `security_mappings` | Security/LMAX identity map | ingestion + instrument |
| `working_leaves_observations` | Explicit unavailable state | unique ingestion |
| `model_runs` | Four fresh Qubes runs | stable model-run ID |
| `target_weights` | 288 consumed decision rows | model run + instrument |
| `target_position_stages` | Account/market sizing scope | unique model run |
| `target_positions` | 288 computed targets | stage + instrument |
| `position_only_drift_stages` | Position snapshot scope | unique model run |
| `position_only_drifts` | 288 computed deltas | stage + instrument |
| `broker_adjusted_drift_stages` | Four explicit blocked stages | unique model run |
| `cycle_results` | Four ManualPaperCycle/R009 results | ingestion + model run |

The principal lineage is represented by foreign keys. JSONB is limited to the
compact list of projection-leg SecurityIds on a consumed quote. No principal
ID, SHA, business key, or parent-child relationship is hidden in JSONB.

All foreign keys use `ON DELETE RESTRICT`. Uniqueness prevents duplicate
session ingestion, contradictory artifact identities, duplicate
ModelRun/SecurityId weights, and duplicate per-run stages. Check constraints
validate SHA-256 shape, test-account scope, no-order flags, blocked working
leaves, bid/ask validity, positive mapping increments, and zero exit codes.

## Numeric precision decision

The source types are .NET `decimal`; no value is converted to `double` in the
persistence projection.

| Values | Observed ARCH6B maximum absolute value | Required scale | PostgreSQL | Rounding policy |
| --- | ---: | ---: | --- | --- |
| Target weights, benchmark parameters | 0.194392 | 12 | `numeric(28,12)` | reject scale > 12 |
| Bid/ask | 426.19 (observed scale 28) | 28 | `numeric(38,28)` | reject scale > 28 |
| Mapping ratios/increments and notional | 366,169.26477384 | 12 | `numeric(28,12)` | reject scale > 12 |
| NAV | 1,883,664.27 | 8 | `numeric(28,8)` | reject scale > 8 |
| Base/venue quantities and drift | 18,411,585 / 18,411,423.05 | 8 | `numeric(28,8)` | reject scale > 8 |

`numeric(38,28)` permits 10 integral digits, `numeric(28,12)` permits
16 integral digits, and `numeric(28,8)` permits 20.
The planner validates both the integral envelope and decimal scale before any
future insert. Unit tests verify EF precision metadata, exact decimal parsing,
and fail-closed overflow. The retained headroom supports session comparison
without silently changing contractual values.

## Artifact retention boundary

PostgreSQL stores only IDs, complete SHA-256 values, logical URIs, sizes,
contract versions, timestamps, classifications, and decision-effective rows.
Raw LMAX capture, Polygon responses, Qubes input archives, complete
`AggregatedWeights.txt`, stdout/stderr, GPU telemetry, and evidence ZIPs remain
outside PostgreSQL by content-addressed reference. In particular, the 673,058
historical finite weight cells are not copied into PMS; only the 288 consumed
weights are projected.

## Immutability and transaction policy

Artifacts, snapshots, model runs, weights, targets, drifts, and cycle results
are append-only. `PmsShadowDbContext` rejects `Modified` and `Deleted` entries,
and exposes no generic update repository. ARCH6C inserts a completed ingestion
envelope in its dry-run plan. A future applied repository may implement the
separate transition `PENDING -> COMPLETED` or `PENDING -> FAILED`, but it must
not expose downstream facts as qualified before the completion transition.

The future apply transaction is one serializable unit in this order:

1. reserve ingestion/session identity;
2. insert source artifact references;
3. insert account, position, market, and working-leaves snapshots;
4. insert Qubes input snapshots;
5. insert model runs and the 288 target weights;
6. insert target-position stages and 288 targets;
7. insert drift stages, 288 position-only drifts, and four blocked stages;
8. insert four ManualPaperCycle/R009 results;
9. mark the ingestion complete and commit.

Any exception rolls back the entire transaction. ARCH6C only simulates this
boundary in memory and performs no database operation.

## Idempotency policy

Stable IDs are SHA-256-derived for database-only objects. Existing business IDs
and ARCH6B model-run IDs are preserved. The source session is unique, artifacts
are content-addressed, model-run output and input-snapshot content are checked,
and all per-run facts have composite natural keys.

- First application returns `Applied`.
- An identical retry returns `AlreadyAppliedIdentical` without new identity.
- Same source session with another evidence SHA fails closed.
- Same ModelRun ID with another output SHA fails closed.
- Same snapshot ID with another content SHA fails closed.
- An interruption before commit leaves no visible applied plan and can retry.
- Concurrent identical attempts serialize to one winner and identical readers.

## No-order policy

The PostgreSQL model contains no `TradeIntent`, executable risk decision,
parent/child order, execution report, fill, authoritative position ledger, FIX
order entry, or broker-route entity. Model-run and cycle-result checks require
`ExecutionAllowed=false`, `NotAnOrder=true`, `OrderEntryEnabled=false`, and
`TradeIntentCount=0`. Working leaves are persisted as unavailable, not empty,
not inferred, and non-authoritative; no fictitious open-order row is created.

## Migration dry run

The design-time factory calls parameterless `UseNpgsql` and contains no
connection string. EF Core generates Up, idempotent, and Down SQL from the model
without opening a connection. Up creates only `pms_shadow` objects plus EF's
migration-history row; it contains no existing-data DML and no destructive
operation. Down removes only the dedicated schema tables and its migration
history row. No PostgreSQL extension, privilege, owner, secret, absolute path,
or environment timestamp is embedded.

Database execution, PostgreSQL integration tests, migration apply, and rollback
execution belong exclusively to ARCH6D.
