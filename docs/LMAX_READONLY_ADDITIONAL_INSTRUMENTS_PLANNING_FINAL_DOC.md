# LMAX Read-Only Additional Instruments Planning Final Documentation Pack

## Executive Summary

Phase 6Z-D freezes the additional-instrument planning state for the LMAX read-only Demo MarketData expansion path.

The planning pipeline is complete for GBPUSD, EURGBP, USDJPY, and AUDUSD. It is intentionally non-executable. A `PASS` means the local planning artifacts are internally consistent, sanitized, and ready for future operator consideration one instrument at a time. It does not authorize an external run.

No LMAX connection, SecurityListRequest, MarketData snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, or trading-state mutation is introduced by this phase.

## Validated Additional Instrument List

| Symbol | Slash Symbol | SecurityID | SecurityIDSource | Environment | Venue Profile | Request Mode | Symbol Encoding | MarketDepth |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GBPUSD | GBP/USD | 4002 | 8 | Demo | DemoLondon | SnapshotPlusUpdates | SecurityIdOnly | 1 |
| EURGBP | EUR/GBP | 4003 | 8 | Demo | DemoLondon | SnapshotPlusUpdates | SecurityIdOnly | 1 |
| USDJPY | USD/JPY | 4004 | 8 | Demo | DemoLondon | SnapshotPlusUpdates | SecurityIdOnly | 1 |
| AUDUSD | AUD/USD | 4007 | 8 | Demo | DemoLondon | SnapshotPlusUpdates | SecurityIdOnly | 1 |

## SecurityID Source

The SecurityIDs were sourced from uploaded LMAX instrument CSV files containing `Instrument Name`, `LMAX ID`, and `LMAX symbol` columns.

The selected values are the London/NewYork 400x IDs:

| Instrument | Selected DemoLondon/NewYork ID |
| --- | --- |
| GBP/USD | 4002 |
| EUR/GBP | 4003 |
| USD/JPY | 4004 |
| AUD/USD | 4007 |

Tokyo 600x IDs exist in the uploaded Tokyo file, but they are explicitly not selected for the current DemoLondon profile.

## Artifact Chain Summary

The frozen planning chain includes:

- Sanitized confirmation records from the uploaded LMAX instrument CSVs.
- Phase 6N planning manifest.
- Phase 6O per-instrument safety gates.
- Phase 6P snapshot preflights.
- Phase 6Q approval envelopes.
- Phase 6R dry-run reports.
- Phase 6S attempt gates.
- Phase 6T execution plans.
- Phase 6U operator signoffs.
- Phase 6V final readiness artifacts.
- Phase 6Z-A aggregate planning pipeline manifest.
- Phase 6Z-C operator console planning status report and read-only UI/API summary.

## Per-Instrument Planning State

| Symbol | Slash | SecurityID | Planning | Safety Gate | Preflight | Approval | Dry Run | Attempt Gate | Execution Plan | Operator Signoff | Final Readiness | Executable |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GBPUSD | GBP/USD | 4002 | AcceptedForPlanning | PASS | PASS | AcceptedForPlanning | PASS | PASS | PASS | SignedForPlanning | PASS | false |
| EURGBP | EUR/GBP | 4003 | AcceptedForPlanning | PASS | PASS | AcceptedForPlanning | PASS | PASS | PASS | SignedForPlanning | PASS | false |
| USDJPY | USD/JPY | 4004 | AcceptedForPlanning | PASS | PASS | AcceptedForPlanning | PASS | PASS | PASS | SignedForPlanning | PASS | false |
| AUDUSD | AUD/USD | 4007 | AcceptedForPlanning | PASS | PASS | AcceptedForPlanning | PASS | PASS | PASS | SignedForPlanning | PASS | false |

## Safety Confirmations

| Safety Field | Frozen Value |
| --- | --- |
| executableCount | 0 |
| IsApprovedForExternalRun | false for all instruments |
| canRunExternalSnapshot | false for all instruments |
| eligibleForManualSnapshotAttempt | false for all instruments |
| runtimeShadowReplaySubmit | false |
| scheduler/polling | false |
| orderSubmission | false |
| gatewayRegistration | false |
| tradingMutation | false |
| API/Worker gateway mode | FakeLmaxGateway only |
| noSensitiveContent | true |

## What PASS Means

`PASS` means the additional-instrument planning pipeline is complete, internally consistent, sanitized, and non-executable.

Operators may inspect the planning manifest, safety gates, preflights, approval envelopes, dry-run reports, attempt gates, execution plans, operator signoffs, final readiness artifacts, aggregate pipeline manifest, documentation pack, and read-only operator console summary.

## What PASS Does Not Authorize

`PASS` does not authorize:

- External run.
- Scheduler.
- Polling.
- Runtime shadow replay submit.
- Order submission.
- `NewOrderSingle`, cancel, replace, TradeCapture, or OrderStatus flows.
- Real gateway registration.
- Replacing `FakeLmaxGateway`.
- Production or UAT use.
- Multi-instrument batch.
- Automatic retry.
- Trading-state mutation.
- Persistence of live FIX data into trading tables.

## Operator Console Summary

Phase 6Z-C added read-only visibility through:

- `GET /lmax-readonly-runtime/additional-instruments/planning-status`
- `scripts/show-lmax-readonly-additional-instrument-planning-status.ps1`
- The LMAX Shadow operator console panel titled `LMAX Additional MarketData Instruments - Planning Status`

These surfaces are status-only. They do not include controls to connect to LMAX, request snapshots, replay evidence, schedule work, submit orders, edit credentials, or register a gateway.

## Rollback And Abort Criteria

Abort any future additional-instrument execution planning if any of the following appears:

- Wrong symbol, slash symbol, SecurityID, or SecurityIDSource.
- Non-Demo environment or non-DemoLondon venue profile.
- `IsApprovedForExternalRun=true`.
- `canRunExternalSnapshot=true` outside a later explicit execution phase.
- `eligibleForManualSnapshotAttempt=true` outside a later explicit execution phase.
- Scheduler, polling, timer, background job, or hosted service added for LMAX runtime.
- Runtime shadow replay submit enabled.
- Order submission, `NewOrderSingle`, cancel, replace, TradeCapture, or OrderStatus path added.
- Real LMAX gateway registration in API or Worker.
- Trading-table persistence or trading-state mutation.
- Credential values, endpoint values, account identifiers, or raw sensitive FIX in artifacts, docs, UI, logs, or reports.

Rollback for this phase is documentation-only: remove or regenerate the documentation pack and gate report after fixing the underlying local planning artifacts. No database rollback is expected because this phase adds no runtime mutation.

## Next Action Guidance

Today is outside normal FX market hours, so no GBPUSD or additional-instrument snapshot should be attempted.

The next safe options are:

- Stop with the additional-instrument planning state frozen.
- During market hours, proceed only if the operator explicitly chooses Phase 6Z-B for one selected additional instrument.

GBPUSD remains the next selected candidate because its first Saturday attempt completed safely as `CompletedWithEmptyBook`, which is expected outside normal FX market hours. Any future attempt must be one instrument only, manual only, Demo only, read-only, and run through an explicit operator command.

Phase 6Z-E adds a read-only next-action card for this guidance. The card is visibility only; it does not authorize running from the UI and does not add scheduler, polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

## Primary Artifact References

| Artifact | Path |
| --- | --- |
| Planning manifest | `artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-securityid-planning-manifest-20260509-135510.json` |
| Safety gate manifest | `artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-20260509-142938.json` |
| Preflight manifest | `artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json` |
| Aggregate planning pipeline | `artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json` |
| Operator planning status report | `artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json` |
| Phase 6Z-C gate | `artifacts/readiness/phase6zc-additional-instrument-status-panel-gate.json` |
| Phase 6Z-D documentation pack directory | `artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/` |
| Phase 6Z-D gate | `artifacts/readiness/phase6zd-additional-instruments-doc-pack-gate.json` |

## Validation Commands

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-additional-instruments-planning-doc-pack.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json `
  -DocPackFile <generated-doc-pack-json>
```

Both commands are local-only and must not run LMAX, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation.
## Phase 7A Boundary Reference

Phase 7A keeps this additional-instrument planning pack frozen as non-executable. GBPUSD, EURGBP, USDJPY, and AUDUSD remain planning-ready only with `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`.

The next architecture boundary is documented in `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`. The recommended next phase is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. It must remain manual, one instrument at a time, with no scheduler/polling, no runtime shadow replay submit, no orders, no real gateway registration, no production/UAT, no multi-instrument batch execution, and no trading mutation.
