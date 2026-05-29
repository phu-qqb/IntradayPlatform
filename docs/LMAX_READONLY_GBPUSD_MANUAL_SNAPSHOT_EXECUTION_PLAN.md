# LMAX Read-Only GBPUSD Manual Snapshot Execution Plan

## Purpose

Phase 6T defines the execution plan and kill/rollback checklist for a future operator-approved manual Demo GBPUSD read-only MarketData snapshot attempt.

This phase is planning/docs/gate only. It does not authorize or run an external snapshot.

## Current Gate Chain

- Phase 6N planning values: GBPUSD / GBP/USD uses SecurityID `4002`, SecurityIDSource `8`.
- Phase 6O per-instrument safety gate: GBPUSD `PASS`, not executable.
- Phase 6P preflight: GBPUSD `PASS`, not executable.
- Phase 6Q approval envelope: GBPUSD `AcceptedForPlanning`, not executable.
- Phase 6R dry-run report: GBPUSD `PASS`, not executable.
- Phase 6S attempt gate: GBPUSD `PASS`, not executable.

## GBPUSD Parameters

- Symbol: `GBPUSD`
- Slash symbol: `GBP/USD`
- SecurityID: `4002`
- SecurityIDSource: `8`
- Environment: `Demo`
- Venue profile: `DemoLondon`
- Request mode: `SnapshotPlusUpdates`
- Symbol encoding mode: `SecurityIdOnly`
- MarketDepth: `1`

## Still Not Authorized

The following remain not authorized in Phase 6T:

- External run.
- Scheduler or polling.
- Runtime shadow replay submit.
- Order submission.
- `NewOrderSingle`, Cancel, Replace, TradeCapture, or OrderStatus.
- Real gateway registration.
- Trading-state mutation.
- Trading-table persistence.
- Production or UAT use.
- Multi-instrument batch.

`IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain required.

## Future Execution Prerequisites

A later phase must provide all of the following before any manual execution can be considered:

- Explicit operator signoff for the single GBPUSD attempt.
- Demo credentials available only through the existing redacted credential boundary.
- Manual script only; no scheduler or polling.
- All Phase 6 gates through the future execution gate must be `PASS`.
- API and Worker must still remain `FakeLmaxGateway` only.
- The operator must read this kill/rollback plan immediately before the future attempt.
- The future attempt must remain Demo-only, read-only MarketData-only, single-instrument only.

## Future Command Template

DO NOT RUN IN PHASE 6T. This is a future template only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "<future explicit operator signoff reason>" `
  -Symbol GBPUSD `
  -SecurityId 4002 `
  -SecurityIdSource 8 `
  -RequestMode SnapshotPlusUpdates `
  -SymbolEncodingMode SecurityIdOnly `
  -MarketDepth 1
```

## Abort Criteria

Abort immediately if any of these occur:

- Symbol or slash symbol is not `GBPUSD / GBP/USD`.
- SecurityID is not `4002` or SecurityIDSource is not `8`.
- Any order flag is true.
- Scheduler or polling is detected.
- Runtime shadow replay submit is true.
- Credential-shaped content appears in console output, logs, artifacts, reports, or evidence.
- Unknown failure classification occurs.
- Environment is not Demo.
- API or Worker gateway registration changes.
- Mutation guard changes.
- Multi-instrument batch is attempted.

## Kill And Rollback

- Stop the manual process.
- Clear shell variables if needed.
- Verify API health still reports `FakeLmaxGateway`.
- Run the Phase 6S attempt gate again.
- Inspect artifacts for `noSensitiveContent=true`.
- Confirm no database rollback is expected because trading mutation and trading-table persistence are prohibited.
- Preserve sanitized evidence for operator review if an artifact was created.

## Future Post-Run Validation Requirements

A future execution phase must require:

- Snapshot artifact validation.
- Evidence preview mapping.
- Optional manual replay only if explicitly approved in that later phase.
- No observation or mutation guard changes.
- Operator review and signoff.
- Confirmation that no credential values appear in artifacts, docs, evidence, reports, or audit metadata.

## Phase 6T Decision Meaning

`PASS` means this execution plan is internally consistent and safely non-executable.

`PASS` does not authorize an external run.

## Phase 6U Operator Signoff

Phase 6U adds a non-executable operator signoff envelope for this plan. `SignedForPlanning` means the operator reviewed the execution plan and kill/rollback checklist. It does not authorize execution, does not make GBPUSD eligible, and does not change `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, or `canRunExternalSnapshot=false`.

## Phase 6V Final Readiness

Phase 6V adds a final non-executable readiness artifact that aggregates the full GBPUSD planning chain. `PASS` means the chain is complete and consistent. It does not authorize execution and does not change `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, or `canRunExternalSnapshot=false`.

## Phase 6W One-Shot Wrapper

Phase 6W adds the one-shot manual wrapper for GBPUSD. It requires the Phase 6V final readiness artifact and explicit operator flags, hardcodes GBPUSD `4002`, and must stop after one attempt. Result review proceeds in Phase 6X.

## Phase 6X Empty Book Result

The first GBPUSD one-shot Demo read-only snapshot attempt produced a sanitized `CompletedWithEmptyBook` artifact. The request reached a logged-on Demo MarketData session and received one MarketDataSnapshot, but the snapshot contained zero entries, with no MarketDataRequestReject, BusinessMessageReject, session Reject, order submission, shadow replay submit, scheduler start, credential leakage, or trading mutation.

Phase 6X treats this as `PASS_WITH_KNOWN_WARNINGS`. It is not a reject and not a mutation, but it is also not a successful top-of-book observation. It does not authorize an automatic retry, replay, scheduler/polling, additional instruments, gateway registration, orders, UAT/production, or trading-state mutation.

Likely next actions are either a separately approved second manual GBPUSD attempt at a different time, or empty-book evidence preview mapping and optional manual replay planning in a later phase.

## Phase 6Y Market-Hours Retry Preparation

Phase 6Y documents that the first GBPUSD attempt occurred on Saturday while FX was closed, so `CompletedWithEmptyBook` is expected. The market-hours retry plan is captured in `docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_RETRY_PLAN.md`.

The retry preparation remains non-executable. It prepares one future manual attempt for Sunday evening after FX reopen or Monday market hours, and keeps no scheduler/polling, no automatic retry, no runtime shadow replay submit, no orders, no gateway registration, and no trading mutation.
## Phase 7C Market-Hours Closure Workflow

Phase 7C adds the local closure workflow for the next future market-hours GBPUSD result artifact. It does not execute the snapshot command and does not replay automatically.

After the operator separately runs the one-time GBPUSD wrapper during market hours, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1 `
  -ArtifactFile <gbpusd-result-artifact>

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\preview-lmax-readonly-gbpusd-market-hours-snapshot-evidence.ps1 `
  -ArtifactFile <gbpusd-result-artifact>

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-gbpusd-market-hours-closure-manifest.ps1 `
  -ArtifactFile <gbpusd-result-artifact> `
  -EvidencePreviewFile <optional-preview-file>
```

Optional replay is separate and requires `-ConfirmLocalManualReplay`; it posts only sanitized MarketDataOnly evidence to the local API and expects zero observations with mutation unchanged.

`CompletedWithBook` closes as `PASS` when evidence preview is valid. `CompletedWithEmptyBook` closes as `PASS_WITH_KNOWN_WARNINGS`. Any credential leakage, order surface, scheduler, runtime shadow replay submit, gateway registration, or trading mutation is `FAIL`.
