# ARCH6F real-slot economic refresh

## Root cause

The first three PostgreSQL slot attempts persisted a finalized technical manifest, then the
intraday read service reloaded the daily ARCH6B target positions and position-only drifts.
The LMAX capture SHA changed per slot, but that market input was not part of the target
position calculation. Those attempts remain immutable and non-qualifying.

## Corrected contract

Each corrected slot revision:

1. verifies the existing raw capture file against its SHA-256;
2. requires LMAX-primary, complete 49-BBO input, zero contractual gaps, zero Polygon calls,
   and no-order counters;
3. projects the 99 PMS security mappings from direct LMAX observations or the canonical
   USD two-leg projector;
4. reuses the four finalized daily ModelRuns and their 288 target weights as model input;
5. invokes `TargetPositionCalculator` with the slot decision price for every target weight;
6. computes position-only drift against the authoritative ARCH6B position snapshot;
7. writes one append-only economic revision and its market, target, and drift facts in one
   serializable PostgreSQL transaction.

Daily ModelRuns are reused model inputs. Market data, target-position inputs, targets, and
drifts are fresh slot facts. No fresh GPU model run is claimed for these slots.

## Persistence

Migration `20260722231500_AddIntradayEconomicProjectionRevisions` adds:

- `pms_shadow.intraday_projection_revisions`;
- `pms_shadow.intraday_market_data_observations`;
- `pms_shadow.intraday_target_positions`;
- `pms_shadow.intraday_position_only_drifts`.

Migration `20260722234500_CorrectIntradayMarketPriceScaleInvariant` aligns the midpoint
check with the `numeric(28,12)` storage scale by comparing against the midpoint rounded to
12 decimals. It does not rewrite stored prices or economic facts.

Every revision carries the raw capture SHA, market snapshot SHA, economic input SHA,
target and drift SHA values, the superseded technical manifest SHA, and no-order status.
The old `intraday_slots` rows are not updated or deleted. An identical replay returns
`AlreadyAppliedIdentical`; contradictory content fails closed with
`FAILED_CLOSED_CONFLICT`.

The base economic revision migration `Down` removes only these four revision-owned tables. It does not remove the
base PMS schema or the original intraday slot registry.

## Bounded replay

`QQ.Production.Intraday.Tools.Arch6fEconomicReplay` accepts exactly three consecutive
captures. `preflight` verifies only local capture evidence. `project-only` reads the local
test database and calculates all three revisions without migration or writes.
`apply-and-replay` is reserved for the coordinated post-merge authorization: it applies the
migration, inserts the three revisions, immediately replays them to prove idempotence, and
validates latest/history read models.

The tool opens no LMAX session, makes no Polygon call, invokes no GPU engine, exposes no
connection string, and has no order, fill, ledger, FIX order-entry, broker-send, real-account,
or production-database path.
