# Documentation Index

## Start Here

| Document | Audience | Purpose |
| --- | --- | --- |
| [Operator Manual](OPERATOR_MANUAL.md) | Operators, business users, investor-facing operations, leadership | Plain-language operating guide, safety rules, daily workflows, page guide, escalation. |
| [Developer Guide](DEVELOPER_GUIDE.md) | Developers, technical auditors, maintainers | Architecture, API/routes, persistence, UI, LMAX shadow/evidence, scripts, extension rules. |
| [Local Runbook](LOCAL_RUNBOOK.md) | Developers and operators running locally | Exact local setup, scripts, smoke tests, governance and operational flows. |
| [Operational Readiness Checklist](OPERATIONAL_READINESS_CHECKLIST.md) | Developers, operators, release reviewers | Release/readiness gate, validation commands, safety checklist, known warnings, and next-phase criteria. |

## LMAX and Adapter Design

| Document | Audience | Purpose |
| --- | --- | --- |
| [LMAX Connectivity Lab](LMAX_CONNECTIVITY_LAB.md) | Developers/lab users | Isolated lab-only LMAX FIX tooling, evidence capture, safety gates, no runtime registration. |
| [LMAX Adapter Design](LMAX_ADAPTER_DESIGN.md) | Developers/architects | Future adapter architecture, shadow mode, safety boundary, disabled skeleton. |
| [LMAX Read-Only Runtime Adapter Design](LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md) | Developers/architects/release reviewers | Future read-only runtime shadow reader design, activation levels, safety gates, and non-mutating evidence flow. Design-only; not implemented connectivity. |
| [LMAX Read-Only Runtime Adapter Implementation Plan](LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md) | Developers/architects/release reviewers | Phased delivery plan with entry criteria, exit criteria, tests, smokes, rollback gates, Phase 3.5 fake-enabled endpoint proof, and next eligible phase. |
| [LMAX Read-Only Runtime Phase Gates](LMAX_READONLY_RUNTIME_PHASE_GATES.md) | Release reviewers/operators | Concise phase checklist for quick operational review. |
| [LMAX Read-Only Runtime Phase 4 Preflight](LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md) | Developers/architects/release reviewers | Boundary lock for future external read-only prototype; documents hard gates before any socket code exists. |
| [LMAX Read-Only Runtime No-Socket Release Gate](LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md) | Operators/developers | Final local release gate before any separate future socket prototype prompt. |
| [LMAX Read-Only Runtime First Transport Preflight](LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md) | Operators/developers/release reviewers | Phase 5A preflight, kill/rollback plan, abort conditions, and future Phase 5B entry criteria. |
| [LMAX Read-Only Runtime Phase 5A Checklist](LMAX_READONLY_RUNTIME_PHASE5A_CHECKLIST.md) | Operators/developers/release reviewers | Phase 5A checklist plus Phase 5B blocked prototype outcome. |
| [LMAX Read-Only Runtime Phase 5P Stability Decision](LMAX_READONLY_RUNTIME_PHASE5P_STABILITY_DECISION.md) | Operators/developers/release reviewers | Phase 5P closure decision for the 3/3 successful repeated manual Demo snapshot stability run. |
| [LMAX Read-Only Runtime Operational Signoff](LMAX_READONLY_RUNTIME_OPERATIONAL_SIGNOFF.md) | Operators/developers/risk reviewers | Phase 5W operational signoff for the validated controlled manual Demo MarketData workflow. |
| [LMAX Read-Only Demo MarketData Workflow Final Doc](LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md) | Operators/developers/auditors/external reviewers | Phase 5Y final documentation pack for the frozen manual Demo MarketData workflow. |
| [LMAX Read-Only Runtime Phase 6 Operationalization Plan](LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md) | Operators/developers/release reviewers | Phase 6A planning boundary and recommended next safe frontier after Phase 5. |
| [LMAX Read-Only Runtime Phase 6 Boundary Checklist](LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md) | Release reviewers/operators | Required checklist before any Phase 6 implementation prompt, including Phase 6B instrument allowlist design checks. |
| [LMAX Read-Only SecurityID Confirmation Operator Checklist](LMAX_READONLY_SECURITYID_CONFIRMATION_OPERATOR_CHECKLIST.md) | Operators/developers/release reviewers | Phase 6H checklist for sanitized local real SecurityID confirmation record entry and review. |
| [LMAX Read-Only Additional Instruments Planning Final Doc](LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md) | Operators/developers/auditors/release reviewers | Phase 6Z-D final documentation pack freezing the additional-instrument planning pipeline as non-executable. |
| [LMAX Read-Only Runtime Phase 7 Next Boundary ADR](LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md) | Operators/developers/architecture reviewers | Phase 7A architecture decision for the next safe read-only runtime boundary. |
| [LMAX Read-Only Runtime Phase 7 Boundary Checklist](LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md) | Operators/developers/release reviewers | Phase 7A checklist for entering Phase 7B without adding runtime power. |
| [LMAX Read-Only Runtime Phase 7 Controlled Manual Multi-Instrument Workflow Plan](LMAX_READONLY_RUNTIME_PHASE7_CONTROLLED_MANUAL_MULTI_INSTRUMENT_WORKFLOW_PLAN.md) | Operators/developers/release reviewers | Phase 7B planning-only workflow for one-instrument-at-a-time additional-instrument attempts. |
| [GBPUSD Market-Hours Execution Checklist](LMAX_READONLY_GBPUSD_MARKET_HOURS_EXECUTION_CHECKLIST.md) | Operators/release reviewers | Phase 7E runbook checklist for the future manual GBPUSD market-hours attempt and closure sequence. |
| [Adapter Contracts](ADAPTER_CONTRACTS.md) | Developers/testers | Neutral venue contract, FakeLmax parity, LMAX normalized event mapping, shadow observations. |

Phase 4A external-session contract/stub status through Phase 4P no-socket release gate status, Phase 5A first-transport preflight status, Phase 5B prototype-boundary status, Phase 5C credential availability/redaction status, Phase 5D manual Demo snapshot prototype status, Phase 5E failure/retry hardening status, Phase 5F operator-approved sanitized result capture status, Phase 5G snapshot-timeout diagnostics status, Phase 5H MarketDataRequest compatibility status, Phase 5J logon/session diagnostics status, Phase 5L successful snapshot closure status, Phase 5M MarketDataOnly evidence preview mapping status, Phase 5N manual MarketDataOnly replay dry-run status, Phase 5O repeated manual snapshot stability status, Phase 5P stability readiness decision, Phase 5Q controlled manual evidence workflow hardening, Phase 5R optional manual local replay review, Phase 5S controlled manual workflow release gate, Phase 5T controlled manual workflow runbook freeze, Phase 5V final audit pack gate, Phase 5W operational signoff, Phase 5X operator summary, Phase 5Y final documentation pack, Phase 6A planning boundary through Phase 6R GBPUSD dry-run reporting are documented in the Phase Gates and Phase 6 plan docs. Phase 6R `PASS` means dry-run report consistency only and keeps `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`; it is not permission to run.

## Report Samples

| File | Purpose |
| --- | --- |
| [individual-trades.csv](individual-trades.csv) | LMAX individual trade report shape reference. |
| [trades.csv](trades.csv) | LMAX trade summary shape reference. |
| [currency-wallets.csv](currency-wallets.csv) | LMAX wallet report shape reference. |

## Safety Reminder

The main API and Worker remain FakeLmax-only. LMAX FIX connectivity exists only in the isolated Connectivity Lab. Shadow replay is local, offline, and non-mutating.

## Documentation Accuracy Status

Documentation Accuracy Audit #1 checked the new institutional docs against the current codebase routes, scripts, UI navigation, LMAX evidence validator, shadow policy metadata, and safety defaults. These are living documents: future endpoint, script, policy, evidence-contract, or UI changes should update the relevant docs in the same change set.
### Phase 6S - Single-Instrument Manual Snapshot Attempt Gate

Phase 6S adds the GBPUSD attempt gate documentation and local artifacts. The gate checks the Phase 6N planning values, Phase 6O safety gate, Phase 6P preflight, Phase 6Q approval envelope, and Phase 6R dry-run report. A `PASS` is a final pre-execution consistency result only, not authorization to run.

Run eligibility remains disabled: `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`. No LMAX connection, snapshot, replay, scheduler, order flow, gateway registration, or trading mutation is introduced.

### Phase 6T - GBPUSD Execution Plan

See `docs/LMAX_READONLY_GBPUSD_MANUAL_SNAPSHOT_EXECUTION_PLAN.md` for the non-executable GBPUSD execution plan and kill/rollback checklist. The future command template is documented but explicitly marked `DO NOT RUN IN PHASE 6T`.

### Phase 6U - GBPUSD Operator Signoff

Phase 6U adds the non-executable operator signoff envelope for the GBPUSD execution plan. `SignedForPlanning` confirms review only and keeps all run eligibility flags false.

### Phase 6V - GBPUSD Final Readiness

Phase 6V adds the final non-executable readiness aggregation for GBPUSD. A `PASS` is not execution authorization; it only confirms the artifact chain is complete and consistent.

### Phase 6W - GBPUSD Manual Snapshot Attempt

Phase 6W adds the one-shot GBPUSD wrapper and result gate. The wrapper requires the Phase 6V final readiness artifact and explicit operator flags. It is the only GBPUSD path and remains single-instrument, single-attempt, no retry, no orders, no scheduler, no runtime shadow replay submit, and no mutation.

### Phase 6X - GBPUSD Snapshot Artifact Review

Phase 6X reviews the first GBPUSD result artifact. The attempt completed safely as `CompletedWithEmptyBook`: the Demo session logged on, a MarketDataSnapshot arrived, no rejects were observed, and no bid/ask entries were present.

The Phase 6X decision is `PASS_WITH_KNOWN_WARNINGS`. This is a diagnostic closure only; it does not authorize retry, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation. Empty-book artifacts can be mapped to `MarketDataOnly` evidence previews with an explicit warning and empty execution/order/trade/reject arrays.

### Phase 6Y - GBPUSD Market-Hours Retry Preparation

Phase 6Y adds `docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_RETRY_PLAN.md`, a retry readiness model, a local preparation script, and a local gate. It documents that the Saturday empty-book result is expected outside FX market hours and prepares exactly one future manual market-hours retry without executing it.

The readiness artifact keeps `canRunAutomatically=false`, requires manual-only market-hours retry, and confirms no scheduler/polling, runtime shadow replay submit, orders, gateway registration, credential exposure, or trading mutation.

### Phase 6Z-A - Additional Instruments Planning Pipeline

Phase 6Z-A replicates the non-executable planning pipeline for EURGBP, USDJPY, and AUDUSD while retaining GBPUSD coverage. The aggregate pipeline manifest covers GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 with SecurityIDSource `8`, Demo/DemoLondon, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.

`PASS` means planning completeness only: `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`. No external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation is authorized.

### Phase 6Z-C - Additional Instruments Operator Console Summary

Phase 6Z-C adds a read-only status summary for the Phase 6Z-A pipeline. The local script, API endpoint, and operator console panel display aggregate and per-instrument planning status without adding live controls. `executableCount=0`, false run flags, and `FakeLmaxGateway` remain the expected state.

### Phase 6Z-D - Additional Instruments Documentation Pack

Phase 6Z-D adds the final documentation/audit pack for the additional-instrument planning pipeline. It freezes GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 as non-executable planning artifacts only. `PASS` means documentation and artifact-chain consistency; it does not authorize external runs, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, production/UAT, multi-instrument batch, or trading mutation.

### Phase 6Z-E - Market-Hours Next Action Card

Phase 6Z-E adds a read-only next-action summary for the prepared GBPUSD market-hours retry. It shows GBPUSD=4002, the previous safe `CompletedWithEmptyBook` result outside market hours, final readiness `PASS`, retry readiness `PASS`, and planning freeze `PASS`. It adds no execution controls and does not authorize running from the UI.

### Phase 7A - Read-Only Runtime Next Boundary Decision

Phase 7A adds the architecture decision record and boundary checklist for the next safe read-only runtime frontier. The selected recommendation is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run.

The ADR explicitly rejects scheduler/polling, runtime shadow replay submit, order path, production/UAT, real gateway registration, and multi-instrument batch execution for now. Phase 7A is documentation and gate only; it adds no runtime capability.

### Phase 7B - Controlled Manual Multi-Instrument Workflow Plan

Phase 7B adds a planning-only model, builder, gate, and document for sequencing future additional-instrument attempts. The default sequence is GBPUSD, EURGBP, USDJPY, AUDUSD. `PASS` keeps `executableCount=0`, `batchExecutionAllowed=false`, and all instrument run-eligibility flags false.

### Phase 7C - GBPUSD Market-Hours Closure Workflow

Phase 7C adds local-only closure tooling for a future supplied GBPUSD market-hours result artifact. It can review/classify the artifact, map safe book or empty-book results to MarketDataOnly evidence preview, support explicit manual local replay, and build a closure manifest. It does not run GBPUSD, does not replay automatically, and keeps API/Worker `FakeLmaxGateway` only.

### Phase 7D - Post-GBPUSD Next Instrument Decision

Phase 7D adds a local decision artifact for the post-GBPUSD branch point. With no GBPUSD market-hours closure, it remains `PendingGbpusdMarketHoursAttempt`. `CompletedWithBook/PASS` is the only path to `ProceedToEurgbpPlanning`; empty-book warnings require a controlled GBPUSD retry, and failed-safe or unsafe results block for diagnostics. No instrument becomes executable.

### Phase 7E - GBPUSD Market-Hours Execution Checklist

Phase 7E adds the final checklist/runbook pack for the future manual GBPUSD market-hours attempt. It records the exact future command with `DO NOT RUN UNTIL MARKET HOURS`, pre-run checks, one-attempt/no-retry monitoring, the kill switch, Phase 7C post-run closure sequence, and Phase 7D next decision. It is documentation/reporting/gate only and does not authorize automation.

### Phase 7E2 - EURGBP Readiness Rehydration

Phase 7E2 adds `scripts/rehydrate-lmax-readonly-eurgbp-manual-snapshot-readiness.ps1`, `scripts/check-lmax-readonly-runtime-phase7e2-eurgbp-readiness-gate.ps1`, and the EURGBP readiness rehydration model/tests. It is local-only and non-executable, and it only applies after Phase 7D selects `ProceedToEurgbpPlanning`.

### Phase 7F2 - EURGBP Execution Checklist

Phase 7F2 adds `docs/LMAX_READONLY_EURGBP_MANUAL_SNAPSHOT_EXECUTION_CHECKLIST.md`, `scripts/new-lmax-readonly-eurgbp-manual-snapshot-execution-checklist.ps1`, `scripts/check-lmax-readonly-runtime-phase7f2-eurgbp-execution-checklist-gate.ps1`, and the EURGBP checklist model/tests. It documents the future command template, abort criteria, rollback steps, and post-run validation while clearly marking the command `DO NOT RUN IN PHASE 7F2`.

The phase is planning-only: EURGBP remains non-executable, one-instrument-at-a-time remains enforced, and no external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation is authorized.

### Phase 7G2 - EURGBP Final Pre-Run Gate

Phase 7G2 adds `scripts/new-lmax-readonly-eurgbp-final-pre-run-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase7g2-eurgbp-final-prerun-gate.ps1`, and the EURGBP final pre-run gate model/tests. It aggregates Phase 7D, Phase 7E2, and Phase 7F2 into a final consistency artifact.

The phase is still non-executable: EURGBP remains `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`; one-instrument-at-a-time remains enforced; no external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation is authorized.

### Phase 7H - Generic Additional Instrument One-Shot Workflow

Phase 7H adds the generic one-instrument additional MarketData workflow: `scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1`, generic result review, evidence preview, optional local replay, closure manifest, and `scripts/check-lmax-readonly-runtime-phase7h-generic-additional-snapshot-workflow-gate.ps1`.

Supported additional instruments remain GBPUSD `4002`, EURGBP `4003`, USDJPY `4004`, and AUDUSD `4007`. The wrapper accepts exactly one symbol and requires the applicable final pre-run gate before delegating to the isolated manual prototype path. It adds no scheduler, polling, runtime shadow replay submit, order surface, gateway registration, trading mutation, or API/Worker runtime power.

### Phase 7H2 - Generic Additional Instrument Final Pre-Run Gate

Phase 7H2 adds `scripts/new-lmax-readonly-additional-instrument-final-pre-run-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase7h2-additional-instrument-final-prerun-gate.ps1`, and the generic final pre-run gate model/tests. It produces wrapper-compatible final pre-run gates for USDJPY/AUDUSD without running snapshots or adding runtime capability.

The Phase 7H wrapper still rejects generic Phase 6Z-A final-readiness artifacts. Operators must supply a Phase 7H-compatible final pre-run gate plus explicit manual flags before any future one-instrument Demo read-only attempt.
