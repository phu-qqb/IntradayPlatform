# LMAX Read-Only Runtime - Controlled Manual Workflow Review

## 1. Purpose

Phase 5T freezes the controlled manual LMAX MarketData workflow as an operationally documented, reviewable, manual-only process. It adds no runtime capability. It does not authorize scheduler, automatic polling, runtime shadow replay submit, order submission, gateway registration, trading-state mutation, broader instruments, UAT, production, or credential exposure.

## 2. Validated State

The controlled manual workflow has validated:

- Three successful operator-approved Demo EURUSD / SecurityID `4001` read-only market-data snapshots.
- Sanitized snapshot artifacts under `artifacts/lmax-readonly-runtime-demo-snapshot/`.
- `MarketDataOnly` evidence previews under `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/`.
- A fixed Phase 5S release manifest at `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json`.
- A Phase 5S release-gate report at `artifacts/readiness/phase5s-manual-release-gate.json`.
- Runtime shadow replay submit remains `false`.
- External connection attempted during workflow review remains `false`.
- Credential values returned remains `false`.
- API and Worker remain `FakeLmaxGateway` only.

The current Phase 5S decision is `PASS_WITH_WARNINGS` because optional local replay was intentionally not requested. That warning is acceptable for this freeze because replay remains a separate explicit local API operation, not a runtime capability.

## 3. What This Does Not Authorize

`PASS` or `PASS_WITH_WARNINGS` for this workflow does not authorize:

- Scheduler or automatic polling.
- Runtime shadow replay submit.
- Order submission or order-status/trade-capture runtime expansion.
- Real LMAX gateway registration in API or Worker.
- Trading-table persistence or trading-state mutation.
- UAT or production activation.
- Multi-instrument expansion.
- Credential values in output, logs, artifacts, reports, docs, or tests.

## 4. Manual Workflow

Run only from a local shell with explicit operator intent.

```powershell
.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -ConfirmRepeatedManualSnapshots `
  -AttemptCount 3 `
  -DelaySeconds 2 `
  -Reason "Operator-approved Demo EURUSD read-only snapshot stability check"
```

Validate and review the stability result:

```powershell
.\scripts\review-lmax-readonly-runtime-phase5o-stability-results.ps1 `
  -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

Run the controlled manual release review without optional replay:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -ConfirmRepeatedManualSnapshots `
  -AttemptCount 3 `
  -DelaySeconds 5 `
  -Reason "Phase 5S manual workflow release test"
```

Expected result when replay is skipped intentionally:

- `FinalDecision=PASS_WITH_WARNINGS`
- `ArtifactCount=3`
- `EvidencePreviewCount=3`
- `ManualReplayCount=0`
- `RuntimeShadowReplaySubmit=false`
- `ExternalConnectionAttempted=false`
- `CredentialValuesReturned=false`

## 5. Optional Local Replay

Optional replay is local API only. It is never runtime submit. It requires both replay flags:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -ConfirmRepeatedManualSnapshots `
  -ReplayEvidencePreviews `
  -ConfirmLocalManualReplay `
  -AttemptCount 3 `
  -DelaySeconds 5 `
  -Reason "Phase 5S manual workflow release test"
```

Expected replay result for each `MarketDataOnly` preview:

- Replay status `Completed`.
- Observation count `0`.
- Blocking observation count `0`.
- Warning observation count `0`.
- Mutation guard `Unchanged`.

## 6. Freeze Gate

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1
```

The gate is local-only. It does not connect to LMAX, does not require credentials, does not require API by default, and does not submit to shadow replay.

## 7. Rollback And Abort

If the process is stopped or fails:

1. Stop the local shell command with `Ctrl+C` if needed.
2. Clear Phase 5S/5T shell variables.
3. Verify `/health` reports `FakeLmaxGateway` if API is running.
4. Re-run the Phase 5O gate if stability artifacts are in doubt.
5. Re-run the Phase 5S gate against the fixed release manifest.
6. Do not proceed if any credential value, order surface, scheduler, runtime replay submit, gateway registration, or mutation marker appears.

No database rollback is expected because this workflow must not mutate trading state.

## 8. Operator Checklist

- Confirm Demo-only, EURUSD / SecurityID `4001` only.
- Confirm the reason is non-empty and operator-approved.
- Confirm no scheduler or automatic polling is being used.
- Confirm artifacts and previews are under ignored `artifacts/`.
- Confirm optional replay is explicit local API replay only.
- Confirm final decision and warnings are understood.

## 9. Developer Checklist

- API/Worker remain `FakeLmaxGateway` only.
- Runtime still has no shadow replay submit path.
- Prototype remains manual-script-only.
- No order command surface exists in the read-only runtime path.
- No trading repository or mutation dependency was added.
- Credential values are never returned, logged, stored, or documented.

## 10. Risk Checklist

- `PASS_WITH_WARNINGS` is acceptable only when the warning is optional replay skipped.
- Replay, when requested, must be zero-observation and mutation unchanged.
- Any non-zero observation, mutation guard change, credential exposure, order marker, scheduler marker, or gateway registration is a stop condition.

## 11. Next Phase Boundary

Recommended next phase:

- Phase 5U - Optional Local Replay Completion to Convert `PASS_WITH_WARNINGS` to `PASS`, or
- Phase 5U - Read-Only MarketData Workflow Operational Signoff.

Neither next phase should authorize scheduler, polling, runtime replay submit, orders, real gateway registration, trading mutation, production, or multi-instrument expansion without a separate explicit prompt.
