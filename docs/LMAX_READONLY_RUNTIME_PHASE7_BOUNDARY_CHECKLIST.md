# LMAX Read-Only Runtime Phase 7 Boundary Checklist

Phase: 7A  
Scope: planning checklist only; no runtime capability

Use this checklist before entering any Phase 7B implementation prompt.

## Current State Checks

- [ ] Phase 5Y final documentation pack exists and remains the frozen EURUSD manual Demo read-only MarketData workflow record.
- [ ] Phase 5W operational signoff is PASS.
- [ ] Phase 5X operator summary is PASS and remains read-only.
- [ ] Phase 6Z-D additional-instrument documentation pack is PASS.
- [ ] Phase 6Z-E market-hours next-action panel is PASS and remains read-only.
- [ ] Additional-instrument aggregate `executableCount=0`.
- [ ] `IsApprovedForExternalRun=false` for all additional instruments.
- [ ] `canRunExternalSnapshot=false` for all additional instruments.
- [ ] `eligibleForManualSnapshotAttempt=false` for all additional instruments.
- [ ] `runtimeShadowReplaySubmit=false`.
- [ ] `scheduler/polling=false`.
- [ ] `orderSubmission=false`.
- [ ] `gatewayRegistration=false`.
- [ ] `tradingMutation=false`.
- [ ] API and Worker remain `FakeLmaxGateway` only.

## Phase 7B Entry Rules

Any Phase 7B implementation must:

- [ ] Remain manual.
- [ ] Remain one instrument at a time.
- [ ] Require an explicit operator command for any future external attempt.
- [ ] Add no scheduler.
- [ ] Add no polling.
- [ ] Add no timers, background jobs, or hosted services for LMAX.
- [ ] Add no runtime shadow replay submit.
- [ ] Add no order submission.
- [ ] Add no NewOrderSingle, Cancel/Replace, TradeCapture, or OrderStatusRequest.
- [ ] Add no gateway registration.
- [ ] Add no trading-state mutation.
- [ ] Add no live FIX persistence to trading tables.
- [ ] Produce sanitized artifacts only.
- [ ] Preserve credential redaction.
- [ ] Preserve rollback instructions and abort criteria.
- [ ] Preserve API/Worker `FakeLmaxGateway` only.

## Recommended Next Boundary

Recommended:

**Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run**

Phase 7B should plan a controlled manual workflow for GBPUSD, EURGBP, USDJPY, and AUDUSD while preserving one-instrument-at-a-time operation. It must not implement scheduler/polling, runtime shadow replay submit, order path, real gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.

## Explicit Non-Authorizations

Phase 7A and this checklist do not authorize:

- LMAX connection.
- MarketData snapshot.
- SecurityListRequest.
- Replay.
- Scheduler or polling.
- Runtime shadow replay submit.
- Order submission.
- Gateway registration.
- Production or UAT.
- Multi-instrument batch execution.
- Trading-state mutation.

## Phase 7B Closure Checks

- [ ] Phase 7B workflow plan artifact exists.
- [ ] Phase 7B gate is PASS.
- [ ] Sequence remains GBPUSD, EURGBP, USDJPY, AUDUSD.
- [ ] `executableCount=0`.
- [ ] `batchExecutionAllowed=false`.
- [ ] All instruments retain `oneInstrumentAtATime=true`.
- [ ] All instruments retain `maxAttemptsPerInstrument=1`.
- [ ] All instruments retain `retryRequiresNewPhase=true`.
- [ ] All instruments retain `canRunExternalSnapshot=false`.
- [ ] All instruments retain `IsApprovedForExternalRun=false`.
- [ ] API/Worker remain `FakeLmaxGateway` only.
