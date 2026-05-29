# LMAX Read-Only Demo MarketData Workflow Final Documentation Pack

## Overview

This document is the final documentation pack for the frozen Phase 5 LMAX Read-Only Demo MarketData workflow.

The validated workflow is:

1. Manual Demo EURUSD MarketData snapshot attempts.
2. Sanitized runtime snapshot artifacts.
3. MarketDataOnly evidence preview mapping.
4. Optional explicit local API replay of evidence previews.
5. Stability review.
6. Workflow release review.
7. Audit pack.
8. Operational signoff.
9. Read-only operator status summary.

The Phase 5X operational status is `FrozenManualReadOnly`.

Phase 6Z-D separately freezes the additional-instrument planning pipeline for GBPUSD, EURGBP, USDJPY, and AUDUSD in `docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md`. That Phase 6Z-D pack is planning-only and does not alter the frozen EURUSD Phase 5 workflow.

This workflow is manual-only, Demo-only, read-only, and artifact-audited. It does not authorize scheduler use, automatic polling, runtime shadow replay submit, order submission, gateway registration, production/UAT use, multi-instrument expansion, or trading-state mutation.

The validated frozen state is:

| Area | Final State |
| --- | --- |
| Runtime mode | Manual Demo MarketData only |
| Instrument scope | EURUSD / SecurityID 4001 |
| Snapshot artifacts | 3 sanitized artifacts |
| Evidence previews | 3 MarketDataOnly previews |
| Manual local replays | 3 |
| Replay observations | 0 total |
| Mutation guard | Unchanged |
| API/Worker gateway | FakeLmaxGateway only |
| Operational status | FrozenManualReadOnly |
| Signoff decision | PASS |
| Audit pack decision | PASS |

## Artifacts

All generated runtime workflow artifacts are under ignored `artifacts/` folders. They are local operational artifacts, not source-controlled inputs.

### Primary Artifact Folders

| Artifact Type | Folder |
| --- | --- |
| Sanitized snapshot artifacts | `artifacts/lmax-readonly-runtime-demo-snapshot/` |
| MarketDataOnly evidence previews | `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/` |
| Stability summaries | `artifacts/lmax-readonly-runtime-demo-snapshot/stability/` |
| Workflow manifests | `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/` |
| Audit packs | `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/` |
| Readiness, signoff, and gate reports | `artifacts/readiness/` |

### Validated Stability Set

The Phase 5O stability run summary is:

`artifacts/lmax-readonly-runtime-demo-snapshot/stability/lmax-readonly-demo-snapshot-stability-20260508-144517.json`

It references 3 successful sanitized snapshot artifacts and 3 mapped MarketDataOnly evidence previews:

| Attempt | Snapshot Artifact | Evidence Preview | Validation |
| --- | --- | --- | --- |
| 1 | `artifacts/lmax-readonly-runtime-demo-snapshot/lmax-readonly-demo-snapshot-result-20260508-144505.json` | `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/lmax-readonly-demo-snapshot-evidence-preview-20260508-144506.json` | PASS |
| 2 | `artifacts/lmax-readonly-runtime-demo-snapshot/lmax-readonly-demo-snapshot-result-20260508-144510.json` | `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/lmax-readonly-demo-snapshot-evidence-preview-20260508-144511.json` | PASS |
| 3 | `artifacts/lmax-readonly-runtime-demo-snapshot/lmax-readonly-demo-snapshot-result-20260508-144515.json` | `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/lmax-readonly-demo-snapshot-evidence-preview-20260508-144517.json` | PASS |

The artifacts record snapshot success and safe counts only. They must not be copied into documentation with raw protocol content. Use the validators and review scripts to inspect them.

### Final Review and Signoff Artifacts

| Stage | File | Decision |
| --- | --- | --- |
| Replay-enabled workflow manifest | `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/lmax-readonly-marketdata-workflow-20260508-162327.json` | PASS |
| Phase 5S release manifest | `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json` | PASS_WITH_WARNINGS when replay skipped |
| Phase 5V audit pack JSON | `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json` | PASS |
| Phase 5V audit pack Markdown | `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md` | PASS |
| Phase 5V gate report | `artifacts/readiness/phase5v-final-audit-pack-gate.json` | PASS |
| Phase 5W operational signoff JSON | `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.json` | PASS |
| Phase 5W operational signoff Markdown | `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.md` | PASS |
| Phase 5W gate report | `artifacts/readiness/phase5w-operational-signoff-gate.json` | PASS |
| Phase 5X status JSON | `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.json` | FrozenManualReadOnly |
| Phase 5X status Markdown | `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.md` | FrozenManualReadOnly |
| Phase 5X gate report | `artifacts/readiness/phase5x-operator-summary-gate.json` | PASS |

## Workflow Manifest

The final replay-enabled workflow manifest is:

`artifacts/lmax-readonly-runtime-demo-snapshot/workflow/lmax-readonly-marketdata-workflow-20260508-162327.json`

Summary:

| Field | Value |
| --- | --- |
| ArtifactCount | 3 |
| EvidencePreviewCount | 3 |
| ManualReplayCount | 3 |
| ReplayRequested | true |
| ReplayPerformed | true |
| FinalDecision | PASS |
| RuntimeShadowReplaySubmit | false |
| ExternalConnectionAttempted | false for workflow review |
| ObservationCount | 0 total |
| Mutation guard | Unchanged |

The manifest records local review and replay of already-created MarketDataOnly evidence previews. It does not represent a runtime submit path.

## Safety And Non-Authorizations

### Safety Confirmations

The frozen workflow confirms:

- API/Worker remain `FakeLmaxGateway` only.
- Runtime does not submit to shadow replay.
- Manual replay is explicit local API replay only.
- No scheduler was added.
- No automatic polling was added.
- No order submission was added.
- No trading gateway registration was added.
- No trading-state mutation was added.
- No persistence of live FIX data into trading tables was added.
- No credential values were returned, logged, stored, written to artifacts, or displayed.
- Evidence previews are `MarketDataOnly`.
- Evidence previews contain empty execution/order/trade/protocol arrays.
- Manual local replay produced 0 observations.
- Mutation guards remained unchanged.
- Phase 5X status is `FrozenManualReadOnly`.

### What PASS Authorizes

PASS authorizes recognition that the controlled manual Demo read-only MarketData workflow has been validated and frozen as an operationally documented manual process.

Operators may:

- Inspect the frozen workflow status.
- Validate sanitized snapshot artifacts.
- Validate MarketDataOnly evidence previews.
- Review workflow manifests and audit packs.
- Run optional local manual replay with explicit replay flags and a local API.

### What PASS Does Not Authorize

PASS does not authorize:

- Scheduler.
- Automatic polling.
- Runtime shadow replay submit.
- Order submission.
- `NewOrderSingle`, cancel, replace, TradeCapture, or OrderStatus flows.
- Real LMAX gateway registration in API or Worker.
- Replacing `FakeLmaxGateway`.
- Production or UAT use.
- Multi-instrument expansion.
- Automatic execution.
- Trading-state mutation.
- Credential exposure.

## Commands

Commands below are PowerShell examples. Run them from the repository root:

`C:\Users\phili\source\repos\QQ.Production.Intraday`

### Manual Snapshot And Credential Boundary

| Command | Purpose |
| --- | --- |
| `.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck` | Checks local Demo credential availability by label only. Prints no credential values. |
| `.\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "Operator-approved Demo EURUSD read-only snapshot"` | Manually attempts one Demo EURUSD MarketData snapshot. Requires explicit operator approval. |
| `.\scripts\validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1 -ArtifactFile <snapshot-artifact.json>` | Validates a sanitized snapshot artifact. No external connection. |

### Evidence Preview And Replay

| Command | Purpose |
| --- | --- |
| `.\scripts\preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile <snapshot-artifact.json>` | Maps a sanitized snapshot artifact to MarketDataOnly evidence preview. No replay. |
| `.\scripts\replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile <preview.json>` | Explicit local manual replay through existing local API replay path. No runtime submit. |

### Stability And Workflow Review

| Command | Purpose |
| --- | --- |
| `.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 2 -Reason "Manual Demo EURUSD read-only snapshot stability check"` | Manually runs a capped repeated snapshot stability check. No scheduler or polling. |
| `.\scripts\review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json` | Reviews stability results and referenced artifacts/previews. |
| `.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json` | Reviews artifacts and previews without replay. |
| `.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json -ReplayEvidencePreviews -ConfirmLocalManualReplay` | Explicit local replay of all previews. Runtime still does not submit to shadow replay. |
| `.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -ReplayEvidencePreviews -ConfirmLocalManualReplay -AttemptCount 3 -DelaySeconds 5 -Reason "Manual workflow release review"` | Controlled manual workflow release command. Replay remains explicit local API only. |

### Audit, Signoff, Status, And Gates

| Command | Purpose |
| --- | --- |
| `.\scripts\build-lmax-readonly-marketdata-workflow-audit-pack.ps1 -StabilitySummaryFile artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json -WorkflowManifestFile artifacts\lmax-readonly-runtime-demo-snapshot\workflow\lmax-readonly-marketdata-workflow-20260508-162327.json` | Builds final audit pack from validated stability and replay-enabled workflow manifest. |
| `.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 -AuditPackFile artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json -AuditPackMarkdownFile artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md -SignoffBy "local-operator" -Role "Operator" -Reason "Operational signoff for controlled manual Demo MarketData workflow"` | Creates operational signoff. No external connection or replay. |
| `.\scripts\show-lmax-readonly-marketdata-workflow-status.ps1 -SignoffFile artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json` | Prints frozen workflow status and writes sanitized status report. |
| `.\scripts\check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1 -SignoffFile artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json` | Validates Phase 5X status summary surface. |

### Gate Scripts

Run gates locally. Gates do not make external LMAX attempts by default.

| Phase | Gate |
| --- | --- |
| 5B | `scripts/check-lmax-readonly-runtime-phase5b-prototype-gate.ps1` |
| 5C | `scripts/check-lmax-readonly-runtime-phase5c-credential-gate.ps1` |
| 5D | `scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1` |
| 5E | `scripts/check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1` |
| 5F | `scripts/check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1` |
| 5G | `scripts/check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1` |
| 5H | `scripts/check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1` |
| 5J | `scripts/check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1` |
| 5L | `scripts/check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1` |
| 5M | `scripts/check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1` |
| 5N | `scripts/check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1` |
| 5O | `scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1` |
| 5P | `scripts/check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1` |
| 5Q | `scripts/check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1` |
| 5R | `scripts/check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1` |
| 5S | `scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1` |
| 5T | `scripts/check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1` |
| 5V | `scripts/check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1` |
| 5W | `scripts/check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1` |
| 5X | `scripts/check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1` |

## Phase Decisions Summary

| Phase | Purpose | Decision / Status | Known Warnings Or Resolved Issues |
| --- | --- | --- | --- |
| 5B | Dedicated manual Demo snapshot prototype boundary | CLOSED | Prototype initially blocked before socket/logon until credential boundary hardened. |
| 5C | Credential resolver hardening / redaction gate | CLOSED | Credential availability is label-only and redacted. |
| 5D | Manual Demo snapshot socket-capable prototype behind credential gate | CLOSED | Credentials missing in initial environment, so run blocked safely. |
| 5E | Transport failure, credential-missing, retry hardening | CLOSED | Retry metadata disabled by default. No automatic retry. |
| 5F | Operator-approved manual Demo snapshot attempt / sanitized result capture | CLOSED | First run reached logon but timed out before snapshot; sanitized artifact produced. |
| 5G | Snapshot timeout diagnostics | CLOSED | Diagnostics added without raw sensitive FIX. |
| 5H | MarketDataRequest compatibility hardening | CLOSED | Known rejected LMAX request profiles encoded and avoided by default. |
| 5I | Operator-approved SnapshotPlusUpdates diagnostic | CLOSED | FailedSafeLogonRejected observed; no snapshot request sent. |
| 5J | Demo MarketData logon diagnostics / session profile alignment | CLOSED | Logon/profile diagnostics added. |
| 5K | Operator profile correction and successful snapshot | CLOSED | Wrong local environment variable corrected by operator; Demo snapshot completed. |
| 5L | Successful snapshot artifact review / closure | CLOSED | Successful artifact validation PASS. |
| 5M | Evidence preview mapping, no shadow submit | CLOSED | MarketDataOnly preview validates; no runtime shadow replay submit. |
| 5N | Manual MarketDataOnly evidence replay dry-run | CLOSED | Manual local replay completed with 0 observations and mutation unchanged. |
| 5O | Repeated manual snapshot stability check | CLOSED | 3/3 successful snapshots; no failed-safe attempts. |
| 5P | Stability results review / readiness decision | CLOSED | Decision PASS for stability review. |
| 5Q | Controlled manual workflow hardening | CLOSED | PASS_WITH_WARNINGS when replay omitted; warning was expected. |
| 5R | Workflow review with optional replay | CLOSED | Replay-enabled manifest PASS with 3/3 local replays. |
| 5S | Controlled manual workflow release gate | CLOSED | PASS_WITH_WARNINGS for release manifest where replay was intentionally skipped. |
| 5T | Runbook freeze | CLOSED | Documentation/runbook freeze PASS. |
| 5U | Optional local replay completion | CLOSED | Replay-enabled workflow PASS; warning resolved for replay-enabled manifest. |
| 5V | Final audit pack | CLOSED | Audit pack PASS. |
| 5W | Operational signoff / workflow freeze | CLOSED | Operational signoff PASS. |
| 5X | Operator console summary / status panel | CLOSED | Operational status FrozenManualReadOnly; gate PASS. |

## Operator Guidance

### Reading The Frozen Status

Use:

```powershell
.\scripts\show-lmax-readonly-marketdata-workflow-status.ps1 `
  -SignoffFile artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
```

Expected status:

- `OperationalStatus=FrozenManualReadOnly`
- `SignoffDecision=PASS`
- `AuditPackDecision=PASS`
- `ManualReplayCount=3`
- `TotalObservationCount=0`
- `RuntimeShadowReplaySubmit=false`
- `CredentialValuesReturned=false`
- `ApiWorkerGatewayMode=FakeLmaxGateway`

The optional UI/API status surface is:

`GET /lmax-readonly-runtime/marketdata-workflow/status`

It is read-only. It does not connect to LMAX, read credentials, replay evidence, or mutate state.

### Interpreting The Status Panel

The operator status panel displays:

- Frozen/PASS workflow status.
- Signoff and audit decisions.
- Artifact, preview, and manual replay counts.
- Total replay observation count.
- Runtime shadow replay submit flag.
- Credential values returned flag.
- API/Worker gateway mode.
- What the workflow authorizes.
- What it explicitly does not authorize.

No panel control starts LMAX, schedules polling, submits replay from runtime, or submits orders.

### Running Optional Local Replay

Optional local replay remains explicit and manual:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 `
  -StabilitySummaryFile artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json `
  -ReplayEvidencePreviews `
  -ConfirmLocalManualReplay
```

Expected replay result for MarketDataOnly previews:

- `Completed`
- `ObservationCount=0`
- `BlockingObservationCount=0`
- `WarningObservationCount=0`
- Mutation guard unchanged

### Stop Conditions

Stop and review immediately if any of these appear:

- Any credential value in output, logs, docs, artifacts, or reports.
- Any non-redacted raw FIX logon or password tag.
- Any order-capable message path.
- Any scheduler or polling behavior.
- Any runtime shadow replay submit.
- Any gateway registration other than `FakeLmaxGateway`.
- Any nonzero replay observation count for MarketDataOnly evidence.
- Any trading-state mutation.

### Rollback Guidance

If a process is stopped or fails:

1. Stop the local process.
2. Clear Phase 5 shell variables from the current shell.
3. Do not start scheduler or Worker automation.
4. Verify API health reports `FakeLmaxGateway` if API is running.
5. Re-run the relevant local gate.
6. Re-run Phase 5O or Phase 5X gate only after reviewing the failure.
7. No DB rollback is expected because this workflow does not mutate trading state.

## Developer And Technical Notes

### Artifact Validation

Snapshot artifact validation verifies:

- Status is `Completed` or accepted successful closure status.
- Snapshot was received.
- Logon and logout succeeded where present.
- `orderSubmissionAttempted=false`.
- `shadowReplaySubmitAttempted=false`.
- `tradingMutationAttempted=false`.
- `schedulerStarted=false`.
- `credentialValuesReturned=false`.
- `noSensitiveContent=true`.
- `redactionStatus=Redacted`.
- Instrument scope is EURUSD / SecurityID 4001.
- Best bid, best ask, and mid fields are present when snapshot is successful.
- Forbidden sensitive strings are absent.

Validator/script:

- `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactValidator.cs`
- `scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1`

### Evidence Preview Mapping

Evidence preview mapping converts a sanitized snapshot artifact to:

- `schemaVersion=lmax-fix-lifecycle-evidence-v1`
- `captureMode=RuntimeDemoReadOnlySnapshotPreview`
- `evidenceMode=MarketDataOnly`
- Empty execution/order/trade/protocol arrays
- `noSensitiveContent=true`

Mapper/script:

- `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs`
- `scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1`

### Manual Replay Behavior

Manual replay uses the existing local shadow replay endpoint only when explicitly requested. Runtime code does not submit to shadow replay.

Replay expected result for the frozen MarketDataOnly previews:

- Replay status `Completed`.
- Observation counts all zero.
- Mutation guard unchanged.

Scripts:

- `scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1`
- `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1`

### Workflow Manifest Composition

Workflow manifests record:

- Snapshot artifact paths.
- Evidence preview paths.
- Artifact validation results.
- Evidence preview validation results.
- Optional manual replay results.
- Runtime shadow replay submit flag.
- External connection flag for workflow review.
- Final decision.

The replay-enabled manifest is:

`artifacts/lmax-readonly-runtime-demo-snapshot/workflow/lmax-readonly-marketdata-workflow-20260508-162327.json`

### Audit Pack Composition

The audit pack gathers:

- Stability summary.
- Snapshot artifact references.
- Evidence preview references.
- Replay-enabled workflow manifest.
- Manual replay results.
- Safety confirmations.
- Final audit decision.

Audit pack files:

- `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json`
- `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md`

### Operational Signoff

Operational signoff freezes the controlled manual Demo MarketData workflow as validated.

Signoff files:

- `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.json`
- `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.md`

### Operator Status Summary

Phase 5X status summary reads the operational signoff and audit state and exposes a read-only status through script, API, and UI.

Summary script:

- `scripts/show-lmax-readonly-marketdata-workflow-status.ps1`

API endpoint:

- `GET /lmax-readonly-runtime/marketdata-workflow/status`

UI panel:

- `LMAX Read-Only Demo MarketData Workflow`

## References

### Core Documentation

| Document | Purpose |
| --- | --- |
| `docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md` | Phase 5 preflight and first transport boundary. |
| `docs/LMAX_READONLY_RUNTIME_PHASE_GATES.md` | Phase gate status and safety boundaries. |
| `docs/LMAX_READONLY_RUNTIME_PHASE5P_STABILITY_DECISION.md` | Stability decision document. |
| `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md` | Controlled manual workflow review. |
| `docs/LMAX_READONLY_RUNTIME_OPERATIONAL_SIGNOFF.md` | Operational signoff and workflow freeze. |
| `docs/LOCAL_RUNBOOK.md` | Local operational commands and runbook. |
| `docs/OPERATOR_MANUAL.md` | Operator-facing guidance. |
| `docs/DEVELOPER_GUIDE.md` | Developer-facing implementation and validation notes. |
| `docs/OPERATIONAL_READINESS_CHECKLIST.md` | Readiness checklist. |

### Source Code References

| Area | Path |
| --- | --- |
| Snapshot artifact validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactValidator.cs` |
| Evidence preview mapper | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs` |
| Stability closure validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotStabilityClosureValidator.cs` |
| Workflow validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowManifest.cs` |
| Audit pack validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowAuditPack.cs` |
| Operational signoff validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataOperationalSignoff.cs` |
| Status summary validator | `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowStatusSummary.cs` |

### Final Reports

| Report | Path |
| --- | --- |
| Phase 5V final audit pack gate | `artifacts/readiness/phase5v-final-audit-pack-gate.json` |
| Phase 5W operational signoff gate | `artifacts/readiness/phase5w-operational-signoff-gate.json` |
| Phase 5X operator summary gate | `artifacts/readiness/phase5x-operator-summary-gate.json` |
| Phase 5X workflow status JSON | `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.json` |
| Phase 5X workflow status Markdown | `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.md` |

## Final Frozen State

The controlled manual Demo MarketData workflow is frozen as:

- Manual-only.
- Demo-only.
- EURUSD / SecurityID 4001 only.
- Read-only MarketData only.
- Validated by 3 sanitized snapshot artifacts.
- Mapped to 3 MarketDataOnly evidence previews.
- Replayed manually through local API 3 times.
- Completed with 0 observations.
- Completed with mutation guards unchanged.
- Signed off with PASS.
- Exposed through read-only status as `FrozenManualReadOnly`.

No scheduler, polling, runtime shadow replay submit, order submission, gateway registration, production activation, multi-instrument expansion, credential exposure, or trading-state mutation is authorized by this pack.

## Phase 6A Next Boundary

Phase 6A adds the planning boundary after this final documentation pack. It is documented in:

- `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`
- `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`
- `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`

Recommended next phase:

**Phase 6B — Manual Additional MarketData Instrument Allowlist Design, No External Run**

This recommendation is conservative. It keeps the next phase design-only, preserves the frozen manual workflow, and avoids scheduler, polling, runtime shadow replay submit, order submission, gateway registration, and trading-state mutation.
## Phase 7A Boundary Reference

Phase 7A keeps the validated EURUSD Demo read-only MarketData workflow frozen. The workflow remains a manual Demo MarketData evidence workflow with no scheduler/polling, no runtime shadow replay submit, no orders, no real gateway registration, and no trading mutation.

The next architecture boundary is documented in `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`. Its recommended next phase is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run, which does not change the EURUSD workflow freeze.

## Phase 7C Reference

Phase 7C adds closure tooling for a future GBPUSD market-hours result artifact only. It does not alter the frozen EURUSD workflow and does not introduce scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

Any GBPUSD evidence preview or optional manual replay remains separate from the EURUSD frozen record and must stay MarketDataOnly and sanitized.
