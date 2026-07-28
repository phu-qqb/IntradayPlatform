# ARCH7B bracketed global-flat position consumer

## Scope

`lmax_bracketed_global_flat_to_pms_position_snapshot_v1` consumes a local,
content-addressed
`lmax_portal_bracketed_current_position_snapshot_v2` evidence package.
It creates an immutable local PMS position candidate only when the complete,
current, account-scoped LMAX Demo open-position report proves that the whole
account has zero open positions.

The consumer does not connect to FIX, send an order, call Account API or
Databento, or write to PostgreSQL. PostgreSQL access is limited to a
`READ ONLY` transaction that resolves the latest qualified model universe.

## Core downloader compatibility

`arch7b_core_downloader_compatibility_v1` separates three identities:

- `DownloaderVersion` is the Core implementation version;
- `CoreContractVersion` is the economic bracket contract;
- `DownloaderCompatibilityProfile` is the Intraday consumer rule set.

The exact compatibility matrix is:

| Downloader | Profile | Economic contract | Session |
| --- | --- | --- | --- |
| `0.5.0` | `LEGACY_MANUAL_SESSION_V2` | `lmax_portal_bracketed_current_position_snapshot_v2` | `manual-session` |
| `0.6.0` | `AWS_SECRET_RECOVERY_MANUAL_SESSION_V2` | `lmax_portal_bracketed_current_position_snapshot_v2` | `manual-session` |

No range or lexical version comparison is used. Every version other than
`0.5.0` and `0.6.0`, including a future `0.7.0`, fails with
`ARCH7B_CORE_DOWNLOADER_VERSION_REJECTED`. Supporting another version requires
an explicit compatibility-table entry, tests, and validation of its new
metadata.

The legacy profile accepts immutable `0.5.0` manifests without AWS recovery
metadata and does not invent those fields in the result. The `0.6.0` profile
requires matching manifest and contract downloader versions plus sanitized AWS
Secrets Manager recovery provenance: exact recovery/source/credential/login/
bootstrap contracts, a valid secret-reference SHA-256, no recorded credential,
secret value, TOTP, cookie, or authorization value, and a coherent active-
session or authenticated-reopen proof for account `1754288005`.

Session recovery is operational provenance only. It does not provide economic
authority, change the bracket v2 schemas or header hashes, or change the
economic session from `manual-session`.

## Fail-closed contract

The consumer rejects the package unless all of the following are proven:
- `arch7b_core_bracket_report_semantic_verifier_v1` reparses every CSV with
  the Core 0.5.0/0.6.0 `lmax_portal_report_csv_parser_v2` rules and recomputes
  headers, rows, canonical records, duplicate counts, timestamps, account IDs,
  and semantic SHA-256 values;
- attempts are numbered consecutively from one to three, only the terminal
  attempt is stable, and every declared attempt directory and manifest is
  present in the exact final-index inventory;
- all report paths are resolved from the terminal successful attempt number;

- exact Core commit, contract version, downloader version, account,
  environment, and session mode;
- exact contract-file, final-index, evidence, raw-set, semantic-set,
  sub-evidence, header-set, and referenced-file SHA-256 values;
- complete final-index inventory with no missing or extra files;
- no path traversal, reparse point, or secret pattern;
- stable execution and position sets with a monotonic broker-date sequence;
- broker bracket span at most 30 seconds;
- current snapshot status `PROVEN_CURRENT_BRACKETED_SNAPSHOT`;
- raw broker position count exactly zero;
- all no-order, no-FIX, and no-database-write safety flags.

A nonzero broker position count fails with
`NO_GO_ARCH7B_NONZERO_CURRENT_POSITION_MAPPING_NOT_QUALIFIED`. No nonzero
position allocation, netting, duplication, or strategy mapping is defined by
this contract.

## PMS universe

The RDS reader requires target profile `ARCH7B_RDS_TEST`, environment `TEST`,
database `qq_pms_shadow_arch7b_test`, schema `pms_shadow`, PostgreSQL major
version 18, TLS `VerifyFull`, and no pending model change.

The latest completed ingestion must contain exactly:

| Strategy | Required weights |
| --- | ---: |
| INFX7 | 66 |
| INFX8 | 66 |
| INFX9 | 78 |
| INFX10 | 78 |

The reader always selects the latest completed ingestion and then requires its
exact authoritative four-model set. It never falls back to an older completed
ingestion. Each selected model must have one matching Qubes input snapshot from
the same ingestion and strategy, with the same UTC `TargetCloseUtc`, valid
artifact hashes, `NotAnOrder=true`, `ExecutionAllowed=false`, and
`AccountingEligible=false`. Unexpected model weights fail closed.

Their union must contain exactly 99 distinct instruments, each with one
unambiguous `InstrumentId`/`SecurityId` mapping and nonempty canonical symbol,
LMAX instrument ID, and mapping SHA-256. Symbol and LMAX ID reuse is permitted
by the economic contract, but distinct cardinalities and collisions are
inventoried. The required-universe SHA-256 binds the selected ingestion, Qubes
inputs, models, weights, mappings, cardinalities, and source-selection authority.

## Global-flat normalization

The candidate contains one explicit zero line for every required instrument.
Each line records the bracket evidence SHA-256, required-universe SHA-256,
Core commit, P2 broker timestamp, account, provenance, authority code,
LMAX instrument ID, mapping SHA-256, source ingestion ID, and PMS source
session ID.
Deterministic account, position-snapshot, and line identities change whenever
the source evidence, required universe, or instrument mapping changes.

`PositionSnapshotAsOfUtc` is the P2 broker timestamp.
`arch7b_broker_snapshot_after_pms_source_v1` requires P2 and the bracket upper
bound to be at or after the selected ingestion completion, and P2 to be at or
after every selected ModelRun `AsOfUtc`. INFX `TargetCloseUtc` is a scheduled
economic horizon and may follow the broker snapshot; that exception is explicit
as `SCHEDULED_TARGET_CLOSE_MAY_FOLLOW_BROKER_SNAPSHOT_V1` and is never used as
an as-of authority. Host time, file time, and an arbitrary bracket midpoint
are never temporal authorities.

Working-order authority remains `INCONNU`, with blocker
`ARCH7B_WORKING_ORDER_REPORT_AUTHORITY_UNAVAILABLE`.
`BrokerSendAllowed` remains false.

## Offline economic smoke

Two byte-identical runs use the real
`PmsShadowIntradayEconomicProjectionBuilder` with a temporally coherent,
content-addressed market fixture. Expected output is 99 market observations,
288 target positions, and 288 position-only drifts. Every current quantity is
zero, every delta equals its target quantity, and projection integrity must be
`PROVEN`.

## Future append-only import

Import is deliberately outside this change and requires separate
authorization. Every output states:

- `ImportEligibility=NOT_AUTHORIZED_REQUIRES_FRESH_BRACKET_AND_SEPARATE_IMPORT_PACKET`;
- `ImportFreshnessStatus=NOT_EVALUATED_FOR_FUTURE_IMPORT`.

The 2026-07-27 package is immutable historical evidence at its P2 timestamp,
not a standing current snapshot. A future importer must acquire a fresh bracket
immediately before its separately authorized append-only import, then:
The `arch7b-first-e2e-no-order-20260728T103254Z` package is likewise immutable
historical evidence and remains
`HISTORICAL_ABORTED_RUN_NOT_IMPORT_ELIGIBLE`. Requalifying its downloader
compatibility does not revive its slot, run, authorization, owner, armed state,
lock, output directory, or bracket package for import.


1. Revalidate the complete Core evidence and required RDS universe.
