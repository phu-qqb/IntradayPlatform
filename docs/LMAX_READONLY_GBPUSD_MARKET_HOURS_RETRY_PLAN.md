# LMAX Read-Only GBPUSD Market-Hours Retry Plan

## Purpose

Phase 6Y prepares one future operator-approved GBPUSD Demo read-only MarketData snapshot retry during open FX market hours. It does not run GBPUSD, does not connect to LMAX, does not schedule anything, and does not add runtime execution power.

## Saturday Result

The first operator-approved GBPUSD attempt completed safely with:

- `status=CompletedWithEmptyBook`
- `snapshotReceived=true`
- `entryCount=0`
- `bestBid=null`, `bestAsk=null`, `mid=null`
- `MarketDataSnapshot=1`
- `MarketDataRequestReject=0`
- `BusinessMessageReject=0`
- `Reject=0`
- `orderSubmissionAttempted=false`
- `shadowReplaySubmitAttempted=false`
- `tradingMutationAttempted=false`
- `schedulerStarted=false`
- `credentialValuesReturned=false`
- `noSensitiveContent=true`

This is a completed-with-warning state, not a request rejection, not an order, and not a trading mutation.

## Interpretation

The attempt was performed on Saturday while the FX market was closed. An accepted MarketDataSnapshot with an empty book is expected outside active market hours. Phase 6X therefore closed the result as `PASS_WITH_KNOWN_WARNINGS`.

## Retry Window

The next attempt may be considered only during market hours:

- Sunday evening after FX market reopen.
- Monday during normal FX market hours.

There is no automatic retry, timer, scheduler, background service, or polling loop.

## Future Command Template

Do not run this command in Phase 6Y. It is documented only for a future Phase 6Z operator-approved manual attempt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 `
  -FinalReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6Z operator-approved market-hours GBPUSD read-only snapshot attempt"
```

The wrapper remains single-instrument, single-attempt, no retry, Demo-only, read-only MarketData only, GBPUSD `4002`, SecurityIDSource `8`, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.

## Abort Criteria

Abort before any future attempt if any of the following is true:

- Instrument is not `GBPUSD / GBP/USD`.
- SecurityID is not `4002` or SecurityIDSource is not `8`.
- Environment is not Demo or venue profile is not DemoLondon.
- Any order flag or order message surface appears.
- Scheduler, polling, timer, hosted service, or background job is detected.
- Runtime shadow replay submit is enabled.
- Credential values would be printed, logged, written, or returned.
- Failure classification is unknown and not failed-safe.
- API or Worker gateway registration changes away from `FakeLmaxGateway`.
- Trading mutation or trading-table persistence appears.

## Post-Run Validation For Future Phase

A future Phase 6Z result must be reviewed locally:

- If bid/ask/mid are present, proceed to artifact review and evidence preview mapping.
- If `CompletedWithEmptyBook` repeats during market hours, classify it as repeated empty-book and diagnose feed availability, liquidity, or instrument availability.
- If failed-safe, preserve the sanitized artifact and diagnose without automatic retry.
- Do not replay automatically.
- Do not schedule retry.

## Phase 6Y Decision Meaning

`PASS` means the market-hours retry plan is internally safe and complete.

`PASS` does not execute anything, does not authorize scheduler or polling, does not authorize repeated retries, does not authorize orders, and does not authorize runtime shadow replay submit.

Next recommended phase: Phase 6Z - Operator-approved GBPUSD Market-Hours Snapshot Attempt, if and only if the operator explicitly runs it during market hours. Otherwise remain paused until market hours.
