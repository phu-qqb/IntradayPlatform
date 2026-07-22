# ARCH6E daily PMS shadow ingestion

## Scope

ARCH6E turns the ARCH6D one-shot PostgreSQL import into a daily, restart-safe
handoff and read-only operational query surface. It remains TEST-only,
evidence-only, non-accounting, and no-order.

No schema change is required. The two ARCH6D migrations and the existing 18
tables remain unchanged.

## Reused daily cycle

The existing producer remains authoritative:

1. the ARCH5A/ARCH6B daily session runs the four required Qubes models;
2. downstream target position, position-only drift, ManualPaperCycle, and R009
   finish as CompletedNoExternal;
3. the evidence manifest and no-order manifest are finalized;
4. the existing administrative extension point invokes the ARCH6E tool with a
   finalized handoff;
5. the coordinator delegates to Arch6bPmsShadowSessionImporter;
6. EfPmsShadowSessionImportStore retains the PostgreSQL advisory lock,
   serializable transaction, append-only facts, and terminal COMPLETED write.

The local Worker scheduler is inventoried but not changed. ARCH6E adds no
scheduler, polling loop, message queue, outbox, second importer, or second PMS.
It never starts GPU, LMAX, Polygon, Databento, FIX, AWS, or account services.

## Handoff

Contract: pms_shadow_daily_ingestion_request_v1.

The producer uses --build-daily-handoff only after all source-finalization
flags are true. Identity is content-based. The idempotency key is SHA-256 of
contract version, source session ID, and evidence ZIP SHA-256 in that order.
Paths and current timestamps are not identity inputs.

The consumer uses --coordinate-daily-ingestion. Validation must reach
READY_TO_IMPORT before the existing importer is called. Invalid or incomplete
handoffs produce structured fail-closed alerts and perform no business write.

## Recovery

- Before validation: no database call.
- After validation and before import: rerun the same handoff.
- During import: the existing transaction rolls back.
- After commit and before acknowledgement: replay returns
  ALREADY_APPLIED_IDENTICAL.
- Controller restart: rediscover the finalized handoff and replay it.
- Two controllers: the existing session advisory lock serializes import.

COMPLETED remains the final write inside the ARCH6D transaction.

## Read models

EfPmsShadowOperationalReadService uses only AsNoTracking queries over the
18 existing tables. It exposes latest completed session, model runs, target
positions, position-only drift, broker-adjusted blocker status, freshness and
completeness, and concise SHA/URI lineage. Incomplete ingestions are excluded
from latest-session selection.

Freshness has no implicit threshold. Every query requires an expected
operational date, a positive maximum ingestion age, and an explicit UTC clock.
Statuses are Fresh, Stale, MissingToday, Incomplete, and FailedClosed.

The read-only modes are:

- --pms-shadow-latest
- --pms-shadow-session --source-session-id <id>
- --pms-shadow-targets --source-session-id <id>
- --pms-shadow-drifts --source-session-id <id>
- --pms-shadow-lineage --source-session-id <id>
- --pms-shadow-health

All modes require --environment TEST, --no-order, Npgsql for database access,
and an env: secret reference. They expose no mutation method.

## Safety

The existing persistence-plan validation still rejects a real account,
ExecutionAllowed=true, positive trade-intent count, enabled order entry,
inferred empty working leaves, invalid provider, or a migration baseline other
than the exact two ARCH6D migrations.

Broker-adjusted drift remains uncalculated with
BROKER_WORKING_LEAVES_UNOBSERVABLE; working leaves are never assumed zero.
