# LMAX Read-Only EURGBP Manual Snapshot Execution Checklist

Phase 7F2 prepares the future EURGBP manual snapshot execution checklist and kill/rollback plan. It is planning only. It does not approve, schedule, or run an external snapshot.

## Prerequisite Chain

- GBPUSD market-hours closure PASS.
- Phase 7D decision is `ProceedToEurgbpPlanning`.
- Phase 7E2 EURGBP readiness PASS.
- EURGBP remains non-executable with `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`.

## EURGBP Parameters

| Field | Value |
| --- | --- |
| symbol | EURGBP |
| slashSymbol | EUR/GBP |
| SecurityID 4003 | selected DemoLondon planning value |
| SecurityIDSource | 8 |
| Environment | Demo |
| VenueProfile | DemoLondon |
| RequestMode | SnapshotPlusUpdates |
| SymbolEncodingMode | SecurityIdOnly |
| MarketDepth | 1 |

## Still Not Authorized

- External run.
- No scheduler.
- No polling.
- No runtime shadow replay submit.
- No orders.
- No gateway registration.
- Trading mutation.
- Production/UAT.
- No multi-instrument batch.

## Future Execution Prerequisites

- Explicit operator command in a later phase.
- Credentials present only; credential values must not be printed or written.
- Run only from a future manual script/wrapper.
- All gates PASS.
- API/Worker remain FakeLmaxGateway only.
- Kill/rollback plan read before any future command.
- One-instrument-at-a-time remains enforced.

## Future Command Template

DO NOT RUN IN PHASE 7F2. This is a future template only and requires a later explicit operator-approved EURGBP execution phase:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "<future explicit operator-approved EURGBP market-hours reason>" `
  -Instrument EURGBP `
  -SlashSymbol "EUR/GBP" `
  -LmaxInstrumentId 4003 `
  -RequestMode SnapshotPlusUpdates `
  -SymbolEncodingMode SecurityIdOnly `
  -MarketDepth 1
```

The current prototype does not make EURGBP executable in Phase 7F2. The future execution phase must explicitly establish any required manual wrapper and gate it before use.

## Abort Criteria

- Wrong symbol or SecurityID.
- Any order flag is true.
- Scheduler or polling is detected.
- Runtime shadow replay submit is true.
- Credential exposure.
- Unknown failure classification.
- Non-Demo environment.
- Gateway registration changes.
- Mutation guard changes.
- Batch or multi-instrument attempt.

During any future explicit execution phase, `Ctrl+C` or closing the manual process is the kill switch. There is no retry loop and no background worker to stop.

## Rollback

- Stop the manual process.
- Clear shell variables if needed.
- Verify API `/health` still reports FakeLmaxGateway.
- Run the Phase 7E2 gate.
- Inspect artifacts for `noSensitiveContent=true`.
- No DB rollback is expected because mutation is prohibited.

## Future Post-Run Validation

1. Artifact review of the result artifact.
2. Map MarketDataOnly evidence preview if safe.
3. Optionally replay local only with explicit manual confirmation.
4. Build a closure manifest.
5. Run the closure gate.
6. Run the next-instrument decision.

## Phase 7F2 Result

PASS means the EURGBP checklist is internally complete and safe for future operator consideration. PASS does not authorize execution, scheduler/polling, runtime shadow replay submit, orders, gateway registration, trading mutation, Production/UAT, or batch execution.

## Phase 7G2 Final Pre-Run Gate

Phase 7G2 adds the final non-executable EURGBP pre-run consistency gate. It aggregates the corrected GBPUSD `CompletedWithBook` / `PASS` closure, the Phase 7D `ProceedToEurgbpPlanning` decision, the Phase 7E2 EURGBP readiness `PASS`, and the Phase 7F2 EURGBP execution checklist `PASS`.

The Phase 7G2 gate exists but does not authorize execution. A future EURGBP attempt remains separate, manual, Demo-only, and one-instrument-at-a-time.

Safety confirmations for Phase 7G2:

- canRunExternalSnapshot=false
- IsApprovedForExternalRun=false
- eligibleForManualSnapshotAttempt=false
- externalRunAuthorized=false
- No scheduler.
- No polling.
- No runtime shadow replay submit.
- No orders.
- No gateway registration.
- No trading mutation.
- No multi-instrument batch.
- API/Worker remain FakeLmaxGateway only.

Next recommended phase: Phase 7H2 - Operator-approved EURGBP Market-Hours Snapshot Attempt, if explicitly chosen, or stop with EURGBP pre-run closed.
