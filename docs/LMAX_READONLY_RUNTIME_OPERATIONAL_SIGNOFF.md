# LMAX Read-Only Runtime - Operational Signoff

## 1. Purpose

Phase 5W is the operational signoff for the controlled manual Demo MarketData workflow. It freezes the validated process as manual, Demo-only, read-only, artifact-audited, and non-mutating.

This signoff is documentation, validation, and reporting only. It does not add runtime capability.

## 2. Final Workflow State

The validated workflow is:

1. Three operator-approved manual Demo EURUSD / SecurityID `4001` read-only MarketData snapshots.
2. Three sanitized snapshot artifacts.
3. Three `MarketDataOnly` evidence previews.
4. Three explicit manual local replays through the existing local shadow replay API.
5. Zero observations.
6. Mutation guards unchanged.
7. Final Phase 5V audit pack decision `PASS`.

API and Worker remain `FakeLmaxGateway` only. Runtime still does not submit to shadow replay.

## 3. Validated Evidence

Reference inputs:

- Phase 5V audit pack JSON: `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json`
- Phase 5V audit pack Markdown: `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md`
- Phase 5V gate report: `artifacts/readiness/phase5v-final-audit-pack-gate.json`
- Replay-enabled workflow manifest: `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/lmax-readonly-marketdata-workflow-20260508-162327.json`
- Stability summary: `artifacts/lmax-readonly-runtime-demo-snapshot/stability/lmax-readonly-demo-snapshot-stability-20260508-144517.json`

Expected signoff counts:

- `ArtifactCount=3`
- `EvidencePreviewCount=3`
- `ManualReplayCount=3`
- `TotalObservationCount=0`
- `RuntimeShadowReplaySubmit=false`
- `ExternalConnectionAttempted=false`
- `CredentialValuesReturned=false`

## 4. Signoff Meaning

`PASS` authorizes only this recognition:

- The controlled manual Demo read-only MarketData workflow has been validated as an operationally documented process.

`PASS` does not authorize:

- scheduler
- polling
- runtime shadow replay submit
- order submission
- gateway registration
- UAT or production use
- multi-instrument expansion
- automatic execution
- trading-state mutation

## 5. How To Generate Signoff

Run locally:

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 `
  -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json `
  -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md `
  -SignoffBy "local-operator" `
  -Role "Operator" `
  -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
```

Then run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

Both scripts are local-only. They do not connect to LMAX, read credentials, run a snapshot, run replay, start a scheduler, register a gateway, submit orders, submit runtime shadow replay, or mutate trading state.

## 6. Responsibilities

Operator responsibilities:

- confirm the signoff references the intended Phase 5V audit pack;
- verify the final decision is `PASS`;
- keep generated signoff artifacts under ignored `artifacts/readiness/`;
- stop immediately if any secret, raw FIX, order command, scheduler, or mutation signal appears.

Developer responsibilities:

- keep API/Worker on `FakeLmaxGateway`;
- keep runtime shadow replay submit absent;
- keep workflow scripts manual and explicit;
- update this document before any future workflow boundary change.

Risk/review responsibilities:

- treat Phase 5W as a manual Demo read-only workflow signoff only;
- require a separate future prompt for scheduler, polling, runtime replay submit, broader instruments, UAT, production, or orders;
- review generated signoff and gate reports before accepting the freeze.

## 7. Rollback And Abort

Abort if any of the following appears:

- credential values or raw sensitive FIX in output or artifacts;
- order command surface;
- scheduler or polling marker;
- runtime shadow replay submit;
- API/Worker gateway change away from `FakeLmaxGateway`;
- trading-state mutation signal;
- signoff decision other than `PASS`.

Rollback:

1. Stop the local process.
2. Clear shell-only Phase 5W variables.
3. Verify `/health` reports `FakeLmaxGateway` if the local API is running.
4. Re-run the Phase 5V audit pack gate.
5. Re-run the Phase 5W signoff gate.

No DB rollback is expected because Phase 5W does not mutate trading state.

## 8. Next Possible Phases

Recommended next phase:

- Phase 5X - Optional Operator Console Summary / Read-Only Workflow Status Panel

Alternative planning-only next phase:

- Phase 5X - Planning Gate for Future Manual Runtime Shadow Replay Submit, if ever desired

Neither path should enable scheduler, polling, runtime shadow replay submit, order submission, gateway registration, production use, or trading mutation without a separate explicit gate.

## 9. Phase 5X Status Summary

Phase 5X adds a read-only operator status view over this signoff:

- local script: `scripts/show-lmax-readonly-marketdata-workflow-status.ps1`
- local API: `GET /lmax-readonly-runtime/marketdata-workflow/status`
- UI panel: LMAX Shadow page, `LMAX Read-Only Demo MarketData Workflow`
- gate: `scripts/check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1`

The status summary reports the signoff decision, audit-pack decision, artifact count, evidence preview count, manual replay count, total observation count, safety flags, `FakeLmaxGateway` mode, allowed review activities, and explicit non-authorizations.

It remains visibility only. It does not connect to LMAX, read credentials, run snapshots, run replay, submit to shadow replay from runtime, start scheduler/polling, submit orders, register a gateway, or mutate trading state.
