# LMAX Read-Only Runtime — Phase 6 Operationalization Plan

## 1. Purpose

Phase 6A creates the planning boundary after the validated Phase 5 manual Demo EURUSD read-only MarketData workflow.

This phase does not add runtime capability. It does not run LMAX, connect externally, add scheduler or polling behavior, submit to shadow replay from runtime, submit orders, register a gateway, or mutate trading state.

The purpose is to decide the next safe technical frontier and document the gates required before any Phase 6 implementation begins.

## 2. Current Validated Phase 5 State

The Phase 5 workflow is frozen as `FrozenManualReadOnly`.

Validated state:

- 3 successful manual Demo EURUSD / SecurityID `4001` read-only MarketData snapshots.
- 3 sanitized snapshot artifacts.
- 3 `MarketDataOnly` evidence previews.
- 3 explicit manual local replay runs.
- 0 replay observations.
- Mutation guards unchanged.
- Phase 5V audit pack decision `PASS`.
- Phase 5W operational signoff decision `PASS`.
- Phase 5X operator summary status `FrozenManualReadOnly`.
- API/Worker remain `FakeLmaxGateway` only.
- Runtime still does not submit to shadow replay.

Primary references:

- `docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md`
- `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json`
- `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.json`
- `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.json`

## 3. Frozen And Approved

The approved operational workflow is:

1. Operator-approved manual Demo MarketData snapshot.
2. Sanitized snapshot artifact validation.
3. `MarketDataOnly` evidence preview mapping.
4. Optional explicit local manual replay.
5. Workflow manifest review.
6. Audit pack.
7. Operational signoff.
8. Read-only status summary.

The approved workflow is manual-only, Demo-only, read-only, and artifact-audited.

## 4. Explicitly Not Authorized

Phase 5 approval and Phase 6A planning do not authorize:

- Scheduler.
- Automatic polling.
- Runtime shadow replay submit.
- Order submission.
- `NewOrderSingle`, cancel, replace, TradeCapture, or OrderStatus workflows.
- Real LMAX gateway registration in API or Worker.
- Replacing `FakeLmaxGateway`.
- UAT or production use.
- Multi-instrument expansion.
- Automatic execution.
- Trading-state mutation.
- Persisting live FIX data into trading tables.
- Credential exposure in output, docs, reports, tests, artifacts, API responses, or UI.

## 5. Candidate Phase 6 Paths

### A. Manual Workflow Hardening

Scope:

- Additional operational reports.
- Better operator visibility.
- Runbook simplification.
- Audit-pack presentation improvements.

Risk:

- Lowest. No new runtime behavior is required.

Boundary:

- No external run.
- No runtime shadow replay submit.
- No scheduler.
- No polling.
- No order path.

### B. Manual Additional MarketData Instruments

Scope:

- Design an explicit Demo-only instrument allowlist.
- Define per-instrument evidence expectations.
- Define per-instrument artifact, preview, replay, and audit gates.
- Keep the same manual workflow pattern.

Risk:

- Controlled, but higher than pure documentation because it prepares expansion beyond EURUSD.

Boundary:

- Manual only.
- Demo only.
- No scheduler.
- No runtime shadow replay submit.
- No automatic polling.
- No orders.
- No gateway registration.
- No external run until a separate explicit prompt.

### C. Manual Runtime Shadow Replay Submit

Scope:

- Plan a future first runtime-to-shadow boundary.
- Runtime would still be manual and local.
- Runtime would not mutate trading state.

Risk:

- High sensitivity because it introduces a runtime submit path, even if local and non-mutating.

Boundary:

- Requires a separate planning gate before any implementation.
- Requires explicit runtime submit kill switch.
- Requires mutation guard proof.
- Not recommended as the immediate next implementation.

### D. Controlled Read-Only MarketData Service

Scope:

- Future managed service for read-only MarketData sessions.

Risk:

- High, because it introduces scheduler/polling/service lifecycle risk.

Boundary:

- Future only.
- Not recommended yet.

### E. Order / Trading Integration

Scope:

- Any order submission, order status recovery, trade capture, or trading adapter integration.

Risk:

- Out of scope for the read-only MarketData workflow.

Boundary:

- Explicitly forbidden by this plan.
- Requires a separate program, certification plan, and operational signoff.

## 6. Recommended Next Phase

Recommended next phase:

**Phase 6B — Manual Additional MarketData Instrument Allowlist Design, No External Run**

Rationale:

- It extends read-only MarketData coverage planning without scheduler, polling, order, or runtime shadow replay submit risk.
- It preserves the proven Phase 5 manual workflow pattern.
- It requires no external run in the design phase.
- It forces an explicit allowlist before any new instrument is attempted.
- It lets evidence expectations, validators, gates, rollback, and abort conditions be defined before any new runtime activity.

Phase 6B should remain design-only and must not connect to LMAX.

## 7. Required Gates Before Any Phase 6 Implementation

Before implementing any Phase 6 runtime behavior:

- Phase 5Y final documentation pack must exist.
- Phase 5V audit pack must be `PASS`.
- Phase 5W operational signoff must be `PASS`.
- Phase 5X status must be `FrozenManualReadOnly`.
- API/Worker must remain `FakeLmaxGateway` only.
- No scheduler or polling source may be introduced.
- Runtime shadow replay submit must remain absent.
- No order surface may be introduced.
- No gateway registration may be introduced.
- No trading-state mutation dependency may be introduced.
- Any expansion must define:
  - explicit allowlist,
  - manual flags,
  - evidence contract,
  - artifact validation,
  - replay validation,
  - rollback,
  - abort conditions,
  - operator approval language.

## 8. Phase 6A Decision

Phase 6A is a planning-only boundary.

Decision:

- Phase 5 workflow remains frozen.
- No new runtime capability is added.
- The next safest boundary is Phase 6B instrument allowlist design.
- Any Phase 6B implementation requires a separate explicit prompt.

## 9. Phase 6B — Manual Additional MarketData Instrument Allowlist Design, No External Run

Phase 6B designs a candidate allowlist for additional Demo MarketData instruments beyond the validated EURUSD / SecurityID `4001` baseline.

Phase 6B is planning only. It does not connect to LMAX, does not run a snapshot, does not submit to shadow replay, does not schedule or poll, does not submit orders, does not register a gateway, and does not mutate trading state.

### Candidate Allowlist

The initial planning candidates are intentionally marked as requiring Demo SecurityID confirmation before any future external run can be considered. The `SecurityID` values below are planning labels, not approved runnable LMAX identifiers.

| Instrument | Symbol | Slash Symbol | SecurityID | Venue | Liquidity | Demo Readiness | External Run Approved |
| --- | --- | --- | --- | --- | --- | --- | --- |
| GBPUSD | GBPUSD | GBP/USD | TBD-LMAX-DEMO-GBPUSD | LMAX Demo | MajorFxHighLiquidity | CandidateRequiresDemoSecurityIdConfirmation | false |
| USDJPY | USDJPY | USD/JPY | TBD-LMAX-DEMO-USDJPY | LMAX Demo | MajorFxHighLiquidity | CandidateRequiresDemoSecurityIdConfirmation | false |
| EURGBP | EURGBP | EUR/GBP | TBD-LMAX-DEMO-EURGBP | LMAX Demo | MajorCrossFxLiquid | CandidateRequiresDemoSecurityIdConfirmation | false |
| AUDUSD | AUDUSD | AUD/USD | TBD-LMAX-DEMO-AUDUSD | LMAX Demo | MajorFxLiquid | CandidateRequiresDemoSecurityIdConfirmation | false |

### Validation Rules

- Only instruments present in `LmaxReadOnlyInstrumentAllowlist.CandidateEntries` can pass planning validation.
- EURUSD / SecurityID `4001` is excluded because it is the existing validated baseline, not an additional candidate.
- Every candidate must remain `EnvironmentName=Demo`.
- Every candidate must use a Demo read-only venue profile label.
- Every candidate must map to `MarketDataOnly` evidence preview.
- Every candidate must have `IsApprovedForExternalRun=false`.
- Phase 6B safety rules must keep scheduler, polling, runtime shadow replay submit, order submission, gateway registration, external connection approval, trading mutation, and credential values disabled.

### Gate

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1
```

The gate is local-only and writes:

`artifacts/readiness/phase6b-instrument-allowlist-gate.json`

PASS means the allowlist design is documented and locally validated. PASS does not authorize any new external instrument run.

## 10. Phase 6C — Instrument SecurityID Confirmation Workflow, No External Run

Phase 6C adds a local SecurityID manifest for the Phase 6B allowlist.

This phase is planning, documentation, and validation only. It does not connect to LMAX, call external APIs, run snapshots, submit to shadow replay, start scheduler/polling, submit orders, register a gateway, or mutate trading state.

### Local SecurityID Manifest

Manifest:

`src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdManifest.cs`

The manifest maps every Phase 6B candidate symbol to a local Phase 6C SecurityID placeholder and keeps `IsApprovedForExternalRun=false` for every entry.

| Instrument | Phase 6C SecurityID Value | Approved For External Run |
| --- | --- | --- |
| GBPUSD | PHASE6C-DEMO-SECURITYID-GBPUSD | false |
| USDJPY | PHASE6C-DEMO-SECURITYID-USDJPY | false |
| EURGBP | PHASE6C-DEMO-SECURITYID-EURGBP | false |
| AUDUSD | PHASE6C-DEMO-SECURITYID-AUDUSD | false |

These values are local planning placeholders. A later explicit phase must replace or confirm them with approved Demo SecurityID values before any external run can be considered.

### Gate

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6c-securityid-confirmation-gate.ps1
```

The gate writes:

`artifacts/readiness/phase6c-securityid-confirmation-gate.json`

PASS means every Phase 6B symbol has a non-empty manifest value and every `IsApprovedForExternalRun` flag remains false. PASS does not authorize external execution.

## 11. Phase 6D - SecurityID Discovery Planning, No External Run

Phase 6D introduces a second local-only SecurityID discovery manifest for the Phase 6B allowlist. The purpose is to prepare a place for candidate real Demo SecurityID values while keeping every entry blocked from external execution.

This phase remains planning, documentation, and validation only. It does not connect to LMAX, call external APIs, run snapshots, submit to shadow replay, start scheduler/polling, submit orders, register a gateway, or mutate trading state.

### Discovery Manifest

Manifest:

`src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.cs`

Current Phase 6D values are explicit placeholders and are not runnable LMAX identifiers:

| Instrument | Phase 6D Candidate SecurityID | Approved For External Run |
| --- | --- | --- |
| GBPUSD | PHASE6D-DISCOVERY-PENDING-GBPUSD | false |
| USDJPY | PHASE6D-DISCOVERY-PENDING-USDJPY | false |
| EURGBP | PHASE6D-DISCOVERY-PENDING-EURGBP | false |
| AUDUSD | PHASE6D-DISCOVERY-PENDING-AUDUSD | false |

The manifest validator confirms every allowlist symbol has a non-empty candidate value, confirms `IsApprovedForExternalRun=false`, and confirms the local safety markers for external connection, external API call, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, and trading mutation remain false.

### Gate

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1
```

The gate writes:

`artifacts/readiness/phase6d-securityid-discovery-gate.json`

PASS means the placeholder discovery manifest is complete and still blocks external execution. PASS does not authorize any socket run, Demo snapshot, scheduler, polling, runtime shadow replay submit, order path, gateway registration, UAT/production use, or trading mutation.

## 12. Phase 6E - SecurityID Source Evidence Review, No External Run

Phase 6E defines the evidence-review process required before Phase 6D placeholder values can ever be replaced with accepted planning SecurityIDs. It is planning and validation only.

Phase 6E does not connect to LMAX, call external APIs, run snapshots, run replay, add scheduler/polling, submit runtime shadow replay, submit orders, register a gateway, expose credentials, or mutate trading state. Every candidate remains `IsApprovedForExternalRun=false`.

### Evidence Source Types

Accepted source types are:

- `OfficialLmaxDocument`
- `ConnectivityLabSanitizedOutput`
- `OperatorManualConfirmation`
- `VendorSupportConfirmation`
- `Other`

Evidence records must include symbol, slash symbol, proposed SecurityID, source type, source reference, review reason, confidence, decision, `IsApprovedForExternalRun=false`, and `noSensitiveContent=true`.

### Current Candidate Status

Current default status is `NeedsMoreEvidence` for all candidates:

| Instrument | Current Proposed SecurityID | Review Decision | Approved For External Run |
| --- | --- | --- | --- |
| GBPUSD | PHASE6D-DISCOVERY-PENDING-GBPUSD | NeedsMoreEvidence | false |
| USDJPY | PHASE6D-DISCOVERY-PENDING-USDJPY | NeedsMoreEvidence | false |
| EURGBP | PHASE6D-DISCOVERY-PENDING-EURGBP | NeedsMoreEvidence | false |
| AUDUSD | PHASE6D-DISCOVERY-PENDING-AUDUSD | NeedsMoreEvidence | false |

`AcceptedForPlanning` requires a non-placeholder proposed SecurityID, evidence reference, reviewer, reviewed timestamp, and `High` or `Confirmed` confidence. Even accepted planning values remain blocked from external execution until a later explicit phase.

### Gate

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1
```

The gate writes:

`artifacts/readiness/phase6e-securityid-evidence-review-gate.json`

Expected current decision is `PASS_WITH_KNOWN_WARNINGS` because evidence is still pending. `PASS_WITH_KNOWN_WARNINGS` is acceptable only when the warning is that SecurityID source evidence still needs review. It does not authorize any external run.

Next recommended phase:

**Phase 6F - Manual SecurityID Evidence Capture / Operator Confirmation Records, No External Run**

## 13. Phase 6F - Manual SecurityID Confirmation Records, No External Run

Phase 6F creates a local workflow for recording sanitized manual confirmation records for candidate SecurityIDs. It is still planning and validation only.

Phase 6F does not connect to LMAX, call external APIs, run snapshots, run replay, add scheduler/polling, submit runtime shadow replay, submit orders, register a gateway, expose credentials, or mutate trading state. Confirmation records cannot set `IsApprovedForExternalRun=true`.

### Scripts

Create one sanitized record:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 `
  -Symbol GBPUSD `
  -SlashSymbol "GBP/USD" `
  -ProposedSecurityId "<sanitized-demo-security-id>" `
  -EvidenceSourceType OperatorManualConfirmation `
  -EvidenceReference "<sanitized local reference>" `
  -CapturedBy "local-operator" `
  -ReviewedBy "local-reviewer" `
  -ReviewReason "Planning confirmation only; no external run approval" `
  -Confidence High `
  -Decision AcceptedForPlanning
```

Review records:

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
```

Run the Phase 6F gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1
```

### Expected Current Status

If no confirmation records exist yet, the gate returns `PASS_WITH_KNOWN_WARNINGS`. That warning is acceptable because the boundary is safe and manual records have not been captured.

Next recommended phase:

- If valid confirmation records exist for all candidates: **Phase 6G - Apply Accepted SecurityID Planning Values, Still IsApprovedForExternalRun=false**.
- If records are still missing: **Phase 6G - Manual SecurityID Record Entry for Candidate Instruments**.

## 14. Phase 6G - Manual SecurityID Record Entry Workflow Hardening, No External Run

Phase 6G hardens the local record-entry workflow before real SecurityID values are entered.

It adds:

- template generation for GBPUSD, USDJPY, EURGBP, and AUDUSD;
- `-WhatIfPreview` support for record creation;
- explicit no-overwrite behavior unless `-Force` is supplied;
- clearer review summaries by instrument;
- conflict detection for multiple accepted SecurityIDs on the same symbol;
- a local Phase 6G gate.

Run:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1
```

Expected current result is `PASS_WITH_KNOWN_WARNINGS` when no accepted records exist yet. Templates are written under ignored artifacts and are not confirmation records.

Phase 6G does not authorize external runs. `IsApprovedForExternalRun` remains false, and no LMAX connection, external API call, snapshot, replay, scheduler/polling, order path, gateway registration, or trading mutation is added.

Next recommended phase:

**Phase 6H - Enter Real SecurityID Confirmation Records, if values are available.** If values are not available, remain blocked pending evidence.

## Phase 6H - Real Confirmation Record Entry

Phase 6H implements the local-only real record entry path without approving any external run. Real sanitized records live under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`. Operators should preview with `scripts/new-lmax-readonly-securityid-confirmation-record.ps1 -WhatIfPreview`, write only reviewed records, review with `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`, and gate with `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1`.

`PASS` means GBPUSD, USDJPY, EURGBP, and AUDUSD each have valid `AcceptedForPlanning` records. `PASS_WITH_KNOWN_WARNINGS` means some records remain missing/pending but safe. `FAIL` means unsafe, invalid, sensitive, conflicting, or externally approved content. `AcceptedForPlanning` remains planning-only; `IsApprovedForExternalRun=false` is still mandatory.

Next recommended phase: **Phase 6I - Apply Accepted SecurityID Planning Values, Still IsApprovedForExternalRun=false**, only if accepted records exist. Otherwise remain pending evidence.

## Phase 6I - Manual SecurityList Discovery

Phase 6I adds a manual Demo-only SecurityListRequest discovery path to identify candidate SecurityIDs directly from LMAX Demo FIX market-data metadata. The operator script is `scripts/run-lmax-readonly-runtime-demo-securitylist-discovery.ps1`; the local gate is `scripts/check-lmax-readonly-runtime-phase6i-securitylist-discovery-gate.ps1`.

The discovery artifact is planning-only and written under `artifacts/lmax-readonly-runtime-securityid-discovery/`. It includes candidate matches and unmatched candidates, redacted credential profile labels only, and safety flags proving no orders, snapshots, replay, scheduler, gateway registration, or trading mutation occurred.

Next recommended phase: **Phase 6J - Prepare Confirmation Records from SecurityList Discovery, No External Run** if discovery succeeds, or **Phase 6J - SecurityList Failure Diagnostics** if discovery fails.

## Phase 6J - SecurityList Failure Diagnostics

The first manual SecurityList discovery attempt failed safely with `FailedSafeSecurityListRequestRejected`, zero instruments, and all safety flags false. Phase 6J adds diagnostics and request-profile compatibility controls. It does not run another external request by default.

Next recommended phase: **Phase 6K - Operator-approved SecurityList AutoSequence Discovery Attempt** if diagnostics are ready. If LMAX Demo does not support SecurityListRequest, use `VendorSupportConfirmation` as the fallback evidence source.

## Phase 6L - SecurityList Unknown Reject Analysis / Fallback Decision

The Phase 6K operator-approved AutoSequence discovery attempt failed safely with `FailedSafeSecurityListUnknownReject`, zero instruments, zero candidate matches, and GBPUSD, USDJPY, EURGBP, and AUDUSD still unmatched. Phase 6L reviews that sanitized artifact locally and does not run another external request.

The fallback review writes `artifacts/readiness/phase6l-securitylist-fallback-decision.json` and the gate writes `artifacts/readiness/phase6l-securitylist-fallback-gate.json`. If the artifact does not include attempt-level reject tag/text, the analysis must keep the classification conservative and recommend vendor/support or other official manual confirmation rather than guessing that a SecurityList profile remains viable.

Next recommended phase: **Phase 6M - VendorSupportConfirmation Record Preparation, No External Run** if the fallback is chosen. All instruments remain `IsApprovedForExternalRun=false`; no snapshot, replay, scheduler, order path, gateway registration, credential exposure, or trading mutation is authorized.

## Phase 6M - Uploaded LMAX Instrument CSV SecurityID Records

Phase 6M uses uploaded LMAX instrument CSV files as official source evidence for local `AcceptedForPlanning` confirmation records. The CSV extractor reads `Instrument Name`, `LMAX ID`, and `LMAX symbol`, matches GBP/USD, EUR/GBP, USD/JPY, and AUD/USD, and selects only DemoLondon/NewYork 400x IDs for the current profile.

Selected planning values:

- GBP/USD -> 4002
- EUR/GBP -> 4003
- USD/JPY -> 4004
- AUD/USD -> 4007

Tokyo 600x IDs are recognized as a different profile and intentionally not selected. The generated records remain `IsApprovedForExternalRun=false` and do not authorize external runs.

Next recommended phase: **Phase 6N - Apply Accepted SecurityID Planning Values to Planning Manifest, Still IsApprovedForExternalRun=false**.

## Phase 6N - Apply Accepted SecurityID Planning Values

Phase 6N applies the Phase 6M accepted confirmation records to a local planning manifest. It replaces pending placeholder planning values with confirmed DemoLondon/NewYork planning SecurityIDs while preserving the non-executable boundary.

Applied planning values:

- GBP/USD -> 4002, SecurityIDSource=8
- EUR/GBP -> 4003, SecurityIDSource=8
- USD/JPY -> 4004, SecurityIDSource=8
- AUD/USD -> 4007, SecurityIDSource=8

The manifest is written under `artifacts/lmax-readonly-runtime-securityid-planning/` and includes Demo/DemoLondon scope, confirmation record references, `decision=AcceptedForPlanning`, `noSensitiveContent=true`, and `IsApprovedForExternalRun=false`. It does not authorize external runs, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation.

## Phase 6O - Per-Instrument Safety Gate Design

Phase 6O creates a local per-instrument safety gate manifest from the Phase 6N planning manifest. It validates the current DemoLondon values:

- GBP/USD -> 4002, SecurityIDSource=8
- EUR/GBP -> 4003, SecurityIDSource=8
- USD/JPY -> 4004, SecurityIDSource=8
- AUD/USD -> 4007, SecurityIDSource=8

Each instrument gate checks accepted planning value, SecurityIDSource=8, Demo environment, DemoLondon venue profile, MarketDataOnly intent, no external-run approval, no order capability, no runtime shadow replay submit, no scheduler/polling, no trading mutation, and a future explicit operator prompt requirement. `PASS` means planning data is safe and complete; it does not make the instrument executable. `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false` remain mandatory.

Next recommended phase: **Phase 6P - Manual Additional Instrument Snapshot Preflight Design, No External Run**, if the safety gate manifest passes, or fix planning records if Phase 6O fails.

## Phase 6P - Manual Additional Instrument Snapshot Preflight Design

Phase 6P defines the future one-off manual Demo read-only MarketData snapshot preflight envelope. It validates the current additional-instrument values:

- GBP/USD -> 4002, SecurityIDSource=8
- EUR/GBP -> 4003, SecurityIDSource=8
- USD/JPY -> 4004, SecurityIDSource=8
- AUD/USD -> 4007, SecurityIDSource=8

The request profile remains `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, and `MarketDepth=1`, with capped runtime, wait, and event limits. `PASS` means preflight design is safe and complete only; `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory. No external run, SecurityListRequest, snapshot, replay, scheduler, order, gateway registration, credential exposure, trading-table persistence, or trading mutation is authorized.

Next recommended phase: **Phase 6Q - Manual Additional Instrument Snapshot Attempt Approval Envelope, No External Run**, or **Phase 6Q - Single-Instrument Manual Snapshot Dry-Run Report, No External Run**.

## Phase 6Q - Manual Additional Instrument Snapshot Attempt Approval Envelope

Phase 6Q creates a planning-only approval envelope for one selected instrument, sourced from a PASS Phase 6P preflight manifest. It records requested/reviewed operator ids, reason, selected symbol, SecurityID, request profile, source preflight decision, and attestations for Demo-only, read-only MarketData-only, no orders, no scheduler/polling, no runtime shadow replay submit, no trading mutation, single instrument only, and future explicit manual run requirement.

`AcceptedForPlanning` means the envelope is complete for planning only. It does not authorize an external run; `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory.

Next recommended phase: **Phase 6R - Single-Instrument Manual Snapshot Dry-Run Report, No External Run**, if a valid envelope exists, or create an envelope first if missing.

## Phase 6R - Single-Instrument Manual Snapshot Dry-Run Report

Phase 6R creates a local GBPUSD dry-run report from Phase 6N planning values, Phase 6O safety gate, Phase 6P preflight, and Phase 6Q approval envelope. It records GBP/USD 4002, SecurityIDSource=8, Demo/DemoLondon, `SnapshotPlusUpdates`, `SecurityIdOnly`, MarketDepth=1, source decisions, required future step, and the blocking reason that Phase 6R is dry-run only.

`PASS` means report consistency only. It does not authorize an external run; `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory.

Next recommended phase: **Phase 6S - Single-Instrument Manual Snapshot Attempt Gate, Still No External Run**, or **Phase 6S - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run**.
### Phase 6S - Single-Instrument Manual Snapshot Attempt Gate

Phase 6S is implemented as the final non-executable gate before a future operator-approved GBPUSD Demo read-only MarketData snapshot attempt can be considered. It aggregates Phase 6N through Phase 6R artifacts and produces a gate decision.

`PASS` means all prerequisites are consistent for future consideration only. It does not authorize execution. The next recommended phase is Phase 6T - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run, or Phase 6T - Manual GBPUSD Snapshot Execution Plan / Kill-Rollback Plan, Still No External Run.

### Phase 6T - Manual GBPUSD Snapshot Execution Plan / Kill-Rollback Plan

Phase 6T is implemented as planning/docs/gate only. It records the GBPUSD future command template, abort criteria, rollback steps, and post-run validation requirements while keeping every run eligibility flag false.

Next recommended phase: Phase 6U - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run, or Phase 6U - GBPUSD Execution Plan Review / Final Pre-Run Gate, Still No External Run.

### Phase 6U - Operator Signoff For One Future GBPUSD Attempt

Phase 6U is implemented as a human-control artifact. The operator signoff confirms review of the Phase 6T execution plan and kill/rollback checklist. `SignedForPlanning` does not authorize execution and keeps all run eligibility flags false.

Next recommended phase: Phase 6V - Final Manual GBPUSD Snapshot Execution Readiness Gate, Still No External Run, or Phase 6V - Operator-approved Manual GBPUSD Snapshot Attempt, if and only if a final readiness gate is explicitly passed later.

### Phase 6V - Final Manual GBPUSD Snapshot Execution Readiness Gate

Phase 6V is implemented as the final non-executable aggregation of the GBPUSD artifact chain. `PASS` means the pre-execution readiness package is complete and consistent. It does not authorize execution.

Next recommended phase: Phase 6W - Operator-approved Manual GBPUSD Snapshot Attempt, if and only if the operator explicitly chooses to execute, or stop here with all readiness closed.

### Phase 6W - Operator-Approved Manual GBPUSD Snapshot Attempt

Phase 6W is implemented as a wrapper and result gate for one manual Demo GBPUSD read-only snapshot attempt. It requires final readiness, uses GBPUSD `4002`, performs no automatic retry, and remains outside API/Worker runtime execution.

Next recommended phase: Phase 6X - GBPUSD Snapshot Artifact Review / Evidence Preview Mapping if the snapshot succeeds, or Phase 6X - GBPUSD Snapshot Failure Diagnostics if it fails.
## Phase 6X - GBPUSD Snapshot Artifact Review / Empty Book Diagnostics

Phase 6X closes the first GBPUSD manual Demo read-only snapshot attempt as a safe `CompletedWithEmptyBook` result. The artifact has one MarketDataSnapshot, zero entries, no rejects, no credential values, no orders, no scheduler, no runtime shadow replay submit, and no trading mutation.

The decision is `PASS_WITH_KNOWN_WARNINGS`. The state supports diagnostic documentation and optional empty-book evidence preview mapping only. It does not authorize a retry or any automatic/executable path.

Next recommended phase: Phase 6Y - optional second operator-approved GBPUSD snapshot attempt at a different time, or Phase 6Y - GBPUSD EmptyBook evidence preview mapping / manual replay planning.

## Phase 6Y - GBPUSD Market-Hours Retry Preparation

Phase 6Y prepares one future manual GBPUSD retry during open FX market hours after the Saturday `CompletedWithEmptyBook` result. It writes a local retry readiness artifact and documents the future command, but does not execute it.

The retry remains single-attempt, manual-only, market-hours-only, no scheduler/polling, no automatic retry, no runtime shadow replay submit, no orders, no gateway registration, no credential exposure, and no trading mutation. `PASS` means the plan is safe, not executable.

Next recommended phase: Phase 6Z - Operator-approved GBPUSD Market-Hours Snapshot Attempt, or remain paused until market hours.
## Phase 6Z-A - Additional Instruments Planning Pipeline Replication

Phase 6Z-A is implemented as the local-only replication of the GBPUSD planning pipeline for EURGBP, USDJPY, and AUDUSD. It builds or reuses planning-only artifacts for all four additional instruments: approval envelope, dry-run report, attempt gate, execution plan, operator signoff, and final readiness.

The aggregate pipeline manifest is written under `artifacts/lmax-readonly-runtime-securityid-planning/pipeline/` and must close with `instrumentCount=4`, `readyForFutureManualConsiderationCount=4`, `executableCount=0`, and `finalDecision=PASS`.

The phase does not authorize execution. `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain mandatory for every instrument. It adds no external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, credential exposure, or trading mutation.

Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for One Selected Additional Instrument, only if an operator explicitly chooses one instrument and one manual attempt, or stop with all planning closed.

## Phase 6Z-C - Additional Instruments Operator Console Summary

Phase 6Z-C is implemented as read-only visibility over the Phase 6Z-A pipeline. It adds a sanitized summary model, local show script, local gate, read-only API endpoint, and operator console panel.

The status surface displays aggregate `PASS`, `instrumentCount=4`, `readyForFutureManualConsiderationCount=4`, `executableCount=0`, and per-instrument decisions for GBPUSD, EURGBP, USDJPY, and AUDUSD. It does not authorize execution and does not add any live control.

Next recommended phase: Phase 6Z-D - Additional Instruments Documentation Pack / Final Planning Freeze, or wait until market hours for an explicitly chosen one-instrument Phase 6Z-B attempt.

## Phase 6Z-D - Additional Instruments Documentation Pack / Final Planning Freeze

Phase 6Z-D is implemented as documentation/reporting/gate only. It adds `docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md`, `scripts/build-lmax-readonly-additional-instruments-planning-doc-pack.ps1`, and `scripts/check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1`.

The pack freezes GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 as non-executable planning artifacts. It validates the Phase 6Z-A aggregate pipeline manifest and Phase 6Z-C operator planning status report, then records `instrumentCount=4`, `readyForFutureManualConsiderationCount=4`, `executableCount=0`, and false run flags for all instruments.

Phase 6Z-D does not connect to LMAX, call external APIs, run SecurityListRequest, request snapshots, replay evidence, add scheduler/polling, submit to shadow replay, add order submission, register gateways, expose credentials, or mutate trading state.

Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for one selected additional instrument, only if the market is open and the operator explicitly chooses; otherwise stop with planning frozen.

## Phase 6Z-E - Market-Hours Action Card / Operator Next-Step Panel

Phase 6Z-E is implemented as read-only UI/API/script visibility for the prepared GBPUSD market-hours retry. It adds `LmaxReadOnlyMarketHoursNextActionSummary`, `GET /lmax-readonly-runtime/market-hours-next-action`, `scripts/show-lmax-readonly-market-hours-next-action.ps1`, `scripts/check-lmax-readonly-runtime-phase6ze-market-hours-action-card-gate.ps1`, and an operator console panel.

The summary binds the Phase 6V final readiness, Phase 6Y retry readiness, Phase 6X empty-book review, and Phase 6Z-D documentation pack. It shows GBPUSD=4002, previous `CompletedWithEmptyBook` outside market hours, `executableCount=0`, and false run flags.

Phase 6Z-E does not connect to LMAX, run SecurityListRequest, run snapshots, run replay, add scheduler/polling, submit to shadow replay, add orders, register gateways, expose credentials, or mutate trading state. The actual future run remains a manual operator command only, during market hours.

Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for GBPUSD during market hours, or stop until the market opens.
## Phase 7A Boundary Handoff

Phase 7A closes the Phase 6 operationalization arc with a planning-only architecture decision. The current state is: EURUSD workflow frozen, additional-instrument planning frozen, GBPUSD market-hours retry prepared, and operator console visibility in place.

The selected next boundary is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. Phase 7B must not implement scheduler/polling, runtime shadow replay submit, orders, real gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.
