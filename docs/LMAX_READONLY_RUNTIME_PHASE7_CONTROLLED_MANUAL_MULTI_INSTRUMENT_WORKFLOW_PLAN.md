# LMAX Read-Only Runtime Phase 7B Controlled Manual Multi-Instrument Workflow Plan

Status: Implemented as planning only  
Phase: 7B  
Scope: no external run, no runtime capability

## Purpose

Phase 7B defines a controlled manual workflow plan for future read-only Demo MarketData snapshot attempts across multiple prepared instruments, while preserving the one-instrument-at-a-time rule.

This phase does not run LMAX, does not request SecurityList, does not request MarketData snapshots, does not replay evidence, and does not make any instrument executable.

## Current State

- EURUSD Demo read-only MarketData workflow is validated and frozen.
- Additional-instrument planning pipeline is complete and frozen.
- Additional-instrument aggregate planning decision is PASS.
- `executableCount=0`.
- `IsApprovedForExternalRun=false` for all additional instruments.
- `canRunExternalSnapshot=false` for all additional instruments.
- `eligibleForManualSnapshotAttempt=false` for all additional instruments.
- API/Worker remain `FakeLmaxGateway` only.
- Runtime shadow replay submit remains absent.
- Scheduler/polling remains absent.
- Order submission, real gateway registration, and trading mutation remain absent.

## Proposed Instrument Sequence

1. GBPUSD / GBP/USD / SecurityID 4002
2. EURGBP / EUR/GBP / SecurityID 4003
3. USDJPY / USD/JPY / SecurityID 4004
4. AUDUSD / AUD/USD / SecurityID 4007

GBPUSD is first because it already has one safe outside-market-hours `CompletedWithEmptyBook` result and market-hours retry readiness. EURGBP, USDJPY, and AUDUSD remain pending until the GBPUSD market-hours outcome is reviewed.

## One-Instrument-at-a-Time Rule

Every future attempt must be one instrument only. No multi-instrument batch is allowed. The plan sets:

- `oneInstrumentAtATime=true`
- `maxAttemptsPerInstrument=1`
- `retryRequiresNewPhase=true`
- `marketHoursOnly=true`
- `manualOperatorCommandOnly=true`
- `batchExecutionAllowed=false`

## Future Attempt Prerequisites

Each future instrument attempt requires its own explicit future phase and must require:

- Final readiness PASS for that instrument.
- Operator signoff.
- Explicit manual operator command.
- Market hours.
- One attempt only.
- Existing kill/rollback instructions.
- Sanitized artifact output.

## After Each Attempt

After any future attempt:

- Review the result artifact.
- Map evidence preview if appropriate.
- Run optional manual local replay only if explicitly approved and appropriate.
- Update planning/status reports before considering the next instrument.
- Stop the sequence if the result is failed-safe, empty-book during market hours, unsafe, ambiguous, or missing required diagnostics.

## Stop Conditions

Stop before the next instrument if any of these occur:

- Result is failed-safe and not yet reviewed.
- Result is empty book during market hours and not yet diagnosed.
- Artifact contains sensitive content.
- Any order, scheduler, runtime shadow replay submit, gateway registration, or trading mutation flag is true.
- Instrument identity or SecurityID does not match the expected 400x DemoLondon value.
- Environment is not Demo.
- A multi-instrument batch is attempted.

## What PASS Means

`PASS` means the manual multi-instrument workflow plan is internally complete and safe as a planning artifact.

## What PASS Does Not Authorize

`PASS` does not authorize:

- External LMAX connection.
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

## Scripts

Build the planning artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-controlled-manual-multi-instrument-workflow-plan.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7B controlled manual multi-instrument read-only snapshot workflow plan"
```

Gate the plan:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7b-controlled-manual-workflow-plan-gate.ps1 `
  -WorkflowPlanFile <generated workflow plan file>
```

Both scripts are local-only and do not use credentials.

## Next Recommended Phase

Recommended next phase:

**Phase 7C - GBPUSD Market-Hours Manual Snapshot Attempt Closure / Evidence Workflow**

Use Phase 7C only if a future explicit operator-approved GBPUSD market-hours attempt is run. Otherwise, wait for market hours.

## Phase 7C Closure Workflow

Phase 7C is now implemented as the post-run closure workflow for the future GBPUSD market-hours attempt. It does not run the attempt. After an operator separately runs the existing GBPUSD wrapper and obtains a sanitized artifact, Phase 7C provides:

- `scripts/review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1`
- `scripts/preview-lmax-readonly-gbpusd-market-hours-snapshot-evidence.ps1`
- `scripts/replay-lmax-readonly-gbpusd-market-hours-evidence-preview.ps1`
- `scripts/build-lmax-readonly-gbpusd-market-hours-closure-manifest.ps1`
- `scripts/check-lmax-readonly-runtime-phase7c-gbpusd-closure-gate.ps1`

The closure classifier accepts `CompletedWithBook` as `PASS`, accepts `CompletedWithEmptyBook` or `FailedSafe` as `PASS_WITH_KNOWN_WARNINGS` when all unsafe flags remain false, and fails any artifact with order submission, runtime shadow replay submit, scheduler, mutation, credential leakage, sensitive content, wrong symbol, or wrong SecurityID.

Manual replay remains explicit and local only. Runtime still does not submit to shadow replay, and the sequence remains stopped until the GBPUSD result is reviewed.

## Phase 7D Post-GBPUSD Decision Gate

Phase 7D is implemented as the branch decision after the GBPUSD closure workflow. It does not run any snapshot and does not authorize the next instrument.

Decision rules:

- If no GBPUSD market-hours closure exists, stay pending on GBPUSD.
- If GBPUSD closes `CompletedWithBook` with `PASS`, the next planning candidate is EURGBP.
- If GBPUSD closes `CompletedWithEmptyBook` with `PASS_WITH_KNOWN_WARNINGS`, prepare a controlled GBPUSD retry in a new phase.
- If GBPUSD is failed-safe or unsafe, block the sequence for diagnostics.

All outcomes keep `executableCount=0`, `batchExecutionAllowed=false`, and every run-eligibility flag false. EURGBP remains planning-only until a separate future phase explicitly prepares it.

## Phase 7E Market-Hours Checklist Pack

Phase 7E packages the future GBPUSD market-hours operator procedure into a checklist document and sanitized JSON/Markdown artifact. It preserves the Phase 7B sequence and does not advance to EURGBP.

The checklist records:

- The exact future GBPUSD wrapper command, marked `DO NOT RUN UNTIL MARKET HOURS`.
- Pre-run checks for market hours, credential presence only, FakeLmaxGateway, no scheduler/polling, no runtime shadow replay submit, no order path, final readiness, retry readiness, and Phase 7C scripts.
- During-run one-attempt/no-retry monitoring and kill switch.
- Post-run Phase 7C review/evidence/closure sequence and Phase 7D decision.
- Rollback and explicit non-authorizations.

Phase 7E is not an execution phase and does not authorize automation.

## Phase 7E2 EURGBP Readiness Rehydration

GBPUSD has now closed with `CompletedWithBook` / `PASS`, and Phase 7D selected `ProceedToEurgbpPlanning`. Phase 7E2 rehydrates EURGBP / EUR/GBP / SecurityID `4003` readiness from the frozen planning pipeline and source manifests.

## Phase 7G2 EURGBP Final Pre-Run Gate

Phase 7G2 closes the EURGBP pre-run planning chain by aggregating Phase 7D, Phase 7E2, and Phase 7F2 into one non-executable gate artifact. It confirms that EURGBP is the next selected instrument only after GBPUSD has a `CompletedWithBook` / `PASS` closure.

This gate preserves the workflow rules: one instrument at a time, no batch execution, no scheduler/polling, no runtime shadow replay submit, no orders, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway` only. `PASS` means final pre-run consistency, not execution authorization.

This is not execution approval. EURGBP remains non-executable with `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, `executableCount=0`, and `batchExecutionAllowed=false`.

## Phase 7H Generic One-Instrument Workflow

Phase 7H reduces duplicate per-instrument scripting by adding one generic manual-only wrapper and closure workflow for the supported additional instruments. It preserves the sequencing rule from this plan: one instrument at a time, no batch execution, no loop, no automatic retry, no scheduler/polling, no runtime shadow replay submit, no orders, no gateway registration, and no trading mutation.

The selected immediate candidate remains EURGBP / EUR/GBP / SecurityID `4003`. The future operator command must supply `-Symbol EURGBP`, the Phase 7G2 final pre-run gate file, `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty reason. The wrapper validates the gate before delegating to the isolated manual snapshot prototype path.

Next recommended step: run the EURGBP one-shot manually only if the operator explicitly chooses during market hours, then use the generic Phase 7H review, evidence preview, optional local replay, and closure manifest scripts.

## Phase 7H2 Generic Final Pre-Run Gate Extension

Phase 7H2 keeps the controlled sequence intact while avoiding more bespoke per-instrument pre-run scripts. It adds a generic final pre-run gate builder for supported additional instruments so USDJPY and AUDUSD can receive the same Phase 7H wrapper-compatible gate contract that EURGBP already has.

The sequence remains one instrument at a time. A Phase 7H2 final pre-run gate is a local compatibility and safety artifact only: `externalRunAuthorized=false`, `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, `IsApprovedForExternalRun=false`, and `batchExecutionAllowed=false`. The Phase 7H wrapper still requires explicit operator flags and must continue rejecting generic Phase 6Z-A readiness artifacts.
