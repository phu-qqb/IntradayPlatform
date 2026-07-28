# ARCH7B fresh position import critical path

## Contract

The operational contract is `arch7b_fresh_position_import_fast_path_v1`.

`Arch7bPositionImportContract.MaximumAgeSeconds` remains exactly 300 seconds. The fast path does not add tolerance, alter broker P2, redate evidence, clamp age, round age, or use host time as economic authority. It removes work from the P2-to-apply path.

## Two modes

`consume-bracketed-global-flat` is the qualification mode. It retains full Core validation, the read-only RDS universe read, the canonical 99-line snapshot, smoke A, smoke B, determinism, and the complete evidence bundle. It can additionally emit the canonical four-file import package for parity qualification.

`prepare-fresh-position-import-package` is the operational consumer. It performs full Core package and SHA validation, downloader 0.5/0.6 compatibility validation, semantic recomputation, the exact read-only RDS universe read, temporal and mapping lineage validation, and the canonical 99-line snapshot. It then writes only the import package.

The fast path does not run either smoke, build a synthetic economic projection, create a ZIP, run tests, build, restore, publish, or copy unrelated artifacts.

## Minimal package

The closed package contains exactly:

1. `manifest.json`
2. `required-pms-universe.json`
3. `pms-bracketed-global-flat-position-snapshot.json`
4. `normalized-position-lines.csv`

The canonical writer is shared by full and fast modes. With the same Core package and RDS snapshot, all four files are byte-for-byte identical. Variable timing evidence is written outside the package so it cannot alter canonical identities or violate the importer's closed inventory.

The manifest records `SMOKE_NOT_EXECUTED_IN_CRITICAL_PATH_PREQUALIFIED_CODE_PATH`. It never claims that a deferred smoke ran.

## Pre-P2 preparation

Complete all of the following before the bracket:

- Release build and dependency restore;
- Core and Intraday tests and qualification;
- portal session probe;
- RDS tunnel creation and connection test;
- bounded importer-role validation;
- PostgreSQL typed-clock and host-clock diagnostics;
- binary publication;
- prepared command lines;
- fresh empty output roots;
- owner lease and `arm-import` for the exact source ingestion, target, repository/build commit, and future authorization.

The wrapper rejects arm state created after broker P2. It never creates a late arm.

## Post-P2 critical path

After broker P2, run `run-fresh-position-import-fast-path` once with the exact Core evidence root, Core SHAs, output root, already-published armed state, owner, authorization, source ingestion, clean repository/build commit, target fingerprint, and environment-only RDS secret reference.

The wrapper performs:

1. prearmed-state, owner, authorization, target, source-ingestion, and repository validation;
2. fast consumer and canonical package write;
3. PostgreSQL typed-clock package SLO check;
4. read-only pre-ready plan and atomic ready-marker publication;
5. PostgreSQL typed-clock ready SLO check;
6. final read-only plan under the same package and target;
7. PostgreSQL typed-clock plan and apply-start SLO checks;
8. the existing serializable append-only apply with ready-marker validation and readback.

It stops at the first blocker. There is no retry, second database, alternate package, alternate owner, or alternate authorization.

## Internal SLOs

Broker P2 is the economic start. PostgreSQL typed UTC is authoritative for operational decisions. `Stopwatch` is diagnostic only.

| Milestone | Maximum from P2 | Blocker |
|---|---:|---|
| Four-file package ready | 60 s | `ARCH7B_POSITION_FAST_PATH_PACKAGE_SLO_EXCEEDED` |
| Ready marker possible/published | 90 s | `ARCH7B_POSITION_FAST_PATH_READY_SLO_EXCEEDED` |
| Final plan start | 120 s | `ARCH7B_POSITION_FAST_PATH_PLAN_SLO_EXCEEDED` |
| Apply start | 150 s | `ARCH7B_POSITION_FAST_PATH_APPLY_START_SLO_EXCEEDED` |
| Commit/readback expected | 180 s | monitored expectation; canonical 300-second freshness still applies |

These targets preserve at least 120 seconds of margin to the unchanged 300-second gate.

## Smoke deferral

The deterministic smoke remains mandatory qualification coverage and remains in full mode. It is not allowed to delay ready, plan, or apply. A deferred smoke may run only after append-only commit/readback or after a terminal NO_GO, and its evidence stays outside the import package.

## Evidence and cleanup

The fast consumer writes phase timings outside the package. The wrapper writes one immutable JSON event per step in a new append-only timeline directory. Final ZIP, Markdown, and detailed reporting occur only after commit/readback or a terminal blocker.

On any blocker:

- preserve the first blocker and timeline;
- do not retry with another database or package;
- do not publish a later ready marker;
- do not start apply;
- perform normal tunnel/process cleanup;
- leave the sealed historical run unchanged.

No LMAX acquisition, FIX logon, order, Fill, PositionLedgerEvent, Account API call, Databento API call, or production mutation belongs to this corrective PR or its read-only qualification.