## ARCH7B

Requalifies the existing LMAX Demo FIX order-entry path as one bounded, known-order lifecycle:

- one deterministic GBPUSD opening order on Demo account `1754288005`;
- one residual cancel maximum;
- one opposite-side flatten for the durable, deduplicated executed opening quantity;
- broker ExecutionReport authority and final flat reconciliation;
- no real account and no production mutation.

## Instrument identity contract

- PMS source identity consists of the internal `InstrumentId`, canonical
  `SecurityId`, and canonical symbol.
- Execution venue identity consists of the LMAX instrument ID, FIX
  `SecurityIDSource=8`, and execution symbol.
- For historical GBPUSD, PMS `SecurityId=68` and LMAX `SecurityID=4002`.
- PostgreSQL preflight joins the exact source-ingestion `security_mappings`
  row by internal `InstrumentId`; it never equates or rewrites the two IDs.

## BBO contract

- Opening is BUY at the ask of a fresh, sequence-valid, content-addressed LMAX observation acquired inside the authorized window.
- Flatten is constructed only after opening terminal CumQty equals the durable sum of unique validated opening Fills.
- Flatten opens a separate bounded `SnapshotPlusUpdates` LMAX market-data session and aggregates one complete bid/ask from no prior state. Freshness is `nowUtc - observedAtUtc <= 5 seconds`; session start and observation must remain post-opening-terminal, so attempts two and three remain usable.
- After acquisition, the economic BBO is frozen before cleanup. Unsubscribe with `263=2` and the same `MDReqID` plus logout share one cancellable budget capped at 1,000 ms and the absolute lifecycle deadline.
- The unsubscribe slice cannot consume the logout slice. On timeout/error the socket is force-closed before best-effort stream/socket disposal; all started tasks are awaited and the final cleanup snapshot records actual attempted/succeeded states.
- Cleanup failure is a sanitized diagnostic, never invalidates an already complete, fresh, sequence-valid BBO and never masks the primary economic blocker.
- The bounded streaming contract is `BOUNDED_SNAPSHOT_PLUS_UPDATES_ONE_BBO_THEN_UNSUBSCRIBE`.
- PostgreSQL preflight reads the ChildOrder-bound revision from `intraday_projection_revisions` and `intraday_market_data_observations`, then validates its source identity through the same-ingestion `security_mappings` row; the legacy market snapshot tables are not a fallback.
- Polygon, stale snapshots, crossed books, over-wide spreads, non-tick prices and silent opening-observation reuse are fail-closed.
- No fresh flatten BBO activates the kill-switch evidence, creates no flatten order and never exceeds the global lifecycle deadline.

## Persistence and recovery

- Adds seven append-only ARCH7B tables under the existing `pms_shadow` schema.
- Persists D/F/H intent before socket write.
- Persists raw ExecutionReport SHA, MsgSeqNum and PossDup.
- Persists distinct opening and flatten observation SHA values in the outbound ledger.
- Never resends a persisted D or F after restart; known-order status only.
- Uses durable deduplicated Fills as the flatten quantity authority.
- Attempts FIX Logout on every post-logon exit before disposing the order-entry stream/socket and releasing the advisory lease.

## Validation

- Bounded cleanup, BBO retry and known-order contract tests: 47 passed.
- Relevant ARCH6F, ARCH7A, ARCH7B and ConnectivityLab regressions: 350 passed.
- Release solution build: zero errors.
- EF pending model changes: none.
- Migration: `20260723085240_AddArch7bLmaxDemoKnownOrderLifecycle`.
- SQL Up SHA-256: `8911b660adeca29d8af0d4141fe54b4f5adb429f649019a458554b4072fb20ff`.
- SQL idempotent SHA-256: `39f6911028f3cf2cb69b020f5f0d4dfa2fcb58bab3ba0913caedc128bd817ad0`.
- SQL full Down SHA-256: `8450c28d7e859589c72a238b78f7e74e009f540472835364633eb714b92a1ba8`.

## Safety

Preparation performed with zero PostgreSQL connections, migration applies, secret reads, FIX logons, outbound orders, real Fills, real PositionLedgerEvents, real-account access or production mutations.

Live execution remains disabled and requires a separate exact authorization packet after publication and migration review.
