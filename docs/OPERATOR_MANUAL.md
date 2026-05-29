# QQ.Production.Intraday - Operator Manual

## 1. What This System Is

`QQ.Production.Intraday` is an internal platform for intraday FX operations. It brings together views that are normally called PMS, OMS, and EMS:

- PMS: portfolio, positions, cash/wallets, and PnL.
- OMS: model runs, trade intents, orders, and risk decisions.
- EMS: execution state, fills, market data, and broker-side evidence.

It also helps operators monitor risk, exceptions, governance approvals, daily operations, LMAX EOD reconciliation, and LMAX shadow evidence.

The current environment is safe and local. It is designed for development, testing, rehearsal, and auditability, not live trading.

## 2. Most Important Safety Message

The current runtime is **SAFE LOCAL**.

- The main platform cannot send real LMAX orders.
- The main API and Worker use only `FakeLmaxGateway`.
- Real LMAX connectivity exists only in isolated Connectivity Lab scripts.
- Shadow replay is diagnostic only. It does not change orders, fills, positions, wallets, risk, or reconciliation.
- The shadow reader is disabled by default and does not connect to LMAX.
- The read-only runtime fake preview endpoint, when deliberately enabled for local testing, uses in-memory fake scenarios only. It does not call LMAX and does not submit to shadow replay.
- The future external read-only session skeleton is present for developer planning only. It is not functional and has no socket activation, FIX logon, credential use, order submission, or trading-state mutation.
- The guarded transport boundary is also developer planning only. Its connect/read/disconnect operations are disabled and do not open a network connection.
- The external read-only preflight and dry-run report endpoints validate future operator intent only. They return blocked/no-network guidance and always report that no session started, no external connection was attempted, no credentials were read, no shadow replay was submitted, and no trading state was changed.
- The external read-only signoff endpoint validates future human signoff metadata only. It can show that required attestations were provided, but it does not approve or authorize execution.
- The external read-only pre-activation audit endpoint validates the intent, dry-run report, and signoff chain as metadata only. It records the final no-execution outcome but does not approve, persist, or authorize execution.
- The external read-only readiness snapshot endpoint summarizes the entire blocked chain as metadata only. It can show why the future reader is not ready, but it cannot start a session or approve execution.
- The final no-socket release gate script summarizes whether all Phase 4A-4O no-socket safeguards are present. A passing gate is not live connectivity approval; it only means a separate future socket prototype prompt may be considered.
- The future configuration envelope uses profile labels only. It contains no credential values and does not make the external reader functional.
- The external read-only preflight endpoint validates a future run request only. It cannot start a session and reports no external connection, credential read, shadow replay submit, or trading mutation.
- The UI has no credential forms and no live LMAX trading controls.

If the health banner does not show FakeLmax-only behavior, or if safety status is unclear, stop and escalate before proceeding.

Before moving to a new technical phase, the team should run the Operational Readiness Checklist and release gate. It confirms build/test health, documentation freshness, FakeLmax-only safety, shadow replay behavior, and known warnings. The checklist lives at [OPERATIONAL_READINESS_CHECKLIST.md](OPERATIONAL_READINESS_CHECKLIST.md).

## 3. Key Concepts in Human Language

PMS:

- The portfolio view: positions, targets, drift, cash, wallets, and PnL.

OMS:

- The order-management view: model runs, trade instructions, risk checks, orders, and fills.

EMS:

- The execution-management view: execution state, market data, fills, and future broker evidence.

Risk Control Center:

- The place to see active risk controls, trading windows, instrument controls, venue controls, and kill-switch state.

Reconciliation:

- The process of comparing internal records with imported broker/EOD records to find breaks.

Exceptions and breaks:

- Items that require review, investigation, approval, or resolution.

Governance and four-eyes:

- Maker/checker approval for sensitive actions.

Audit trail:

- A record of who did what, when, why, and with which correlation ID.

Daily Operations:

- The job and runbook control center for Start of Day, Intraday, and End of Day routines.

LMAX EOD:

- Imported local LMAX report files used for daily reconciliation.

LMAX Shadow:

- A diagnostic comparison tool that replays LMAX-like evidence and creates observations. It does not trade and does not change trading state.

Connectivity Lab:

- A separate developer/lab tool area for controlled LMAX FIX experiments. It is not part of the main runtime.

## 4. Daily Workflow

### Start of Day

1. Check the health and safety banner.
2. Confirm the gateway is FakeLmax-only and live trading is false.
3. Check reference data integrity.
4. Confirm an active risk set exists.
5. Review previous EOD reconciliation status.
6. Review open exceptions and blocking items.
7. Check Daily Operations Start of Day runbook status.
8. Complete manual confirmations only when you have reviewed the checks.

### Intraday

1. Monitor the Command Center.
2. Review model runs and processing state.
3. Review OMS orders, fills, and risk decisions.
4. Review PMS positions and drift.
5. Watch warnings and blocking exceptions.
6. Do not clear blocking items without a reason and, where required, approval.
7. Treat LMAX Shadow warnings as diagnostic evidence, not live trading failures, unless policy and context say otherwise.

### End of Day

1. Import generated/local LMAX EOD reports.
2. Run EOD reconciliation.
3. Review breaks and exception cases.
4. Review wallets and USD PnL summary.
5. Assign or resolve exceptions with reasons.
6. Check the audit trail.
7. Complete End of Day runbook manual confirmation only after review.

## 5. UI Page Guide

### Command Center

Purpose:

- High-level runtime, safety, operations, risk, exception, and reconciliation state.

Healthy state:

- FakeLmax gateway.
- Live trading false.
- External connections false.
- No blocking exceptions.
- Daily jobs/runbooks healthy.
- The top banner reads `SAFE LOCAL / FakeLmax-only`.

Operator aids:

- `Operator Next Actions` gives plain-language triage hints such as do-not-proceed, review risk blocks, or inspect failed jobs.
- `Latest Audit Events` shows recent operator/system activity with reasons and correlation context.

Escalate if:

- Gateway is not FakeLmax.
- Live trading appears enabled.
- External connections appear enabled unexpectedly.
- Blocking exceptions or failed daily jobs are present.

### PMS

Purpose:

- Positions, target positions, drift, wallets, cash, and PnL.

Healthy state:

- Positions are explainable.
- Drift is understood.
- Wallet/PnL summaries are available after EOD.

Escalate if:

- Position changes are unexpected.
- PnL/wallet numbers are missing after EOD.
- Drift cannot be explained.

### Model Weights

Purpose:

- View, validate, and promote DB-staged model weight batches.

Healthy state:

- Batches validate cleanly.
- Promotion is explicit and audited.

Safe actions:

- Create fake local batch.
- Validate.
- Promote ready batches with a reason.

Escalate if:

- Blocking validation issues exist.
- A batch is promoted unexpectedly.

### OMS

Purpose:

- Model runs, trade intents, risk decisions, orders, and fills.

Healthy state:

- Model runs process explicitly.
- Risk decisions are explainable.
- Orders and fills match the local simulator path.

Escalate if:

- Model runs are blocked.
- Orders/fills are duplicated or unexpected.
- Risk blocks are unclear.

### EMS

Purpose:

- Execution state, fills, market data, and execution-quality views.

Healthy state:

- Fills are consistent with orders.
- Market data is recent enough for local processing.

Escalate if:

- Fill state does not match order state.
- Market data is stale when a run needs fresh data.

### Market Data

Purpose:

- Local market data snapshots and bars.

Healthy state:

- Snapshots and 15 minute bars are present for required instruments.

Safe actions:

- Create local fake snapshots.
- Build local bars.

Escalate if:

- Required data is stale or missing.

### Reconciliation

Purpose:

- Compare internal state with broker/EOD report state and identify breaks.

Healthy state:

- No unresolved blocking breaks.

Escalate if:

- Blocking or critical breaks appear.
- A break is waived without a clear reason.

### LMAX EOD

Purpose:

- Import and review LMAX-style EOD reports, wallet data, and daily reconciliation inputs.

Healthy state:

- Import runs complete.
- Validation issues are understood.
- EOD reconciliation is clean or breaks are assigned.

Escalate if:

- Files are missing.
- Blocking import or reconciliation issues exist.

### Exceptions

Purpose:

- Manage operational breaks and exception cases.

Healthy state:

- Warnings are acknowledged or assigned.
- Blocking items are investigated and resolved with reasons.

Safe actions:

- Acknowledge.
- Assign.
- Investigate.
- Add notes.
- Resolve, waive, or mark false positive only with clear reason and approval if required.

The page labels actions in operator language: acknowledge means seen, investigation means actively reviewing, and resolve/waive/false-positive require clear reasons.

### Risk & Admin / Risk Control Center

Purpose:

- View and manage risk sets, trading windows, venue/instrument controls, and kill switch.

Healthy state:

- One appropriate active risk set.
- Controls are understood.
- Kill switch status is intentional.

Escalate if:

- No active risk set exists.
- Controls are changed unexpectedly.
- Kill switch is cleared without approval.

### Governance

Purpose:

- View approvals, permissions, and maker/checker decisions.

Healthy state:

- Pending approvals are understood.
- The requester does not approve their own request.

Escalate if:

- Approval chain is unclear.
- A sensitive action is executed without proper approval.
- The same operator appears as both requester and approver for a four-eyes action.

### Daily Operations

Purpose:

- Job runs, runbooks, checklist, timeline, and local schedule metadata.

Healthy state:

- Start of Day, Intraday, and End of Day runbooks are complete or intentionally waiting for operator confirmation.
- Failed jobs are reviewed.

Escalate if:

- Critical jobs fail.
- Runbook waits unexpectedly.
- Retry behavior is unclear.
- A failed step has no clear reason, correlation ID, retry path, or owner.

### LMAX Shadow

Purpose:

- Replay sanitized LMAX evidence and compare it with internal state.

Healthy state:

- Replay status is Completed or CompletedWithWarnings for expected lab evidence.
- Warnings are understood.
- Blocking observations are investigated.

Important:

- LMAX Shadow does not call LMAX.
- LMAX Shadow does not trade.
- LMAX Shadow does not change positions or fills.
- Observation rows show evidence mode, policy code, rationale/suggested operator action, and whether the policy can create an exception case.
- Filters include status, severity, type, evidence mode, policy code, replay ID, symbol, fingerprint, and text search.
- Open a row to see workflow links such as replay run ID, observation ID, fingerprint, linked exception case ID, correlation ID, and policy code.
- Technical payloads are kept in the advanced raw JSON section so operators can first read the plain-language guidance.

### Connectivity Lab

Purpose:

- Developer/lab-only LMAX FIX experiments and evidence capture.

Important:

- Do not run external lab scripts unless explicitly intended.
- Demo order lifecycle scripts can submit Demo orders only with explicit safety flags.
- Lab evidence files must be sanitized and should not be committed.

## 6. Exceptions and Breaks

Severity meanings:

- Info: useful record; normally no action.
- Warning: review and understand.
- Blocking: action should not proceed until reviewed/resolved.
- Critical: urgent blocking issue; escalate.

Common actions:

- Acknowledge: "I have seen this."
- Assign: "This person/team owns investigation."
- Investigate: "Work is in progress."
- Resolve: "The issue is fixed or explained."
- Waive: "We accept this exception for a stated reason."
- False positive: "The system flagged it, but it is not a real issue."

Every meaningful action requires a reason and is audited. Never hide a real break.

### Closing the workflow

When an exception originates from shadow evidence, follow this path:

1. Open the exception case.
2. Read the source fields: replay run ID, shadow observation ID, fingerprint, policy code, evidence mode, and correlation ID.
3. Open the matching LMAX Shadow observation if needed and compare the rationale/suggested operator action.
4. Review audit events for the same exception case ID or correlation ID.
5. If approval is required, complete the governance step before resolving, waiving, or marking false positive.
6. Close only with a reason that explains the evidence reviewed and the decision made.

If the source link is missing or unclear, keep the case open and escalate.

## 7. Governance and Four-Eyes

Four-eyes means one person requests and a different person approves.

Why it matters:

- Prevents accidental or unilateral sensitive changes.
- Creates an audit trail.
- Separates maker and checker roles.

Examples of sensitive actions:

- Activating or retiring risk sets.
- Clearing kill switch.
- Waiving blocking or critical exceptions.
- Marking blocking/critical exceptions false positive or resolved.

The Governance page is the place to check pending approvals, requested action, requester, approver role, status, and decision history. A four-eyes item is not complete until a permitted checker has approved it and the audited action has executed.

## 8. Risk Control Center

Risk sets can be:

- Draft
- Active
- Retired
- Archived

Instrument and venue controls can include:

- Trading enabled.
- Report import enabled.
- Market data enabled.

The kill switch is a strong control. Never clear it casually. If a risk block exists and you do not understand it, stop and escalate.

## 9. LMAX Shadow in Plain English

LMAX Shadow compares evidence that looks like LMAX broker evidence with the platform's internal records.

It can say:

- "This LMAX fill matches an internal fill."
- "LMAX has a trade capture report that is not in internal fills."
- "LMAX returned an order status for an order we do not know."
- "LMAX rejected a request."

It does not:

- Send orders.
- Change positions.
- Create fills.
- Change risk decisions.
- Change reconciliation.

### How to follow a shadow observation

1. Start on the LMAX Shadow page and filter by severity, evidence mode, policy code, replay ID, symbol, or fingerprint.
2. Open the observation drawer.
3. Read the operator guidance first. It explains whether the item is expected lab evidence, review-only, or blocking.
4. Use the workflow links to find the replay run, linked exception case, correlation ID, and related audit events.
5. If the observation says it creates or should create an exception, inspect the exception case before taking action.
6. Acknowledge, resolve, or ignore only with a reason.

Warnings mean review and explain. Blocking means stop the related workflow until the issue is investigated.

Evidence modes:

| Mode | Plain-language meaning |
| --- | --- |
| EmptyReadOnly | A valid capture with no trading evidence. |
| MarketDataOnly | Market data context only. |
| TradeCaptureOnly | Trade capture recovery evidence only. |
| OrderStatusOnly | Order status evidence only. |
| ProtocolRejectOnly | A rejected FIX request. |
| MixedReadOnly | More than one read-only evidence type. |
| SyntheticLifecycle | Sanitized test lifecycle evidence. |

Important interpretations:

- TradeCaptureOnly warning in lab often means a lab-created LMAX Demo trade is not in internal state. That is expected when the main platform never submitted the order.
- `ExecType=I` is order status only. It is not a fill.
- MarketDataOnly evidence creates no trading observation.
- Protocol rejects can be serious depending on whether they came from read-only recovery or order-path context.

## 10. What Not To Do

- Do not enter credentials into the UI.
- Do not commit generated evidence files.
- Do not run LMAX external lab scripts unless explicitly intended.
- Do not run Demo order lifecycle scripts unless explicitly intended.
- Do not waive blocking exceptions without understanding them.
- Do not assume shadow warnings are live trading failures.
- Do not manually edit generated files except for diagnostics.
- Do not proceed if safety status is unclear.

## 11. Common Scenarios

### I see a warning in LMAX Shadow

1. Open the observation detail.
2. Read the policy code, rationale, and suggested action.
3. Check whether the evidence mode is lab/read-only.
4. If it is expected lab evidence, acknowledge with a reason.
5. If it is unexpected or repeated, escalate to a developer or risk owner.

### I see a blocking observation

1. Stop related operational action.
2. Open the linked exception case if present.
3. Review policy code and evidence mode.
4. Assign or investigate with a reason.
5. Escalate to risk/developer support.

### TradeCaptureMissingInternalFill appears

This means LMAX trade-capture evidence contains an execution ID that does not match an internal fill. In lab read-only context, this is usually a warning because the main platform did not submit the lab order.

Escalate if:

- The evidence is not from a lab capture.
- The same issue appears unexpectedly.
- It affects EOD reconciliation.

### ProtocolReject appears

Read the reject context:

- Read-only request reject: usually warning.
- Order-path reject: blocking and serious.
- Unknown reject context: treat cautiously and escalate.

### EOD reconciliation break appears

1. Review the break details.
2. Check LMAX EOD import status.
3. Assign or investigate.
4. Resolve only when explained.
5. Waive only with clear reason and approval if required.

### Daily ops job failed

1. Open Daily Operations.
2. Open the job detail drawer.
3. Review steps, events, error message, and correlation ID.
4. Check whether there is a retry/original run relationship.
5. Retry only when the job is rerunnable and the reason is clear.
6. Escalate when the failure affects Start of Day, Intraday Cycle, End of Day, reconciliation, risk, or audit.

### Risk set is not active

1. Stop model-run/order processing.
2. Check Risk Control Center.
3. Read the risk decision detail: check name, status, observed value, limit value, unit, message, and related instrument/venue/model fields.
4. Request/complete appropriate approval if needed.
5. Do not bypass risk controls.

### API health is not safe

Stop. Escalate. Do not proceed if FakeLmax-only safety is not visible.

### UI shows stale data

1. Refresh the page.
2. Check API health.
3. Check whether the API is running on `http://localhost:5050`.
4. Escalate if data remains stale.

## 12. Escalation Guide

Escalate to:

- Operator lead for daily workflow questions.
- Risk approver for risk blocks, kill switch, or blocking exceptions.
- Developer for API/UI/script errors.
- LMAX/support only for lab connectivity issues, never from main runtime.

Do not proceed toward trading if safety status is unclear.

## 13. Glossary

PMS:

- Portfolio management system. Positions, targets, wallets, and PnL.

OMS:

- Order management system. Model runs, trade intents, orders, and risk decisions.

EMS:

- Execution management system. Execution state, fills, and broker-side evidence.

EOD:

- End of day. Daily broker reports and reconciliation process.

FIX:

- Financial Information eXchange protocol used by venues/brokers.

LMAX:

- FX venue/broker integration target.

TradeCapture:

- FIX recovery/evidence message family for trades.

ExecutionReport:

- FIX message that reports order or execution state.

OrderStatus:

- FIX status-only response. `ExecType=I` is not a fill.

ExecType=I:

- Order status report only.

ClOrdID:

- Client order ID assigned by the requester.

BrokerOrderId:

- Broker-side order ID.

ExecID:

- Broker execution ID. Used to identify fills.

UTI:

- Unique trade identifier. Present in EOD individual-trades files; not currently available from FIX AE evidence.

Shadow Replay:

- Local replay of sanitized evidence into observations.

Evidence:

- JSON file or normalized payload describing LMAX-like reports.

Mutation:

- A change to trading state such as orders, fills, positions, model runs, risk, wallets, or reconciliation.

Exception case:

- Operational case created for a warning/blocking/critical issue.

Audit event:

- Record of an action, reason, actor, and correlation ID.

Four-eyes:

- Maker/checker approval by two different people.

Runbook:

- Ordered operational checklist or workflow such as Start of Day or End of Day.

## LMAX Read-Only Runtime Phase 5B Operator Note

The Phase 5D Demo snapshot prototype is not a live trading control. It is a manual-only, read-only market-data boundary that can attempt only one Demo EURUSD / SecurityID `4001` snapshot when all manual flags and local credential labels are present.

The safe checks are:

Phase 5E adds clearer operator outcomes. `BlockedMissingCredentials` means fix local labels and rerun the credential check. `FailedSafeConnectionError`, `FailedSafeLogonTimeout`, and `FailedSafeSnapshotTimeout` mean stop, roll back to default API startup, run the Phase 5D/5E gates, and review sanitized diagnostics before considering another manual attempt. Retry metadata is guidance only and never causes automatic retry.

Phase 5F adds sanitized result capture for the same manual Demo EURUSD snapshot path. The script writes only ignored, sanitized diagnostics under `artifacts/lmax-readonly-runtime-demo-snapshot/`; this is not shadow replay evidence and does not authorize any trading action. Abort and escalate if a result ever shows credential values, raw sensitive FIX, order submission, scheduler start, shadow replay submit, or trading mutation.

Phase 5G adds clearer timeout diagnostics. If a run logs on but no snapshot arrives, inspect the sanitized request mode, message counters, response classification, and session warnings/errors. `FailedSafeSnapshotTimeout` means no snapshot/reject was observed before timeout. `FailedSafeMarketDataRequestRejected`, `FailedSafeBusinessReject`, or `FailedSafeSessionReject` means the session returned a reject-class message. `CompletedWithEmptyBook` means a snapshot arrived without bid/offer entries.

Phase 5H changes the safe diagnostic default to `SnapshotPlusUpdates` with `SecurityIdOnly`. Known rejected profiles, including `SnapshotOnly` / `263=0`, tag-55 symbol encodings, and `InternalSymbol`, block locally unless explicitly allowed for a known-rejected diagnostic. Do not use known-rejected diagnostics unless the goal is to reproduce an already understood LMAX reject.

Phase 5J adds logon/session diagnostics because the current blocker is MarketData FIX logon confirmation. If a run reaches TCP/TLS but receives Logout before logon is confirmed, inspect only the sanitized diagnostics: profile labels, presence/length booleans, first inbound message type, redacted Logout/Reject text, and runtime-vs-lab profile comparison. Do not proceed if any credential value, comp-id value, raw Logon FIX, tag `553`, or tag `554` appears anywhere.

Phase 5L closes the first successful Demo read-only snapshot. A successful artifact must validate as `Completed`, `snapshotReceived=true`, `logonSucceeded=true`, `logoutSucceeded=true`, `credentialValuesReturned=false`, `noSensitiveContent=true`, and `redactionStatus=Redacted`, with no order submission, scheduler start, shadow replay submit, or trading mutation. Validate an artifact locally with:

```powershell
.\scripts\validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

```powershell
.\scripts\check-lmax-readonly-runtime-phase5b-prototype-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1
```

If the manual prototype script is run, it must include an explicit reason and Demo/read-only confirmation. It must still report no order submission, no scheduler, no shadow replay submit, no gateway replacement, and no trading mutation.

Credential availability checks are also local-only:

```powershell
.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck
```

The output must show labels and present/missing booleans only. Abort and escalate if any credential value appears in console output, logs, reports, evidence, or API responses.
## External Read-Only Snapshot Evidence Preview

Phase 5M lets an operator or developer preview the first successful Demo EURUSD snapshot as sanitized market-data evidence. This is not a replay submission and does not create observations.

Run:

```powershell
.\scripts\preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

The preview must report `MarketDataOnly`, `snapshotReceived=true`, no sensitive content, and zero execution/order/trade/reject events. Do not use this step to enable scheduler, register a gateway, submit to shadow replay, or generalize toward order handling.

## Manual MarketDataOnly Replay Dry-Run

Phase 5N allows a local operator to replay the sanitized preview through the existing shadow replay endpoint. Start the local API, then run:

```powershell
.\scripts\replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile .\artifacts\lmax-readonly-runtime-demo-snapshot\evidence-preview\<preview-file>.json
```

Expected result: `Completed` with zero observations and no order/fill/position count changes. This is manual/offline replay only; runtime still does not submit to shadow replay.

## Repeated Manual Snapshot Stability Check

Phase 5O lets an operator run a small, explicit stability set of Demo EURUSD read-only snapshots. This is manual only: it requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, `-ConfirmRepeatedManualSnapshots`, a reason, and a capped attempt count.

Example:

```powershell
.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -ConfirmRepeatedManualSnapshots `
  -AttemptCount 3 `
  -DelaySeconds 2 `
  -Reason "Phase 5O operator-approved repeated Demo EURUSD read-only snapshot stability check"
```

The script reuses the existing manual snapshot prototype for each planned attempt. It validates successful artifacts, creates `MarketDataOnly` previews, and writes a sanitized stability summary under the ignored artifacts tree. It is not a scheduler, not polling, and not automatic retry. Do not use it to submit orders, register a gateway, enable runtime shadow replay submit, or mutate trading state.

## Stability Results Review

Phase 5P reviews the repeated manual snapshot summary and gives a readiness decision:

```powershell
.\scripts\review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

Expected result for the completed Phase 5O run is `PASS`: 3 requested, 3 completed, 3 successful snapshots, no failed-safe attempts, and no order, scheduler, runtime shadow replay submit, trading mutation, or credential value return. This PASS does not authorize scheduler, polling, order submission, gateway registration, runtime shadow replay submit, production use, or broader instrument coverage.

## Controlled MarketData Evidence Workflow Review

Phase 5Q lets an operator or developer review the complete manual evidence workflow as a sanitized manifest. It does not run another snapshot, does not require credentials, and does not replay by default:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
.\scripts\check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

The default result may be `PASS_WITH_WARNINGS` because replay was intentionally not requested. Optional replay requires explicit local replay flags and still uses only the manual local replay script/API. Treat Phase 5Q as an artifact review control only; it does not authorize scheduler, polling, order submission, gateway registration, runtime shadow replay submit, production use, or trading mutation.

## Manual MarketData Workflow Replay Review

Phase 5R closes the replay-omitted warning only when the operator explicitly asks for local manual replay and the local API is running:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 `
  -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json `
  -ReplayEvidencePreviews `
  -ConfirmLocalManualReplay
```

Expected result: one replay result for each `MarketDataOnly` preview, every replay `Completed`, zero observations, unchanged mutation guards, `runtimeShadowReplaySubmit=false`, and `externalConnectionAttempted=false`. This is local replay only. It does not run another Demo snapshot, connect to LMAX, schedule work, submit orders, register a gateway, submit to shadow replay from runtime, or mutate trading state.

## Controlled Manual Workflow Release Gate

Phase 5S creates the release manifest for the controlled manual workflow:

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
.\scripts\check-lmax-readonly-runtime-phase5s-release-gate.ps1
```

Replay flags are optional. If replay is skipped, the expected decision is `PASS_WITH_WARNINGS`. If replay is requested, the local API must be running and each `MarketDataOnly` preview must replay as `Completed` with zero observations and unchanged mutation guard. This release gate still does not approve scheduler, polling, order submission, gateway registration, runtime shadow replay submit, production use, broader instruments, or trading mutation.

Rollback if stopped or failed: clear Phase 5S shell variables, verify `/health` reports `FakeLmaxGateway`, rerun the Phase 5O gate if the stability summary is in doubt, then rerun the Phase 5S gate.

## Controlled Manual MarketData Workflow Freeze

Phase 5T freezes this as the operator workflow. The process remains manual-only: Demo EURUSD / SecurityID `4001`, sanitized artifacts, `MarketDataOnly` previews, and optional local replay only when explicitly requested. Runtime never submits to shadow replay.

Operator prerequisites:
- Confirm this is Demo-only and read-only.
- Confirm there is a clear reason.
- Confirm no scheduler, polling, or automated loop is being used.
- Confirm optional replay is local API replay only and requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay`.

Runbook command for the frozen release review:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 5 -Reason "Phase 5S manual workflow release test"
.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1
```

Stop immediately if a credential value appears, any order message appears, runtime shadow replay submit appears, scheduler/polling appears, API/Worker gateway changes away from `FakeLmaxGateway`, or mutation guard changes. Rollback: stop the shell command, clear Phase 5S/5T variables, verify `/health` reports `FakeLmaxGateway`, and rerun the Phase 5O/5S gates.

## Final MarketData Workflow Audit Pack

Phase 5V packages the validated manual workflow into one local audit pack. Use it only after the replay-enabled manifest exists and has `FinalDecision=PASS`.

```powershell
.\scripts\build-lmax-readonly-marketdata-workflow-audit-pack.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json -WorkflowManifestFile .\artifacts\lmax-readonly-runtime-demo-snapshot\workflow\lmax-readonly-marketdata-workflow-20260508-162327.json
.\scripts\check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\<audit-pack>.json
```

The audit pack contains sanitized counts, artifact/preview paths, replay run ids, zero-observation replay results, safety confirmations, and the final decision. It does not run LMAX, replay, scheduler, polling, orders, or runtime mutation.
## Controlled Manual MarketData Operational Signoff

Phase 5W is the final operator signoff for the controlled manual Demo read-only MarketData workflow. The signoff confirms that the Phase 5V audit pack passed, with three sanitized snapshot artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, and no mutation.

Generate and check signoff:

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md -SignoffBy "local-operator" -Role "Operator" -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

The scripts are local-only and do not run LMAX, snapshots, replay, credentials, scheduler, orders, gateway registration, or trading mutation. A `PASS` means only that the controlled manual Demo read-only MarketData workflow has been validated. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.

### Read-Only Workflow Status

Phase 5X adds a read-only operator panel named `LMAX Read-Only Demo MarketData Workflow` on the LMAX Shadow page. It shows signoff decision, audit-pack decision, artifact/preview/replay counts, zero observations, safety flags, and the expected `FakeLmaxGateway` API/Worker mode.

Script view:

```powershell
.\scripts\show-lmax-readonly-marketdata-workflow-status.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
```

This panel is informational only. It has no credential fields, host/port fields, live controls, scheduler controls, replay button, order button, or gateway activation path.

## LMAX Read-Only Runtime Phase 6A Planning Boundary

The controlled manual Demo MarketData workflow is frozen as `FrozenManualReadOnly`. Phase 6A is planning only. It does not run another external snapshot, does not replay from runtime, and does not add scheduler, polling, orders, gateway registration, or trading-state mutation.

Operator-facing Phase 6A files:

- `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`
- `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`
- `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`

Run the local planning gate with:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6a-planning-gate.ps1
```

The recommended next boundary is `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run`. PASS does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, production/UAT use, multi-instrument execution, or trading-state mutation.

## LMAX Read-Only Runtime Phase 6B Instrument Allowlist

Phase 6B is a planning checklist for possible future Demo MarketData instruments. It does not approve any new manual run.

Candidate instruments:

- GBPUSD.
- USDJPY.
- EURGBP.
- AUDUSD.

These are candidate labels only. Their Demo SecurityIDs must be confirmed in a later explicit phase before any operator can run them. The current validated runtime workflow remains EURUSD / SecurityID `4001` only.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1
```

PASS means the allowlist document and validator are present. PASS does not authorize external connections, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, production/UAT, or trading mutation.

## LMAX Read-Only Runtime Phase 6C SecurityID Confirmation

Phase 6C records local SecurityID placeholders for the Phase 6B candidate instruments. It is not an operator runbook for live execution.

Current local manifest:

- GBPUSD -> `PHASE6C-DEMO-SECURITYID-GBPUSD`
- USDJPY -> `PHASE6C-DEMO-SECURITYID-USDJPY`
- EURGBP -> `PHASE6C-DEMO-SECURITYID-EURGBP`
- AUDUSD -> `PHASE6C-DEMO-SECURITYID-AUDUSD`

Every instrument still has `IsApprovedForExternalRun=false`.

Run the local gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6c-securityid-confirmation-gate.ps1
```

PASS means the local manifest is complete and external runs remain blocked. It does not authorize a socket run, scheduler, polling, runtime shadow replay submit, orders, gateway registration, production/UAT, or trading mutation.

## LMAX Read-Only Runtime Phase 6D SecurityID Discovery Planning

Phase 6D gives operators and reviewers a local list of candidate SecurityID placeholders for the Phase 6B instruments. These are not runnable LMAX identifiers and do not approve any instrument for an external run.

Current placeholder manifest:

- GBPUSD -> `PHASE6D-DISCOVERY-PENDING-GBPUSD`
- USDJPY -> `PHASE6D-DISCOVERY-PENDING-USDJPY`
- EURGBP -> `PHASE6D-DISCOVERY-PENDING-EURGBP`
- AUDUSD -> `PHASE6D-DISCOVERY-PENDING-AUDUSD`

Every entry remains `IsApprovedForExternalRun=false`.

Run the local gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1
```

PASS means the discovery placeholders are complete and still blocked from execution. It does not authorize LMAX connection, market-data snapshot, replay, scheduler, polling, order submission, gateway registration, production/UAT, or trading mutation.

## LMAX Read-Only Runtime Phase 6E SecurityID Evidence Review

Phase 6E is a review checklist for SecurityID source evidence. It is not an execution checklist.

The accepted source types are official LMAX documentation, sanitized Connectivity Lab output, operator manual confirmation, vendor support confirmation, or another reviewed source. Records must never contain credential values, host/user/password values, raw sensitive FIX, account identifiers, or order/trading authorization language.

Current status:

- GBPUSD -> `NeedsMoreEvidence`
- USDJPY -> `NeedsMoreEvidence`
- EURGBP -> `NeedsMoreEvidence`
- AUDUSD -> `NeedsMoreEvidence`

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1
```

`PASS_WITH_KNOWN_WARNINGS` is expected while evidence remains pending. `PASS` or `PASS_WITH_KNOWN_WARNINGS` still does not authorize a socket run, snapshot, replay, scheduler, polling, order submission, gateway registration, production/UAT, or trading mutation.

## LMAX Read-Only Runtime Phase 6F SecurityID Confirmation Records

Phase 6F lets an operator/developer create sanitized local confirmation records for candidate SecurityIDs. A confirmation record can support planning, but it cannot authorize an external run.

Use only sanitized references. Do not include credentials, endpoints, account identifiers, raw FIX, order language, production/UAT approval, or execution approval.

Create:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized local reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only; no external run approval" -Confidence High -Decision AcceptedForPlanning
```

Review:

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1
```

`IsApprovedForExternalRun` is always false. `PASS` means confirmation records are locally valid for planning only. It does not authorize LMAX connection, snapshot, replay, scheduler, polling, order submission, gateway registration, production/UAT, or trading mutation.

## LMAX Read-Only Runtime Phase 6G Record Entry Hardening

Phase 6G adds safer operator tooling before real records are entered.

Generate templates:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force
```

Preview without writing:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "PHASE6D-DISCOVERY-PENDING-GBPUSD" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "Pending sanitized source reference" -CapturedBy "local-operator" -Decision Draft -WhatIfPreview
```

Run the workflow gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1
```

The gate may return `PASS_WITH_KNOWN_WARNINGS` when records are missing. That warning does not authorize a run. It only means the entry workflow is safe and waiting for evidence.

## Phase 6H Real Confirmation Records

When trusted sanitized evidence exists, create real records under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`. Preview first:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only" -Confidence High -Decision AcceptedForPlanning -WhatIfPreview
```

Then write only if the preview is clean, review with `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`, and run `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1`. `PASS` means every candidate has an accepted planning record; `PASS_WITH_KNOWN_WARNINGS` means evidence is still missing or pending; `FAIL` means stop and fix unsafe/conflicting/sensitive content. `AcceptedForPlanning` never approves an external run, and `IsApprovedForExternalRun=false` must remain present for every record.

## Phase 6I SecurityList Discovery

Use Phase 6I only when explicitly approved for a manual Demo read-only discovery. It sends SecurityListRequest only and does not request market data snapshots.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-securitylist-discovery.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6I operator-approved Demo SecurityList discovery for additional read-only MarketData instruments"
```

The artifact reports `status`, `totalInstrumentCount`, `candidateMatches`, and `unmatchedCandidates`. Any discovered SecurityID is planning evidence only. Do not use it for external runs until a later approved phase explicitly says so.

## Phase 6J Diagnostics And Profiles

The first SecurityList attempt failed safely. Phase 6J lets operators inspect that failure and prepare a safer next manual attempt. Available profiles include `MinimalRequest`, `ProductFx`, `SecurityTypeFx`, `LabCompatibleFallback`, `SymbolExact`, `CandidateSymbolsOneByOne`, and `AutoSequence`.

`AutoSequence` skips known-rejected profiles unless `-AllowKnownRejectedDiagnostics` is explicitly supplied. Phase 6J does not itself authorize another external attempt; it only prepares diagnostics and compatibility controls.

## Phase 6L Fallback Decision

The Phase 6K AutoSequence attempt failed safely with zero candidate matches. Review that artifact only with:

```powershell
.\scripts\review-lmax-readonly-runtime-securitylist-discovery-failure.ps1 -DiscoveryArtifactFile artifacts\lmax-readonly-runtime-securityid-discovery\lmax-securitylist-discovery-20260509-145908.json
```

If the review reports missing reject diagnostics or no supported SecurityList profile evidence, use vendor/support, official LMAX documentation, or another approved manual source before preparing SecurityID confirmation records. Phase 6L does not run another request and does not approve external execution; every instrument remains `IsApprovedForExternalRun=false`.

## Phase 6M CSV Confirmation Records

Phase 6M uses uploaded LMAX instrument CSVs as official source evidence. For the current DemoLondon/NewYork profile, select the 400x IDs only: GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007. Tokyo 600x IDs may appear in the Tokyo CSV but are not selected.

Run the CSV record script only locally with `-ConfirmPlanningOnly`. The generated records are `AcceptedForPlanning`, `Confidence=Confirmed`, `EvidenceSourceType=OfficialLmaxDocument`, and `IsApprovedForExternalRun=false`. They do not authorize a live run, snapshot, replay, scheduler, order path, gateway registration, or trading-state mutation.

## Phase 6N Planning Manifest

Phase 6N copies the accepted Phase 6M planning values into a local planning manifest. The manifest is not runtime configuration and does not approve any instrument for external execution. It records GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007 with SecurityIDSource `8`, Demo, DemoLondon, and `IsApprovedForExternalRun=false`.

Use the Phase 6N gate to validate the manifest before any later preflight design work. Do not run snapshots, replay, scheduler/polling, orders, or gateway registration from this phase.

## Phase 6O Per-Instrument Safety Gates

Phase 6O builds a local safety gate manifest from the Phase 6N planning manifest. It checks GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007 for Demo/DemoLondon planning completeness while keeping every instrument non-executable.

Run the builder and gate locally only:

```powershell
.\scripts\build-lmax-readonly-additional-instrument-safety-gates.ps1 -PlanningManifestFile <phase-6n-manifest>
.\scripts\check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1 -PlanningManifestFile <phase-6n-manifest> -SafetyGateManifestFile <phase-6o-manifest>
```

`PASS` means the planning data is safe and complete. It does not approve an external run or snapshot attempt. `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false` remain required for every instrument.

## Phase 6P Additional Snapshot Preflight

Phase 6P creates a local preflight design artifact for a possible later one-off manual Demo read-only MarketData snapshot attempt. It does not run a snapshot and does not make any instrument eligible.

The preflight profile is fixed to `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, and `MarketDepth=1` for GBP/USD 4002, EUR/GBP 4003, USD/JPY 4004, and AUD/USD 4007. A `PASS` result means only that the design envelope is safe. `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory.

## Phase 6Q Approval Envelope

Phase 6Q records human/operator planning approval metadata for one selected additional instrument. It is not an execution approval. An `AcceptedForPlanning` envelope confirms the operator reviewed the preflight and attestations, while all run flags remain false.

Use the review script to confirm envelopes are safe. `PASS_WITH_KNOWN_WARNINGS` means no accepted envelope exists yet. `PASS` means at least one accepted envelope exists and remains non-executable. `FAIL` means an unsafe, invalid, conflicting, or run-authorizing envelope was found.

## Phase 6R Dry-Run Report

Phase 6R creates a local GBPUSD dry-run report from the planning, safety gate, preflight, and approval envelope artifacts. It explains the future required step and records the blocking reason: Phase 6R is dry-run only; external snapshot is not authorized.

`PASS` confirms report consistency only. It does not approve a manual snapshot. `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain required.
### Phase 6S - GBPUSD Attempt Gate

Phase 6S creates a planning-only GBPUSD attempt gate. A `PASS` confirms that the current planning, safety, preflight, approval envelope, and dry-run artifacts agree. It does not authorize a snapshot.

The operator-facing interpretation is deliberately narrow: `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain enforced. A later explicit Phase 6T decision is required before any further consideration.

### Phase 6T - GBPUSD Execution Plan

Phase 6T provides the kill/rollback checklist and future command template for GBPUSD. The template is not runnable in Phase 6T. The operator should treat `PASS` as plan completeness only, with no external run, snapshot, replay, scheduler, orders, gateway registration, or mutation authorized.

### Phase 6U - Operator Signoff

Phase 6U lets an operator sign that the Phase 6T plan and kill/rollback checklist were reviewed. `SignedForPlanning` is not permission to run. A later Phase 6V gate is still required before any execution can be considered.

### Phase 6V - Final Readiness

Phase 6V aggregates the full GBPUSD planning chain into one final readiness decision. `PASS` means the chain is complete and consistent. It still does not permit an external snapshot, and all execution eligibility flags remain false.

### Phase 6W - One Manual GBPUSD Attempt

Phase 6W provides the controlled wrapper for exactly one Demo GBPUSD read-only MarketData snapshot attempt. The wrapper requires final readiness, explicit external-connection and Demo-read-only flags, and it hardcodes GBPUSD `4002`. Stop after one result, whether success or failed-safe.
## Phase 6X GBPUSD Empty-Book Outcome

The first GBPUSD Demo read-only snapshot attempt completed with an empty book. This means the session logged on and a MarketDataSnapshot arrived, but no bid/ask entries were present. It is not an order event, not a reject, and not a trading mutation.

The Phase 6X review decision is `PASS_WITH_KNOWN_WARNINGS`. Do not run a second attempt, replay, scheduler, or any broader instrument action unless a later phase explicitly authorizes that manual step.

## Phase 6Y Market-Hours Retry Preparation

Phase 6Y prepares, but does not run, one future GBPUSD retry for Sunday evening after FX reopen or Monday market hours. The Saturday empty-book result is treated as expected outside market hours.

The preparation artifact is not execution approval. It confirms that the future retry must be manual-only, single-attempt, Demo-only, read-only MarketData only, with no scheduler/polling, no automatic retry, no orders, no runtime shadow replay submit, and no trading mutation.

## Phase 6Z-A Operator Note

Phase 6Z-A closes the non-executable planning pipeline for all additional Demo MarketData instruments: GBPUSD / GBP/USD / 4002, EURGBP / EUR/GBP / 4003, USDJPY / USD/JPY / 4004, and AUDUSD / AUD/USD / 4007.

Every instrument remains planning-only. `PASS` on the pipeline means the approval envelope, dry-run, attempt gate, execution plan, operator signoff, and final readiness artifacts are present and internally safe. It does not authorize a snapshot attempt.

The aggregate pipeline must show `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` for every instrument. Future market-hours attempts require a separate explicit operator-approved phase and only one selected instrument at a time.

## Phase 6Z-C Operator Console Summary

The LMAX Shadow page now includes `LMAX Additional MarketData Instruments — Planning Status`. It shows the aggregate pipeline decision, `executableCount=0`, each instrument SecurityID, and non-executable safety flags.

This panel is visibility only. It does not include buttons or fields for snapshots, replay, scheduler/polling, credentials, host/port, gateway registration, order submission, or trading mutation.

## Phase 6Z-D Final Planning Freeze

Phase 6Z-D creates the final documentation/audit pack for the additional-instrument planning pipeline. The final document is `docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md`, and generated pack artifacts live under `artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/`.

The freeze confirms GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 are prepared only as non-executable planning records. `PASS` means the documentation pack and artifact chain are consistent. It does not authorize an external run.

No snapshot should be run on Sunday/outside market hours. A future market-hours attempt requires an explicit operator command in a later phase, one selected instrument at a time.

## Phase 6Z-E Market-Hours Next Action

The LMAX Shadow page now includes `LMAX Market-Hours Next Action`. It reminds operators that the next useful external action is to wait for market hours, then use a separate manual operator command for one GBPUSD read-only snapshot attempt.

The card shows GBPUSD / GBP/USD / SecurityID 4002, the previous `CompletedWithEmptyBook` result outside market hours, final readiness `PASS`, retry readiness `PASS`, and planning freeze `PASS`.

This card is visibility only. It does not include buttons or fields for snapshot execution, replay, scheduler/polling, credentials, host/port, gateway registration, order submission, or trading mutation.
## Phase 7A Operator Boundary

Phase 7A is an architecture decision only. It records that the EURUSD workflow is frozen, additional-instrument planning is frozen, and GBPUSD market-hours retry visibility is prepared.

The selected next planning boundary is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. This does not authorize an LMAX connection or snapshot. Future market-hours attempts remain separate explicit operator commands, one instrument at a time.

Still not authorized: scheduler, polling, runtime shadow replay submit, orders, real gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.

## Phase 7B Controlled Manual Workflow Plan

Phase 7B defines the future manual sequence for additional instruments:

1. GBPUSD
2. EURGBP
3. USDJPY
4. AUDUSD

GBPUSD remains the next candidate because its first outside-market-hours attempt safely returned `CompletedWithEmptyBook` and the market-hours retry plan is ready. EURGBP, USDJPY, and AUDUSD remain pending until GBPUSD is reviewed.

This is not permission to run. It only documents one-instrument-at-a-time handling. There is no batch execution, no scheduler, no polling, no runtime shadow replay submit, no orders, no real gateway registration, and no trading mutation.

## Phase 7C GBPUSD Market-Hours Closure

Phase 7C is the closure workflow after a future GBPUSD market-hours attempt. It does not start the attempt and does not retry automatically.

If a future operator-approved GBPUSD market-hours artifact exists, run the Phase 7C review first. `CompletedWithBook` is a clean success if top-of-book is present. `CompletedWithEmptyBook` is a safe warning when there are no rejects and no unsafe flags. `FailedSafe` is accepted only as a safe failure. Any order, scheduler, runtime shadow replay submit, gateway registration, trading mutation, credential leakage, wrong symbol, or wrong SecurityID is a failure.

Evidence preview and local replay remain separate manual steps. There is no UI button, scheduler, polling loop, or runtime submit path.

## Phase 7D Next Instrument Decision

Phase 7D decides the next planning step after GBPUSD is closed. It is not a run command.

Current state, before a GBPUSD market-hours closure exists: `PendingGbpusdMarketHoursAttempt`.

After closure:

- `CompletedWithBook` and `PASS`: EURGBP can become the next planning candidate.
- `CompletedWithEmptyBook` and `PASS_WITH_KNOWN_WARNINGS`: prepare a controlled GBPUSD retry in a new phase.
- Failed-safe or unsafe result: block the sequence and diagnose before any next instrument.

The one-instrument-at-a-time rule remains active. There is no batch execution, scheduler, polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

## Phase 7E Monday / Market-Hours Checklist

Phase 7E provides the final checklist for the future GBPUSD market-hours manual attempt. It does not run the command.

Before the future attempt, operators must confirm market hours, Demo-only intent, credential presence only, API/Worker `FakeLmaxGateway`, final readiness `PASS`, Phase 6Y retry readiness `PASS`, no scheduler/polling, no runtime shadow replay submit, no order path, and Phase 7C closure scripts.

During the future attempt: one attempt only, no retry, no batch. Use Ctrl+C or close the process as the kill switch.

After the future attempt: run Phase 7C review, evidence preview if safe, optional local replay only if appropriate and explicitly confirmed, closure manifest, Phase 7C gate, and Phase 7D next-instrument decision.

## Phase 7E2 EURGBP Operator Boundary

EURGBP becomes the next selected planning instrument only after GBPUSD closes with `CompletedWithBook` / `PASS` and Phase 7D selects `ProceedToEurgbpPlanning`. Phase 7E2 rehydrates EURGBP readiness as planning-only; it does not authorize an EURGBP run.

Operators must continue to enforce one instrument at a time, no batch execution, no scheduler/polling, no runtime shadow replay submit, no orders, no gateway registration, no credential exposure, and no trading mutation.

## Phase 7F2 EURGBP Checklist Boundary

Phase 7F2 provides the EURGBP manual snapshot execution checklist and kill/rollback plan. It is an operator-facing planning artifact only. The future command template is visible for review and is marked `DO NOT RUN IN PHASE 7F2`.

## Phase 7G2 EURGBP Final Pre-Run Gate

Phase 7G2 provides the final EURGBP pre-run consistency gate. It confirms that GBPUSD closed with `PASS`, Phase 7D selected `ProceedToEurgbpPlanning`, Phase 7E2 EURGBP readiness is `PASS`, and the Phase 7F2 checklist is `PASS`.

This gate does not authorize execution. EURGBP remains `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`. Any future EURGBP attempt must be a separate explicit operator-approved Demo-only manual phase.

Before any later EURGBP run, an explicit future phase must confirm market hours, operator intent, Demo read-only scope, one-instrument-at-a-time execution, and all gates. Phase 7F2 itself keeps `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`.

## Phase 7H Generic Additional Instrument Operator Flow

Phase 7H replaces bespoke per-instrument run scripts with one generic one-shot wrapper for GBPUSD, EURGBP, USDJPY, and AUDUSD. It remains manual-only and one-instrument-at-a-time. It has no UI run button and no scheduler.

For the current sequence, the selected instrument is EURGBP. The future operator command must include `-Symbol EURGBP`, the Phase 7G2 final pre-run gate file, `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a reason. If the final gate is missing, not `PASS`, points at the wrong instrument, or contains any executable flag, the wrapper stops before any external connection.

Phase 7H also provides the common post-run steps: review the sanitized artifact, map MarketDataOnly evidence preview if safe, optionally replay locally with explicit confirmation, build a closure manifest, and run the Phase 7H gate. None of these steps authorize orders, gateway registration, runtime shadow replay submit, scheduler/polling, or trading-state mutation.

## Phase 7H2 Final Pre-Run Gate Preparation

For USDJPY and AUDUSD, first build a Phase 7H-compatible final pre-run gate. The generic one-shot wrapper must not be pointed at the older Phase 6Z-A final-readiness artifact; that artifact remains planning-only and is intentionally rejected by wrapper validation.

The generated final pre-run gate records the exact instrument identity, Demo/DemoLondon MarketData profile, one-instrument-at-a-time control, `batchExecutionAllowed=false`, and all run eligibility flags false. It is still not permission to run externally. A future snapshot still requires the operator to manually invoke the Phase 7H wrapper with `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a fresh reason.
