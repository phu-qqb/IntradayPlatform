# LMAX Read-Only Runtime — Phase 6 Boundary Checklist

This checklist must pass before any Phase 6 implementation work begins.

| Gate | Required Status | Evidence / Check | Result |
| --- | --- | --- | --- |
| Phase 5Y final documentation pack | Exists | `docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md` | Required |
| Phase 5V audit pack | PASS | `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json` | Required |
| Phase 5W operational signoff | PASS | `artifacts/readiness/lmax-readonly-marketdata-operational-signoff-20260508-165858.json` | Required |
| Phase 5X workflow status | FrozenManualReadOnly | `artifacts/readiness/lmax-readonly-marketdata-workflow-status-20260508-172233.json` | Required |
| API/Worker gateway | FakeLmaxGateway only | Source scan and `/health` if API is available | Required |
| Scheduler/polling | Not present for LMAX runtime workflow | Source scan | Required |
| Runtime shadow replay submit | Not present | Source scan | Required |
| Order surface | Not present | Source scan | Required |
| Gateway registration | No real LMAX gateway registration | Source scan | Required |
| Trading-state mutation | Not present | Source scan | Required |
| Credentials | No values in docs/reports/artifacts/API/UI | Redaction and sensitive-string scans | Required |
| Next phase | Explicit prompt required | Phase 6A plan | Required |

## Expansion Checklist

Any future Phase 6 expansion must define all of the following before implementation:

- Allowlist.
- Manual operator flags.
- Evidence contract.
- Artifact validation.
- Evidence preview validation.
- Manual replay validation, if replay is in scope.
- Rollback instructions.
- Abort conditions.
- Operator approval language.
- Gate script.
- Documentation updates.

## Recommended Next Boundary

Recommended:

**Phase 6B — Manual Additional MarketData Instrument Allowlist Design, No External Run**

This next boundary is planning/design only. It must not connect to LMAX, run scheduler/polling, submit runtime shadow replay, submit orders, register a gateway, or mutate trading state.

## Phase 6B Allowlist Checklist

- [ ] `LmaxReadOnlyInstrumentAllowlist` exists.
- [ ] `LmaxReadOnlyInstrumentAllowlistValidator` exists.
- [ ] Candidate instruments beyond EURUSD / SecurityID `4001` are listed.
- [ ] Candidate SecurityID labels are present and marked as requiring Demo confirmation.
- [ ] Candidates are Demo-only.
- [ ] Candidates are planning-only and `IsApprovedForExternalRun=false`.
- [ ] Evidence mode is `MarketDataOnly`.
- [ ] Scheduler and polling remain disabled.
- [ ] Runtime shadow replay submit remains disabled.
- [ ] Order submission remains disabled.
- [ ] Gateway registration remains disabled.
- [ ] Trading mutation remains disabled.
- [ ] Gate report is written to `artifacts/readiness/phase6b-instrument-allowlist-gate.json`.

## Phase 6D SecurityID Discovery Checklist

- [ ] `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest` exists.
- [ ] GBPUSD, USDJPY, EURGBP, and AUDUSD each have a Phase 6D placeholder candidate SecurityID.
- [ ] Placeholder values are explicitly marked as local planning placeholders and not runnable LMAX identifiers.
- [ ] Every candidate keeps `IsApprovedForExternalRun=false`.
- [ ] External connection and external API call markers remain false.
- [ ] Scheduler and polling remain disabled.
- [ ] Runtime shadow replay submit remains disabled.
- [ ] Order submission remains disabled.
- [ ] Gateway registration remains disabled.
- [ ] Trading mutation remains disabled.
- [ ] Gate report is written to `artifacts/readiness/phase6d-securityid-discovery-gate.json`.

## Phase 6E Evidence Review Checklist

- [ ] `LmaxReadOnlyInstrumentSecurityIdSourceEvidence` exists.
- [ ] `LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator` exists.
- [ ] GBPUSD, USDJPY, EURGBP, and AUDUSD each have an evidence review record.
- [ ] Default records are `NeedsMoreEvidence` until source evidence is reviewed.
- [ ] `AcceptedForPlanning` records require non-placeholder SecurityIDs, evidence reference, reviewer, reviewed timestamp, and High/Confirmed confidence.
- [ ] Every record keeps `IsApprovedForExternalRun=false`.
- [ ] External connection, external API call, snapshot, and replay markers remain false.
- [ ] Scheduler and polling remain disabled.
- [ ] Runtime shadow replay submit remains disabled.
- [ ] Order submission remains disabled.
- [ ] Gateway registration remains disabled.
- [ ] Trading mutation remains disabled.
- [ ] Gate report is written to `artifacts/readiness/phase6e-securityid-evidence-review-gate.json`.

## Phase 6F Confirmation Records Checklist

- [ ] `LmaxReadOnlyInstrumentSecurityIdConfirmationRecord` exists.
- [ ] `LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator` exists.
- [ ] Local record creation and review scripts exist.
- [ ] Sample sanitized JSON record exists.
- [ ] Records, if any, are stored only under ignored artifacts.
- [ ] Accepted records use non-placeholder SecurityIDs.
- [ ] Every record keeps `IsApprovedForExternalRun=false`.
- [ ] Missing accepted records produce `PASS_WITH_KNOWN_WARNINGS`, not executable approval.
- [ ] Conflicting accepted SecurityIDs fail.
- [ ] External connection, external API call, snapshot, and replay markers remain false.
- [ ] Scheduler and polling remain disabled.
- [ ] Runtime shadow replay submit remains disabled.
- [ ] Order submission remains disabled.
- [ ] Gateway registration remains disabled.
- [ ] Trading mutation remains disabled.
- [ ] Gate report is written to `artifacts/readiness/phase6f-confirmation-records-gate.json`.

## Phase 6G Record Entry Hardening Checklist

- [ ] Template generator exists.
- [ ] Creation script supports preview mode and no-overwrite default.
- [ ] Review script reports latest record, accepted count, pending count, and conflicts per instrument.
- [ ] Templates are generated only under ignored artifacts.
- [ ] No accepted confirmation records are required for the gate to remain safe.
- [ ] Missing records produce `PASS_WITH_KNOWN_WARNINGS`.
- [ ] Every record and template keeps `IsApprovedForExternalRun=false`.
- [ ] External connection, external API call, snapshot, and replay markers remain false.
- [ ] Scheduler and polling remain disabled.
- [ ] Runtime shadow replay submit remains disabled.
- [ ] Order submission remains disabled.
- [ ] Gateway registration remains disabled.
- [ ] Trading mutation remains disabled.
- [ ] Gate report is written to `artifacts/readiness/phase6g-record-entry-workflow-gate.json`.

## Phase 6H Real Confirmation Records Checklist

- [ ] Real records directory is `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`.
- [ ] Directory remains ignored by git.
- [ ] Creation script supports `-OutputDirectory`, `-OutputFile`, `-WhatIfPreview`, and overwrite only with `-Force`.
- [ ] Review script reads the real directory by default and templates only by explicit request.
- [ ] Missing or pending records produce `PASS_WITH_KNOWN_WARNINGS`.
- [ ] All four valid accepted records produce `PASS`.
- [ ] Unsafe, conflicting, sensitive, or externally approved records produce `FAIL`.
- [ ] Every record keeps `IsApprovedForExternalRun=false`.
- [ ] `AcceptedForPlanning` is planning-only and does not authorize an external run.
- [ ] No LMAX connection, external API call, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.
- [ ] Gate report is written to `artifacts/readiness/phase6h-real-confirmation-records-gate.json`.

## Phase 6I SecurityList Discovery Checklist

- [ ] Manual SecurityList discovery script exists and requires explicit operator flags.
- [ ] Gate does not connect to LMAX or run SecurityListRequest.
- [ ] Discovery model parses SecurityList metadata and candidate matches without live LMAX tests.
- [ ] Discovery artifact, if supplied, is sanitized and has no credential values, host, user, password, account, or raw sensitive FIX.
- [ ] `IsApprovedForExternalRun=false` remains true for all candidate matches.
- [ ] No market-data snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, or trading mutation occurred.
- [ ] Gate report is written to `artifacts/readiness/phase6i-securitylist-discovery-gate.json`.

## Phase 6J SecurityList Diagnostics Checklist

- [ ] Diagnostics model and artifact validator exist.
- [ ] Failed artifact can be parsed without exposing secrets.
- [ ] Request profiles include safe defaults and known-rejected metadata.
- [ ] `AutoSequence` skips known-rejected profiles unless explicitly allowed.
- [ ] Gate validates artifact and source safety without connecting to LMAX.
- [ ] Gate report is written to `artifacts/readiness/phase6j-securitylist-diagnostics-gate.json`.

## Phase 6L SecurityList Fallback Checklist

- [ ] Fallback decision model and failure review script exist.
- [ ] Phase 6K AutoSequence artifact can be parsed without exposing secrets.
- [ ] Attempt profiles, reject diagnostics if present, candidate matches, and unmatched candidates are summarized.
- [ ] Missing reject diagnostics are reported as missing rather than inferred.
- [ ] Fallback decision remains non-authorizing and keeps `IsApprovedForExternalRun=false`.
- [ ] Gate validates artifact and source safety without connecting to LMAX or running SecurityListRequest.
- [ ] Gate report is written to `artifacts/readiness/phase6l-securitylist-fallback-gate.json`.

## Phase 6M CSV SecurityID Records Checklist

- [ ] CSV extractor model and generation script exist.
- [ ] Uploaded CSVs contain `Instrument Name`, `LMAX ID`, and `LMAX symbol`.
- [ ] DemoLondon/NewYork selected values are GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007.
- [ ] Tokyo 600x IDs are observed but not selected for the current profile.
- [ ] Generated records validate as `AcceptedForPlanning`, `Confirmed`, and `OfficialLmaxDocument`.
- [ ] All records keep `IsApprovedForExternalRun=false`.
- [ ] Gate report is written to `artifacts/readiness/phase6m-csv-securityid-records-gate.json`.

## Phase 6N Planning Manifest Checklist

- [ ] Planning manifest model, validator, apply script, and gate exist.
- [ ] Manifest contains GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007.
- [ ] Every entry uses `securityIdSource=8`, `environmentName=Demo`, and `venueProfileName=DemoLondon`.
- [ ] Every entry references an accepted confirmation record.
- [ ] All manifest and instrument `IsApprovedForExternalRun` values remain false.
- [ ] Gate report is written to `artifacts/readiness/phase6n-planning-values-gate.json`.

## Phase 6O Per-Instrument Safety Gate Checklist

- [ ] Per-instrument safety gate model, aggregate manifest model, builder script, and gate script exist.
- [ ] Safety gate manifest is generated from the Phase 6N planning manifest.
- [ ] Manifest contains one gate result each for GBPUSD, EURGBP, USDJPY, and AUDUSD.
- [ ] Every result uses the accepted planning SecurityID and SecurityIDSource=8.
- [ ] Every result is scoped to `environmentName=Demo` and `venueProfileName=DemoLondon`.
- [ ] Every result keeps `IsApprovedForExternalRun=false`.
- [ ] Every result keeps `eligibleForManualSnapshotAttempt=false`.
- [ ] Aggregate manifest keeps `allApprovedForExternalRun=false` and `anyEligibleForManualSnapshotAttempt=false`.
- [ ] Gate report is written to `artifacts/readiness/phase6o-per-instrument-safety-gate.json`.
- [ ] No LMAX connection, external API call, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, runtime shadow replay submit, or trading mutation occurred.

## Phase 6P Additional Snapshot Preflight Checklist

- [ ] Additional snapshot preflight model, aggregate manifest model, builder script, and gate script exist.
- [ ] Preflight manifest is generated from the Phase 6N planning manifest and Phase 6O safety gate manifest.
- [ ] Manifest contains one preflight result each for GBPUSD, EURGBP, USDJPY, and AUDUSD.
- [ ] Every request uses `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, and `MarketDepth=1`.
- [ ] Runtime, wait, and event caps are within configured safe bounds.
- [ ] Every result keeps `canRunExternalSnapshot=false`.
- [ ] Every result keeps `eligibleForManualSnapshotAttempt=false`.
- [ ] Every result keeps `IsApprovedForExternalRun=false`.
- [ ] Aggregate manifest keeps `anyCanRunExternalSnapshot=false`, `anyEligibleForManualSnapshotAttempt=false`, and `anyApprovedForExternalRun=false`.
- [ ] Gate report is written to `artifacts/readiness/phase6p-additional-snapshot-preflight-gate.json`.
- [ ] No LMAX connection, external API call, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, runtime shadow replay submit, trading-table persistence, or trading mutation occurred.

## Phase 6Q Approval Envelope Checklist

- [ ] Approval envelope model, creation script, review script, and gate script exist.
- [ ] Envelope references a PASS Phase 6P preflight manifest.
- [ ] Envelope is for one selected symbol only.
- [ ] Accepted envelopes include requested/reviewed operator ids, reason, and all planning attestations.
- [ ] `AcceptedForPlanning` is documented as planning-only.
- [ ] `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false`.
- [ ] Gate report is written to `artifacts/readiness/phase6q-approval-envelope-gate.json`.
- [ ] No LMAX connection, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, runtime shadow replay submit, trading-table persistence, or trading mutation occurred.

## Phase 6R Dry-Run Report Checklist

- [ ] Dry-run report model, creation script, review script, and gate script exist.
- [ ] Dry-run report references Phase 6N/6O/6P/6Q source artifacts.
- [ ] Report is for GBPUSD / GBP/USD / SecurityID 4002.
- [ ] Source planning decision is `AcceptedForPlanning`.
- [ ] Source safety gate, preflight, and approval decisions are PASS/AcceptedForPlanning as applicable.
- [ ] `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false`.
- [ ] Gate report is written to `artifacts/readiness/phase6r-single-instrument-dryrun-gate.json`.
- [ ] No LMAX connection, snapshot, replay, scheduler/polling, order submission, gateway registration, credential exposure, runtime shadow replay submit, trading-table persistence, or trading mutation occurred.
### Phase 6S Boundary

Phase 6S remains local-only. It validates a GBPUSD attempt gate artifact and source artifact consistency, while keeping `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`.

The phase must not connect to LMAX, run SecurityListRequest, request snapshots, replay evidence, add scheduler/polling, add orders, register a real gateway, or mutate trading state.

### Phase 6T Boundary

Phase 6T may document a future command template only when it is clearly marked non-executable in Phase 6T. It must not connect to LMAX, request snapshots, replay evidence, start scheduler/polling, add orders, register a real gateway, mutate trading state, or approve an external run.

### Phase 6U Boundary

Phase 6U may create a `SignedForPlanning` operator signoff only. The signoff must not approve an external run, make GBPUSD eligible, connect to LMAX, request snapshots, replay evidence, start scheduler/polling, add orders, register a real gateway, or mutate trading state.

### Phase 6V Boundary

Phase 6V may create a final readiness artifact only. It must not approve an external run, make GBPUSD eligible, connect to LMAX, request snapshots, replay evidence, start scheduler/polling, add orders, register a real gateway, or mutate trading state.

### Phase 6W Boundary

Phase 6W permits only one explicit operator-run wrapper command for GBPUSD `4002`. It must not add scheduler/polling, retries, batches, runtime shadow replay submit, orders, gateway registration, trading-table persistence, or trading-state mutation.
## Phase 6X Boundary

- Phase 6X may review the sanitized GBPUSD result artifact and write local readiness reports.
- Phase 6X may classify `CompletedWithEmptyBook` as `PASS_WITH_KNOWN_WARNINGS`.
- Phase 6X may map empty-book data into `MarketDataOnly` preview shape with warning metadata.
- Phase 6X must not connect to LMAX, request another snapshot, run replay automatically, start scheduler/polling, submit to shadow replay from runtime, submit orders, register a gateway, persist to trading tables, or mutate trading state.
- API and Worker must remain `FakeLmaxGateway` only.

## Phase 6Y Boundary

- Phase 6Y may read the Phase 6V final readiness and Phase 6X review reports.
- Phase 6Y may write a local retry readiness artifact and document a future Phase 6Z command.
- Phase 6Y must not run GBPUSD, connect to LMAX, request snapshots, replay evidence, read credentials, schedule work, poll, start timers/background jobs, submit to shadow replay from runtime, submit orders, register a gateway, persist live FIX data, or mutate trading state.
- The retry remains manual-only, market-hours-only, and one attempt only.
## Phase 6Z-A Boundary Checklist

- Phase 6Z-A is local-only and does not connect to LMAX.
- It does not run SecurityListRequest, market-data snapshots, replay, scheduler/polling, timers, hosted services, or background jobs.
- It does not submit to runtime shadow replay.
- It does not add order submission, `NewOrderSingle`, Cancel/Replace, TradeCapture, or OrderStatus paths.
- It does not register a real gateway and API/Worker remain `FakeLmaxGateway`.
- It does not mutate trading state or persist live FIX data into trading tables.
- It preserves `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, `canRunExternalSnapshot=false`, and aggregate `executableCount=0`.
- A `PASS` pipeline decision means planning completeness only, not executable approval.

## Phase 6Z-C Boundary Checklist

- The status script, endpoint, and UI panel are read-only.
- They read sanitized local planning artifacts only.
- They expose no credential, host, port, external connection, snapshot, replay, scheduler, gateway, or order controls.
- They preserve `executableCount=0`, `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false`.
- API/Worker remain `FakeLmaxGateway`.

## Phase 6Z-D Boundary Checklist

- Phase 6Z-D is documentation/reporting/gate only.
- The documentation pack builder reads local sanitized artifacts only.
- The final doc and generated pack freeze GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 as non-executable planning values.
- It preserves `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` for every instrument.
- It does not connect to LMAX, call external APIs, run SecurityListRequest, run snapshots, run replay, add scheduler/polling/timers/hosted services, submit to shadow replay, submit orders, register gateways, expose credential values, persist live FIX data, or mutate trading state.
- Any future execution remains a separate explicit operator-approved market-hours phase, one selected instrument at a time.

## Phase 6Z-E Boundary Checklist

- Phase 6Z-E is read-only next-action visibility only.
- The next-action summary reads local sanitized artifacts only: Phase 6V final readiness, Phase 6Y retry readiness, Phase 6X review, and Phase 6Z-D documentation pack.
- The selected instrument must be GBPUSD / GBP/USD / SecurityID 4002 / SecurityIDSource 8.
- The previous result must remain the documented safe `CompletedWithEmptyBook` outside-market-hours warning.
- `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain mandatory.
- The UI panel must not include controls for external connections, snapshots, replay, scheduler/polling, credentials, host/port, orders, gateway registration, or mutation.
- The phase must not connect to LMAX, run SecurityListRequest, run snapshots, run replay, add timers/background jobs/hosted services, submit to shadow replay, submit orders, register gateways, expose credential values, persist live FIX data, or mutate trading state.
## Phase 7A Handoff Checklist

- [ ] Phase 7A ADR exists and recommends Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run.
- [ ] Phase 7A boundary checklist exists.
- [ ] Phase 7A gate is PASS.
- [ ] Phase 6 remains closed with additional-instrument planning non-executable.
- [ ] Scheduler/polling remains absent.
- [ ] Runtime shadow replay submit remains absent.
- [ ] Orders remain absent from API/Worker runtime surfaces.
- [ ] Real gateway registration remains absent.
- [ ] API/Worker remain `FakeLmaxGateway` only.
