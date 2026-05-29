# LMAX Read-Only SecurityID Confirmation Operator Checklist

This checklist covers Phase 6F/6G manual SecurityID confirmation records for additional Demo MarketData planning candidates.

The workflow is local-only. It does not connect to LMAX, call external APIs, run market-data snapshots, run replay, add scheduler/polling, submit orders, register a gateway, submit runtime shadow replay, or mutate trading state.

## When To Create A Record

Create a confirmation record only when you have sanitized evidence for a candidate instrument:

- GBPUSD
- USDJPY
- EURGBP
- AUDUSD

Evidence can support planning only. It does not authorize an external run.

## Acceptable Evidence Sources

- `OfficialLmaxDocument`
- `ConnectivityLabSanitizedOutput`
- `OperatorManualConfirmation`
- `VendorSupportConfirmation`
- `Other`

Do not paste credentials, host/user/password values, account identifiers, raw FIX, or sensitive session values into records.

## Required Fields

- `Symbol`
- `SlashSymbol`
- `ProposedSecurityId`
- `EvidenceSourceType`
- `EvidenceReference`
- `CapturedBy`
- `ReviewedBy` for `AcceptedForPlanning`
- `ReviewReason` for `AcceptedForPlanning`
- `Confidence=High` or `Confidence=Confirmed` for `AcceptedForPlanning`
- `IsApprovedForExternalRun=false`
- `NoSensitiveContent=true`

## Forbidden Content

- Credentials, passwords, secrets, tokens, API keys, private keys.
- Host/user/password values.
- Raw FIX containing sensitive tags, especially 553 or 554.
- Account identifiers.
- Order message fields or order authorization.
- Production/UAT approval language.
- Any text implying an external run is approved.

## Generate Templates

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force
```

Templates are written under ignored artifacts:

`artifacts/lmax-readonly-runtime-securityid-confirmations/templates/`

## Create A Draft Record

Draft and `NeedsMoreEvidence` records may use blank or placeholder SecurityIDs.

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "PHASE6D-DISCOVERY-PENDING-GBPUSD" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "Pending sanitized source reference" -CapturedBy "local-operator" -Decision Draft
```

## Create An AcceptedForPlanning Record

`AcceptedForPlanning` requires a non-placeholder SecurityID and reviewer metadata.

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized local reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only; no external run approval" -Confidence High -Decision AcceptedForPlanning
```

## Review Records

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1
```

## Decision Meaning

| Decision | Meaning |
| --- | --- |
| `PASS` | All four candidates have valid `AcceptedForPlanning` records and all remain non-executable. |
| `PASS_WITH_KNOWN_WARNINGS` | Records are missing, pending, or draft, but the boundary is safe. |
| `FAIL` | A record is unsafe, invalid, conflicting, sensitive, or implies authorization. |

## Safety Confirmation

`IsApprovedForExternalRun` remains false for every record. Confirmation records do not authorize sockets, snapshots, replay, scheduler, polling, order submission, gateway registration, production/UAT, or trading mutation.

## Phase 6H Real Records

Real confirmation records are written only after trusted operator evidence is available:

`artifacts/lmax-readonly-runtime-securityid-confirmations/real/`

Use `-WhatIfPreview` first, then write the record only if the preview is sanitized and non-executable. The review script reads the real directory by default; templates are not treated as real accepted records unless explicitly requested.

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1
```

`PASS` means all four candidates have valid `AcceptedForPlanning` records. `PASS_WITH_KNOWN_WARNINGS` means records are missing or pending but safe. `FAIL` means unsafe, invalid, conflicting, sensitive, or externally approved content. Next recommended phase is Phase 6I to apply accepted planning values while still keeping `IsApprovedForExternalRun=false`, or remain pending evidence if records are missing.

## Phase 6I Discovery Inputs

Phase 6I can produce sanitized `SecurityListRequest` discovery artifacts under `artifacts/lmax-readonly-runtime-securityid-discovery/`. Use `candidateMatches` as source evidence for later Phase 6H/6J record preparation, and include the artifact path as the sanitized evidence reference. The discovery artifact itself does not approve external runs and does not create accepted confirmation records automatically.

If SecurityList discovery fails, Phase 6J diagnostics should be reviewed before creating confirmation records. A failed artifact is not evidence for a SecurityID value; it is evidence only that the attempted request profile failed safely.

## Phase 6L Fallback Inputs

The Phase 6K AutoSequence artifact may be used only to decide the next evidence source. If `scripts/review-lmax-readonly-runtime-securitylist-discovery-failure.ps1` reports no candidate matches or missing reject diagnostics, do not create accepted SecurityID records from that artifact. Use `VendorSupportConfirmation`, an official LMAX document, or approved manual web GUI instrument information as the confirmation source in a later phase. Any resulting record must remain `IsApprovedForExternalRun=false`.

## Phase 6M CSV Evidence

Uploaded LMAX instrument CSVs can be used as `OfficialLmaxDocument` evidence when they include `Instrument Name`, `LMAX ID`, and `LMAX symbol`. For the current DemoLondon/NewYork profile, accepted planning records should use GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007. Tokyo values EUR/USD=6001, GBP/USD=6002, EUR/GBP=6003, USD/JPY=6004, and AUD/USD=6007 are not selected for the current profile.

The CSV-backed records remain local planning records only. Do not mark any record or instrument approved for external run.

## Phase 6N Planning Manifest Use

After Phase 6M records pass review, Phase 6N applies only the accepted planning values to a local manifest. The manifest must reference the accepted confirmation records and must keep `IsApprovedForExternalRun=false` for GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007. The manifest is not permission to run snapshots or live execution.

## Phase 6O Safety Gate Use

Phase 6O reads the Phase 6N manifest and creates one per-instrument safety gate result for GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007. The gate is a planning review artifact only. It confirms the accepted planning values are complete and safe, but every instrument must remain `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false`.

If Phase 6O passes, the next recommended phase is Phase 6P - Manual Additional Instrument Snapshot Preflight Design, No External Run. If Phase 6O fails, fix the confirmation records or planning manifest before proceeding.

## Phase 6P Preflight Use

Phase 6P turns the accepted planning values and Phase 6O safety gates into a local preflight design manifest. It keeps GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007 as planning values only. The profile is `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, `MarketDepth=1`.

The preflight manifest is not permission to connect or run a snapshot. `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` must remain false until a later explicit phase.

## Phase 6Q Approval Envelope

The Phase 6Q approval envelope is a planning artifact for one selected instrument. It references a PASS Phase 6P preflight result and records operator/reviewer ids, reason, and attestations. Even when the envelope is `AcceptedForPlanning`, it does not permit a snapshot or external run. The next phase must still be a no-external-run dry-run report or a separate explicit boundary.

## Phase 6R Dry-Run Report

Phase 6R consumes the accepted GBPUSD approval envelope and source planning artifacts to create a dry-run report. The report is local evidence that the chain is internally consistent. It does not change `IsApprovedForExternalRun`, `eligibleForManualSnapshotAttempt`, or `canRunExternalSnapshot`; all remain false.
### Phase 6S Context

The GBPUSD `4002` planning value now feeds a Phase 6S single-instrument attempt gate. This gate is non-executable and only confirms that the accepted planning record, planning manifest, safety gate, preflight, approval envelope, and dry-run report are consistent.

No SecurityID confirmation record or gate result may set `IsApprovedForExternalRun=true`.

Phase 6T keeps the same GBPUSD `4002` planning value in a non-executable execution plan. The plan does not change SecurityID approval state and does not authorize a snapshot.

Phase 6U keeps the same GBPUSD `4002` planning value in a non-executable operator signoff. It does not change SecurityID approval state and does not authorize a snapshot.

Phase 6V keeps the same GBPUSD `4002` planning value in a final non-executable readiness artifact. It does not change SecurityID approval state and does not authorize a snapshot.

Phase 6W uses the same GBPUSD `4002` value for the one-shot wrapper only. It must not make any other Phase 6 instrument runnable.
## Phase 6Z-A Planning Pipeline Closure

The official CSV-backed planning values remain the source for the additional-instrument pipeline: GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007, all with SecurityIDSource `8`.

Phase 6Z-A does not change confirmation records and does not approve external runs. It only uses the accepted planning values to replicate the downstream non-executable planning chain for EURGBP, USDJPY, and AUDUSD, alongside GBPUSD. All generated artifacts must preserve `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`.

## Phase 6Z-D Planning Freeze

Phase 6Z-D freezes the CSV-backed SecurityID planning values in the final additional-instrument documentation pack:

- GBPUSD / GBP/USD / 4002
- EURGBP / EUR/GBP / 4003
- USDJPY / USD/JPY / 4004
- AUDUSD / AUD/USD / 4007

The freeze is documentation/reporting only. It does not edit confirmation records, approve external runs, run snapshots, schedule retries, submit to shadow replay, submit orders, register gateways, or mutate trading state. `IsApprovedForExternalRun=false` remains mandatory for every confirmation-derived planning value.
