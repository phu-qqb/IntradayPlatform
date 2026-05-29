# LMAX Read-Only GBPUSD Market-Hours Execution Checklist

## Purpose

This checklist prepares the future operator-approved manual Demo GBPUSD read-only MarketData snapshot attempt and the required post-run closure sequence.

Phase 7E is documentation, reporting, and gate only. It does not connect to LMAX, run a snapshot, run SecurityListRequest, run replay, schedule work, submit to shadow replay, submit orders, register a gateway, or mutate trading state.

## Required Market Condition

- FX market must be open.
- Do not run during weekend or closed-market hours.
- If market status is uncertain, stop and wait.

## Future Manual Command

DO NOT RUN UNTIL MARKET HOURS.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 `
  -FinalReadinessFile "artifacts\lmax-readonly-runtime-securityid-planning\final-readiness\lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json" `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6Z-B operator-approved GBPUSD market-hours read-only snapshot attempt"
```

This command is recorded for a future explicit operator action only. Phase 7E must not run it.

## Pre-Run Checklist

- Confirm FX market hours.
- Confirm Demo-only intent.
- Confirm credential presence only; do not print or copy credential values.
- Confirm API/Worker remain `FakeLmaxGateway` only.
- Confirm no scheduler or polling.
- Confirm runtime still does not submit to shadow replay.
- Confirm no order path.
- Confirm final readiness `PASS`.
- Confirm Phase 6Y retry readiness `PASS`.
- Confirm Phase 7C closure scripts exist.
- Confirm Phase 7D current decision is `PendingGbpusdMarketHoursAttempt`.

## During-Run Monitoring

- One attempt only.
- No retry.
- No batch or additional instruments.
- Use Ctrl+C or close the process as the kill switch.
- Stop immediately if symbol, SecurityID, environment, or request mode is wrong.

## Post-Run Sequence

1. Review the artifact with the Phase 7C review script.
2. Map evidence preview if the artifact is safe.
3. Optionally replay locally only if appropriate and explicitly confirmed.
4. Build the Phase 7C closure manifest.
5. Run the Phase 7C gate.
6. Run the Phase 7D next-instrument decision.

## Result Interpretation

- `CompletedWithBook`: proceed to evidence preview, optional local replay, closure manifest, Phase 7C gate, and Phase 7D. Phase 7D may allow EURGBP planning.
- `CompletedWithEmptyBook` during market hours: do not proceed to EURGBP; prepare retry or diagnostics in a new controlled phase.
- `FailedSafe`: diagnostics; no retry without a new phase.
- `UnsafeFail`: stop.

## Rollback

- Stop the process.
- Clear shell-only variables if needed.
- Verify `/health` reports `FakeLmaxGateway`.
- Inspect the artifact for `noSensitiveContent=true`.
- Run Phase 7C gate after review.
- No DB rollback is expected because mutation is prohibited.

## Explicit Non-Authorizations

This checklist does not authorize:

- No scheduler.
- No polling.
- No runtime shadow replay submit.
- No orders.
- No gateway registration.
- No production or UAT.
- No multi-instrument batch.
- No trading-state mutation.
- No automatic execution.
