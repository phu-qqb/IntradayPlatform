## ARCH7B

Requalifies the existing LMAX Demo FIX order-entry path as one bounded, known-order lifecycle:

- one deterministic GBPUSD opening order on Demo account `1754288005`;
- one residual cancel maximum;
- one opposite-side flatten for the durable, deduplicated executed opening quantity;
- broker ExecutionReport authority and final flat reconciliation;
- no real account and no production mutation.

## BBO contract

- Opening is BUY at the ask of a fresh, sequence-valid, content-addressed LMAX observation acquired inside the authorized window.
- Flatten is constructed only after opening terminal CumQty equals the durable sum of unique validated opening Fills.
- Flatten opens a separate SnapshotOnly LMAX market-data session and uses the bid of a new observation whose SHA differs from the opening observation.
- Polygon, stale snapshots, crossed books, over-wide spreads, non-tick prices and silent opening-observation reuse are fail-closed.
- No fresh flatten BBO activates the kill-switch evidence, creates no flatten order and never exceeds the global lifecycle deadline.

## Persistence and recovery

- Adds seven append-only ARCH7B tables under the existing `pms_shadow` schema.
- Persists D/F/H intent before socket write.
- Persists raw ExecutionReport SHA, MsgSeqNum and PossDup.
- Persists distinct opening and flatten observation SHA values in the outbound ledger.
- Never resends a persisted D or F after restart; known-order status only.
- Uses durable deduplicated Fills as the flatten quantity authority.

## Validation

- ARCH7B targeted tests: 48 passed.
- Coupled ARCH6C-F, ARCH7A, PMS and PostgreSQL regressions: see `docs/architecture/arch7b/validation_summary.json`.
- Release solution build: zero errors.
- EF pending model changes: none.
- Migration: `20260723085240_AddArch7bLmaxDemoKnownOrderLifecycle`.
- SQL Up SHA-256: `8911b660adeca29d8af0d4141fe54b4f5adb429f649019a458554b4072fb20ff`.
- SQL idempotent SHA-256: `39f6911028f3cf2cb69b020f5f0d4dfa2fcb58bab3ba0913caedc128bd817ad0`.
- SQL full Down SHA-256: `8450c28d7e859589c72a238b78f7e74e009f540472835364633eb714b92a1ba8`.

## Safety

Preparation performed with zero PostgreSQL connections, migration applies, secret reads, FIX logons, outbound orders, real Fills, real PositionLedgerEvents, real-account access or production mutations.

Live execution remains disabled and requires a separate exact authorization packet after publication and migration review.
