# QQ.Production.Intraday - Developer Guide

## 1. Purpose and Scope

`QQ.Production.Intraday` is a local-safe institutional intraday FX platform with PMS, OMS, and EMS style workflows. It models portfolio state, target weights, model runs, trade intents, orders, fills, positions, market data, risk controls, governance, exceptions, daily operations, LMAX EOD reconciliation, and LMAX shadow evidence replay.

The current runtime is intentionally conservative:

- The main API and Worker are `FakeLmaxGateway` only.
- The main runtime does not connect to LMAX.
- The main runtime cannot submit real LMAX orders.
- The LMAX FIX connectivity code lives only in `tools/QQ.Production.Intraday.Lmax.ConnectivityLab`.
- Shadow replay is offline, local, and non-mutating.
- The live shadow reader skeleton is disabled and no-op by default.

Out of scope today:

- Production broker connectivity.
- Runtime LMAX gateway registration.
- Live market data ingestion in API/Worker.
- Credential capture in the UI.
- Runtime order submission to LMAX.
- Production scheduling or Windows Service installation.

## 2. Safety Boundary Summary

The safety boundary is part of the architecture, not just configuration.

| Area | Current behavior |
| --- | --- |
| API execution gateway | `FakeLmaxGateway` only |
| Worker execution gateway | `FakeLmaxGateway` only |
| LMAX FIX connectivity | Connectivity Lab only |
| LMAX Account REST API | Parked/diagnostic only |
| Shadow replay | Local JSON replay into observations/audit/exceptions only |
| Shadow reader | Disabled/no-op skeleton |
| Runtime order controls | No live LMAX order controls |
| Credentials | No credential forms; evidence JSON must not contain secrets |
| Trading-state mutation from shadow | Forbidden |

Shadow replay may write:

- `LmaxShadowReplayRuns`
- `LmaxShadowObservations`
- audit events
- exception cases for blocking observations

Shadow replay must not write or update:

- orders
- fills
- positions
- position ledger
- model runs
- target positions
- drift snapshots
- risk decisions
- reconciliation state
- wallets or PnL state

## 3. Solution Layout

| Project | Responsibility |
| --- | --- |
| `QQ.Production.Intraday.Domain` | Domain entities, strongly typed IDs, enums, operational/runbook/shadow models. |
| `QQ.Production.Intraday.Application` | Use-case services, job/runbook orchestration, risk/governance/audit/exception services, LMAX shadow replay and reader services. |
| `QQ.Production.Intraday.Infrastructure.SqlServer` | EF Core DbContext, SQL Server LocalDB mappings, migrations, repository implementation. |
| `QQ.Production.Intraday.Infrastructure.Simulator` | Local simulator services, including `FakeLmaxGateway` and fake market data/EOD helpers. |
| `QQ.Production.Intraday.Infrastructure.Lmax` | Dormant LMAX adapter design types, normalized DTOs, message builders, mappers, safety gates, shadow helpers. Not registered in API/Worker. |
| `QQ.Production.Intraday.Api` | Minimal API endpoints, local operator context, runtime wiring. |
| `QQ.Production.Intraday.Worker` | Local worker host. It remains FakeLmax-only. |
| `QQ.Production.Intraday.Ui` | React/Vite/TypeScript local operator cockpit. |
| `tools/QQ.Production.Intraday.Lmax.ConnectivityLab` | Isolated lab-only LMAX FIX and diagnostic tooling. |
| `tests/QQ.Production.Intraday.Tests.Unit` | Unit tests for domain/application/infrastructure/lab contracts. |
| `tests/QQ.Production.Intraday.Tests.Integration` | Integration tests for API/persistence/runtime safety. |

## 4. Architecture Overview

High-level local flow:

```text
DB model weights
  -> validation
  -> promotion
  -> explicit model run
  -> target positions and drift
  -> risk checks
  -> trade intents
  -> FakeLmax child orders
  -> fills
  -> internal positions
  -> reconciliation, audit, exceptions
```

Market data flow:

```text
fake/local market data snapshots
  -> 15 minute bars
  -> model-run freshness checks
  -> Daily Operations visibility
```

LMAX EOD flow:

```text
generated/local LMAX EOD files
  -> import runs
  -> validation issues
  -> EOD reconciliation
  -> breaks and exception cases
  -> wallet and USD PnL summary
```

LMAX shadow flow:

```text
Connectivity Lab evidence JSON or synthetic fixture
  -> contract validation and normalization
  -> POST /lmax-shadow/replay
  -> observation policy classification
  -> shadow observations
  -> audit events
  -> exception cases for blocking observations only
```

Operational control flow:

```text
Daily Operations jobs
  -> job runs, steps, events
  -> audit and exceptions
  -> runbooks
  -> disabled local scheduler metadata
```

## 5. Domain and Application Concepts

Model weights:

- `ModelWeightBatch` is a DB-staged model-weight upload/import batch.
- `ModelWeightRow` is one instrument weight row inside a batch.
- `ModelWeightValidationIssue` records blocking or warning issues.
- Promotion makes a validated batch the source for explicit model-run processing.

Model runs and target state:

- `ModelRun` records an explicit operator/system processing request.
- Target weights are transformed into target positions.
- Drift snapshots compare current internal positions to target positions.
- Duplicate model-run processing is blocked.

Orders and execution:

- Parent/child order state models internal order intent and venue child order lifecycle.
- Fills are persisted idempotently by broker execution ID.
- Persisted fills drive position ledger changes.
- Broker state and internal state are deliberately separate.

Risk:

- Risk limit sets are versioned.
- Instrument and venue controls can enable/disable trading, market data, and report import behavior.
- Risk decisions record allowed/blocked outcomes for model-run/order processing.

Governance:

- Operator context uses local `X-Operator-Id`.
- Approval requests implement local maker/checker behavior.
- Sensitive actions require reason and can require four-eyes approval.

Exceptions:

- Exception cases model operational breaks, warnings, blocking items, and investigation/resolution workflow.
- Important actions are audited.

Operations:

- Operational jobs wrap existing local actions without rewriting business logic.
- Runbooks orchestrate jobs and manual gates for Start of Day, Intraday Cycle, and End of Day.
- Local scheduler metadata exists but is disabled by default.

LMAX shadow:

- Replay runs are immutable evidence-processing runs.
- Observations are fingerprinted, deduped within a replay run, and linked to policy metadata.
- Shadow replay never mutates trading state.

## 6. Database and Migrations

Local persistence uses SQL Server LocalDB by default:

```text
Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Useful scripts:

```powershell
.\scripts\update-local-db.ps1
.\scripts\reset-local-db.ps1 -SeedDemoData
```

Migration practices:

- Use descriptive migration names.
- Keep seed data idempotent.
- Use strongly typed IDs consistently.
- Keep audit/job/runbook/shadow history protected from accidental cascade delete.
- Add explicit indexes for query-heavy operational tables.
- When EF creates migration metadata, make sure required attributes are present and snapshot changes are intentional.

Important migrations include:

- `InitialLocalSqlServerSchema`
- `EnforceReferenceDataUniqueness`
- `AddModelWeightSourceTables`
- `AddDailyOperationsJobControl`
- `AddOperationalRunbooksAndLocalScheduler`
- `AddLmaxShadowObservationStore`
- `AddLmaxShadowObservationFingerprint`

Common pitfalls:

- Old local DBs can contain duplicate seed data from early development.
- Minimal API list responses are often wrapped as `{ value: [...], Count: n }`.
- Single-item JSON arrays must remain arrays in PowerShell scripts.
- `DateOnly` values should be ISO `yyyy-MM-dd`, not compact `yyyyMMdd`, unless a normalizer explicitly handles legacy input.

## 7. API Reference

Most list responses are wrapped:

```json
{
  "value": [],
  "Count": 0
}
```

Most mutating local endpoints require a reason and write an audit event.

### Health

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/health` | Runtime safety and dependency status. Check `executionGateway`, `liveTradingEnabled`, and `externalConnectionsEnabled`. |

### Model weights

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/model-weight-batches` | List DB-staged model weight batches. |
| GET | `/model-weight-batches/{id}` | Batch details. |
| GET | `/model-weight-batches/{id}/rows` | Batch rows. |
| GET | `/model-weight-batches/{id}/validation-issues` | Validation issues. |
| POST | `/model-weight-batches/fake` | Create local fake batch. |
| POST | `/model-weight-batches/{id}/validate` | Validate a batch. |
| POST | `/model-weight-batches/{id}/promote` | Promote a batch. |
| POST | `/model-weight-batches/promote-ready` | Promote ready batches. |

### Model runs, orders, fills, positions

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/model-runs` | List model runs. |
| POST | `/model-runs` | Create explicit model run. |
| POST | `/model-runs/{id}/process` | Process one model run through local simulator path. |
| GET | `/target-positions` | Target positions. |
| GET | `/drift-snapshots` | Drift snapshots. |
| GET | `/trade-intents` | Trade intents. |
| GET | `/risk-decisions` | Legacy/top-level risk decisions list. |
| GET | `/risk/decisions` | Risk decisions list. |
| GET | `/risk/decisions/{id}` | Risk decision details. |
| GET | `/orders` | Parent/child orders. |
| GET | `/fills` | Fills. |
| GET | `/positions/internal` | Internal positions. |
| GET | `/positions/broker` | Broker-position provider view. |

### Market data

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/market-data/snapshots` | Local market data snapshots. |
| POST | `/market-data/fake-snapshots` | Create local fake snapshots. |
| GET | `/market-data/bars` | 15 minute bars. |
| POST | `/market-data/build-bars` | Build bars from snapshots. |

### LMAX EOD, reconciliation, PnL

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/lmax-eod/import-runs` | EOD import run history. |
| GET | `/lmax-eod/import-runs/{id}` | Import run details. |
| GET | `/lmax-eod/validation-issues` | Import validation issues. |
| GET | `/lmax-eod/individual-trades` | Imported individual trades. |
| GET | `/lmax-eod/trade-summaries` | Imported trade summaries. |
| GET | `/lmax-eod/currency-wallets` | Imported wallets. |
| POST | `/lmax-eod/generate-fake` | Generate local fake EOD reports. |
| POST | `/lmax-eod/import-generated` | Import generated local reports. |
| POST | `/lmax-eod/import-report-set` | Import a report set. |
| POST | `/lmax-eod/import-individual-trades` | Import one individual-trades file. |
| POST | `/lmax-eod/import-trades-summary` | Import one trade-summary file. |
| POST | `/lmax-eod/import-currency-wallets` | Import one currency-wallets file. |
| POST | `/eod-reconciliation/run` | Run EOD reconciliation. |
| GET | `/eod-reconciliation/runs` | Reconciliation runs. |
| GET | `/eod-reconciliation/breaks` | EOD breaks. |
| GET | `/eod-pnl/summary` | Wallet/PnL summary. |

### Risk and governance

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/risk/limit-sets` | Risk sets. |
| POST | `/risk/limit-sets` | Create risk set. |
| POST | `/risk/limit-sets/{id}/clone` | Clone risk set. |
| POST | `/risk/limit-sets/{id}/activate` | Activate risk set, possibly approval-gated. |
| POST | `/risk/limit-sets/{id}/retire` | Retire risk set, possibly approval-gated. |
| GET | `/risk/limits?riskLimitSetId={id}` | Global limits for a risk set. |
| PUT | `/risk/limits/{id}` | Update a global limit. |
| GET | `/risk/instrument-limits?riskLimitSetId={id}` | Instrument limits for a risk set. |
| PUT | `/risk/instrument-limits/{id}` | Update an instrument limit. |
| GET | `/risk/venue-limits?riskLimitSetId={id}` | Venue limits for a risk set. |
| PUT | `/risk/venue-limits/{id}` | Update a venue limit. |
| GET | `/risk/trading-windows` | Trading windows, optionally filtered by `modelName`. |
| PUT | `/risk/trading-windows/{id}` | Update a trading window. |
| GET | `/risk/instruments` | Instruments with aliases and venue mappings. |
| PUT | `/risk/instruments/{id}/controls` | Update instrument trading/report/market-data controls. |
| GET | `/risk/venues` | Venues. |
| PUT | `/risk/venues/{id}/controls` | Update venue trading/report/market-data controls. |
| POST | `/admin/kill-switch` | Activate kill switch. |
| POST | `/admin/kill-switch/clear` | Clear kill switch, approval-gated. |
| GET | `/operators/current` | Current local operator context. |
| GET | `/operators` | Operators. |
| GET | `/operators/{operatorId}` | Operator details. |
| GET | `/operators/{operatorId}/permissions` | Operator permission set. |
| GET | `/approvals` | Approval requests. |
| GET | `/approvals/{id}` | Approval request details. |
| POST | `/approvals/{id}/approve` | Approve a request. |
| POST | `/approvals/{id}/reject` | Reject a request. |
| POST | `/approvals/{id}/cancel` | Cancel a request. |
| POST | `/approvals/{id}/execute` | Execute an approved request. |
| GET | `/approvals/{id}/decisions` | Approval decision history. |

### Audit and exceptions

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/audit/events` | Query audit events. |
| GET | `/audit/events/{id}` | Audit event details. |
| GET | `/audit/events/by-entity` | Audit events for entity. |
| GET | `/audit/events/by-correlation/{correlationId}` | Audit events for correlation ID. |
| GET | `/exceptions` | Exception cases. |
| GET | `/exceptions/{id}` | Exception case details. |
| GET | `/exceptions/{id}/actions` | Exception action history. |
| GET | `/exceptions/{id}/notes` | Exception notes. |
| POST | `/exceptions` | Create case. |
| POST | `/exceptions/{id}/acknowledge` | Acknowledge a case. |
| POST | `/exceptions/{id}/assign` | Assign a case. |
| POST | `/exceptions/{id}/investigate` | Move a case into investigation. |
| POST | `/exceptions/{id}/resolve` | Resolve a case, approval-gated for blocking/critical cases. |
| POST | `/exceptions/{id}/false-positive` | Mark false positive, approval-gated for blocking/critical cases. |
| POST | `/exceptions/{id}/waive` | Waive a case, approval-gated for blocking/critical cases. |
| POST | `/exceptions/{id}/reopen` | Reopen a case. |
| POST | `/exceptions/{id}/notes` | Add a note. |

### Daily operations jobs

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/ops/jobs/definitions` | Job definitions. |
| GET | `/ops/jobs/runs` | Job runs. |
| GET | `/ops/jobs/runs/{id}` | Job run details. |
| GET | `/ops/jobs/runs/{id}/steps` | Job steps. |
| GET | `/ops/jobs/runs/{id}/events` | Job events. |
| POST | `/ops/jobs/run` | Run job by type. |
| POST | `/ops/jobs/runs/{id}/retry` | Retry a rerunnable job. |
| GET | `/ops/daily-summary` | Daily operations summary. |
| GET | `/ops/daily-checklist` | Daily checklist. |
| GET | `/ops/timeline` | Operational timeline. |

Convenience job endpoints:

- `POST /ops/run-reference-check`
- `POST /ops/build-bars`
- `POST /ops/promote-ready-weights`
- `POST /ops/process-pending-model-runs`
- `POST /ops/run-eod-reconciliation`

### Runbooks and schedules

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/ops/runbooks/definitions` | Runbook definitions. |
| GET | `/ops/runbooks/definitions/{id}` | Runbook definition details. |
| GET | `/ops/runbooks/runs` | Runbook runs. |
| GET | `/ops/runbooks/runs/{id}` | Runbook run details. |
| GET | `/ops/runbooks/runs/{id}/steps` | Runbook step runs. |
| POST | `/ops/runbooks/run` | Run Start of Day, Intraday Cycle, or End of Day. |
| POST | `/ops/runbooks/runs/{id}/run-next-step` | Continue a runbook. |
| POST | `/ops/runbooks/runs/{id}/complete-manual-step` | Complete a manual gate. |
| POST | `/ops/runbooks/runs/{id}/cancel` | Cancel runbook. |
| POST | `/ops/runbooks/runs/{id}/retry` | Retry runbook. |
| GET | `/ops/schedules` | Disabled-by-default local schedule definitions plus scheduler enabled flag. |
| POST | `/ops/schedules` | Create a local schedule definition. |
| PUT | `/ops/schedules/{id}` | Update a local schedule definition. |

### LMAX shadow replay

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/lmax-shadow/replay` | Replay normalized evidence into shadow observations. |
| GET | `/lmax-shadow/replay-runs` | Replay run history. |
| GET | `/lmax-shadow/replay-runs/{id}` | Replay run details. |
| GET | `/lmax-shadow/observations` | Shadow observations with filters. |
| POST | `/lmax-shadow/observations/{id}/acknowledge` | Acknowledge with reason. |
| POST | `/lmax-shadow/observations/{id}/resolve` | Resolve with reason. |
| POST | `/lmax-shadow/observations/{id}/ignore` | Ignore with reason. |

Observation filters include `replayRunId`, `severity`, `status`, `type`, `symbol`, `brokerExecutionId`, `brokerOrderId`, `clientOrderId`, `fingerprint`, and `limit`.

### Shadow reader skeleton

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/lmax-shadow-reader/status` | Disabled/no-op reader status and safety gates. |
| POST | `/lmax-shadow-reader/run` | Blocked diagnostic run attempt. No sockets, credentials, or LMAX calls. |

### Reference data

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/instruments` | Enabled instruments. |
| GET | `/venues` | Enabled venues. |

## 8. UI Reference for Developers

The UI lives in:

```text
src/QQ.Production.Intraday.Ui
```

Key files:

- `src/App.tsx` - page composition, state, drawers, actions.
- `src/api/types.ts` - API DTO types.
- `src/api/apiClient.ts` - API client functions.
- CSS/theme files - shared visual system.
- Reusable primitives include data tables, status chips, action feedback, drawers, toasts, and top status controls.

Main pages:

- Command Center
- PMS
- Model Weights
- OMS
- EMS
- Market Data
- Reconciliation
- LMAX EOD
- Exceptions
- Risk & Admin / Risk Control Center
- Governance
- Daily Operations
- LMAX Shadow
- Connectivity Lab

Patterns:

- Use status chips for status/severity values.
- Use action buttons with pending/failed/success feedback for mutating actions.
- Use detail drawers for row details, operator guidance, workflow links, and advanced raw JSON.
- Use global toasts for API operation feedback.
- Send `X-Operator-Id` for local attribution.
- Never add credential forms or live LMAX enable controls.
- The top status bar should continue to show `SAFE LOCAL / FakeLmax-only`, current operator, environment, persistence provider, execution gateway, live-trading flag, external-connection flag, database/migration state, market-data provider, and reference-data integrity.
- Command Center should favor operator triage: high-level safety, exceptions, risk, daily ops, LMAX shadow, latest audit events, and next-action hints before raw technical detail.
- LMAX Shadow should keep raw payloads in the detail drawer/advanced JSON while table rows expose evidence mode, policy code, severity, status, suggested action, and exception-policy cues.
- Shared detail drawers should surface cross-workflow IDs when present, including `replayRunId`, `observationId`, `exceptionCaseId`, `approvalRequestId`, `riskDecisionId`, `jobRunId`, `runbookRunId`, `fingerprint`, `policyCode`, `evidenceMode`, and `correlationId`, including IDs embedded in sanitized metadata JSON.
- Exception, audit, governance, risk, and daily-operations views should preserve the operator closure path: source event -> observation/break -> exception case -> audit trail -> approval if required -> reasoned resolution.

## 9. LMAX Integration Architecture

The selected path is FIX-first:

- FIX Market Data for future market data.
- FIX Trading for future order entry, execution reports, order-status recovery, and trade-capture recovery.
- LMAX EOD files for official daily reconciliation.

The Account REST API path is parked and diagnostic only. It is not required for platform operation.

Important boundaries:

- Connectivity Lab is the only place that may connect to LMAX.
- Shadow replay consumes local sanitized evidence JSON and does not connect to LMAX.
- Shadow reader skeleton is disabled and no-op.
- The adapter skeleton in `Infrastructure.Lmax` is inert and unregistered.
- Future runtime integration must begin with explicit shadow-mode quality gates.

## 10. LMAX Evidence Contract

Schema version:

```text
lmax-fix-lifecycle-evidence-v1
```

Top-level shape:

```json
{
  "schemaVersion": "lmax-fix-lifecycle-evidence-v1",
  "createdAtUtc": "2026-05-06T17:03:57Z",
  "capturedAtUtc": "2026-05-06T17:03:57Z",
  "source": "ConnectivityLab",
  "inputSource": "LabEvidenceFile",
  "reason": "Replay LMAX lab evidence",
  "environment": "Demo",
  "captureMode": "ReadOnly",
  "redaction": "SanitizedNoCredentials",
  "dryRun": false,
  "instrument": "EURUSD",
  "instrumentSymbol": "EURUSD",
  "lmaxInstrumentId": "4001",
  "securityId": "4001",
  "slashSymbol": "EUR/USD",
  "marketData": null,
  "executionReports": [],
  "orderStatuses": [],
  "tradeCaptureReports": [],
  "protocolRejects": [],
  "warnings": []
}
```

Normalization rules:

- `orderStatusReports` is normalized to `orderStatuses` for legacy evidence.
- Compact trade dates such as `20260506` normalize to `2026-05-06`.
- Missing `tradeUti` is emitted as explicit `null`.
- Arrays remain arrays, including one-item arrays.
- FIX side values `1` and `2` normalize to `Buy` and `Sell`.
- Evidence containing credentials, passwords, authorization headers, or raw logon secrets fails validation.

Generated evidence files are lab artifacts and must not be committed.

## 11. LMAX Evidence Modes

| Mode | Contains | Expected replay behavior | Exception case |
| --- | --- | --- | --- |
| `EmptyReadOnly` | No trading evidence, optional metadata | Completed, 0 observations | No |
| `MarketDataOnly` | Market data context only | Completed, 0 trading observations | No |
| `TradeCaptureOnly` | TradeCapture AE evidence | Warning if no matching internal fill/order in lab context | No |
| `OrderStatusOnly` | ExecType=I status report | Warning/Info; never a fill observation | No by default |
| `ProtocolRejectOnly` | FIX session/business reject evidence | Warning for read-only requests; blocking for order-path/unknown reject | Blocking only |
| `MixedReadOnly` | Market data plus order status/trade capture | Dedupe by fingerprint; max applicable severity | Policy-dependent |
| `SyntheticLifecycle` | Fixture lifecycle evidence | Lab warnings for missing internal records | No by default |

## 12. Shadow Observation Policy

Each observation may expose:

- `policyCode`
- `evidenceMode`
- `sourceEventType`
- `rationale`
- `suggestedOperatorAction`
- `createsExceptionCase`

Current policy codes:

| Policy code | Meaning | Severity |
| --- | --- | --- |
| `LMAX_SHADOW_ER_FILL_MATCH` | ExecutionReport fill matches internal fill. | Info |
| `LMAX_SHADOW_ER_FILL_MISSING_INTERNAL_LAB` | ExecutionReport fill has no internal fill in lab/offline context. | Warning |
| `LMAX_SHADOW_TC_FILL_MATCH` | TradeCapture AE matches internal fill. | Info |
| `LMAX_SHADOW_TC_MISSING_INTERNAL_FILL_READONLY` | TradeCapture AE has no internal fill in read-only/lab context. | Warning |
| `LMAX_SHADOW_ORDER_STATUS_MATCH` | OrderStatus matches internal order. | Info |
| `LMAX_SHADOW_ORDER_STATUS_MISMATCH` | OrderStatus conflicts with internal order state. | Warning |
| `LMAX_SHADOW_ORDER_STATUS_UNKNOWN_ORDER_READONLY` | Status-only LMAX order is not known internally. | Warning |
| `LMAX_SHADOW_PROTOCOL_REJECT_READONLY` | Protocol reject for read-only recovery/market-data request. | Warning |
| `LMAX_SHADOW_PROTOCOL_REJECT_ORDER_PATH` | Protocol reject for order-path or unknown context. | Blocking |
| `LMAX_SHADOW_DUPLICATE_EXECUTION` | Duplicate execution ID in evidence. | Warning |

Market-data-only evidence currently creates no trading observations, so no market-data policy code is persisted. For less common observation types not listed above, the policy layer uses a generated fallback code in the form `LMAX_SHADOW_<ObservationTypeUppercase>`.

Rules:

- `ExecType=I` is order status only and must not create a fill observation.
- TradeCapture AE is recovery evidence, not official EOD reconciliation.
- EOD files remain the official daily reconciliation source.
- Warning observations do not create exception cases by default.
- Blocking observations create or link exception cases with replay ID, observation ID, fingerprint, policy code, and evidence mode.
- Dedupe is by fingerprint within a replay run; repeated replay preserves history but produces stable fingerprints.

## 13. Scripts and Runbooks

Local-only core scripts:

- `reset-local-db.ps1`
- `update-local-db.ps1`
- `run-api.ps1`
- `run-ui.ps1`
- `run-worker.ps1`
- `check-reference-data.ps1`
- `smoke-local.ps1`
- `smoke-db-weights-local.ps1`
- `smoke-lmax-eod-local.ps1`
- `smoke-governance-local.ps1`
- `smoke-daily-ops-local.ps1`
- `smoke-runbooks-local.ps1`
- `smoke-lmax-shadow-local.ps1`
- `smoke-lmax-shadow-reader-local.ps1`
- `smoke-lmax-evidence-coverage-local.ps1`
- `validate-lmax-lab-evidence-file.ps1`
- `replay-lmax-lab-evidence-file.ps1`

LMAX lab scripts:

- Account REST scripts are parked/diagnostic only.
- FIX logon/snapshot/trade-capture/order-status scripts are lab-only.
- Demo order lifecycle scripts can submit Demo orders only when explicit lab safety flags are passed.
- Read-only evidence capture scripts require explicit `-AllowExternalConnections`.

Scripts that can connect externally are named `lmax-lab-*` and require explicit connection flags. Replay/validate/smoke scripts are local-only and must not call LMAX.

## 14. Build/Test/Validation

Use `npm.cmd`, not `npm.ps1`, on PowerShell systems with restrictive execution policy.

Before a phase transition, run the operational readiness gate:

```powershell
.\scripts\run-operational-readiness-gate.ps1
```

The gate executes the standard backend/frontend validation, evidence fixture validation, and local shadow/readiness smokes when the API is available. It writes a machine-readable report under `artifacts/readiness/`, which is ignored by git. Decision criteria are documented in [OPERATIONAL_READINESS_CHECKLIST.md](OPERATIONAL_READINESS_CHECKLIST.md).

```powershell
dotnet restore QQ.Production.Intraday.sln --configfile NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
dotnet build QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
dotnet test QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false

cd src/QQ.Production.Intraday.Ui
npm.cmd run typecheck
npm.cmd run build
npm.cmd test
```

Common local smokes:

```powershell
.\scripts\smoke-local.ps1
.\scripts\smoke-db-weights-local.ps1
.\scripts\smoke-governance-local.ps1
.\scripts\smoke-daily-ops-local.ps1
.\scripts\smoke-runbooks-local.ps1
.\scripts\smoke-lmax-shadow-local.ps1
.\scripts\smoke-lmax-shadow-reader-local.ps1
.\scripts\smoke-lmax-evidence-coverage-local.ps1
```

Evidence validation:

```powershell
.\scripts\validate-lmax-lab-evidence-file.ps1 -EvidenceFile .\tests\fixtures\lmax-shadow\lmax-tradecapture-only-evidence-v1.json
.\scripts\replay-lmax-lab-evidence-file.ps1 -EvidenceFile .\tests\fixtures\lmax-shadow\lmax-tradecapture-only-evidence-v1.json
```

## 15. Configuration

Important defaults:

- `Persistence:Provider` defaults to local SQL Server mode for development.
- API/Worker runtime gateway is FakeLmax.
- `LocalScheduler:Enabled=false`.
- LMAX adapter options default disabled.
- Shadow reader options default disabled/no-op.
- Live trading flags default false.
- External connection flags default false.

Credentials:

- Do not put credentials in `appsettings.json`.
- Do not add credential fields to UI DTOs or forms.
- Use user-secrets/environment variables only for isolated Connectivity Lab work.
- Evidence JSON must not contain credentials, authorization headers, or raw logon messages with passwords.

## 16. Known Warnings / Known Issues

- `NU1903` warning for `System.Security.Cryptography.Xml` may appear during restore/build.
- SQL Server LocalDB may be unavailable in restricted environments.
- Use `npm.cmd`, not `npm`, if PowerShell blocks `npm.ps1`.
- PowerShell interpolates `"$Variable:"` incorrectly; use format strings such as `("{0}: {1}" -f $name, $value)`.
- Preserve single-item JSON arrays in PowerShell with `@(...)`.
- Normalize LMAX compact dates from `yyyyMMdd` to `yyyy-MM-dd`.
- EF migrations can fail if generated metadata attributes or snapshots are incomplete.

## 17. Extension Guidelines

For any new endpoint:

- Add explicit DTOs.
- Return plain string IDs.
- Require reason for mutating actions.
- Sanitize metadata.
- Add audit events.
- Add permission checks if applicable.
- Add tests for success, validation failure, and permission denial.

For any new UI page:

- Keep it operational and task-focused.
- Use existing status chips, action feedback, drawer, and toast patterns.
- Do not add credential forms or live enable buttons.
- Test no live LMAX controls appear.
- Keep the drilldown path visible for operators: show linked entity IDs, correlation IDs, policy context, audit context, and next-step guidance before raw technical payloads.
- Put raw JSON and dense metadata behind an advanced section unless the page is explicitly developer-only.

For any new migration:

- Add explicit indexes for query patterns.
- Avoid cascade delete of history/audit tables.
- Keep seed changes idempotent.
- Update docs with migration purpose.

For any new operational job/runbook:

- Wrap existing local logic.
- Preserve audit, exceptions, risk, governance, and reconciliation checks.
- Define status semantics before wiring UI.
- Add smoke coverage.

For any new evidence mode or shadow policy:

- Add fixture evidence.
- Validate and normalize the evidence contract.
- Define policy code, rationale, severity, and exception behavior.
- Prove no trading-state mutation.

For any future LMAX live-read-only adapter component:

- Treat [LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md](LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md) as the design boundary.
- Follow [LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md](LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md) and [LMAX_READONLY_RUNTIME_PHASE_GATES.md](LMAX_READONLY_RUNTIME_PHASE_GATES.md) before adding any runtime-facing code.
- Phase 1 provides inert interfaces and disabled/no-op behavior only.
- Phase 2 provides service-level fake/in-memory fixture evidence preview only; no sockets, credentials, replay submit, or trading mutation.
- Phase 3 exposes manual local diagnostic endpoints: `GET /lmax-readonly-runtime/status`, `POST /lmax-readonly-runtime/run`, `GET /lmax-readonly-runtime/runs`, and `GET /lmax-readonly-runtime/runs/{id}`. They are disabled/blocked by default, require a reason for run attempts, and can only preview known local fixture files when explicitly fake-enabled in test/local configuration.
- Phase 3.5 proves that explicit fake-enabled test configuration can complete local fixture previews through the endpoint. The response includes evidence mode, per-source event counts, validation issue counts, run id, run mode, safety gates, and fixture file name.
- Phase 3 still blocks `SubmitToShadowReplay=true`; replay submission remains a later gate.
- Phase 4 preflight locks the future external read-only boundary in [LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md](LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md). Phase 4A adds only external-session contracts and a disabled stub. Phase 4B adds an in-memory fake transport harness that emits deterministic read-only events and counters. Phase 4C maps those fake events to sanitized evidence preview JSON. Phase 4D exposes that path through `POST /lmax-readonly-runtime/fake-transport-preview` for predefined in-memory scenarios only. Phase 4E adds `LmaxReadOnlyExternalSessionSkeleton`, which is a hard-disabled future real-session boundary. Phase 4F adds `ILmaxReadOnlyGuardedTransport` and `LmaxReadOnlyGuardedTransportDisabled`, which define but block future read-only transport operations. Phase 4G adds `LmaxReadOnlyExternalSessionOptions` and a validator for an inactive, no-secret-value configuration envelope. Phase 4H adds `ILmaxReadOnlyCredentialProfileResolver` and `LmaxReadOnlyCredentialProfileResolverDisabled`; `CredentialProfileName` is a label only and no credential values are read, used, stored, logged, or returned. Phase 4I adds `LmaxReadOnlyVenueProfileRegistryDisabled`; `VenueProfileName` is a label only and no host/port/user/account/endpoint/session values are exposed. Phase 4J adds `LmaxReadOnlyExternalSessionRunIntentValidator`; manual run intent is validate-only and starts no session. Phase 4K exposes that validator at `POST /lmax-readonly-runtime/external-run-intent/validate`; it returns structured issues and safety gates while reporting `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`. Phase 4L adds `LmaxReadOnlyExternalSessionDryRunReportGenerator` and `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`; the report aggregates disabled boundaries while still reporting no session/connection/credential/replay/mutation attempts. Phase 4M adds signoff validation, Phase 4N adds pre-activation audit validation, Phase 4O adds readiness snapshot validation, and Phase 4P adds the final no-socket release gate. Phase 5A adds the first-transport preflight/kill-rollback plan. Phase 5B adds `LmaxReadOnlySocketPrototypeTransport`, `scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1`, and `scripts/check-lmax-readonly-runtime-phase5b-prototype-gate.ps1`; it remains blocked before socket/logon because credential resolver hardening is still required. It is not registered in API/Worker and adds no order path, scheduler, shadow replay submit, gateway replacement, or trading mutation.
- The fake transport preview endpoint supports `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, `WarningOnly`, and `ErrorOnly`; it requires a reason, rejects unknown scenarios and `SubmitToShadowReplay=true`, and stores run summaries in memory only.
- The Phase 4E skeleton reports `SkeletonOnly`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, `ShadowReplaySubmitImplemented=false`, and `TradingMutationImplemented=false`.
- The Phase 4F guarded transport reports `NetworkTransportImplemented=false`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, and `ReadOnlyOnly=true`; its connect/read/disconnect methods always block.
- The Phase 4G configuration envelope validates safe-disabled defaults, blocks future-looking external activation, caps runtime/events, requires Demo for initial external prototype, and uses only non-secret profile labels.
- The Phase 4H credential-profile boundary reports `ResolverMode=Disabled`, `CredentialReadImplemented=false`, `CredentialUseImplemented=false`, `SensitiveMaterialReturned=false`, and `RedactionRequired=true`; it does not resolve user-secrets, environment variables, appsettings values, or vault entries.
- The Phase 4I venue-profile boundary recognizes `DemoLondon` as an inactive future prototype label and blocks `Uat`, `Production`, unknown labels, and environment/profile mismatches.
- The Phase 4J run-intent boundary requires a manual reason and operator id; `FutureExternalReadOnlyManual` remains blocked, and `ValidateOnly`/`PreviewOnly` do not start anything.
- The Phase 4K endpoint requires a reason, accepts no host/user/password/account/session/endpoint/raw FIX fields, persists no real run, and starts no session.
- The Phase 4L endpoint requires a reason, accepts no host/user/password/account/session/endpoint/raw FIX fields, persists no report, starts no session, and returns disabled-boundary status markers for credential resolver, guarded transport, and skeleton.
- The Phase 4M endpoint requires a reason and signer metadata, accepts no host/user/password/account/session/endpoint/raw FIX fields, persists no approval, and always returns `canAuthorizeExecution=false`.
- The Phase 4N endpoint requires a reason and safe intent/report/signoff summary fields, accepts no host/user/password/account/session/endpoint/raw FIX fields, persists no execution authorization, and always returns `canAuthorizeExecution=false`.
- The Phase 4O endpoint requires a reason and safe intent fields, accepts no host/user/password/account/session/endpoint/raw FIX fields, persists no execution authorization, and always returns `canStartSession=false`.
- Phase 5C adds `LmaxReadOnlyCredentialProfileResolverEnvironment`, `LmaxReadOnlyCredentialAvailabilityResult`, `LmaxReadOnlyCredentialRedactionPolicy`, `scripts/check-lmax-readonly-runtime-demo-credentials.ps1`, and `scripts/check-lmax-readonly-runtime-phase5c-credential-gate.ps1`. It checks required Demo environment labels for presence only and returns labels/booleans/counts/redaction status, never values.
- Phase 5D adds the first isolated manual Demo market-data socket prototype behind that credential gate. It is script-only, supports only EURUSD / SecurityID `4001`, returns sanitized diagnostics, and remains outside API/Worker. It adds no order submission, scheduler, gateway registration, shadow replay submit, trading-table persistence, or trading-state mutation.
- Phase 5E adds explicit failure taxonomy and disabled retry metadata for Phase 5D. Fake hooks cover missing credentials, connection failure, logon timeout/reject, snapshot timeout, logout warning, and max-event failure without live LMAX. Retry metadata never triggers automatic external retry.
- Phase 5F adds operator-approved manual sanitized result capture for that same Demo EURUSD snapshot path. The script prints planned safety flags, writes only sanitized JSON under the ignored `artifacts/lmax-readonly-runtime-demo-snapshot/` tree, keeps retry disabled, and the Phase 5F gate masks credential labels so it cannot make an external attempt.
- Phase 5G adds sanitized timeout/reject diagnostics for that manual snapshot path. It records request mode, request metadata, message counters, response classification, and sanitized session warnings/errors. `SecurityIdOnly`, `SlashSymbolOnly`, `SymbolOnly`, and `AutoSequence` remain manual read-only market-data diagnostics only.
- Phase 5H adds `LmaxReadOnlyMarketDataRequestCompatibility`, splitting request mode (`SnapshotPlusUpdates`, `SnapshotOnly`, `AutoSequence`) from symbol encoding (`SecurityIdOnly`, tag-55 modes, `InternalSymbol`, `Auto`). Defaults avoid the known rejected `263=0` and tag-55 shapes; known rejected profiles block locally unless explicitly allowed for diagnostics.
- Phase 5J adds `LmaxReadOnlyFixLogonDiagnostics` and `LmaxReadOnlyFixSessionProfileComparison` for the Demo MarketData logon path. Diagnostics include labels, booleans, lengths, session settings, first inbound message type/text after redaction, and runtime-vs-Connectivity-Lab profile comparison. `MsgType=5` before logon confirmation maps to `FailedSafeLogonLogoutReceived`; `MsgType=3` maps to `FailedSafeLogonRejectReceived`. The manual script supports `-ShowSanitizedLogonDiagnostics`.
- Phase 5L adds `LmaxReadOnlyDemoSnapshotArtifactValidator`, `scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1`, and `scripts/check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1`. It validates successful sanitized snapshot artifacts only; it does not add external behavior, scheduler, gateway registration, shadow replay submit, persistence, or trading mutation.
- Keep runtime activation levels disabled until a separate future gate approves movement.
- Keep it outside API/Worker until an explicit activation gate.
- Start with shadow mode only.
- No order submission.
- No trading-state mutation.
- No credentials in UI.
- Prove API/Worker remain FakeLmax-only.
## LMAX Read-Only Runtime Phase 5M Evidence Preview

Phase 5M adds a local mapper from the validated successful Demo EURUSD snapshot artifact to a `MarketDataOnly` `lmax-fix-lifecycle-evidence-v1` preview. `LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper` validates the Phase 5L artifact first, then emits sanitized market-data fields with empty execution, order-status, trade-capture, and protocol-reject arrays.

Use `scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile <artifact>` to produce and validate an ignored preview JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/`. This path does not submit to shadow replay, create observations, register a gateway, start a scheduler, or mutate trading state. API and Worker remain `FakeLmaxGateway` only.

## LMAX Read-Only Runtime Phase 5N Manual Replay Dry-Run

Phase 5N adds a separate manual script for replaying the Phase 5M `MarketDataOnly` preview through the existing local shadow replay API. `scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile <preview>` validates the preview, confirms all replay arrays are empty, captures order/fill/position counts, posts to `/lmax-shadow/replay`, and expects `Completed` with zero observations.

This is not a runtime submit path. The runtime prototype and mapper still do not reference `ILmaxShadowReplayService` or call `/lmax-shadow/replay`; only the manual script can invoke the existing local API endpoint.

## LMAX Read-Only Runtime Phase 5O Stability Check

Phase 5O adds a capped, manual repeated-snapshot workflow in `scripts/run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1`. It requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, `-ConfirmRepeatedManualSnapshots`, a non-empty `-Reason`, and `-AttemptCount` within 1..5. `-DelaySeconds` is capped at 1..10 and is only a bounded pause between planned manual attempts, not scheduler or polling behavior.

Each attempt delegates to the existing manual Demo EURUSD snapshot prototype, validates each successful sanitized artifact, maps it to a `MarketDataOnly` preview, and writes a sanitized ignored stability summary. Preview replay is disabled by default and requires the explicit `-ReplayEvidencePreviews` flag. `LmaxReadOnlyDemoSnapshotStabilitySummaryValidator` verifies the summary has no credential values, no order submission, no runtime shadow replay submit, no scheduler, and no trading mutation.

## LMAX Read-Only Runtime Phase 5P Stability Decision

Phase 5P adds `LmaxReadOnlyDemoSnapshotStabilityClosureValidator`, `scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1`, and `scripts/check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1`. The closure validator checks the Phase 5O stability summary, requires completed attempts to match requested attempts, requires all attempts to be successful snapshots, validates referenced artifacts with the Phase 5L validator, and validates referenced previews as `MarketDataOnly`.

The review command is local-only. It does not call the runtime prototype, connect to LMAX, require credentials, submit to shadow replay, or mutate state. A Phase 5P `PASS` is readiness to consider another explicit manual MarketData evidence workflow phase only; it does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, or production use.

## LMAX Read-Only Runtime Phase 5Q Workflow Hardening

Phase 5Q adds `LmaxReadOnlyMarketDataWorkflowValidator`, `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1`, and `scripts/check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1`. The review script takes a Phase 5O stability summary or explicit artifact list, validates Phase 5L snapshot artifacts, validates or regenerates Phase 5M `MarketDataOnly` previews, optionally records explicitly requested Phase 5N manual replay results, and writes an ignored sanitized workflow manifest.

Default workflow review does not replay, does not require API availability, does not connect externally, and does not call the runtime prototype. Missing optional replay is recorded as a warning, so the expected default decision for the 3/3 stability summary is `PASS_WITH_WARNINGS`. Runtime code still does not submit to shadow replay; replay is only the separate manual script/API path when `-ReplayEvidencePreviews -ConfirmLocalReplay` is supplied.

## LMAX Read-Only Runtime Phase 5R Manual Replay Review

Phase 5R keeps replay explicit and local. `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1 -ReplayEvidencePreviews -ConfirmLocalManualReplay` requires the local API, then invokes the existing manual replay script for each validated `MarketDataOnly` preview. The generated manifest records replay status, replay run id when available, zero observation counts, mutation guard, and `noSensitiveContent=true`.

`LmaxReadOnlyMarketDataWorkflowValidator` treats no replay as `PASS_WITH_WARNINGS`, but a replay-reviewed manifest passes only when replay count equals preview count, every replay is `Completed`, all observation counts are zero, mutation guards are `Unchanged`, `runtimeShadowReplaySubmit=false`, and `externalConnectionAttempted=false`. Runtime code still does not submit to shadow replay.

## LMAX Read-Only Runtime Phase 5S Release Gate

Phase 5S adds `LmaxReadOnlyManualWorkflowReleaseValidator`, `scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1`, and `scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1`. The release script validates the closed Phase 5O stability summary through the Phase 5Q/5R workflow path, writes a fixed ignored release manifest, and records rollback instructions.

The release gate validates referenced snapshot artifacts with the Phase 5L script, validates referenced previews with the evidence validator, checks optional replay results, and scans for forbidden scheduler/order/gateway/runtime-submit surfaces. Skipped replay is `PASS_WITH_WARNINGS`; explicit local replay can produce `PASS` only when all preview replays are zero-observation and mutation-guard unchanged.

## LMAX Read-Only Runtime Phase 5T Runbook Freeze

Phase 5T freezes the controlled manual workflow as documentation and a local review gate only. It adds no runtime service, scheduler, gateway registration, replay submit, order command, or mutation dependency.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1
```

The gate verifies the Phase 5S manifest/report, the frozen runbook document, `PASS`/`PASS_WITH_WARNINGS` semantics, optional replay warning reason, and the continued absence of scheduler/polling, runtime shadow replay submit, order command surface, gateway registration, and trading mutation dependencies. `PASS_WITH_WARNINGS` remains acceptable when the only warning is optional replay skipped.

## LMAX Read-Only Runtime Phase 5V Final Audit Pack

Phase 5V adds `LmaxReadOnlyMarketDataWorkflowAuditPackValidator`, `scripts/build-lmax-readonly-marketdata-workflow-audit-pack.ps1`, and `scripts/check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1`. The builder consumes a validated Phase 5O stability summary and replay-enabled Phase 5R workflow manifest, validates referenced Phase 5L artifacts and Phase 5M previews, and writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/`.

The final gate requires `FinalDecision=PASS`, artifact/preview/replay counts to match, replay observations to remain zero, mutation guards to stay `Unchanged`, and all runtime safety scans to remain closed. It does not run an external socket attempt or replay.
## Phase 5W Operational Signoff

Phase 5W adds only the operational signoff validator, local signoff script, gate script, and signoff documentation for the controlled manual Demo MarketData workflow. It validates the Phase 5V audit pack and records that the validated path is three manual Demo EURUSD snapshots, three sanitized artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, and unchanged mutation guards.

Run:

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md -SignoffBy "local-operator" -Role "Operator" -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

The scripts do not connect externally, read credentials, run snapshots, perform replay, register gateways, schedule work, submit orders, submit runtime shadow replay, or mutate trading state. `PASS` recognizes the manual Demo read-only workflow only; it does not authorize scheduler, polling, runtime replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.

## Phase 5X Operator Summary

Phase 5X adds a read-only status summary over the Phase 5W signoff. The model is `LmaxReadOnlyMarketDataWorkflowStatusSummary`, the endpoint is `GET /lmax-readonly-runtime/marketdata-workflow/status`, and the local script is `scripts/show-lmax-readonly-marketdata-workflow-status.ps1`.

The endpoint reads signoff/reporting artifacts only. It does not read credentials, connect to LMAX, run the runtime prototype, run replay, mutate state, register a gateway, or schedule work. The UI panel appears on the LMAX Shadow page and exposes no live controls.

## Phase 6A Operationalization Planning Boundary

Phase 6A adds a documentation and gate-only boundary after the validated Phase 5 manual Demo EURUSD MarketData workflow. It introduces no new runtime capability.

Files:

- `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`
- `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`
- `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`

The Phase 6A gate verifies the Phase 5Y final documentation pack, Phase 5V audit pack, Phase 5W operational signoff, and Phase 5X status summary where available. It also source-scans the LMAX runtime/script scope for scheduler or polling markers, runtime shadow replay submit markers outside explicit manual replay scripts, order command surface, trading-state mutation references, and API/Worker gateway registration.

Recommended next phase: `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run`. This keeps the next change design-only and avoids scheduler, polling, runtime shadow replay submit, order submission, gateway registration, and trading-state mutation.

## Phase 6B Instrument Allowlist Design

Phase 6B adds `LmaxReadOnlyInstrumentAllowlist` and `LmaxReadOnlyInstrumentAllowlistValidator`. This is a planning model only; it is not wired into the socket prototype, API execution path, Worker, scheduler, or shadow replay submit path.

The candidate allowlist currently includes GBPUSD, USDJPY, EURGBP, and AUDUSD. Each entry records symbol metadata, Demo venue label, liquidity tier, Demo-readiness status, a placeholder SecurityID confirmation label, `EvidenceMode=MarketDataOnly`, and `IsApprovedForExternalRun=false`.

Validation rules:

- additional candidates must not include the existing EURUSD / `4001` baseline,
- candidates must remain Demo-only,
- candidates must use `MarketDataOnly`,
- candidates must be planning-only,
- safety rules must keep scheduler, polling, runtime shadow replay submit, order submission, gateway registration, external connection approval, credential values, and trading mutation disabled.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1
```

## Phase 6C/6D SecurityID Manifests

Phase 6C and Phase 6D keep SecurityID work in local planning manifests only. Neither manifest is wired into the runtime prototype, API execution path, Worker, scheduler, or shadow replay submit path.

- Phase 6C manifest: `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdManifest.cs`
- Phase 6D discovery manifest: `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.cs`
- Phase 6D gate: `scripts/check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1`

Phase 6D candidate values are explicit placeholders:

| Symbol | Placeholder |
| --- | --- |
| GBPUSD | PHASE6D-DISCOVERY-PENDING-GBPUSD |
| USDJPY | PHASE6D-DISCOVERY-PENDING-USDJPY |
| EURGBP | PHASE6D-DISCOVERY-PENDING-EURGBP |
| AUDUSD | PHASE6D-DISCOVERY-PENDING-AUDUSD |

The validator requires every allowlist symbol to have a non-empty candidate value, every `IsApprovedForExternalRun` flag to remain false, and all local safety flags to show no external connection, no external API call, no scheduler/polling, no runtime shadow replay submit, no order submission, no gateway registration, and no trading mutation.

## Phase 6E SecurityID Evidence Review

Phase 6E adds `LmaxReadOnlyInstrumentSecurityIdSourceEvidence`, `LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest`, and `LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator`.

The default manifest lists GBPUSD, USDJPY, EURGBP, and AUDUSD as `NeedsMoreEvidence`, using the Phase 6D placeholder values and `IsApprovedForExternalRun=false`.

Validator rules:

- Symbol must exist in the Phase 6B allowlist.
- `AcceptedForPlanning` requires a non-placeholder proposed SecurityID.
- `AcceptedForPlanning` requires an evidence reference, reviewer, reviewed timestamp, and `High` or `Confirmed` confidence.
- `IsApprovedForExternalRun` must remain false.
- `noSensitiveContent` must be true.
- Credential-shaped strings, raw sensitive FIX tags, order/trading authorization language, Production, and UAT authorization language fail validation.
- Safety flags must show no external connection, external API call, snapshot, replay, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, or trading mutation.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1
```

Expected current decision is `PASS_WITH_KNOWN_WARNINGS` because real SecurityID evidence has not been accepted yet.

## Phase 6F SecurityID Confirmation Records

Phase 6F adds `LmaxReadOnlyInstrumentSecurityIdConfirmationRecord` and `LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator`.

The local scripts are:

- `scripts/new-lmax-readonly-securityid-confirmation-record.ps1`
- `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`
- `scripts/check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1`

Record validation requires Phase 6B allowlist membership, matching slash symbol, non-empty proposed SecurityID, evidence reference, captured-by metadata, no sensitive content, and `IsApprovedForExternalRun=false`. `AcceptedForPlanning` additionally requires a non-placeholder SecurityID, reviewer, reviewed timestamp, review reason, and High or Confirmed confidence.

The review validator returns `PASS_WITH_KNOWN_WARNINGS` when accepted records are missing but the boundary remains safe. It fails conflicting accepted SecurityIDs, placeholders accepted for planning, credential-shaped content, order/trading/Production/UAT authorization language, or any external-run approval.

## Phase 6G Record Entry Workflow Hardening

Phase 6G hardens the Phase 6F scripts:

- `new-lmax-readonly-securityid-confirmation-record.ps1` supports `-WhatIfPreview`, `-OutputFile`, and no-overwrite-by-default behavior.
- `new-lmax-readonly-securityid-confirmation-record-template.ps1` generates per-symbol templates under ignored artifacts.
- `review-lmax-readonly-securityid-confirmation-records.ps1` prints per-instrument counts, accepted/pending state, conflicts, and issue codes.
- `check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1` verifies the workflow without creating accepted records or calling external systems.

The current safe state is `PASS_WITH_KNOWN_WARNINGS` until accepted records exist for all four candidate instruments.

## Phase 6H Real Confirmation Records

Phase 6H changes the default real-record location to `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`, which remains ignored. Developers should use `scripts/new-lmax-readonly-securityid-confirmation-record.ps1 -WhatIfPreview` to inspect sanitized JSON before writing, then run `scripts/review-lmax-readonly-securityid-confirmation-records.ps1` and `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1`.

`PASS` means all four Phase 6 candidates have valid `AcceptedForPlanning` records. `PASS_WITH_KNOWN_WARNINGS` means some instruments are still missing/pending but safe. `FAIL` means invalid, sensitive, conflicting, or externally approved records. `AcceptedForPlanning` does not authorize external runs; `IsApprovedForExternalRun=false` remains required and no snapshot, replay, scheduler, order path, real gateway, or trading mutation is introduced. Next phase is Phase 6I to apply accepted planning values while still non-executable, or remain pending evidence.

## Phase 6I SecurityList Discovery

Phase 6I adds a manual FIX `SecurityListRequest` discovery model and script. The C# model parses fake SecurityList responses, matches GBPUSD, USDJPY, EURGBP, and AUDUSD, detects conflicts, redacts sensitive text, and writes safe artifacts. Automated tests do not require LMAX.

The manual script is `scripts/run-lmax-readonly-runtime-demo-securitylist-discovery.ps1`; it requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and `-Reason`. It sends only SecurityListRequest on Demo market-data FIX, never snapshots, orders, replay, scheduler, gateway registration, or trading mutation. Gate with `scripts/check-lmax-readonly-runtime-phase6i-securitylist-discovery-gate.ps1`; without an artifact the expected decision is `PASS_WITH_KNOWN_WARNINGS`.

## Phase 6J SecurityList Diagnostics

Phase 6J adds `LmaxReadOnlySecurityListDiscoveryArtifactValidator`, `LmaxReadOnlySecurityListFailureDiagnostics`, and explicit profile definitions. Failed artifacts are parsed for sanitized status, request profile, request type, reject tag/text, logon/request/logout flags, and safety flags.

Request profiles are `MinimalRequest`, `ProductFx`, `SecurityTypeFx`, `LabCompatibleFallback`, `AllSecurities`, `SymbolExact`, `CandidateSymbolsOneByOne`, and `AutoSequence`. Known-rejected profiles are blocked locally unless `-AllowKnownRejectedDiagnostics` is present; `AutoSequence` uses safe profiles first.

## Phase 6L SecurityList Fallback Analysis

Phase 6L adds `LmaxReadOnlySecurityListDiscoveryFallbackDecision` and `LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator`. The validator reviews sanitized AutoSequence failure artifacts, extracts attempt-level profile/reject metadata when present, detects missing reject diagnostics, and produces a non-authorizing fallback decision.

The local review script is `scripts/review-lmax-readonly-runtime-securitylist-discovery-failure.ps1`; the gate is `scripts/check-lmax-readonly-runtime-phase6l-securitylist-fallback-gate.ps1`. Both are local-only and must not connect to LMAX, run SecurityListRequest, request snapshots, replay evidence, schedule/poll, submit orders, register gateways, expose credentials, or mutate trading state. A fallback decision never sets `IsApprovedForExternalRun=true`.

## Phase 6M CSV SecurityID Records

Phase 6M adds `LmaxReadOnlyInstrumentCsvSecurityIdExtractor` for sanitized LMAX instrument CSV content. It requires `Instrument Name`, `LMAX ID`, and `LMAX symbol`, matches the four Phase 6 candidates, selects DemoLondon/NewYork 400x IDs, and records Tokyo 600x IDs as observed but not selected.

The generation script is `scripts/new-lmax-readonly-securityid-records-from-instrument-csv.ps1`; the gate is `scripts/check-lmax-readonly-runtime-phase6m-csv-securityid-records-gate.ps1`. The expected selected values are GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007. Generated records are planning-only `AcceptedForPlanning` records and always keep `IsApprovedForExternalRun=false`.

## Phase 6N Planning Manifest

Phase 6N adds `LmaxReadOnlyInstrumentSecurityIdPlanningManifest`, its builder, and its validator. The builder consumes accepted confirmation records and writes a planning-only manifest with `securityIdSource=8`, Demo, DemoLondon, confirmation record references, and `IsApprovedForExternalRun=false`.

The apply script is `scripts/apply-lmax-readonly-securityid-planning-values.ps1`; the gate is `scripts/check-lmax-readonly-runtime-phase6n-planning-values-gate.ps1`. The validator fails missing records, conflicting records, placeholders, low-confidence/non-accepted records, sensitive content, Production/UAT/order authorization language, unsafe flags, or any external-run approval.

## Phase 6O Per-Instrument Safety Gates

Phase 6O adds `LmaxReadOnlyPerInstrumentSafetyGate`, `LmaxReadOnlyAdditionalInstrumentSafetyGateManifest`, builders, validators, `scripts/build-lmax-readonly-additional-instrument-safety-gates.ps1`, and `scripts/check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1`.

The gate consumes the Phase 6N planning manifest and produces one result for GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007. A passing result validates accepted planning value, SecurityIDSource=8, Demo/DemoLondon scope, MarketDataOnly intent, no order capability, no runtime shadow replay submit, no scheduler/polling, no trading mutation, and a future explicit operator prompt requirement. `PASS` is a planning safety result only; `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false` must remain false.

## Phase 6P Additional Snapshot Preflight

Phase 6P adds `LmaxReadOnlyAdditionalInstrumentSnapshotPreflight`, aggregate preflight manifests, validators, `scripts/build-lmax-readonly-additional-instrument-snapshot-preflights.ps1`, and `scripts/check-lmax-readonly-runtime-phase6p-additional-snapshot-preflight-gate.ps1`.

The validator requires the Phase 6N planning value and Phase 6O safety gate to match the requested instrument. It enforces `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, Demo/DemoLondon, `MarketDepth=1`, runtime/wait caps of 1..30 seconds, event cap of 1..25, required operator id and reason, no sensitive content, and no Production/UAT/order authorization language. It fails any executable flag, including `allowExternalConnections`, `allowOrderSubmission`, `schedulerEnabled`, `submitToShadowReplay`, `persistToTradingTables`, `isApprovedForExternalRun`, `eligibleForManualSnapshotAttempt`, or `canRunExternalSnapshot`.

## Phase 6Q Approval Envelope

Phase 6Q adds `LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope`, a creation script, a review script, and a gate. The validator binds an envelope to a PASS Phase 6P preflight result and requires matching symbol, slash symbol, SecurityID, SecurityIDSource=8, Demo/DemoLondon scope, request profile, safe caps, operator reason, reviewer for `AcceptedForPlanning`, and all planning attestations.

`AcceptedForPlanning` is deliberately non-executable. Validation fails if `isApprovedForExternalRun`, `eligibleForManualSnapshotAttempt`, or `canRunExternalSnapshot` is true, or if sensitive content or Production/UAT/order authorization language appears.

## Phase 6R Dry-Run Report

Phase 6R adds `LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport`, dry-run creation/review scripts, and a gate. The validator aggregates the Phase 6N planning manifest, Phase 6O safety gate, Phase 6P preflight, and Phase 6Q approval envelope for GBPUSD / SecurityID 4002.

The report fails if any source decision is missing or non-PASS, if the approval envelope is not `AcceptedForPlanning`, if any run eligibility or attempt flag is true, or if sensitive/authorization language appears. A `PASS` report remains non-executable.
### Phase 6S - Single-Instrument Manual Snapshot Attempt Gate

Phase 6S introduces `LmaxReadOnlySingleInstrumentSnapshotAttemptGate` and validator coverage for a non-executable GBPUSD attempt gate. The gate validates source artifact consistency across planning, safety, preflight, approval envelope, and dry-run inputs.

The implementation must keep all execution and eligibility flags false. `PASS` means source consistency only; it does not permit external connectivity or snapshot execution.

### Phase 6T - GBPUSD Execution Plan

Phase 6T introduces `LmaxReadOnlyGbpusdManualSnapshotExecutionPlan` and validator coverage for the planning-only command template, abort criteria, rollback steps, and post-run validation checklist. The validator requires the Phase 6S attempt gate to be `PASS`, GBPUSD `4002`, SecurityIDSource `8`, and all execution flags false.

### Phase 6U - GBPUSD Operator Signoff

Phase 6U introduces `LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff` and validator coverage for `SignedForPlanning` attestations. The validator requires the execution plan and optional Phase 6T gate to be `PASS`, all planning attestations to be true for signed signoffs, and all execution flags to remain false.

### Phase 6V - GBPUSD Final Readiness

Phase 6V introduces `LmaxReadOnlyGbpusdManualSnapshotFinalReadiness` and validator coverage across the full GBPUSD source chain. The validator requires expected decisions from Phase 6N through 6U, source agreement on GBPUSD `4002`, and all run eligibility and attempt flags false.

### Phase 6W - GBPUSD Snapshot Result

Phase 6W introduces `LmaxReadOnlyGbpusdManualSnapshotResult` and result validation for a sanitized GBPUSD snapshot outcome. The wrapper script delegates to the existing manual prototype with fixed GBPUSD parameters and a required final readiness file. Automated tests must not require live LMAX.
## Phase 6X GBPUSD Empty-Book Review

Phase 6X adds local validation for the first GBPUSD Demo read-only snapshot artifact. `CompletedWithEmptyBook` is accepted as `PASS_WITH_KNOWN_WARNINGS` only when logon succeeded, a snapshot was received, `entryCount=0`, bid/ask/mid are null, reject counts are zero, credential values were not returned, and all order/shadow/scheduler/mutation flags remain false.

Use `scripts/review-lmax-readonly-gbpusd-snapshot-result.ps1 -ArtifactFile <artifact>` for artifact review and `scripts/check-lmax-readonly-runtime-phase6x-gbpusd-result-review-gate.ps1 -ArtifactFile <artifact>` for the gate. These commands are local-only and must not connect to LMAX, run snapshots, replay evidence, or mutate trading state.

## Phase 6Y GBPUSD Market-Hours Retry Preparation

Phase 6Y introduces `LmaxReadOnlyGbpusdMarketHoursRetryReadiness` and `scripts/prepare-lmax-readonly-gbpusd-market-hours-retry.ps1`. The model validates that the previous result was `CompletedWithEmptyBook`, the retry is manual-only and market-hours-only, `retryAttemptCount=1`, `canRunAutomatically=false`, and all scheduler/polling/order/shadow/mutation flags remain disabled.

The preparation script reads the Phase 6V final readiness and Phase 6X review reports only. It does not connect to LMAX, run a snapshot, replay evidence, read credentials, or schedule work.

## Phase 6Z-A Additional-Instrument Planning Pipeline

Phase 6Z-A adds a local-only aggregate planning pipeline for the additional Demo MarketData instruments. The builder is `scripts/build-lmax-readonly-additional-instrument-planning-pipeline.ps1`; it reads the Phase 6N planning manifest, Phase 6O safety gate manifest, and Phase 6P preflight manifest, then creates missing non-executable approval, dry-run, attempt-gate, execution-plan, operator-signoff, and final-readiness artifacts for selected known symbols.

Allowed symbols are GBPUSD, EURGBP, USDJPY, and AUDUSD. The validated values are GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007, all with SecurityIDSource `8`, Demo/DemoLondon scope, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.

The aggregate validator requires `executableCount=0`, all expected artifact decisions, and false run flags: `IsApprovedForExternalRun`, `eligibleForManualSnapshotAttempt`, `canRunExternalSnapshot`, `externalConnectionAttempted`, `snapshotAttempted`, `replayAttempted`, `orderSubmissionAttempted`, `shadowReplaySubmitAttempted`, `tradingMutationAttempted`, and `schedulerStarted`. Phase 6Z-A does not add connectivity, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, or trading mutation.

Gate: `scripts/check-lmax-readonly-runtime-phase6za-additional-instrument-pipeline-gate.ps1`.

## Phase 6Z-C Additional-Instrument Planning Status

Phase 6Z-C adds `LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary` and a read-only status projection over the Phase 6Z-A aggregate manifest. The API endpoint is `GET /lmax-readonly-runtime/additional-instruments/planning-status`; it reads the latest local pipeline manifest and returns sanitized DTOs only.

## Phase 6Z-D Additional-Instrument Planning Final Documentation Pack

Phase 6Z-D adds the final local documentation/audit pack for the additional-instrument planning pipeline. The builder script is `scripts/build-lmax-readonly-additional-instruments-planning-doc-pack.ps1`; it reads the Phase 6Z-A aggregate pipeline manifest and Phase 6Z-C planning status report, validates non-executable flags, and writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/`.

The Phase 6Z-D gate is `scripts/check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1`. It validates the final doc, builder, generated pack, manifest, status report, `executableCount=0`, false run flags, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

Phase 6Z-D is docs/reporting only. It does not connect to LMAX, run SecurityListRequest, request snapshots, replay evidence, read credentials, register gateways, add timers/hosted services, or mutate trading state.

## Phase 6Z-E Market-Hours Next Action Summary

Phase 6Z-E adds `LmaxReadOnlyMarketHoursNextActionSummary` and a read-only projection over the GBPUSD final readiness, Phase 6Y retry readiness, Phase 6X empty-book review, and Phase 6Z-D documentation pack. The API endpoint is `GET /lmax-readonly-runtime/market-hours-next-action`; it reads local sanitized artifacts only and returns the next recommended operator action without starting anything.

The local script is `scripts/show-lmax-readonly-market-hours-next-action.ps1`, and the gate is `scripts/check-lmax-readonly-runtime-phase6ze-market-hours-action-card-gate.ps1`. The UI panel is status-only and must not add buttons or controls for external connections, snapshots, replay, scheduler/polling, credentials, host/port, orders, gateway registration, or mutation.

Phase 6Z-E does not run LMAX, SecurityListRequest, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, credential reads, or trading-state mutation.

The UI consumes the endpoint through `getLmaxReadOnlyAdditionalInstrumentPlanningStatus()` and renders the LMAX Shadow page planning-status panel. The panel intentionally has no execution buttons, replay controls, scheduler controls, credential fields, host/port fields, or gateway controls.

Script: `scripts/show-lmax-readonly-additional-instrument-planning-status.ps1`.

Gate: `scripts/check-lmax-readonly-runtime-phase6zc-additional-instrument-status-panel-gate.ps1`.

## Phase 7A Next Boundary ADR

Phase 7A adds no runtime model or service. It creates `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`, `docs/LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase7a-next-boundary-gate.ps1`.

The recommended next implementation boundary is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. Any Phase 7B work must remain manual, one instrument at a time, explicit-operator-command based, sanitized-artifact based, and must not add scheduler/polling, runtime shadow replay submit, order surface, gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.

## Phase 7B Controlled Manual Workflow Plan

Phase 7B adds `LmaxReadOnlyControlledManualMultiInstrumentWorkflowPlan`, `scripts/build-lmax-readonly-controlled-manual-multi-instrument-workflow-plan.ps1`, and `scripts/check-lmax-readonly-runtime-phase7b-controlled-manual-workflow-plan-gate.ps1`.

The validator requires GBPUSD, EURGBP, USDJPY, and AUDUSD in that sequence, SecurityIDSource `8`, expected 400x SecurityIDs, `oneInstrumentAtATime=true`, `maxAttemptsPerInstrument=1`, `retryRequiresNewPhase=true`, `marketHoursOnly=true`, and `manualOperatorCommandOnly=true`. It fails if any executable flag, scheduler/polling, runtime replay submit, order, gateway registration, or trading mutation flag is enabled.

## Phase 7C GBPUSD Closure Workflow

Phase 7C adds local post-run closure tooling only. The review script classifies a supplied GBPUSD / SecurityID `4002` artifact as `CompletedWithBook`, `CompletedWithEmptyBook`, `FailedSafe`, or `UnsafeFail`. The evidence preview script maps safe book or empty-book artifacts to MarketDataOnly evidence without runtime submit. The replay script is explicit manual local replay only and requires `-ConfirmLocalManualReplay`.

The gate is `scripts/check-lmax-readonly-runtime-phase7c-gbpusd-closure-gate.ps1`. With no artifact supplied, it returns `PASS_WITH_KNOWN_WARNINGS` because no market-hours result has been produced. Phase 7C adds no external connection, snapshot, automatic replay, scheduler/polling, order surface, gateway registration, credential exposure, or trading mutation.

## Phase 7E2 Developer Notes

Phase 7E2 introduces `LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration`, a local-only EURGBP rehydration script, a gate script, and unit tests. The validator fails non-`ProceedToEurgbpPlanning` Phase 7D states, wrong EURGBP SecurityID, executable flags, batch execution, attempts, scheduler/replay/order/mutation flags, sensitive content, and authorization language.

## Phase 7F2 Developer Notes

Phase 7F2 introduces `LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist`, a local-only checklist generation script, a gate script, and unit tests. The validator requires EURGBP / SecurityID `4003`, Phase 7E2 readiness `PASS`, previous GBPUSD closure `PASS`, previous Phase 7D decision `ProceedToEurgbpPlanning`, `oneInstrumentAtATime=true`, `batchExecutionAllowed=false`, all run flags false, and a future command template marked `DO NOT RUN IN PHASE 7F2`.

## Phase 7G2 Developer Notes

Phase 7G2 introduces `LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate`, a local-only final pre-run artifact builder, a gate script, and unit tests. The validator requires EURGBP / SecurityID `4003`, Demo/DemoLondon, Phase 7D `ProceedToEurgbpPlanning`, previous GBPUSD closure `PASS`, Phase 7E2 readiness `PASS`, Phase 7F2 checklist `PASS`, `oneInstrumentAtATime=true`, `batchExecutionAllowed=false`, all run flags false, no scheduler/runtime replay/order/mutation/gateway registration flags, and API/Worker `FakeLmaxGateway`.

The model is an aggregate consistency check only. It must not be used by runtime code to permit external snapshots.

## Phase 7H Generic Additional Instrument Workflow

Phase 7H adds `LmaxReadOnlyAdditionalInstrumentManualSnapshotWorkflow` and generic local scripts for one supported additional instrument per invocation. The supported symbols are GBPUSD `4002`, EURGBP `4003`, USDJPY `4004`, and AUDUSD `4007`, all with SecurityIDSource `8`, Demo/DemoLondon, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.

## Phase 7H2 Generic Final Pre-Run Gate

Phase 7H2 adds `LmaxReadOnlyAdditionalInstrumentFinalPreRunGate` and `LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator`. The validator enforces the Phase 7H wrapper contract for supported additional instruments: exact symbol/security ID mapping, Demo/DemoLondon profile, `oneInstrumentAtATime=true`, `batchExecutionAllowed=false`, all run eligibility flags false, no scheduler/replay/order/mutation/gateway flags, `apiWorkerGatewayMode=FakeLmaxGateway`, and `finalDecision=PASS`.

Regression coverage includes USDJPY/AUDUSD pass cases, wrong/Tokyo 600x IDs, unknown symbols, unsafe flags, authorization language, and rejection of a Phase 6Z-A final-readiness-shaped JSON as a Phase 7H wrapper gate.

`scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1` validates the supplied final pre-run gate before delegating to the isolated manual prototype. It refuses unknown symbols, multiple symbols, wrong IDs, wrong profile, missing explicit flags, non-`PASS` gates, executable run flags, scheduler/shadow/order/mutation flags, and non-`FakeLmaxGateway` source metadata.

The generic review, evidence preview, optional local replay, and closure manifest scripts remain local-only. They do not add API/Worker registration, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, or trading mutation.
