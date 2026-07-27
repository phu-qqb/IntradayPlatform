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

## Fail-closed contract

The consumer rejects the package unless all of the following are proven:

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

Their union must contain exactly 99 distinct instruments, each with one
unambiguous security mapping. The required-universe SHA-256 binds the selected
ingestion, models, weights, and mappings.

## Global-flat normalization

The candidate contains one explicit zero line for every required instrument.
Each line records the bracket evidence SHA-256, required-universe SHA-256,
Core commit, P2 broker timestamp, account, provenance, and authority code.
Deterministic account, position-snapshot, and line identities change whenever
the source evidence or required universe changes.

`PositionSnapshotAsOfUtc` is the P2 broker timestamp. Host time, file time, and
an arbitrary bracket midpoint are never temporal authorities.

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
authorization. A future importer must:

1. Revalidate the complete Core evidence and required RDS universe.
