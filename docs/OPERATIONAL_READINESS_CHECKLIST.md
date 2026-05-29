# QQ.Production.Intraday - Operational Readiness Checklist

## Phase 6A Planning Boundary Checklist

- [ ] `docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md` exists.
- [ ] `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md` exists.
- [ ] `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md` exists.
- [ ] Phase 5V audit pack decision is `PASS`.
- [ ] Phase 5W operational signoff decision is `PASS`.
- [ ] Phase 5X workflow status is `FrozenManualReadOnly`.
- [ ] API/Worker remain `FakeLmaxGateway` only.

## Phase 7E2 Readiness Addendum

- [ ] EURGBP readiness rehydration artifact exists.
- [ ] Corrected Phase 7D decision is `ProceedToEurgbpPlanning`.
- [ ] Selected instrument is EURGBP / EUR/GBP / SecurityID `4003`.
- [ ] `canRunExternalSnapshot=false`.
- [ ] `IsApprovedForExternalRun=false`.
- [ ] `eligibleForManualSnapshotAttempt=false`.
- [ ] One-instrument-at-a-time remains true.
- [ ] Batch execution remains false.
- [ ] `executableCount=0`.
- [ ] External connection, snapshot, replay, order, shadow replay submit, scheduler, and mutation flags remain false.
- [ ] API/Worker remain `FakeLmaxGateway` only.
- [ ] No scheduler or polling has been added for the LMAX runtime workflow.
- [ ] Runtime still does not submit to shadow replay.
- [ ] No order submission or order command surface has been added.
- [ ] No gateway registration has been added.
- [ ] No trading-state mutation has been added.
- [ ] Phase 6B requires a separate explicit prompt.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6a-planning-gate.ps1
```

PASS means the planning boundary is documented and ready for a separate Phase 6B design prompt. It does not authorize an external run, scheduler, polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

## Phase 6B Instrument Allowlist Checklist

- [ ] `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentAllowlist.cs` exists.
- [ ] `LmaxReadOnlyInstrumentAllowlistValidator` exists.
- [ ] Candidate list includes additional instruments beyond EURUSD / `4001`.
- [ ] Candidate instruments are Demo-only.
- [ ] Candidate SecurityID labels are marked as requiring confirmation.
- [ ] Candidate instruments are not approved for external runs.
- [ ] Evidence mode is `MarketDataOnly`.
- [ ] Unit tests cover metadata completeness and unsafe runtime flags.
- [ ] `scripts/check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1` returns PASS.
- [ ] No scheduler, polling, runtime shadow replay submit, order submission, gateway registration, or trading mutation was added.

## 1. Purpose

This checklist is the institutional release gate before moving `QQ.Production.Intraday` into a more advanced phase. It confirms that the local platform is buildable, testable, documented, safe, and still inside the approved FakeLmax-only runtime boundary.

This gate is not a production launch approval. It is a controlled readiness checkpoint for the next technical phase.

## 2. Current Approved Safety State

The approved state for this phase is:

- API and Worker remain `FakeLmaxGateway` only.
- `liveTradingEnabled=false` in the main runtime.
- `externalConnectionsEnabled=false` in the main runtime.
- No real LMAX gateway is registered in API or Worker.
- No credential forms exist in the UI.
- No live order controls exist in the main runtime.
- Shadow Reader is disabled and no-op.
- Read-only runtime LMAX adapter remains design-only/disabled; no runtime FIX reader is registered.
- Read-only runtime adapter implementation status is `Phase 5A / First Transport Prototype Preflight, Still No Socket`; it remains skeleton/fake/preview/transport-interface/config/credential-boundary/venue-label/run-intent/preflight/report/signoff/audit/snapshot/release-gate/kill-rollback-plan-only, disabled/blocked outside explicit local fake-preview config, and non-mutating.
- Phase 4/5A preflight boundaries are documentation/tests/config-gates only; Phase 4A adds contracts/stubs, Phase 4B adds fake in-memory transport only, Phase 4C adds sanitized preview mapping only, Phase 4D adds a local fake-preview endpoint only, Phase 4E adds a blocked skeleton only, Phase 4F adds a disabled guarded transport boundary only, Phase 4G adds a typed inactive config envelope only, Phase 4H adds a disabled credential-profile resolver only, Phase 4I adds a disabled/static venue-profile registry only, Phase 4J adds a validate-only run-intent envelope only, Phase 4K adds a validate-only preflight endpoint only, Phase 4L adds a no-network dry-run report endpoint only, Phase 4M adds a metadata-only signoff endpoint only, Phase 4N adds a metadata-only pre-activation audit endpoint only, Phase 4O adds a metadata-only readiness snapshot endpoint only, Phase 4P adds a final local no-socket release gate only, Phase 5A adds first-transport kill/rollback planning only, and functional transport implementation has not started.
- Shadow Replay is local, offline, and non-mutating.
- Connectivity Lab is the only place where external LMAX FIX may be used, and only through explicit lab scripts and safety flags.
- Generated evidence files are lab artifacts and must not be committed.

## 3. Required Validation Commands

Backend:

```powershell
dotnet restore QQ.Production.Intraday.sln --configfile NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
dotnet build QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
dotnet test QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false
```

Frontend:

```powershell
cd src\QQ.Production.Intraday.Ui
npm.cmd run typecheck
npm.cmd run build
npm.cmd test
```

Local shadow/evidence checks, with API running at `http://localhost:5050`:

```powershell
.\scripts\smoke-lmax-shadow-local.ps1
.\scripts\smoke-lmax-shadow-reader-local.ps1
.\scripts\smoke-lmax-evidence-coverage-local.ps1
.\scripts\smoke-lmax-readonly-runtime-fake-local.ps1
```

Evidence fixture validation:

```powershell
Get-ChildItem .\tests\fixtures\lmax-shadow\*.json | ForEach-Object {
  .\scripts\validate-lmax-lab-evidence-file.ps1 -EvidenceFile $_.FullName
}
```

Optional replay of a captured local evidence file, if present:

```powershell
.\scripts\replay-lmax-lab-evidence-file.ps1 -EvidenceFile .\artifacts\lmax-lab\evidence\<file>.json
```

One-command gate:

```powershell
.\scripts\run-operational-readiness-gate.ps1
```

## 4. Safety Gate Checklist

- [ ] `/health` reports `executionGateway=FakeLmaxGateway`.
- [ ] `/health` reports `liveTradingEnabled=false`.
- [ ] `/health` reports `externalConnectionsEnabled=false`.
- [ ] Shadow Reader status is `Disabled`.
- [ ] Shadow Reader run is blocked safely by default.
- [ ] Read-only runtime diagnostic endpoints remain disabled/blocked by default.
- [ ] Read-only runtime adapter implementation plan is followed before any future runtime read-only code.
- [ ] No read-only runtime phase proceeds unless the prior phase gate passes.
- [ ] Phase 1 inert runtime interface layer is present and remains disabled/no-op.
- [ ] Phase 2 fake/in-memory adapter remains fixture-only.
- [ ] Phase 3 manual endpoint requires a reason, rejects unsafe fixture names, and remains local diagnostic only.
- [ ] Phase 3.5 fake-enabled preview is proven only by explicit test/local configuration; default runtime remains `Disabled` / `DesignOnly`.
- [ ] Phase 3.5 does not submit shadow replay evidence unless a future gate explicitly enables a safe local path.
- [ ] Phase 4 preflight script passes and confirms Phase 4 remains blocked/not implemented.
- [ ] Phase 4A external session contract/stub exists and remains disabled/not started.
- [ ] Phase 4A external session contracts expose no order-submission methods/events and no credential-shaped DTO fields.
- [ ] Phase 4B fake transport harness exists and remains no-network/in-memory only.
- [ ] Phase 4B fake transport creates no evidence and submits nothing to shadow replay.
- [ ] Phase 4C fake transport evidence preview mapper exists and remains no-shadow-submit/no-persistence.
- [ ] Phase 4C mapped evidence validates and contains no credentials or order-command surface.
- [ ] Phase 4D fake transport preview endpoint exists and remains default blocked/no-shadow-submit.
- [ ] Fake transport preview uses only predefined scenarios and accepts no raw FIX, host/user/password fields, credentials, external URLs, or order controls.
- [ ] Phase 4E external read-only session skeleton exists and reports socket activation, FIX logon, credential use, order submission, shadow replay submit, scheduler, gateway registration, and trading mutation as not implemented/false.
- [ ] Phase 4F guarded transport interface exists, disabled transport blocks connect/read/disconnect, and no real network transport exists.
- [ ] Phase 4G config envelope exists, sample config is inactive, and no credential values or host/user/password fields exist.
- [ ] Phase 4H credential-profile boundary exists, resolver is disabled/no-op, `CredentialProfileName` is label-only, and no credential values are read, used, stored, logged, or returned.
- [ ] Phase 4I venue-profile boundary exists, registry descriptors are inactive/disabled, `VenueProfileName` is label-only, and no host/port/user/account/session/endpoint values are exposed.
- [ ] Phase 4J run-intent envelope exists, manual reason/operator id are required, `FutureExternalReadOnlyManual` remains blocked, and no endpoint/session start path exists.
- [ ] Phase 4K preflight endpoint exists at `POST /lmax-readonly-runtime/external-run-intent/validate`, requires a reason, returns blocked/validate-only diagnostics, and reports no session start, external connection, credential read, shadow replay submit, or trading mutation.
- [ ] Phase 4L dry-run report endpoint exists at `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`, requires a reason, aggregates disabled-boundary statuses, and reports no session start, external connection, credential read, shadow replay submit, or trading mutation.
- [ ] Phase 4M signoff endpoint exists at `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`, requires a reason/signer/attestations, cannot authorize execution, and reports no session start, external connection, credential read, shadow replay submit, or trading mutation.
- [ ] Phase 4N pre-activation audit endpoint exists at `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`, requires a reason plus safe intent/report/signoff summaries, cannot authorize execution, and reports no session start, external connection, credential read, shadow replay submit, or trading mutation.
- [ ] Phase 4O readiness snapshot endpoint exists at `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`, requires a reason, cannot start a session, and reports no external connection, credential read, shadow replay submit, or trading mutation.
- [ ] Phase 4P final no-socket release gate exists at `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`, writes only an ignored readiness report, and adds no socket/network/execution capability.
- [ ] Phase 5A preflight exists at `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1`, verifies the kill/rollback plan and first-transport entry criteria, and adds no socket/network/execution capability.
- [ ] No credentials appear in UI/API responses/evidence fixtures.
- [ ] No generated evidence files are committed.
- [ ] Local scheduler auto-run remains disabled unless explicitly testing local scheduler metadata.
- [ ] No real LMAX gateway is registered in API/Worker.
- [ ] No order submission path exists in the main runtime.

## 5. LMAX Shadow/Evidence Checklist

- [ ] Evidence fixtures validate.
- [ ] Evidence modes are covered:
  - `EmptyReadOnly`
  - `MarketDataOnly`
  - `TradeCaptureOnly`
  - `OrderStatusOnly`
  - `ProtocolRejectOnly`
  - `MixedReadOnly`
  - `SyntheticLifecycle`
- [ ] Real captured `TradeCaptureOnly` evidence, if present, validates and replays successfully.
- [ ] Replay mutation guard remains unchanged for orders, fills, and positions where endpoints are available.
- [ ] Observation DTOs expose `policyCode`, `evidenceMode`, `sourceEventType`, rationale, and suggested operator action.
- [ ] Blocking protocol reject observations create/link exception cases according to policy.
- [ ] Warning observations do not create exception cases by default.

## 6. Operator Console Checklist

- [ ] Top bar shows `SAFE LOCAL / FakeLmax-only`.
- [ ] Current operator is visible.
- [ ] Command Center shows a clear safety notice.
- [ ] LMAX Shadow page shows evidence mode, policy code, rationale, and suggested action.
- [ ] Exceptions page uses clear action language for acknowledge, investigate, resolve, waive, and false positive.
- [ ] Risk Control Center shows do-not-proceed messaging for blocked risk decisions.
- [ ] Detail drawer shows workflow links and keeps raw metadata in an advanced section.

## 7. Documentation Checklist

- [ ] `README.md` links to core docs.
- [ ] Developer Guide is current.
- [ ] Operator Manual is current.
- [ ] Docs Index is current.
- [ ] Local Runbook is current.
- [ ] LMAX Connectivity Lab and Adapter Design docs are current.
- [ ] LMAX Read-Only Runtime Adapter Design doc is current.
- [ ] LMAX Read-Only Runtime Adapter Implementation Plan and Phase Gates docs are current.
- [ ] Adapter Contracts doc is current.
- [ ] New endpoints, scripts, policy codes, or evidence modes are documented in the same change set.

## 8. Known Warnings / Accepted Exceptions

- `NU1903` warnings for `System.Security.Cryptography.Xml` are known and accepted for now.
- SQL Server LocalDB may not work in restricted/sandboxed environments; the expected developer environment is a local Windows machine.
- API-dependent smokes require the API running at the expected localhost URL.
- Generated lab evidence should stay out of git.

## 9. Release Gate Decision

Decision statuses:

- `PASS`: all required validation commands and safety gates pass.
- `PASS WITH KNOWN WARNINGS`: all safety gates pass, and only documented accepted warnings remain.
- `FAIL`: any safety gate fails, required validation fails, or a runtime live pathway appears.

`PASS` is only appropriate when build, tests, frontend validation, evidence validation, local smokes, and safety checks are green. `PASS WITH KNOWN WARNINGS` is appropriate for documented warnings such as `NU1903`, or intentionally skipped API-dependent smokes when API availability was explicitly out of scope for that run.

## 10. Next Phase Eligibility

The next technical phase may start only if:

- This release gate passes or passes with documented accepted warnings.
- Docs are current.
- Smokes are green, or skipped smokes are explicitly documented.
- API/Worker remain FakeLmax-only.
- Read-only runtime adapter remains local fake/diagnostic plus external-session contract/stub/fake-transport/preview only, disabled/blocked by default, with no runtime external connection.
- Future implementation follows the phase plan; Phase 5D socket/logon is limited to the isolated manual Demo EURUSD market-data snapshot script and remains outside API/Worker.
- Phase 5B prototype gate passes: `scripts/check-lmax-readonly-runtime-phase5b-prototype-gate.ps1`.
- Phase 5C credential gate passes: `scripts/check-lmax-readonly-runtime-phase5c-credential-gate.ps1`.
- Phase 5D demo snapshot gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1`.
- Phase 5E failure hardening gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1`.
- Phase 5F manual snapshot gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1`.
- Phase 5G snapshot diagnostics gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1`.
- Phase 5H MarketDataRequest compatibility gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1`.
- Phase 5J logon diagnostics gate passes without making an external attempt: `scripts/check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1`.
- Phase 5L successful snapshot closure gate passes without making an external attempt when a sanitized success artifact is supplied: `scripts/check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1 -ArtifactFile <artifact>`.
- Future live-read-only work remains behind explicit gates, governance, runbook controls, and separate quality checks.
## Phase 5M Evidence Preview Closure

- [ ] Validate the successful snapshot artifact with `scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1`.
- [ ] Generate and validate the local evidence preview with `scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile <artifact>`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1 -ArtifactFile <artifact>`.
- [ ] Confirm the preview is `MarketDataOnly`, contains Demo EURUSD / SecurityID `4001` market data, and has zero execution, order-status, trade-capture, and protocol-reject events.
- [ ] Confirm no shadow replay submit, scheduler, gateway registration, order submission, live FIX persistence, or trading-state mutation was added.

## Phase 5N Manual MarketDataOnly Replay Dry-Run

- [ ] Replay only a validated Phase 5M preview using `scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile <preview>`.
- [ ] Confirm replay status is `Completed`.
- [ ] Confirm `observationCount=0`, `blockingObservationCount=0`, and `warningObservationCount=0`.
- [ ] Confirm order, fill, and internal position counts are unchanged.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1 -EvidencePreviewFile <preview>`.
- [ ] Confirm runtime code still does not submit to shadow replay.

## Phase 5O Repeated Manual Snapshot Stability

- [ ] Run `scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1` before any repeated manual attempt.
- [ ] Confirm the stability script requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, `-ConfirmRepeatedManualSnapshots`, non-empty `-Reason`, and explicit `-AttemptCount`.
- [ ] Confirm `AttemptCount` is capped at 1..5 and `DelaySeconds` is capped at 1..10.
- [ ] Confirm repeated attempts are planned manual attempts, not automatic retry, scheduler, or polling.
- [ ] Confirm successful attempt artifacts are validated and mapped to `MarketDataOnly` previews.
- [ ] Confirm preview replay remains off by default and only runs through the explicit manual `-ReplayEvidencePreviews` flag.
- [ ] Confirm the stability summary is sanitized, ignored by git, and reports no credential values, no order submission, no runtime shadow replay submit, no scheduler, and no trading mutation.

## Phase 5P Stability Readiness Decision

- [ ] Review the operator-run stability summary with `scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile <summary>`.
- [ ] Confirm the review decision is `PASS` for the 3/3 stability summary.
- [ ] Confirm referenced snapshot artifacts validate with the Phase 5L validator.
- [ ] Confirm referenced evidence previews validate as `MarketDataOnly`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1 -StabilitySummaryFile <summary>`.
- [ ] Confirm PASS does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, or production use.

## Phase 5Q Controlled Manual MarketData Evidence Workflow

- [ ] Run `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile <summary>`.
- [ ] Confirm artifact count and evidence preview count match the stability summary.
- [ ] Confirm default workflow review does not replay previews and reports `PASS_WITH_WARNINGS` only because replay is omitted.
- [ ] Confirm optional replay, if used, requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay` and uses only the manual local replay script/API.
- [ ] Confirm the workflow manifest is sanitized, ignored by git, and reports `runtimeShadowReplaySubmit=false`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1 -StabilitySummaryFile <summary>`.
- [ ] Confirm no scheduler, polling, order submission, gateway registration, runtime shadow replay submit, trading mutation, or credential value exposure was added.

## Phase 5R Manual Replay Workflow Review

- [ ] Start only the local API if manual replay is requested; do not run the runtime snapshot prototype.
- [ ] Run `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile <summary> -ReplayEvidencePreviews -ConfirmLocalManualReplay`.
- [ ] Confirm `ManualReplayCount` equals `EvidencePreviewCount`.
- [ ] Confirm each replay result is `Completed`.
- [ ] Confirm each replay has zero observations, zero blocking observations, and zero warning observations.
- [ ] Confirm each replay mutation guard is `Unchanged`.
- [ ] Confirm the manifest reports `runtimeShadowReplaySubmit=false` and `externalConnectionAttempted=false`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1 -WorkflowManifestFile <manifest>`.
- [ ] Confirm PASS does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, external snapshot attempts, or production use.

## Phase 5S Controlled Manual Workflow Release Gate

- [ ] Run `scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 5 -Reason "<reason>"`.
- [ ] Include `-ReplayEvidencePreviews -ConfirmLocalManualReplay` only when local API replay is explicitly intended.
- [ ] Confirm the release manifest is written to `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json`.
- [ ] Confirm artifact count equals evidence preview count.
- [ ] Confirm all referenced artifacts validate through the Phase 5L validator.
- [ ] Confirm all referenced previews validate as `MarketDataOnly`.
- [ ] Confirm skipped replay yields `PASS_WITH_WARNINGS`, or replayed manifests have replay count equal preview count and all replay observations zero.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1`.
- [ ] Confirm no scheduler, polling, order submission, gateway registration, runtime shadow replay submit, external snapshot attempt, trading mutation, or credential value exposure was added.

## Phase 5T Controlled Manual Workflow Runbook Freeze

- [ ] Confirm `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md` exists.
- [ ] Confirm the frozen runbook documents prerequisites, commands, optional replay, rollback, stop conditions, and decision semantics.
- [ ] Confirm Phase 5S manifest exists and is `PASS` or `PASS_WITH_WARNINGS`.
- [ ] Confirm Phase 5S report exists and warnings, if any, are limited to optional replay skipped.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1`.
- [ ] Confirm no external socket attempt was made by the gate.
- [ ] Confirm no manual replay was performed by the gate.
- [ ] Confirm no scheduler/polling, runtime shadow replay submit, order path, gateway registration, trading mutation, or credential value exposure was added.

## Phase 5V Final Audit Pack Gate

- [ ] Confirm replay-enabled workflow manifest exists and has `FinalDecision=PASS`.
- [ ] Run `scripts/build-lmax-readonly-marketdata-workflow-audit-pack.ps1 -StabilitySummaryFile <summary> -WorkflowManifestFile <manifest>`.
- [ ] Confirm audit pack JSON and Markdown are written under ignored audit-pack artifacts.
- [ ] Confirm ArtifactCount, EvidencePreviewCount, and ManualReplayCount are all `3`.
- [ ] Confirm total observation count is `0`.
- [ ] Confirm all mutation guards are `Unchanged`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1 -AuditPackFile <audit-pack>`.
- [ ] Confirm no scheduler/polling, runtime shadow replay submit, order path, gateway registration, trading mutation, external LMAX run, manual replay, or credential value exposure was added by Phase 5V.
## Phase 5W Operational Signoff Checklist

- Phase 5V audit pack JSON decision is `PASS`.
- Artifact count is greater than zero.
- Evidence preview count equals artifact count.
- Manual replay count equals evidence preview count.
- Total observation count is zero.
- `runtimeShadowReplaySubmit=false`.
- `externalConnectionAttempted=false` for the audit/signoff workflow.
- `credentialValuesReturned=false`.
- `orderSubmissionAttempted=false`.
- `shadowReplaySubmitAttempted=false`.
- `tradingMutationAttempted=false`.
- `schedulerStarted=false`.
- API/Worker remain `FakeLmaxGateway` only.
- No scheduler, polling, runtime replay submit, order surface, gateway registration, or trading mutation source was added.

Run:

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md -SignoffBy "local-operator" -Role "Operator" -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

`PASS` freezes the controlled manual Demo read-only MarketData workflow as a documented process only. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.

## Phase 5X Operator Summary Checklist

- `GET /lmax-readonly-runtime/marketdata-workflow/status` returns a read-only status summary or a clear `NotAvailable` state.
- `scripts/show-lmax-readonly-marketdata-workflow-status.ps1` reads the Phase 5W signoff and writes sanitized ignored status artifacts.
- The LMAX Shadow UI panel shows frozen workflow status and what is not authorized.
- No live controls, credential fields, host/port fields, scheduler controls, replay submit button, order button, or gateway activation path are added.
- `runtimeShadowReplaySubmit=false`, `credentialValuesReturned=false`, and API/Worker remain `FakeLmaxGateway`.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
```

## Phase 6D SecurityID Discovery Planning Checklist

- [ ] Confirm `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.cs` exists.
- [ ] Confirm GBPUSD, USDJPY, EURGBP, and AUDUSD each have a Phase 6D placeholder candidate SecurityID.
- [ ] Confirm every `IsApprovedForExternalRun` value remains `false`.
- [ ] Confirm the manifest safety markers show no external connection, external API call, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, or trading mutation.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1`.
- [ ] Confirm the report `artifacts/readiness/phase6d-securityid-discovery-gate.json` is written.
- [ ] Confirm PASS does not authorize a snapshot, replay, scheduler, polling, order path, gateway registration, production/UAT, or trading mutation.

## Phase 6E SecurityID Evidence Review Checklist

- [ ] Confirm `LmaxReadOnlyInstrumentSecurityIdSourceEvidence` exists.
- [ ] Confirm `LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator` exists.
- [ ] Confirm GBPUSD, USDJPY, EURGBP, and AUDUSD each have an evidence review record.
- [ ] Confirm current pending records are `NeedsMoreEvidence`.
- [ ] Confirm accepted records, if any, use non-placeholder SecurityIDs with reviewed source references.
- [ ] Confirm every record keeps `IsApprovedForExternalRun=false`.
- [ ] Confirm `noSensitiveContent=true`.
- [ ] Confirm no credential-shaped strings, raw FIX password tags, order authorization, Production authorization, or UAT authorization appear.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1`.
- [ ] Confirm the report `artifacts/readiness/phase6e-securityid-evidence-review-gate.json` is written.
- [ ] Confirm `PASS_WITH_KNOWN_WARNINGS` is acceptable only for pending/needs-more-evidence status.
- [ ] Confirm the phase does not connect to LMAX, call external APIs, run snapshots, run replay, schedule/poll, submit orders, register a gateway, or mutate trading state.

## Phase 6F Confirmation Records Checklist

- [ ] Confirm `LmaxReadOnlyInstrumentSecurityIdConfirmationRecord` exists.
- [ ] Confirm `LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator` exists.
- [ ] Confirm `scripts/new-lmax-readonly-securityid-confirmation-record.ps1` exists.
- [ ] Confirm `scripts/review-lmax-readonly-securityid-confirmation-records.ps1` exists.
- [ ] Confirm sample record exists at `docs/examples/lmax-readonly-securityid-confirmation-record.sample.json`.
- [ ] Confirm records, if any, are stored under ignored `artifacts/lmax-readonly-runtime-securityid-confirmations/`.
- [ ] Confirm accepted records use non-placeholder SecurityIDs.
- [ ] Confirm every record keeps `IsApprovedForExternalRun=false`.
- [ ] Confirm no credential-shaped strings, raw FIX password tags, order authorization, Production authorization, UAT authorization, or external-run approval language appears.
- [ ] Run `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1`.
- [ ] Confirm no LMAX connection, external API call, snapshot, replay, scheduler/polling, order submission, gateway registration, or trading mutation occurred.

## Phase 6H Real Confirmation Records Checklist

- [ ] Confirm real records, if any, are under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`.
- [ ] Confirm `-WhatIfPreview` was used before writing real records.
- [ ] Confirm each `AcceptedForPlanning` record has non-placeholder SecurityID, sanitized evidence reference, captured/reviewed metadata, review reason, and High/Confirmed confidence.
- [ ] Confirm `scripts/review-lmax-readonly-securityid-confirmation-records.ps1` returns `PASS` or safe `PASS_WITH_KNOWN_WARNINGS`.
- [ ] Confirm `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1` writes `artifacts/readiness/phase6h-real-confirmation-records-gate.json`.
- [ ] Confirm `IsApprovedForExternalRun=false` for every record and instrument.
- [ ] Confirm `AcceptedForPlanning` is planning-only and does not authorize external runs.
- [ ] Confirm no LMAX connection, external API call, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.

## Phase 6I SecurityList Discovery Checklist

- [ ] Confirm discovery is operator-approved, Demo-only, read-only, and manual.
- [ ] Confirm automated validation did not run SecurityListRequest.
- [ ] Confirm manual script requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and `-Reason`.
- [ ] Confirm discovery artifact is under `artifacts/lmax-readonly-runtime-securityid-discovery/`.
- [ ] Confirm artifact has `credentialValuesReturned=false`, `orderSubmissionAttempted=false`, `shadowReplaySubmitAttempted=false`, `tradingMutationAttempted=false`, and `schedulerStarted=false`.
- [ ] Confirm all candidate matches keep `isApprovedForExternalRun=false`.
- [ ] Confirm no snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.

## Phase 6J SecurityList Diagnostics Checklist

- [ ] Confirm the failed SecurityList artifact validates as safe and sanitized.
- [ ] Confirm reject text, if present, is redacted.
- [ ] Confirm request profiles and `AutoSequence` are documented.
- [ ] Confirm known-rejected profiles are blocked locally unless explicitly allowed.
- [ ] Confirm the Phase 6J gate does not connect to LMAX.
- [ ] Confirm `IsApprovedForExternalRun=false` and all no-order/no-replay/no-mutation flags remain false.

## Phase 6L SecurityList Fallback Checklist

- [ ] Confirm the Phase 6K AutoSequence artifact is sanitized and has `noSensitiveContent=true`.
- [ ] Confirm the review script reports final status, attempted profiles, candidate matches, unmatched candidates, and fallback decision.
- [ ] Confirm missing reject tag/text is reported instead of guessed when the artifact lacks attempt diagnostics.
- [ ] Confirm fallback remains non-authorizing and `IsApprovedForExternalRun=false`.
- [ ] Confirm no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.
- [ ] Confirm next step is vendor/support or other official manual confirmation, unless a future diagnostic retry is explicitly approved.

## Phase 6M CSV SecurityID Records Checklist

- [ ] Confirm uploaded CSVs include `Instrument Name`, `LMAX ID`, and `LMAX symbol`.
- [ ] Confirm selected DemoLondon/NewYork IDs are GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007.
- [ ] Confirm Tokyo 600x IDs are documented but not selected for the current profile.
- [ ] Confirm generated records are `AcceptedForPlanning`, `Confidence=Confirmed`, and `EvidenceSourceType=OfficialLmaxDocument`.
- [ ] Confirm every generated record has `IsApprovedForExternalRun=false` and `noSensitiveContent=true`.
- [ ] Confirm record review returns `PASS` before any planning manifest application.
- [ ] Confirm no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.

## Phase 6N Planning Manifest Checklist

- [ ] Confirm accepted Phase 6M records review is `PASS`.
- [ ] Confirm planning manifest includes GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007.
- [ ] Confirm every planning entry has `securityIdSource=8`, `environmentName=Demo`, and `venueProfileName=DemoLondon`.
- [ ] Confirm every planning entry has `decision=AcceptedForPlanning`, `noSensitiveContent=true`, and `IsApprovedForExternalRun=false`.
- [ ] Confirm the manifest is stored under `artifacts/lmax-readonly-runtime-securityid-planning/`.
- [ ] Confirm no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.

## Phase 6G Record Entry Workflow Checklist

- [ ] Confirm template script exists.
- [ ] Generate templates with `scripts/new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force`.
- [ ] Confirm four templates are written under ignored artifacts.
- [ ] Confirm creation script supports `-WhatIfPreview` and no-overwrite-by-default behavior.
- [ ] Confirm review script prints per-instrument accepted/pending/missing/conflict state.
- [ ] Run `scripts/check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1`.
- [ ] Confirm `PASS_WITH_KNOWN_WARNINGS` is acceptable only when records are missing/pending but safe.
- [ ] Confirm `IsApprovedForExternalRun=false` remains true for all records and templates.
- [ ] Confirm no LMAX connection, external API call, snapshot, replay, scheduler/polling, order submission, gateway registration, or trading mutation occurred.

## Phase 6O Per-Instrument Safety Gate Checklist

- [ ] Confirm `scripts/build-lmax-readonly-additional-instrument-safety-gates.ps1` exists.
- [ ] Confirm `scripts/check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1` exists.
- [ ] Build the safety gate manifest from the Phase 6N planning manifest.
- [ ] Confirm GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 are present with SecurityIDSource=8.
- [ ] Confirm each instrument gate is `PASS`.
- [ ] Confirm aggregate final decision is `PASS`.
- [ ] Confirm `IsApprovedForExternalRun=false` for every instrument.
- [ ] Confirm `eligibleForManualSnapshotAttempt=false` for every instrument.
- [ ] Confirm no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, runtime shadow replay submit, credential exposure, or trading mutation occurred.

## Phase 6P Additional Snapshot Preflight Checklist

- [ ] Confirm `scripts/build-lmax-readonly-additional-instrument-snapshot-preflights.ps1` exists.
- [ ] Confirm `scripts/check-lmax-readonly-runtime-phase6p-additional-snapshot-preflight-gate.ps1` exists.
- [ ] Build the preflight manifest from Phase 6N planning and Phase 6O safety gate manifests.
- [ ] Confirm GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 are present.
- [ ] Confirm request profile is `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, `MarketDepth=1`.
- [ ] Confirm each preflight result is `PASS`.
- [ ] Confirm `canRunExternalSnapshot=false` for every instrument.
- [ ] Confirm `eligibleForManualSnapshotAttempt=false` for every instrument.
- [ ] Confirm `IsApprovedForExternalRun=false` for every instrument.
- [ ] Confirm no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, runtime shadow replay submit, credential exposure, trading-table persistence, or trading mutation occurred.

## Phase 6Q Approval Envelope Checklist

- [ ] Confirm approval envelope model, creation script, review script, and gate script exist.
- [ ] Confirm source Phase 6P preflight manifest is PASS.
- [ ] Confirm any `AcceptedForPlanning` envelope has reviewer, reason, and all planning attestations.
- [ ] Confirm `AcceptedForPlanning` does not authorize a run.
- [ ] Confirm `canRunExternalSnapshot=false`.
- [ ] Confirm `eligibleForManualSnapshotAttempt=false`.
- [ ] Confirm `IsApprovedForExternalRun=false`.
- [ ] Confirm review report is PASS or PASS_WITH_KNOWN_WARNINGS when no envelope exists.
- [ ] Confirm no LMAX connection, snapshot, replay, scheduler/polling, order submission, gateway registration, runtime shadow replay submit, credential exposure, trading-table persistence, or trading mutation occurred.

## Phase 6R Dry-Run Report Checklist

- [ ] Confirm dry-run model, creation script, review script, and gate exist.
- [ ] Confirm report references Phase 6N planning, Phase 6O safety gate, Phase 6P preflight, and Phase 6Q approval envelope.
- [ ] Confirm report is for GBPUSD / GBP/USD / SecurityID 4002.
- [ ] Confirm dry-run decision is `PASS`.
- [ ] Confirm `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false`.
- [ ] Confirm no external connection, snapshot, replay, scheduler, order, shadow replay submit, gateway registration, or trading mutation was attempted.
### Phase 6S Readiness

- Phase 6S attempt gate model, creation script, review script, gate script, and tests exist.
- GBPUSD source artifacts are checked for consistent symbol, SecurityID `4002`, SecurityIDSource `8`, Demo environment, DemoLondon venue profile, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.
- `PASS` is a consistency result only; it is not run approval.
- `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain required.
- No LMAX connection, snapshot, replay, scheduler/polling, order surface, gateway registration, or trading mutation is allowed.

### Phase 6T Readiness

- GBPUSD execution plan document exists.
- Future command template is marked `DO NOT RUN IN PHASE 6T`.
- Abort criteria, kill/rollback steps, and post-run validation requirements are documented.
- `externalRunAuthorized=false`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain required.

### Phase 6U Readiness

- Operator signoff model, creation script, review script, gate script, and tests exist.
- `SignedForPlanning` requires all planning attestations.
- `SignedForPlanning` does not authorize execution.
- All run eligibility and attempt flags remain false.

### Phase 6V Readiness

- Final readiness model, generation script, review script, gate script, and tests exist.
- All GBPUSD artifacts from planning through operator signoff are aggregated.
- `PASS` means final pre-execution readiness only.
- No execution, snapshot, scheduler, replay, order flow, gateway registration, or trading mutation is authorized.

### Phase 6W Readiness

- GBPUSD one-shot wrapper exists.
- Phase 6V final readiness file is required before any connection.
- Result gate exists and is local-only by default.
- Result artifacts must be sanitized and show no orders, no scheduler, no runtime shadow replay submit, no mutation, and no credential values.
## Phase 6X GBPUSD Empty-Book Readiness

- [ ] Review `artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260509-191234.json`.
- [ ] Confirm `CompletedWithEmptyBook`, `snapshotReceived=true`, `entryCount=0`, and zero reject counts.
- [ ] Confirm `orderSubmissionAttempted=false`, `shadowReplaySubmitAttempted=false`, `tradingMutationAttempted=false`, and `schedulerStarted=false`.
- [ ] Confirm `credentialValuesReturned=false`, `noSensitiveContent=true`, and `redactionStatus=Redacted`.
- [ ] Confirm Phase 6X gate result is `PASS_WITH_KNOWN_WARNINGS`.
- [ ] Confirm no automatic retry, replay, scheduler, gateway registration, orders, or trading mutation is introduced.

## Phase 6Y Market-Hours Retry Checklist

- [ ] Confirm Phase 6V final readiness remains `PASS`.
- [ ] Confirm Phase 6X review is `PASS_WITH_KNOWN_WARNINGS` with `CompletedWithEmptyBook`.
- [ ] Confirm Saturday/out-of-market interpretation is documented.
- [ ] Confirm retry is Sunday evening after FX reopen or Monday market hours only.
- [ ] Confirm `retryAttemptCount=1`.
- [ ] Confirm `canRunAutomatically=false`.
- [ ] Confirm no scheduler, polling, background service, timer, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- [ ] Confirm Phase 6Y gate passes before any future Phase 6Z manual attempt.
## Phase 6Z-A Readiness Checklist

- [x] Additional planning pipeline builder exists: `scripts/build-lmax-readonly-additional-instrument-planning-pipeline.ps1`.
- [x] Phase 6Z-A gate exists: `scripts/check-lmax-readonly-runtime-phase6za-additional-instrument-pipeline-gate.ps1`.
- [x] Pipeline includes GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 with SecurityIDSource `8`.
- [x] Safety gate, preflight, approval envelope, dry-run, attempt gate, execution plan, operator signoff, and final readiness decisions are represented for each instrument.
- [x] `executableCount=0`.
- [x] `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` for all instruments.
- [x] No external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation is authorized by this phase.

## Phase 6Z-C Readiness Checklist

- [x] Read-only planning status model exists.
- [x] Read-only planning status script exists: `scripts/show-lmax-readonly-additional-instrument-planning-status.ps1`.
- [x] Read-only endpoint exists: `GET /lmax-readonly-runtime/additional-instruments/planning-status`.
- [x] Operator console panel displays the additional-instrument planning status.
- [x] The panel exposes no external connection, snapshot, replay, scheduler, credential, host/port, gateway, or order controls.
- [x] `executableCount=0`.
- [x] `IsApprovedForExternalRun=false` and `canRunExternalSnapshot=false` remain visible for all instruments.

## Phase 6Z-D Readiness Checklist

- [x] Final additional-instrument planning document exists: `docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md`.
- [x] Documentation pack builder exists: `scripts/build-lmax-readonly-additional-instruments-planning-doc-pack.ps1`.
- [x] Phase 6Z-D gate exists: `scripts/check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1`.
- [x] Documentation pack references the Phase 6Z-A pipeline manifest and Phase 6Z-C planning status report.
- [x] GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 are frozen as planning-only values.
- [x] `executableCount=0`.
- [x] `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` for all instruments.
- [x] No market-hours snapshot attempt was made in Phase 6Z-D.
- [x] No scheduler/polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation is authorized.

## Phase 6Z-E Readiness Checklist

- [x] Market-hours next-action model exists.
- [x] Read-only next-action script exists: `scripts/show-lmax-readonly-market-hours-next-action.ps1`.
- [x] Read-only endpoint exists: `GET /lmax-readonly-runtime/market-hours-next-action`.
- [x] Operator console panel displays the GBPUSD next action.
- [x] Previous GBPUSD result is shown as `CompletedWithEmptyBook` outside market hours.
- [x] Final readiness, retry readiness, and planning freeze are shown as safe source decisions.
- [x] The panel exposes no external connection, snapshot, replay, scheduler, credential, host/port, gateway, or order controls.
- [x] `executableCount=0`.
- [x] `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`.
- [x] No external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation is authorized.
## Phase 7A Readiness Addendum

- [ ] Phase 7A ADR exists: `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`.
- [ ] Phase 7A boundary checklist exists: `docs/LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md`.
- [ ] Phase 7A gate is PASS: `artifacts/readiness/phase7a-next-boundary-gate.json`.
- [ ] Recommended next phase is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run.
- [ ] Scheduler/polling remains absent.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission remains absent.
- [ ] Real gateway registration remains absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.

## Phase 7B Readiness Addendum

- [ ] Phase 7B workflow plan doc exists.
- [ ] Phase 7B workflow plan artifact exists.
- [ ] Phase 7B gate is PASS.
- [ ] Sequence is GBPUSD, EURGBP, USDJPY, AUDUSD.
- [ ] `executableCount=0`.
- [ ] `batchExecutionAllowed=false`.
- [ ] `oneInstrumentAtATime=true` for every instrument.
- [ ] `maxAttemptsPerInstrument=1` for every instrument.
- [ ] `retryRequiresNewPhase=true` for every instrument.
- [ ] `canRunExternalSnapshot=false` for every instrument.
- [ ] `IsApprovedForExternalRun=false` for every instrument.
- [ ] Scheduler/polling remains absent.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission remains absent.
- [ ] Gateway registration remains absent.
- [ ] Trading mutation remains absent.

## Phase 7C Readiness Addendum

- [ ] Phase 7C closure model exists.
- [ ] Phase 7C review script exists.
- [ ] Phase 7C evidence preview script exists.
- [ ] Phase 7C optional manual replay script exists.
- [ ] Phase 7C closure manifest builder exists.
- [ ] Phase 7C gate exists.
- [ ] With no market-hours artifact supplied, Phase 7C gate is `PASS_WITH_KNOWN_WARNINGS`.
- [ ] No Phase 7C script runs LMAX or requests snapshots.
- [ ] Replay is optional, local, explicit, and requires confirmation.
- [ ] Scheduler/polling remains absent.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission remains absent.
- [ ] Gateway registration remains absent.
- [ ] Trading mutation remains absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.

## Phase 7K16 Final Signoff Addendum

- [ ] Phase 7K15 final evidence pack is closed.
- [ ] Phase 7K16 final operator signoff artifact exists.
- [ ] Successful current-cycle additional-instrument evidence includes GBPUSD, EURGBP, and AUDUSD.
- [ ] USDJPY remains parked as a separate troubleshooting rail.
- [ ] External attempts remain stopped for the day.
- [ ] `anyInstrumentExternalRunAllowed=false`.
- [ ] `externalAdditionalInstrumentAttemptsCurrentlyAllowed=false`.
- [ ] `futureExternalRunCanBeConsidered=false`.
- [ ] `directRunAuthorization=false`.
- [ ] No scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.
- [ ] API/Worker remain `FakeLmaxGateway` only.
- [ ] Localhost API health timeout remains a separate optional-replay follow-up, not an LMAX evidence failure.

## Phase 7H Generic Additional Instrument Checklist

## Phase 7H2 Generic Final Pre-Run Gate Checklist

- [ ] Build the selected instrument final pre-run gate with `scripts/new-lmax-readonly-additional-instrument-final-pre-run-gate.ps1`.
- [ ] Confirm the selected instrument is one of GBPUSD `4002`, EURGBP `4003`, USDJPY `4004`, or AUDUSD `4007`.
- [ ] Confirm `oneInstrumentAtATime=true` and `batchExecutionAllowed=false`.
- [ ] Confirm `externalRunAuthorized=false`, `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false`.
- [ ] Confirm no scheduler/polling, runtime shadow replay submit, order submission, gateway registration, or trading mutation flags are true.
- [ ] Confirm API/Worker remain `FakeLmaxGateway`.
- [ ] Confirm generic Phase 6Z-A final-readiness artifacts are not accepted as Phase 7H wrapper gates.
- [ ] Do not run the Phase 7H wrapper unless a human explicitly chooses a future one-instrument Demo read-only attempt.

- [ ] Generic wrapper accepts exactly one of GBPUSD, EURGBP, USDJPY, or AUDUSD.
- [ ] The selected instrument has a `PASS` final pre-run gate before any manual external attempt.
- [ ] `batchExecutionAllowed=false` and no multi-instrument command is used.
- [ ] No automatic retry, scheduler, polling, timer, or hosted service exists.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission, gateway registration, and trading mutation remain absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.

## Phase 7E Readiness Addendum

- [ ] GBPUSD market-hours execution checklist doc exists.
- [ ] Checklist pack builder exists.
- [ ] Phase 7E gate exists.

## Phase 7G2 Readiness Addendum

- [ ] EURGBP final pre-run gate artifact exists.
- [ ] Phase 7G2 gate is `PASS`.
- [ ] Phase 7D decision is `ProceedToEurgbpPlanning`.
- [ ] Phase 7E2 EURGBP readiness is `PASS`.
- [ ] Phase 7F2 EURGBP execution checklist is `PASS`.
- [ ] `canRunExternalSnapshot=false`.
- [ ] `IsApprovedForExternalRun=false`.
- [ ] `eligibleForManualSnapshotAttempt=false`.
- [ ] `oneInstrumentAtATime=true`.
- [ ] `batchExecutionAllowed=false`.
- [ ] No scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.
- [ ] Future command is present and marked `DO NOT RUN UNTIL MARKET HOURS`.
- [ ] Pre-run checks include market hours, credential presence only, `FakeLmaxGateway`, final readiness, Phase 6Y retry readiness, and Phase 7C scripts.
- [ ] During-run monitoring enforces one attempt only and no retry.
- [ ] Kill switch is documented.
- [ ] Post-run sequence includes Phase 7C review, evidence preview, optional local replay, closure manifest, Phase 7C gate, and Phase 7D decision.
- [ ] No scheduler/polling remains.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission remains absent.
- [ ] Gateway registration remains absent.
- [ ] Trading mutation remains absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.

## Phase 7D Readiness Addendum

- [ ] Phase 7D decision model exists.
- [ ] Phase 7D decision script exists.
- [ ] Phase 7D gate exists.
- [ ] Current decision before GBPUSD market-hours closure is `PendingGbpusdMarketHoursAttempt`.
- [ ] `canRunExternalSnapshot=false`.
- [ ] `IsApprovedForExternalRun=false`.
- [ ] `eligibleForManualSnapshotAttempt=false`.
- [ ] `batchExecutionAllowed=false`.
- [ ] `executableCount=0`.
- [ ] EURGBP is selected only after GBPUSD `CompletedWithBook/PASS`.
- [ ] Empty-book warning leads to controlled GBPUSD retry planning, not EURGBP execution.
- [ ] Failed-safe or unsafe GBPUSD result blocks for diagnostics.
- [ ] Scheduler/polling remains absent.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Order submission remains absent.
- [ ] Gateway registration remains absent.
- [ ] Trading mutation remains absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.
