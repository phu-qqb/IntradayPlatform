# Local Runbook

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB for persistent local mode
- PowerShell

Check the environment:

```powershell
.\scripts\check-env.ps1
```

## Restore, Build, Test

```powershell
.\scripts\restore-build-test.ps1
```

Before moving to a new technical phase, run the operational readiness gate:

```powershell
.\scripts\run-operational-readiness-gate.ps1
```

The gate is local-only. It validates backend/frontend health, evidence fixtures, FakeLmax-only safety, shadow replay, shadow reader blocking behavior, and evidence coverage. It never calls LMAX, never submits orders, and never uses credentials. See [OPERATIONAL_READINESS_CHECKLIST.md](OPERATIONAL_READINESS_CHECKLIST.md) for the release decision criteria.

Equivalent commands:

```powershell
dotnet restore .\QQ.Production.Intraday.sln --configfile .\NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
dotnet build .\QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
dotnet test .\QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false
```

## LocalDB

Default connection:

```text
Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Update the local database and seed reference data:

```powershell
.\scripts\update-local-db.ps1
```

Add demo data:

```powershell
.\scripts\update-local-db.ps1 -SeedDemoData
```

Demo seed creates deterministic fake market data snapshots only. It does not create a persistent sample model run; local model runs are created by `scripts/smoke-local.ps1` or `POST /model-runs`.

Reset the local database:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
```

Prompt #4.2 adds unique reference-data constraints. If an old local database has duplicate reference rows from early seed runs, the new migration or startup integrity check may fail. For local development, reset the local database explicitly:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
```

Production/RDS remediation is not implemented yet and must use a controlled duplicate-remediation plan later.

Inspect LocalDB:

```powershell
sqllocaldb info
sqllocaldb start MSSQLLocalDB
```

Then connect with SQL Server Management Studio or Azure Data Studio to `(localdb)\MSSQLLocalDB`.

## Run API

```powershell
.\scripts\run-api.ps1
```

The API uses `Persistence:Provider = SqlServerLocal` by default and still registers only `FakeLmaxGateway`.

LMAX adapter contract and simulator parity details are documented in [ADAPTER_CONTRACTS.md](ADAPTER_CONTRACTS.md). These checks are design-gate infrastructure only; they do not register a real LMAX adapter or enable live trading.

## LMAX Adapter Boundary

The Connectivity Lab has validated the LMAX Demo FIX lifecycle, including market-data snapshot, Demo order lifecycle, order-status recovery, trade-capture recovery, and lifecycle evidence checks. That validation does not change local runtime behavior.

The API and Worker remain `FakeLmaxGateway` only. No LMAX FIX session, real gateway, shadow-mode service, credential form, live market-data provider, or order-submission path is registered in the main runtime.

The dormant adapter design gate lives in `src/QQ.Production.Intraday.Infrastructure.Lmax` and is documented in `docs/LMAX_ADAPTER_DESIGN.md`. It defines contracts, normalized DTOs, safety options, and in-memory shadow-mode observations for future work. Future LMAX integration must begin in shadow mode and must not mutate orders, fills, positions, or execution state.

The adapter contract parity gate is documented in `docs/ADAPTER_CONTRACTS.md`. It checks that simulator events and LMAX FIX-normalized events map into the same neutral venue lifecycle contract before any real adapter work is considered.

The adapter skeleton includes pure FIX message builders and mappers plus a runtime safety validator. It is disabled by default and not registered. There is no local runbook step, UI control, or API endpoint that enables it.

The live shadow reader skeleton is separate from the adapter skeleton and is disabled by default. Its local status/run endpoints are diagnostics for future read-only shadow work only. They do not accept credentials, do not call the Connectivity Lab, do not open FIX sessions, do not submit orders, and do not mutate trading state.

The future read-only runtime adapter design is documented in [LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md](LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md). It defines activation levels, safety gates, evidence batching, and certification requirements for a possible future runtime shadow reader. It is design-only today: no sockets, credentials, runtime FIX reader, DI registration, scheduler activation, order submission, or trading-state mutation are added.

The future implementation sequence is documented in [LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md](LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md), with quick-review gates in [LMAX_READONLY_RUNTIME_PHASE_GATES.md](LMAX_READONLY_RUNTIME_PHASE_GATES.md). Phase 1 is implemented as inert runtime interfaces, a disabled adapter, a disabled evidence sink, and a no-op run store. Phase 2 is implemented as a service-level fake/in-memory adapter that reads local evidence fixtures and produces preview counts only. Phase 3 adds manual local diagnostic endpoints at `GET /lmax-readonly-runtime/status`, `POST /lmax-readonly-runtime/run`, `GET /lmax-readonly-runtime/runs`, and `GET /lmax-readonly-runtime/runs/{id}`. They are disabled/blocked by default, require a reason for run attempts, reject unsafe fixture names, and never accept credentials, connection fields, order controls, or scheduler controls. Phase 3.5 proves through integration tests that an explicit fake-enabled test configuration can complete fixture-only previews and return per-source event counts without submitting to shadow replay. Phase 4D adds `POST /lmax-readonly-runtime/fake-transport-preview` for predefined in-memory fake transport scenarios only; it remains disabled by default and no-shadow-submit. Phase 4E adds a hard-disabled external-session skeleton only; it reports `SkeletonOnly` and no socket/logon/credential/order/replay/mutation implementation. Phase 4F adds a guarded transport interface and disabled transport only; even `ConnectReadOnlyAsync` is a blocked contract method and does not open a socket. Phase 4G adds a typed configuration envelope and inactive sample with no credential values, host/user/password fields, socket activation, or live controls. Phase 4H adds a credential-profile boundary and disabled resolver only; `CredentialProfileName` is a label and no credential values are read, used, stored, logged, or returned. Phase 4I adds a non-secret venue-profile boundary and disabled/static registry only; `VenueProfileName` is a label and no host, port, user, account, sender/target comp, endpoint, session, or credential values are exposed. Phase 4J adds a run-intent envelope and validator only; it requires a manual reason and operator id but starts no external session. Phase 4K adds `POST /lmax-readonly-runtime/external-run-intent/validate`, a validate-only endpoint that returns blocked/structured safety diagnostics and always reports no session start, external connection, credential read, shadow replay submit, or trading mutation. Phase 4L adds `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`, a no-network report endpoint that aggregates intent/options validation, venue profile status, disabled credential resolver status, disabled guarded transport status, blocked skeleton status, safety gates, expected outcome, and operator guidance while still starting nothing. Phase 4M adds `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`, a metadata-only signoff endpoint that checks required attestations and maker/checker rules but always returns `canAuthorizeExecution=false`. Phase 4N adds `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`, a metadata-only audit envelope endpoint that checks the intent/report/signoff chain and stable blockers but always returns `canAuthorizeExecution=false`. Phase 4O adds `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`, a metadata-only snapshot endpoint that aggregates the full no-network chain and always returns `canStartSession=false`. Phase 4P adds `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`, the final local no-socket release gate; it validates the Phase 4A-4O boundary and writes an ignored readiness report, but it does not add connectivity or execution capability. Phase 5A adds first-transport preflight planning through `docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md`, `docs/LMAX_READONLY_RUNTIME_PHASE5A_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1`; it defines kill/rollback and abort controls but still adds no socket capability. Run `scripts/smoke-lmax-readonly-runtime-fake-local.ps1` against a local API to confirm the safe default; use `-ExpectFakeEnabled` and `-ExpectFakeTransportPreviewEnabled` only when the API has deliberately been launched with the fake-enabled local preview script. Run `scripts/smoke-lmax-readonly-runtime-external-preflight-local.ps1` to confirm the Phase 4K, Phase 4L, Phase 4M, Phase 4N, and Phase 4O endpoints stay validate-only/no-network/no-authorization. Run `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1` and `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1` before considering any separately prompted socket prototype.

Default disabled read-only runtime smoke:

```powershell
# Terminal 1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api.ps1

# Terminal 2
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1
```

Explicit fake-enabled fixture preview smoke:

```powershell
# Terminal 1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1

# Terminal 2
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled
```

Explicit fake transport preview smoke:

```powershell
# Terminal 1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1

# Terminal 2
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled -ExpectFakeTransportPreviewEnabled
```

The fake-enabled launcher sets only local fixture/transport-preview environment variables. It keeps `AllowExternalConnections=false`, `AllowCredentialUse=false`, `AllowOrderSubmission=false`, `PersistToTradingTables=false`, `PersistRawFixMessages=false`, `SchedulerEnabled=false`, `SubmitToShadowReplay=false`, and `DryRun=true`.

Phase 4 preflight boundary check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase4-preflight.ps1
```

This preflight does not connect to LMAX and does not call Connectivity Lab scripts. It checks that the Phase 4 boundary document exists, default appsettings remain disabled/design-only, runtime DTOs do not expose credential-shaped fields, API/Worker remain FakeLmax-only, generated evidence is not dirty, and API posture is safe when the local API is available.

Phase 4A adds only the external read-only session contracts and disabled stub. There is still no socket, FIX logon/logout, credential use, Connectivity Lab call, evidence creation, shadow replay submit, scheduler, gateway registration, or trading-state mutation. The same preflight script checks the Phase 4A contract/stub boundary and verifies no order-submission event or method surface exists.

Phase 4B adds only the external-session fake transport harness. It runs in memory from predefined read-only messages, emits deterministic event counters, and still has no socket, FIX logon/logout, credential use, Connectivity Lab call, evidence creation, shadow replay submit, scheduler, gateway registration, or trading-state mutation. Event-to-evidence preview mapping is deferred to Phase 4C.

Phase 4C adds only fake transport to evidence preview mapping. It creates sanitized `lmax-fix-lifecycle-evidence-v1` preview JSON locally and validates the shape in tests. It still does not submit to shadow replay, persist evidence, connect externally, use credentials, schedule work, register a gateway, or mutate trading state.

Phase 4D exposes the fake transport preview through `POST /lmax-readonly-runtime/fake-transport-preview`. Supported scenarios are `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, `WarningOnly`, and `ErrorOnly`. The endpoint requires a reason, rejects unknown scenarios and `SubmitToShadowReplay=true`, and returns mode/count/validation summaries only. It still does not submit to shadow replay, persist evidence, connect externally, use credentials, schedule work, register a gateway, or mutate trading state.

Phase 4E adds only `LmaxReadOnlyExternalSessionSkeleton`. It is not a live connection path. It does not instantiate network clients, perform FIX logon/logout, read credentials, create evidence, submit to shadow replay, schedule work, register a gateway, or mutate trading state.

Phase 4F adds only `ILmaxReadOnlyGuardedTransport` and `LmaxReadOnlyGuardedTransportDisabled`. It defines the future read-only transport boundary but no real network transport. The disabled transport blocks connect/read/disconnect, returns no events, and reports no socket, FIX logon, credential use, order submission, shadow replay submit, scheduler, gateway registration, or trading-state mutation.

Phase 4G adds only `LmaxReadOnlyExternalSessionOptions` and its validator. The sample config lives at `docs/examples/lmax-readonly-external-session-options.sample.json`, remains inactive, and uses non-secret profile labels only. It does not add credential values, host/user/password fields, socket activation, real transport, FIX logon/logout, scheduler activation, order controls, shadow replay submit, or trading-state mutation.

Phase 4H adds only `ILmaxReadOnlyCredentialProfileResolver` and `LmaxReadOnlyCredentialProfileResolverDisabled`. The resolver is safe for status/tests only: it does not read user-secrets, environment variables, appsettings values, vaults, or credential material; it returns no credential values and blocks future credential use while the resolver remains disabled.

Phase 4I adds only `LmaxReadOnlyVenueProfileRegistryDisabled` and related label/descriptor records. `DemoLondon` is a future prototype label but inactive. `Uat`, `Production`, unknown labels, and environment/profile mismatches are blocked. Runtime descriptors contain no host, port, user, account, sender/target comp, endpoint, session, or credential values.

Phase 4J adds only `LmaxReadOnlyExternalSessionRunIntent` and `LmaxReadOnlyExternalSessionRunIntentValidator`. A future manual external read-only request can be represented as intent data with reason, operator id, labels, mode, dry-run flag, capped limits, and safety booleans. The validator blocks `FutureExternalReadOnlyManual` because implementation has not started. It adds no endpoint, opens no socket, reads no credentials, submits no shadow replay, persists nothing, and mutates no trading state.

Phase 4K adds only `POST /lmax-readonly-runtime/external-run-intent/validate`. The endpoint validates the Phase 4J intent envelope and returns structured issues, safety gates, and next operator action. It always returns `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`.

Phase 4L adds only `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`. The endpoint uses the same safe request shape and returns a report with intent validation, options validation, venue profile status, credential resolver status, guarded transport status, skeleton status, safety gates, `expectedOutcome`, `blockedReason`, and `nextOperatorAction`. It always reports no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

Phase 4M adds only `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`. The endpoint validates signoff metadata and required attestations for a dry-run report. It can report signoff metadata as signed/not executable, but it never persists approval and always returns `canAuthorizeExecution=false`, `executionStillBlocked=true`, and no session, external connection, credential read, shadow replay submit, or trading mutation attempt.

Phase 4N adds only `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`. The endpoint validates pre-activation audit metadata built from the intent, dry-run report, signoff, and stable blockers. It never persists execution authorization and always returns `canAuthorizeExecution=false`, `executionStillBlocked=true`, and no session, external connection, credential read, shadow replay submit, or trading mutation attempt.

Phase 4O adds only `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`. The endpoint generates one readiness snapshot from intent validation, dry-run report, signoff, audit, config, venue, credential, transport, and skeleton status. It never persists execution authorization and always returns `canStartSession=false` with no session, external connection, credential read, shadow replay submit, or trading mutation attempt.

Phase 4P adds the final no-socket release gate. Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-no-socket-release-gate.ps1
```

The gate is local-only. It validates evidence fixtures, Phase 4 preflight, runtime source scans, generated evidence status, and API-dependent smokes when the API is available. A `PASS` or `PASS WITH KNOWN WARNINGS` does not mean live connectivity exists; it only means the no-socket boundary is ready for review before any separate future socket prototype prompt.

Phase 5A adds the first transport prototype preflight and kill/rollback plan. Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase5a-preflight.ps1
```

This check is also local-only. It does not connect to LMAX, call lab external scripts, read credentials, open sockets, start a session, or submit to shadow replay. It confirms the Phase 5A planning docs exist, runs the no-socket release gate, checks disabled defaults, scans for forbidden implementation surfaces, verifies FakeLmax-only registration, and checks local API health/smoke behavior when available.

## Run UI

The local operator cockpit lives at:

```text
src/QQ.Production.Intraday.Ui
```

Run it with:

```powershell
.\scripts\run-ui.ps1
```

The UI listens on `http://localhost:5173` and calls the local API at `http://localhost:5050` by default. Override the API URL with:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5050"
```

The cockpit is local-only. It displays backend state and sends explicit local commands only: create fake snapshots, build bars, create/promote fake DB model weight batches, create a local model run, process a model run through FakeLmax, and activate or clear the kill switch. It has no controls for live trading, external connectivity, real LMAX settings, broker credentials, or production secrets.

The UI shell is organized for operator workflows:

- Command Center for health, safety, reference integrity, current activity, and break counts
- Daily Operations for local job runs, daily checklist, retryable job history, and operational timeline
- PMS for positions, targets, drift, wallet/cash/PnL views
- Model Weights for DB-staged batches and promotion
- OMS for model runs, trade intents, risk decisions, orders, and fills
- EMS for execution state, local market data, fills, and future execution-quality views
- Exceptions for acknowledging, assigning, investigating, resolving, waiving, and documenting operational breaks
- Reconciliation and LMAX EOD for intraday breaks, EOD reports, wallet/PnL, and audit
- Risk Control Center for active risk profile, versioned risk limit sets, trading windows, instrument/venue controls, kill switch, and recent risk decisions
- Audit Journal for append-only operator/system action history and correlation IDs
- Connectivity Lab for read-only script guidance only

The top status bar is always visible and must show `FakeLmaxGateway`, `FakeMarketDataProvider`, `liveTradingEnabled=false`, and `externalConnectionsEnabled=false` during normal local operation.

The comfort/readability pass groups the UI into calmer operator sections: the safety bar separates Runtime, Safety, Data, and Reference state; the left navigation is grouped by Operations, Trading, Data, and Control; tables shorten technical IDs and keep full values in row details; the right drawer groups Summary, IDs, Timestamps, Details, and Raw JSON. These changes are UI-only and do not add live trading controls, credential forms, real LMAX controls, or any new backend execution path.

Mutating and longer-running UI actions now provide visible feedback. Buttons show running labels such as `Creating...`, `Promoting...`, `Processing...`, `Reconciling...`, disable while pending, and expose `aria-busy`. A global operation toast shows the latest action, success/failure outcome, elapsed “still working” messaging after a short delay, and expandable API error details. This applies to model weights, model runs, market data, LMAX EOD, exceptions, risk lifecycle actions, and kill switch actions.

If the browser shows CORS errors, confirm the API is running in `Development` and the UI is using `http://localhost:5173` or `http://127.0.0.1:5173`.

## Operator Audit Trail

The local API writes append-only `OperatorAuditEvents` for important operator/system actions: model weight batch creation, validation, promotion, model-run creation and processing, blocked process results, kill-switch activation/clear, blocking reference-data checks, fake LMAX EOD generation/import, and EOD reconciliation runs.

Each API request gets a correlation ID. Pass one explicitly when you want to stitch together a local workflow:

```powershell
$headers = @{
  "X-Correlation-Id" = "local-run-001"
  "X-Operator-Id" = "local-admin"
}
Invoke-RestMethod -Headers $headers -Uri "http://localhost:5050/audit/events?limit=20"
```

Operator headers are local attribution and governance context only and are not production authentication. Audit metadata is sanitized before storage for keys containing `password`, `secret`, `token`, or `apiKey`; do not put credentials into request metadata. There are no audit update/delete endpoints.

Useful audit queries:

```powershell
Invoke-RestMethod "http://localhost:5050/audit/events?limit=100"
Invoke-RestMethod "http://localhost:5050/audit/events/by-entity?entityType=ModelRun&entityId=<id>"
Invoke-RestMethod "http://localhost:5050/audit/events/by-correlation/<correlationId>"
```

## Exception Management

Warning and blocking reconciliation breaks create `ExceptionCases` automatically. Info breaks are retained as breaks but do not create cases by default. Exception cases are the local operator workflow object; the source break stays in the reconciliation/EOD view.

Supported statuses are `Open`, `Acknowledged`, `Investigating`, `Resolved`, `FalsePositive`, `Waived`, and `Closed`. Resolution, false-positive, and waiver actions require a reason. Waiving a blocking or critical case should be treated as an explicit operator decision and is audited.

Useful local API calls:

```powershell
Invoke-RestMethod "http://localhost:5050/exceptions?limit=100"
Invoke-RestMethod "http://localhost:5050/exceptions/<id>/actions"
Invoke-RestMethod "http://localhost:5050/exceptions/<id>/notes"
Invoke-RestMethod -Method Post -ContentType "application/json" -Body '{"reason":"Reviewed"}' "http://localhost:5050/exceptions/<id>/acknowledge"
Invoke-RestMethod -Method Post -ContentType "application/json" -Body '{"reason":"Resolved after report correction"}' "http://localhost:5050/exceptions/<id>/resolve"
```

In the UI, open the Exceptions page to filter cases, select a case, view its action timeline and notes, and perform operator actions. Every action writes both case history and an audit event. Blocking/critical waiver, false-positive, and resolution actions can create a pending approval request instead of executing immediately.

## Local Governance and Approvals

The local governance foundation seeds these development operators:

- `local-viewer`: read-only viewer
- `local-operator`: local model-weight/model-run/EOD operator
- `local-risk`: risk manager
- `local-approver`: approver
- `local-admin`: admin, approver, risk manager, and operator
- `system`: system actor

The UI top bar includes a local operator selector. The selected operator is stored in browser local storage and sent as `X-Operator-Id`. This is for maker/checker testing and audit attribution only; it is not a login mechanism, not password-based authentication, and not connected to an external identity provider.

Four-eyes approval is enabled by default for sensitive local actions. The requester creates a pending `ApprovalRequest`; a different approver/admin approves or rejects it with a reason; an approved request can be executed exactly once. A requester cannot approve their own request.

Approval-gated actions currently include:

- risk limit set activation
- risk limit set retirement
- kill-switch clear
- waiver of blocking/critical exception cases
- false-positive marking of blocking/critical exception cases
- resolution of blocking/critical exception cases

Use the Governance page to view the current operator, permissions, pending approvals, approval history, and approval decision timeline. The approval workflow writes `OperatorAuditEvents` for request creation, approval, rejection, cancellation, execution, permission denial, and approval-required outcomes. It cannot enable live trading, external connections, real LMAX execution, or credential capture.

Run the automated local governance smoke after resetting/seeding LocalDB and starting the API:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1

# In another terminal:
.\scripts\smoke-governance-local.ps1
```

The smoke uses explicit `X-Operator-Id` headers and validates the same maker/checker path used manually: `local-risk` requests risk activation, self-approval is blocked, `local-approver` approves and executes once, kill-switch clear stays active until checker execution, and approval/audit records are visible. It defaults to `http://localhost:5050`, refuses non-local API URLs, prints API error bodies, and does not use credentials, external URLs, LMAX, live trading, or external connections.

## Daily Operations and Job Control

Daily Operations is a local control layer around existing safe workflows. It records persistent `OperationalJobRuns`, step rows, and job events for operator-triggered tasks. It does not rewrite business logic and does not introduce a scheduler, cloud dependency, real LMAX calls, live trading, credentials, or external connections.

Supported local job types include:

- `ReferenceDataIntegrityCheck`
- `BuildMarketDataBars`
- `PromoteReadyWeightBatches`
- `ProcessPendingModelRuns`
- `GenerateFakeLmaxEodReports`
- `ImportGeneratedLmaxEodReports`
- `RunEodReconciliation`
- `CalculateEodPnlSummary`

Every manual job run requires a reason and writes operator audit events such as `OperationalJobStarted`, `OperationalJobSucceeded`, `OperationalJobFailed`, and `OperationalJobRetried`. Job input/output metadata is sanitized before persistence.

Status semantics are deliberately operational:

- `Succeeded`: the wrapper completed and produced the expected local result.
- `Skipped`: the job intentionally did no work, such as missing optional generated EOD paths or no PnL summary for that date.
- `PartiallySucceeded`: the wrapper completed but produced business warnings or cleanly handled blocks.
- `Failed`: infrastructure/programming failure or a critical operational failure.
- `TimedOut`: reserved for future scheduler/timeout enforcement; the current local runner does not emit it yet.

Business blocks are not automatically infrastructure failures. For example, `ProcessPendingModelRuns` can return `PartiallySucceeded` when risk/reconciliation blocks are handled cleanly. `RunEodReconciliation` can succeed while reporting blocking break counts because the reconciliation service and exception workflow own those breaks. `ReferenceDataIntegrityCheck` with blocking issues is treated as `Failed` because startup/reference integrity is a critical control and creates a linked exception case.

Retry creates a new `OperationalJobRun` linked by `RetryOfJobRunId`; it does not mutate the original run. Retry requires a reason, increments retry count, respects the job definition rerunnable flag, and creates an audit event.

Useful API calls:

```powershell
Invoke-RestMethod "http://localhost:5050/ops/jobs/definitions"
Invoke-RestMethod "http://localhost:5050/ops/jobs/runs?limit=50"
Invoke-RestMethod -Method Post -ContentType "application/json" -Body '{"jobType":"ReferenceDataIntegrityCheck","reason":"Manual reference check","input":{}}' "http://localhost:5050/ops/jobs/run"
Invoke-RestMethod "http://localhost:5050/ops/daily-summary"
Invoke-RestMethod "http://localhost:5050/ops/daily-checklist"
```

Run one job from PowerShell:

```powershell
.\scripts\run-ops-job.ps1 -JobType ReferenceDataIntegrityCheck -Reason "Manual operator check"
```

Run the local Daily Operations smoke after starting the API:

```powershell
.\scripts\smoke-daily-ops-local.ps1
```

The smoke validates health, FakeLmax-only safety, daily summary, reference check job, bar build job, ready-weight promotion job, pending model-run processing job, retry linkage, job history, and audit events. It skips EOD reconciliation clearly when no local LMAX EOD import run exists.

## Operational Runbooks and Local Scheduler Foundation

Operational runbooks organize the local Daily Operations jobs into institutional workflows. They are orchestration only: they call existing local jobs, preserve audit/exception/governance controls, and do not introduce live broker connectivity, real LMAX execution, live market data integration, credentials, or external scheduler dependencies.

Default runbooks are seeded idempotently:

- `StartOfDay`: reference-data integrity, latest bar build, active risk profile check, open exception check, and a manual operator confirmation gate.
- `IntradayCycle`: promote ready weight batches, process pending model runs through `FakeLmaxGateway`, build latest bars, and check exceptions.
- `EndOfDay`: generate fake LMAX EOD reports, import generated reports, run EOD reconciliation, calculate USD PnL summary, check EOD exceptions, and complete a manual operator confirmation gate.

Manual gates pause the runbook with `WaitingForOperator`. The operator must complete the step with a reason before the runner continues. Failed required steps stop the runbook; continue-on-warning steps can leave the runbook `PartiallySucceeded` instead of turning a handled business warning into an infrastructure failure.

Useful API calls:

```powershell
Invoke-RestMethod "http://localhost:5050/ops/runbooks/definitions"
Invoke-RestMethod "http://localhost:5050/ops/runbooks/runs?limit=50"
Invoke-RestMethod -Method Post -ContentType "application/json" -Body '{"runbookType":"StartOfDay","reason":"Manual SOD run","input":{}}' "http://localhost:5050/ops/runbooks/run"
Invoke-RestMethod "http://localhost:5050/ops/schedules"
```

Run a runbook from PowerShell:

```powershell
.\scripts\run-runbook.ps1 -RunbookType StartOfDay -Reason "Manual start-of-day checks"
```

Run the local runbook smoke after starting the API:

```powershell
.\scripts\smoke-runbooks-local.ps1
```

The smoke validates health, default runbook definitions, Start-of-Day manual confirmation, linked job runs, Intraday and End-of-Day runbook execution paths, audit events, and scheduler disabled state. It uses local API calls only and does not call LMAX, Connectivity Lab commands, credentials, live trading, or external services.

The local scheduler foundation is present but disabled by default:

```json
{
  "LocalScheduler": {
    "Enabled": false,
    "PollIntervalSeconds": 30
  }
}
```

When disabled, the worker does not trigger scheduled runbooks. When explicitly enabled for local testing, it only evaluates enabled local `OperationalScheduleDefinitions` and triggers runbooks through the same audited runbook runner. It does not submit orders directly, does not call external services, does not invoke real LMAX or Connectivity Lab network commands, and does not bypass startup safety validation. Current limitations: no production scheduler, no Windows Service install, no cloud scheduler, no real LMAX account/position runbook steps, and fake/generated EOD jobs only.

## Risk Control Center

Open the Risk Control Center from the Control navigation group. It is local-only and cannot enable live trading, external connections, credentials, or a real LMAX gateway.

The page shows:

- active risk profile name/version/status and key staleness/exposure thresholds
- risk limit set lifecycle rows: `Draft`, `Active`, `Retired`, `Archived`
- clone, activate, and retire actions with required operator reasons
- global risk limits, instrument risk limits, venue risk limits, and trading windows
- instrument and venue control flags for trading, report import, and market data
- kill switch state and recent risk decisions with observed-vs-limit detail

Active risk profiles are read-only in the cockpit. To change safety-critical settings, clone the active set, make controlled draft changes through the API or UI support that exists for draft rows, then activate the draft with a reason. Activation retires the previous active set for the same fund/model and writes audit events. Retiring a set also requires a reason.

Risk decisions reference the risk limit set used and include check-level detail rows. Normal risk outcomes such as stale data, trading-window closure, no-new-orders cutoff, kill switch active, or limit exceeded remain operational `Blocked`/`Rejected` results, not HTTP 500s.

Interpret risk observed/limit columns as the key check selected by the backend. Rejected/blocked decisions summarize the first failing check. Approved decisions summarize a numeric utilization check when possible and still include passed detail rows such as model staleness, market-data staleness, max trade notional, exposure limits, and trading-window status. Select a risk decision in the UI to inspect the full checks table. Older historical rows may have no details and will show a clear fallback instead of a misleading `None` message.

Execution permission and report-import permission are separate. A known LMAX report alias may import historical EOD rows even if the instrument is disabled for trading. Trading-disabled instruments still block new orders. Current limitations: local operator identity is not production authentication, no external identity provider is integrated, and risk configuration changes affect only local/FakeLmax processing.

## Reference Data Integrity

Duplicate active reference data can make trading decisions ambiguous, especially venue mappings, risk limits, trading windows, instruments, venues, funds, and broker accounts. The API and Worker run a reference data integrity check on startup by default:

- `ReferenceDataIntegrity:CheckOnStartup = true`
- `ReferenceDataIntegrity:FailStartupOnBlockingIssues = true`

Check a running API:

```powershell
.\scripts\check-reference-data.ps1 -BaseUrl http://localhost:5050
```

The script calls `GET /admin/reference-data/integrity`, prints blocking and warning issues, and exits non-zero when blocking issues exist. A blocking reference-data issue also causes model-run processing to return `Blocked` with `ReferenceDataAmbiguous` or `ReferenceDataInvalid`; it does not create trade intents, orders, fills, or position ledger updates when detected before intent creation.

Recommended clean local sequence:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\smoke-local.ps1
```

Recommended UI sequence:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\run-ui.ps1
```

Then in the UI: create fake EURUSD snapshots, build 15-minute bars, create a local `IntradayFxModel` model run, process it, and inspect positions, drift, risk decisions, orders, fills, and reconciliation breaks.

DB-weight UI sequence:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\run-ui.ps1
```

Then in the UI: create a fake model weight batch, validate/promote it, process the generated model run from the Model Runs panel, and inspect downstream execution state. Promotion does not process the model run and does not send orders.

## DB Model Weight Source

Prompt #6 stages model-generated weights in LocalDB before they become canonical `ModelRun` and `TargetWeight` rows. This is the future Qubes/GA integration point. The real Qubes/GA database writer is not implemented yet, and file manifest/CSV ingestion is intentionally not implemented.

Tables:

- `ModelWeightBatches`
- `ModelWeightRows`
- `ModelWeightValidationIssues`

Local scripts:

```powershell
.\scripts\create-fake-weight-batch.ps1
.\scripts\promote-ready-weight-batches.ps1
.\scripts\smoke-db-weights-local.ps1
```

Idempotency rules:

- `SourceSystem + ExternalBatchId` is unique.
- Same explicit external batch id plus same content returns the existing batch.
- Same explicit external batch id plus different content is rejected.
- Already promoted batches return the existing model run id and do not create duplicate model runs or target weights.

Validation checks metadata, row counts, duplicate symbols/raw ids, enabled instrument resolution, and reference data integrity. Validation failures mark the batch rejected and do not create model runs.

## Run Worker

```powershell
.\scripts\run-worker.ps1
```

Worker bar building is configurable under `MarketDataBars`.

Worker model-weight promotion is configurable under `ModelWeights`. `PromoteReadyBatches` defaults to `false`, so the Worker does not promote staged weights unless explicitly configured. Promotion still does not process model runs.

## Smoke Test

Start the API first, then run:

```powershell
.\scripts\smoke-local.ps1 -BaseUrl http://localhost:5050
```

If PowerShell execution policy blocks local scripts, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-local.ps1 -BaseUrl http://localhost:5050
```

The smoke test calls local API endpoints only. It uses dynamic UTC timestamps, creates fake EURUSD snapshots for the previous completed 15-minute bar, builds bars, creates fresh fake snapshots for execution, creates a current local model run, processes it through FakeLmax, and queries orders, fills, positions, and reconciliation breaks. Request failures print the endpoint, safe request body, HTTP status, and response body.

`GET /orders` returns plain string IDs for parent orders, child orders, trade intents, venues, instruments, client order IDs, and broker order IDs. It does not expose nested strongly typed ID value-object shapes.

For the DB-weight path:

```powershell
.\scripts\smoke-db-weights-local.ps1 -BaseUrl http://localhost:5050
```

This smoke creates fresh fake market data, creates a fake model weight batch, validates/promotes it to a model run, then explicitly processes that model run through the existing FakeLmax workflow.

For the LMAX EOD local path:

```powershell
.\scripts\smoke-lmax-eod-local.ps1 -BaseUrl http://localhost:5050
```

This smoke creates/promotes a fake DB weight batch, creates fresh fake market data, processes the generated model run through FakeLmax, generates actual-schema fake LMAX EOD reports, imports `individual-trades.csv`, `trades.csv`, and `currency-wallets.csv`, runs EOD reconciliation, loads USD wallet/PnL summary, then imports a mutated report to confirm a blocking EOD break appears.

## LMAX EOD Reports

The local EOD importer uses the actual report headers received by the fund:

- `individual-trades.csv` is the execution source of truth.
- `trades.csv` is a summary/rollup control report.
- `currency-wallets.csv` is a wallet/cash/PnL report, not an instrument position report.

Import paths are constrained to `data/lmax-eod`. Raw files in `incoming`, `processing`, `archive`, `rejected`, and `generated` are ignored by git; do not commit real broker reports. Commit only `.gitkeep` files or synthetic fixtures under `data/lmax-eod/samples`.

Parser test fixtures use tiny anonymized files under `tests/fixtures/lmax-eod`. To test real local files, copy them into an ignored `data/lmax-eod` folder and pass the paths explicitly to the import scripts.

Timestamp parsing defaults to UTC via `LmaxEodReports:TimestampTimeZone = "UTC"`. Do not rely on the workstation timezone.

LMAX slash symbols resolve through `InstrumentAlias` rows with source `LMAX_REPORT`. The LocalDB reference seed includes `AUD/USD`, `EUR/USD`, `GBP/USD`, `NZD/USD`, `USD/CAD`, `USD/CHF`, and `USD/JPY`. Disabled-for-trading instruments are still valid for EOD report import when the alias and instrument are known; disabled trading blocks order generation, not historical report import.

`currency-wallets.csv` USD conversion uses `value * Rate to Base CCY`. `TotalNetPnlUsd` is defined as:

```text
TotalProfitLossUsd + TotalCommissionUsd + TotalDividendsUsd + TotalFinancingUsd
```

EOD reconciliation only claims execution-derived position delta checks. It does not claim a final official LMAX open-position check until a real LMAX positions report/API is added.

Recommended full local EOD workflow:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\smoke-db-weights-local.ps1
.\scripts\smoke-lmax-eod-local.ps1
.\scripts\smoke-governance-local.ps1
.\scripts\run-ui.ps1
```

## Process Results

`POST /model-runs/{modelRunId}/process` returns a process result. Risk, reconciliation, stale-data, kill-switch, trading-window, missing-data, no-drift, and already-processed outcomes are expected operational states and return HTTP 200 with statuses such as `Blocked`, `AlreadyProcessed`, or `NoActionRequired`.

HTTP 500 is reserved for real infrastructure or programming failures. In Development, inspect the API console/log output for the exception and stack trace.

## Safety Confirmation

Check `/health`. It reports:

- persistence provider
- database reachability
- pending migration count
- execution gateway
- market data provider
- live trading enabled
- external connections enabled

Expected local values:

- execution gateway: `FakeLmaxGateway`
- market data provider: `FakeMarketDataProvider`
- live trading enabled: `false`
- external connections enabled: `false`

`LmaxVenueGateway` remains an unregistered placeholder. No live broker, live LMAX, or live market data connectivity is present.

## LMAX Connectivity Lab

The isolated LMAX Connectivity Lab is a command-line project under `tools/QQ.Production.Intraday.Lmax.ConnectivityLab`. It is not referenced by the API or Worker and does not participate in the production execution workflow.

Dry-run checks:

```powershell
dotnet restore .\QQ.Production.Intraday.sln --configfile .\NuGet.Config
dotnet build .\QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
.\scripts\lmax-lab-print-config.ps1
.\scripts\lmax-lab-public-data-smoke.ps1
.\scripts\lmax-lab-account-config-check.ps1
.\scripts\lmax-lab-account-smoke.ps1
.\scripts\lmax-lab-fix-dry-run.ps1
.\scripts\lmax-lab-fix-order-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1
.\scripts\lmax-lab-fix-capabilities.ps1
.\scripts\lmax-lab-fix-order-status-dry-run.ps1
.\scripts\lmax-lab-order-dry-run.ps1
```

The lab defaults to `AllowExternalConnections=false`, `AllowOrderSubmission=false`, `AllowLiveTrading=false`, and `DryRun=true`. Scripts do not contain secrets and do not make external calls by default. Configure demo/UAT credentials through environment variables or user-secrets only; do not put credentials in appsettings files or commit them.

After credentials are configured, manual Demo FIX logon checks are explicit:

```powershell
.\scripts\lmax-lab-fix-order-logon-smoke.ps1 -AllowExternalConnections
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1 -AllowExternalConnections
```

These commands send only FIX Logon and Logout. They do not submit orders, do not subscribe to market data, and are not connected to the main execution workflow.

Read-only market data snapshot smoke is also explicit:

```powershell
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1 -AllowExternalConnections -Instrument EURUSD -LmaxInstrumentId 4001 -SlashSymbol "EUR/USD"
```

It sends a FIX `MarketDataRequest`, prints bid/ask/mid or reject details, does not submit orders, and does not persist live LMAX data into LocalDB.

LMAX Demo FIX market data snapshot retrieval has been validated in the isolated lab for `EURUSD` using `SecurityId` mode with LMAX instrument id `4001`. The main API/Worker runtime remains FakeLmax-only and does not consume or persist Demo market data.

The LMAX integration strategy is now FIX-only plus EOD files: FIX Market Data, FIX Trading read-only recovery, and LMAX EOD reports. Account REST API discovery is parked as diagnostic only; BasicAuth against `https://account-api.london-demo.lmax.com` returned `401` for likely endpoints and it is not required for platform operation.

Read-only FIX Trading recovery commands:

```powershell
.\scripts\lmax-lab-fix-capabilities.ps1
.\scripts\lmax-lab-fix-trade-capture-smoke.ps1 -AllowExternalConnections -LookbackMinutes 1440 -MaxReports 20 -ShowFixMessages
.\scripts\lmax-lab-fix-order-status-dry-run.ps1 -ClOrdId "known-demo-client-order-id"
.\scripts\lmax-lab-fix-trade-capture-replay.ps1
.\scripts\lmax-lab-fix-execution-report-replay.ps1
.\scripts\lmax-lab-fix-demo-order-dry-run.ps1
```

`fix-capabilities` scans the LMAX trading dictionary when present. Current package findings support `OrderStatusRequest` (`35=H`), `ExecutionReport` (`35=8`), `TradeCaptureReportRequest` (`35=AD`), `TradeCaptureReportRequestAck` (`35=AQ`), and `TradeCaptureReport` (`35=AE`). `OrderMassStatusRequest` (`35=AF`), `RequestForPositions` (`35=AN`), and `PositionReport` (`35=AP`) are treated as unsupported unless a future LMAX dictionary provides them. Trade capture uses a short `568 TradeRequestID` format like `TC26050516395101` to satisfy LMAX Demo's 16-character limit, treats accepted `AQ` with `748=0` as a successful zero-report response, and reports session-level `35=3` rejects explicitly. Synthetic replay normalizes lab fixture `35=AE` messages into an EOD-like comparison shape and lab fixture `35=8` execution reports into conceptual internal order events. The demo order dry-run builds a sanitized tiny `35=D` only and opens no socket.

Live demo order lifecycle is intentionally gated and should not be run as part of normal local validation. It requires `-AllowExternalConnections`, `-AllowOrderSubmission`, `-ConfirmDemoOrder`, and `-DryRun:$false`, plus the lab safety gates. The default validated `35=D` shape omits `21 HandlInst` because LMAX Demo rejected that tag at session level. It remains isolated from API/Worker, does not enable live trading, and does not persist live data.

The lifecycle evidence command is also lab-only and dry-run by default:

```powershell
.\scripts\lmax-lab-fix-demo-lifecycle-evidence.ps1
```

When intentionally run in live Demo mode with all explicit gates, it opens one FIX Trading session, submits the tiny gated demo order, collects `35=8` execution reports, keeps the session logged on, recovers status with read-only `35=H`, recovers fills with read-only `35=AD`/`35=AE`, and logs out once at the end. The trade-capture window is computed after the fill timestamp is known and diagnostics print `FillTransactTimeUtc`, `TradeCaptureStartUtc`, and `TradeCaptureEndUtc`. Diagnostics also show same-session recovery and FIX sequence-number progression. `ExecType=I` from order-status recovery is status-only and is not counted as a fill; fills are identified by `ExecType=F` and matching TradeCapture `ExecID`. If the order fills but recovery evidence is incomplete, the command reports partial success rather than claiming the order failed. The command persists nothing and is not wired into API/Worker.

The lifecycle evidence command can export a sanitized local JSON evidence file:

```powershell
.\scripts\lmax-lab-fix-demo-lifecycle-evidence.ps1 -OutputJsonPath .\artifacts\lmax\evidence.json
```

The export uses schema `lmax-fix-lifecycle-evidence-v1`, contains normalized execution reports, order-status reports, trade-capture reports, consistency checks, and warnings, and deliberately omits credentials, authorization headers, and raw logon messages. Dry-run exports are marked with `dryRun=true`.

After a Demo order exists, `fix-order-status-smoke` can recover its status by known `ClOrdID` without submitting anything:

```powershell
.\scripts\lmax-lab-fix-order-status-smoke.ps1 -AllowExternalConnections -ClOrdID "DL26050607454402" -LmaxInstrumentId 4001 -Side Buy -ShowFixMessages
```

This command requires `AllowOrderSubmission=false`, parses `35=8` execution reports through the lab normalizer, logs out cleanly, and persists nothing.

Parked Account API diagnostics, if ever resumed, remain explicit and safe:

```powershell
.\scripts\lmax-lab-account-discover.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-positions-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-balances-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
```

These commands use GET-only endpoint discovery, print sanitized status/excerpts, do not submit orders, and do not persist live account data into LocalDB.

See [LMAX_CONNECTIVITY_LAB.md](LMAX_CONNECTIVITY_LAB.md) for command details, safety gates, and questions to resolve with LMAX before any real demo/UAT connectivity work.

## Troubleshooting LocalDB

- If `sqllocaldb` is not found, install SQL Server Express LocalDB.
- If database connection fails, run `sqllocaldb start MSSQLLocalDB`.
- If schema is stale, run `.\scripts\update-local-db.ps1`.
- For a clean development reset, run `.\scripts\reset-local-db.ps1`.
- If startup fails with reference-data integrity errors after upgrading an old local database, run `.\scripts\reset-local-db.ps1 -SeedDemoData`.
- If old stale demo model runs appear after upgrading from earlier seeds, run `.\scripts\reset-local-db.ps1 -SeedDemoData`.
- If the model weight source schema is missing, apply migrations with `.\scripts\update-local-db.ps1` or reset the local database.

## NuGet Advisory

The vulnerability audit currently reports transitive `System.Security.Cryptography.Xml 9.0.0` advisories through SQL Server/EF infrastructure dependencies. Directly pinning available .NET 10 package versions did not clear the advisory in the current package graph, so it remains documented rather than hidden with an unstable override.

## LMAX Shadow Replay

Use the LMAX Shadow page to inspect replay runs and observations created from normalized LMAX lab evidence. The page is local and replay-only: there are no credential forms, no live LMAX controls, and no order submission controls. Operators can acknowledge, resolve, or ignore observations with a reason; each action is audited.

To replay a local evidence file while the API is running:

```powershell
.\scripts\replay-lmax-lab-evidence.ps1 -Path .\path\to\evidence.json
```

The script converts exported lifecycle evidence into `POST /lmax-shadow/replay` with `inputSource=LabEvidenceFile`. It only posts to localhost and never opens FIX connections.

To validate the local replay path with a sanitized synthetic fixture:

```powershell
.\scripts\smoke-lmax-shadow-local.ps1
```

The smoke validates FakeLmax-only health, replays `tests/fixtures/lmax-shadow/lmax-fix-lifecycle-evidence-v1.json`, checks replay and observation endpoints, verifies shadow audit events, and makes no external calls.

The lab can also capture a small read-only LMAX Demo evidence file for later local replay:

```powershell
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1 -AllowExternalConnections -TradeCaptureLookbackMinutes 60 -MaxReports 20
.\scripts\replay-lmax-lab-evidence-file.ps1 -EvidenceFile .\artifacts\lmax-lab\evidence\lmax-readonly-evidence-YYYYMMDD-HHMMSS.json
```

The first command skips without network access. The capture command is Connectivity Lab only, requires explicit `-AllowExternalConnections`, uses read-only market-data/trade-capture/order-status requests, requires `AllowOrderSubmission=false`, never sends `NewOrderSingle`, and writes sanitized JSON under `artifacts/lmax-lab/evidence/`. The replay command posts the file to the local shadow API only and makes no live FIX call.

Validate evidence before replaying:

```powershell
.\scripts\validate-lmax-lab-evidence-file.ps1 -EvidenceFile .\tests\fixtures\lmax-shadow\lmax-fix-lifecycle-evidence-v1.json
```

Validation enforces schema `lmax-fix-lifecycle-evidence-v1`, `orderStatuses` arrays, normalized `yyyy-MM-dd` TradeCapture dates, explicit `tradeUti: null` when absent, redaction markers, and no credential-like content. The replay helper prints validation issues and refuses invalid evidence unless explicitly overridden for diagnostics.

Supported evidence modes are `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, and `SyntheticLifecycle`. Empty and market-data-only files are valid and replay with zero observations. OrderStatus `ExecType=I` is status-only and never fill evidence. TradeCapture AE is recovery evidence for shadow comparison; EOD files remain the official daily reconciliation source.

Observation policy is explicit. Warning observations are operator review items and do not create exception cases by default. Blocking observations create/link exception cases and include policy code, evidence mode, replay id, observation id, and fingerprint in metadata. TradeCapture-only missing internal fills are warnings in lab/read-only mode; protocol rejects for order-path messages such as `35=D` are blocking; protocol rejects for read-only recovery requests are warnings.

To validate and replay the coverage fixtures while the local API is running:

```powershell
.\scripts\smoke-lmax-evidence-coverage-local.ps1
```

The smoke validates all supported evidence-mode fixtures, replays them through localhost shadow replay, checks mutation counts where endpoints are available, and makes no LMAX/FIX/network call beyond the local API.

## LMAX Shadow Reader Skeleton

The live shadow reader skeleton is intentionally inert. It is a future-readiness shell for a later read-only LMAX evidence reader, not a live integration.

Current guarantees:

- `LmaxShadowReader:Enabled=false`
- `LmaxShadowReader:AllowExternalConnections=false`
- `LmaxShadowReader:AllowCredentialUse=false`
- `LmaxShadowReader:ReadOnly=true`
- `LmaxShadowReader:AllowOrderSubmission=false`
- `LmaxShadowReader:PersistRawFixMessages=false`
- `LmaxShadowReader:PersistToTradingTables=false`
- `LmaxShadowReader:DryRun=true`

Useful local checks:

```powershell
Invoke-RestMethod "http://localhost:5050/lmax-shadow-reader/status"
Invoke-RestMethod -Method Post -ContentType "application/json" -Body '{"reason":"Verify disabled reader","dryRun":true}' "http://localhost:5050/lmax-shadow-reader/run"
.\scripts\smoke-lmax-shadow-reader-local.ps1
```

The smoke validates `GET /health`, confirms the runtime remains `FakeLmaxGateway` with live trading and external connections disabled, checks that the reader reports `Disabled`, verifies the run endpoint is blocked by default, and confirms available mutation counts are unchanged. It posts only to localhost and makes no FIX/network call.

Shadow Reader Quality Gate #1 proves the disabled skeleton remains blocked under unsafe and contradictory configuration. Safety gate DTOs include gate name, status, observed value, expected safe value, and message. Dangerous settings such as `AllowOrderSubmission=true`, `PersistToTradingTables=true`, `ReadOnly=false`, `DryRun=false`, invalid event limits, or raw FIX persistence produce failed gates and do not execute. Blocked run attempts are audited with failed gate names and sanitized metadata.

Activating a real live shadow reader remains future work and would require explicit configuration, governance approval, runbook controls, operational rehearsal, and a separate safety gate. There is no scheduler auto-run and no UI control to enable LMAX.

Shadow observations include stable fingerprints. Duplicate observations are collapsed inside one replay, but replaying the same file again creates a new replay run with matching fingerprints so history is retained. The smoke also checks that available order/fill/position counts are unchanged by replay.

Blocking shadow observations create exception cases once per replay/fingerprint. Warning observations do not create exception cases by default. Observation acknowledge, resolve, and ignore actions require a reason, remain queryable after transition, and create operator audit events.

## LMAX Read-Only Runtime Phase 5B/5D Prototype Boundary

Phase 5D/5F has a manual Demo snapshot prototype script. It is Demo-only, read-only market-data-only, and supports only EURUSD / SecurityID `4001`. The script refuses without explicit `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty `-Reason`. If the required credential labels are missing, it returns `BlockedMissingCredentials` with `externalConnectionAttempted=false`.

```powershell
.\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "manual demo read-only prototype check"
```

With complete local credential labels, Phase 5D may attempt FIX market-data logon, one EURUSD snapshot request, and logout. Output remains sanitized and always reports `credentialValuesReturned=false`, `orderSubmissionAttempted=false`, `shadowReplaySubmitAttempted=false`, `tradingMutationAttempted=false`, and `schedulerStarted=false`.

Phase 5F adds sanitized result capture. Every blocked, failed-safe, or completed manual run writes sanitized JSON below `artifacts/lmax-readonly-runtime-demo-snapshot/`, which is ignored by git through the `artifacts/` rule. The artifact includes `noSensitiveContent=true`, `redactionStatus=Redacted`, retry metadata with `retryEnabled=false` and `retryAllowed=false`, and only safe market-data fields if a snapshot is received.

Phase 5G adds timeout diagnostics for the case where logon succeeds but no snapshot arrives. Use `-RequestMode SecurityIdOnly`, `-RequestMode SlashSymbolOnly`, `-RequestMode SymbolOnly`, or `-RequestMode AutoSequence` only for explicit operator-approved read-only diagnostics. The output includes message counters for Logon, MarketDataRequest, MarketDataSnapshot, MarketDataRequestReject, BusinessMessageReject, Reject, Logout, Heartbeat, and TestRequest, plus sanitized classification such as `FailedSafeSnapshotTimeout`, `FailedSafeMarketDataRequestRejected`, `FailedSafeBusinessReject`, `FailedSafeSessionReject`, `FailedSafeUnexpectedLogout`, `CompletedWithEmptyBook`, or `Completed`.

Phase 5H changes the diagnostic knobs to separate request style from symbol encoding. Use `-RequestMode SnapshotPlusUpdates` by default; it sends `263=1` and requires an unsubscribe/logout after snapshot or timeout. Use `-SymbolEncodingMode SecurityIdOnly` by default; it sends `48=4001`, `22=8`, and omits `55`. `SnapshotOnly`, symbol encodings that include `55`, and `InternalSymbol` are known-rejected by LMAX Demo and block locally when `-SkipKnownRejectedProfiles` is true. Only use `-AllowKnownRejectedDiagnostics` when intentionally reproducing a known reject.

Phase 5J adds sanitized logon/session diagnostics because the current blocker is MarketData FIX logon confirmation, not the market-data request body. Add `-ShowSanitizedLogonDiagnostics` to print profile labels, credential/comp-id presence and lengths only, FIX session settings, first inbound message type, sanitized Logout/Reject text, TCP/TLS flags, and a runtime-vs-Connectivity-Lab profile comparison. The output never includes username, password, sender/target comp values, raw Logon FIX, tag `553`, or tag `554`.

Example operator-approved diagnostic command:

```powershell
.\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "Phase 5J operator-approved Demo MarketData logon diagnostic" -RequestMode SnapshotPlusUpdates -SymbolEncodingMode SecurityIdOnly -MarketDepth 1 -MaxWaitSeconds 30 -ShowSanitizedLogonDiagnostics
```

Phase 5L closes the first successful Demo read-only snapshot artifact. Validate a sanitized artifact with:

```powershell
.\scripts\validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

Do not submit this artifact to shadow replay. It is operational diagnostics only. Do not enable scheduler, register a gateway, add order commands, persist live FIX data, or mutate trading state.

Phase 5M maps the validated artifact to a sanitized `MarketDataOnly` evidence preview. This is still preview-only: it validates the Phase 5L artifact, writes ignored preview JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/`, validates the `lmax-fix-lifecycle-evidence-v1` contract, and does not submit to shadow replay or create observations.

```powershell
.\scripts\preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

Phase 5N manually replays that preview through the existing local shadow replay API only. Start the local API first if you want to execute the dry-run, then run:

```powershell
.\scripts\replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile .\artifacts\lmax-readonly-runtime-demo-snapshot\evidence-preview\<preview-file>.json
.\scripts\check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1 -EvidencePreviewFile .\artifacts\lmax-readonly-runtime-demo-snapshot\evidence-preview\<preview-file>.json -Replay
```

Expected result is `Completed`, `observationCount=0`, `blockingObservationCount=0`, `warningObservationCount=0`, and unchanged order/fill/position counts. This remains manual/offline; runtime code still does not submit to shadow replay.

Phase 5O adds a capped repeated manual snapshot stability check. It is still Demo-only and EURUSD / SecurityID `4001` only. It is not scheduler, polling, or automatic retry; every attempt is a planned operator-approved attempt through the existing manual prototype path.

```powershell
.\scripts\check-lmax-readonly-runtime-phase5o-stability-gate.ps1
.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -ConfirmRepeatedManualSnapshots `
  -AttemptCount 3 `
  -DelaySeconds 2 `
  -Reason "Phase 5O operator-approved repeated Demo EURUSD read-only snapshot stability check"
```

The stability script validates each successful snapshot artifact, maps each to a `MarketDataOnly` preview, and writes a sanitized summary under `artifacts/lmax-readonly-runtime-demo-snapshot/stability/`. It does not replay previews unless the operator explicitly passes `-ReplayEvidencePreviews`; runtime code still does not submit to shadow replay.

Phase 5P reviews and closes the stability results. It does not run another snapshot and does not call the runtime prototype:

```powershell
.\scripts\review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
.\scripts\check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

Expected Phase 5P decision for the 3/3 run is `PASS`. This means only that a separate Phase 5Q manual MarketData evidence workflow hardening prompt can be considered. It does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, or production use.

Phase 5Q runs the controlled manual MarketData evidence workflow review. It does not run another snapshot and does not require credentials. By default it does not replay previews or require the API; it validates sanitized snapshot artifacts, validates or regenerates `MarketDataOnly` previews, and writes a sanitized workflow manifest under `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/`:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
.\scripts\check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

Expected default workflow decision is `PASS_WITH_WARNINGS` when replay is intentionally omitted. Phase 5R closes that warning only when the operator explicitly requests local manual replay and the local API is available:

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 `
  -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json `
  -ReplayEvidencePreviews `
  -ConfirmLocalManualReplay
.\scripts\check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1 -WorkflowManifestFile .\artifacts\lmax-readonly-runtime-demo-snapshot\workflow\<workflow-manifest>.json
```

The replay path uses only the existing local shadow replay script/API. Expected replay result for each `MarketDataOnly` preview is `Completed`, zero observations, and unchanged mutation guard. Runtime remains separate and still does not submit to shadow replay.

Phase 5S runs the controlled manual workflow release gate. It validates the Phase 5O stability summary, referenced successful artifacts, referenced `MarketDataOnly` previews, and optional replay results. The release manifest is written to a fixed ignored path:

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

Omit `-ReplayEvidencePreviews -ConfirmLocalManualReplay` when local API replay is not intended; the release gate should then report `PASS_WITH_WARNINGS`. If the process is stopped or fails, clear Phase 5S shell variables, verify `/health` reports `FakeLmaxGateway`, and rerun the Phase 5O gate if the stability summary is in doubt.

Phase 5T freezes the controlled manual LMAX MarketData workflow as the current runbook process. It adds no runtime capability and runs no external socket or replay operation by default. The review document is `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md`.

Frozen workflow checklist:

1. Confirm Demo-only EURUSD / SecurityID `4001`, manual-only, no scheduler, no polling.
2. Check credential labels locally only if a new manual snapshot run is intended: `.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck`.
3. Run a new stability check only with explicit operator approval: `.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 2 -Reason "Operator-approved Demo EURUSD read-only snapshot stability check"`.
4. Validate artifacts and run release review: `.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 5 -Reason "Phase 5S manual workflow release test"`.
5. Optional local replay requires both replay flags and localhost API: add `-ReplayEvidencePreviews -ConfirmLocalManualReplay`.
6. Run `.\scripts\check-lmax-readonly-runtime-phase5s-release-gate.ps1` and `.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1`.

Interpretation: `PASS` means the optional replay path also completed with zero observations and unchanged mutation guards. `PASS_WITH_WARNINGS` is acceptable when the only warning is optional replay skipped. `FAIL` is a stop condition. Rollback is to stop the local process, clear Phase 5S/5T shell variables, verify `/health` reports `FakeLmaxGateway`, and rerun the Phase 5O/5S gates if artifacts are in doubt.

Phase 5V builds the final local audit pack after optional local replay has produced a replay-enabled workflow manifest:

```powershell
.\scripts\build-lmax-readonly-marketdata-workflow-audit-pack.ps1 `
  -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json `
  -WorkflowManifestFile .\artifacts\lmax-readonly-runtime-demo-snapshot\workflow\lmax-readonly-marketdata-workflow-20260508-162327.json
.\scripts\check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\<audit-pack>.json
```

The audit pack is JSON plus a sanitized Markdown summary under `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/`. A Phase 5V `PASS` validates the controlled manual Demo MarketData workflow as an auditable artifact set. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, or trading mutation.

Phase 5W signs off that audit pack as the frozen controlled manual Demo MarketData workflow. The signoff scripts are local-only: they do not connect to LMAX, do not read credentials, do not run snapshots, do not perform replay, and do not mutate trading state.

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 `
  -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json `
  -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md `
  -SignoffBy "local-operator" `
  -Role "Operator" `
  -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

Expected Phase 5W decision is `PASS`: three sanitized artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, runtime shadow replay submit false, external connection attempted false for the audit/signoff workflow, and credential values returned false. `PASS` recognizes the validated manual Demo read-only workflow only. It does not authorize scheduler, polling, runtime replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.

Phase 5X shows the frozen workflow status without adding controls:

```powershell
.\scripts\show-lmax-readonly-marketdata-workflow-status.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
.\scripts\check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
```

The local API endpoint is `GET /lmax-readonly-runtime/marketdata-workflow/status`. The cockpit LMAX Shadow page shows the same read-only summary. There are no buttons to connect to LMAX, run snapshots, submit replay from runtime, submit orders, register a gateway, or change scheduler/polling.

Verify the boundary with:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5b-prototype-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5o-stability-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1
.\scripts\check-lmax-readonly-runtime-phase5s-release-gate.ps1
```

Rollback remains simple because no trading state is mutated: stop the process, clear shell-only Phase 5D variables, run the default API startup, verify `/health` reports `FakeLmaxGateway`, and rerun the Phase 5D gate.

## LMAX Read-Only Runtime Phase 5C Credential Availability

Credential availability can be checked locally without connecting to LMAX:

```powershell
.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck
.\scripts\check-lmax-readonly-runtime-phase5c-credential-gate.ps1
```

The check reports only whether these labels are present: `LMAX_DEMO_FIX_USERNAME`, `LMAX_DEMO_FIX_PASSWORD`, `LMAX_DEMO_SENDER_COMP_ID`, and `LMAX_DEMO_TARGET_COMP_ID`. It never prints values. Missing labels cause a non-zero exit. Present labels are required before the Phase 5D manual script can attempt the Demo snapshot.

## LMAX Read-Only Runtime Phase 6A Planning Boundary

Phase 6A is the planning boundary after the frozen Phase 5 workflow. It creates the Phase 6 operationalization plan and boundary checklist, and it does not add runtime capability:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6a-planning-gate.ps1
```

Expected Phase 6A decision is `PASS`. The recommended next phase is `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run`. Phase 6A does not connect to LMAX, does not run scheduler or polling, does not submit runtime shadow replay, does not submit orders, does not register a gateway, and does not mutate trading state.

## LMAX Read-Only Runtime Phase 6B Instrument Allowlist Design

Phase 6B is planning-only. It defines additional candidate Demo MarketData instruments beyond EURUSD / SecurityID `4001` without approving an external run.

Current planning candidates:

- GBPUSD / GBP/USD / `TBD-LMAX-DEMO-GBPUSD`
- USDJPY / USD/JPY / `TBD-LMAX-DEMO-USDJPY`
- EURGBP / EUR/GBP / `TBD-LMAX-DEMO-EURGBP`
- AUDUSD / AUD/USD / `TBD-LMAX-DEMO-AUDUSD`

The `TBD-LMAX-DEMO-*` SecurityID values are confirmation labels, not runnable LMAX identifiers. A later explicit phase must confirm Demo SecurityIDs and update gates before any manual run can be considered.

Run the local allowlist gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1
```

The gate writes `artifacts/readiness/phase6b-instrument-allowlist-gate.json`. It does not connect to LMAX, run replay, read credentials, run scheduler/polling, submit orders, register a gateway, or mutate trading state.

## LMAX Read-Only Runtime Phase 6C SecurityID Confirmation

Phase 6C adds a local manifest that maps each Phase 6B candidate symbol to a Phase 6C SecurityID placeholder. It is still planning-only and does not approve external runs.

Current manifest values:

- GBPUSD -> `PHASE6C-DEMO-SECURITYID-GBPUSD`
- USDJPY -> `PHASE6C-DEMO-SECURITYID-USDJPY`
- EURGBP -> `PHASE6C-DEMO-SECURITYID-EURGBP`
- AUDUSD -> `PHASE6C-DEMO-SECURITYID-AUDUSD`

All `IsApprovedForExternalRun` values remain `false`.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6c-securityid-confirmation-gate.ps1
```

The gate writes `artifacts/readiness/phase6c-securityid-confirmation-gate.json`. It does not connect to LMAX, call APIs, run snapshots, replay evidence, read credentials, schedule/poll, submit orders, register a gateway, or mutate trading state.

## LMAX Read-Only Runtime Phase 6D SecurityID Discovery Planning

Phase 6D adds a local discovery manifest for candidate real Demo SecurityID mappings, but all values remain placeholders and every instrument keeps `IsApprovedForExternalRun=false`.

Current Phase 6D candidate placeholders:

- GBPUSD -> `PHASE6D-DISCOVERY-PENDING-GBPUSD`
- USDJPY -> `PHASE6D-DISCOVERY-PENDING-USDJPY`
- EURGBP -> `PHASE6D-DISCOVERY-PENDING-EURGBP`
- AUDUSD -> `PHASE6D-DISCOVERY-PENDING-AUDUSD`

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1
```

The gate writes `artifacts/readiness/phase6d-securityid-discovery-gate.json`. Phase 6D is local planning only: no LMAX connection, no external API call, no market-data snapshot, no replay, no credentials, no scheduler/polling, no orders, no gateway registration, and no trading-state mutation.

## LMAX Read-Only Runtime Phase 6E SecurityID Evidence Review

Phase 6E defines how operators and developers review source evidence before any Phase 6D placeholder can be replaced by an accepted planning SecurityID. It does not approve any external run.

Current default review status:

- GBPUSD: `NeedsMoreEvidence`
- USDJPY: `NeedsMoreEvidence`
- EURGBP: `NeedsMoreEvidence`
- AUDUSD: `NeedsMoreEvidence`

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1
```

Expected current result is `PASS_WITH_KNOWN_WARNINGS` because all instruments still need evidence. Every record remains `IsApprovedForExternalRun=false`. The gate does not connect to LMAX, call external APIs, run snapshots, run replay, read credentials, schedule/poll, submit orders, register a gateway, or mutate trading state.

## LMAX Read-Only Runtime Phase 6F SecurityID Confirmation Records

Phase 6F captures sanitized local confirmation records. These records are planning artifacts only and never approve external execution.

Create a record:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized local reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only; no external run approval" -Confidence High -Decision AcceptedForPlanning
```

Review records:

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
```

Run the gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1
```

Records are written under `artifacts/lmax-readonly-runtime-securityid-confirmations/`, and review output is written to `artifacts/readiness/phase6f-securityid-confirmation-records-review.json`. If no records exist, `PASS_WITH_KNOWN_WARNINGS` is expected. Phase 6F does not connect to LMAX, call external APIs, run snapshots, run replay, schedule/poll, submit orders, register a gateway, or mutate trading state.

## LMAX Read-Only Runtime Phase 6G Record Entry Hardening

Phase 6G hardens the manual record-entry workflow. Generate templates first:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force
```

Preview a draft without writing:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "PHASE6D-DISCOVERY-PENDING-GBPUSD" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "Pending sanitized source reference" -CapturedBy "local-operator" -Decision Draft -WhatIfPreview
```

Review and gate:

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1
```

Templates are ignored artifacts and are not accepted confirmation records. `PASS_WITH_KNOWN_WARNINGS` is expected until accepted records are entered for all four symbols. Phase 6G remains local-only and does not authorize external runs.

## LMAX Read-Only Runtime Phase 5E Failure Hardening

Phase 5E adds explicit failure classifications and disabled retry metadata. Useful statuses include `BlockedMissingCredentials`, `BlockedSafetyGate`, `BlockedInvalidEnvironment`, `BlockedUnsafeVenue`, `BlockedOrderSubmissionFlag`, `FailedSafeConnectionError`, `FailedSafeLogonRejected`, `FailedSafeLogonTimeout`, `FailedSafeSnapshotTimeout`, `FailedSafeLogoutError`, `FailedSafeMaxRuntimeExceeded`, `FailedSafeMaxEventsExceeded`, `Completed`, and `CompletedWithWarnings`.

The retry policy is guidance only. `retryEnabled=false`, `retryAllowed=false`, and `maxAttempts=1`; no script or gate automatically retries an external connection.

Run the hardening gate locally:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1
```

If a manual prototype run returns a failed-safe status, stop the process, clear shell-only Phase 5D variables, verify API `/health` still reports `FakeLmaxGateway`, run the Phase 5D and Phase 5E gates, and do not retry until the sanitized classification is understood. No database rollback is expected because no trading-state mutation is allowed.

## Phase 6H Local SecurityID Record Entry

Real SecurityID confirmation records are local artifacts only:

```powershell
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only" -Confidence High -Decision AcceptedForPlanning -WhatIfPreview
.\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol "GBP/USD" -ProposedSecurityId "<sanitized-demo-security-id>" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference "<sanitized reference>" -CapturedBy "local-operator" -ReviewedBy "local-reviewer" -ReviewReason "Planning confirmation only" -Confidence High -Decision AcceptedForPlanning
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1
```

Records are written under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`. `PASS_WITH_KNOWN_WARNINGS` is expected while records are missing or pending. `PASS` only confirms planning records for all four candidate instruments; it does not authorize snapshots, replay, scheduler/polling, orders, real gateway registration, or trading-state mutation.

## Phase 6I Manual SecurityList Discovery

Do not run this during automated validation. Only an operator should run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-securitylist-discovery.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6I operator-approved Demo SecurityList discovery for additional read-only MarketData instruments"
```

The script writes sanitized JSON to `artifacts/lmax-readonly-runtime-securityid-discovery/`. Read `candidateMatches` for proposed SecurityIDs and `unmatchedCandidates` for missing symbols. Convert matches into Phase 6H confirmation records only in a later local step; discovered IDs remain planning-only and `IsApprovedForExternalRun=false`.

## Phase 6J Failure Diagnostics

Inspect the failed SecurityList artifact with:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6j-securitylist-diagnostics-gate.ps1 -DiscoveryArtifactFile artifacts\lmax-readonly-runtime-securityid-discovery\lmax-securitylist-discovery-20260509-144711.json
```

The gate is local-only. It validates sanitized reject diagnostics, profile compatibility metadata, and safety flags. It does not connect to LMAX or run another request.

## Phase 6L SecurityList Fallback Decision

After the Phase 6K operator-approved AutoSequence attempt, review the sanitized failure artifact locally:

```powershell
.\scripts\review-lmax-readonly-runtime-securitylist-discovery-failure.ps1 -DiscoveryArtifactFile artifacts\lmax-readonly-runtime-securityid-discovery\lmax-securitylist-discovery-20260509-145908.json
.\scripts\check-lmax-readonly-runtime-phase6l-securitylist-fallback-gate.ps1 -DiscoveryArtifactFile artifacts\lmax-readonly-runtime-securityid-discovery\lmax-securitylist-discovery-20260509-145908.json
```

The review writes `artifacts/readiness/phase6l-securitylist-fallback-decision.json`. If the artifact has no candidate matches and no attempt-level reject tag/text, keep SecurityList discovery non-authorizing and use vendor/support or other official manual confirmation for the candidate SecurityIDs. This phase does not connect to LMAX, run `SecurityListRequest`, request snapshots, replay, schedule/poll, submit orders, register a gateway, expose credentials, or mutate trading state. `IsApprovedForExternalRun=false` remains mandatory.

## Phase 6M Uploaded Instrument CSV Records

Place the uploaded LMAX instrument CSVs in a local workspace path and run:

```powershell
.\scripts\new-lmax-readonly-securityid-records-from-instrument-csv.ps1 `
  -InstrumentCsvFile .\LMAX-Instruments.csv `
  -SecondaryCsvFile .\LMAX-NewYork-Instruments.csv `
  -VenueProfileName DemoLondon `
  -CapturedBy "local-operator" `
  -ReviewedBy "local-operator" `
  -ReviewReason "Phase 6M accepted planning values from uploaded LMAX instrument CSVs" `
  -ConfirmPlanningOnly
```

Then review and gate:

```powershell
.\scripts\review-lmax-readonly-securityid-confirmation-records.ps1
.\scripts\check-lmax-readonly-runtime-phase6m-csv-securityid-records-gate.ps1 `
  -InstrumentCsvFile .\LMAX-Instruments.csv `
  -SecondaryCsvFile .\LMAX-NewYork-Instruments.csv `
  -ReviewRecords
```

Expected DemoLondon/NewYork values are GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007. Tokyo 600x values are not selected for the current profile. These records are `AcceptedForPlanning` only and never authorize external runs.

## Phase 6N Planning Manifest Apply

After the Phase 6M record review is `PASS`, apply the accepted values locally:

```powershell
.\scripts\apply-lmax-readonly-securityid-planning-values.ps1
.\scripts\check-lmax-readonly-runtime-phase6n-planning-values-gate.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<generated-manifest>.json
```

The generated manifest stores GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007 with `securityIdSource=8`, `environmentName=Demo`, `venueProfileName=DemoLondon`, and `IsApprovedForExternalRun=false`. It is still planning-only: no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, or trading-state mutation is allowed.

## Phase 6O Per-Instrument Safety Gates

After Phase 6N closes, build the local per-instrument safety gate manifest:

```powershell
.\scripts\build-lmax-readonly-additional-instrument-safety-gates.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6n-manifest>.json
.\scripts\check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6n-manifest>.json `
  -SafetyGateManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6o-safety-gates>.json
```

The gate checks GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007 with `securityIdSource=8`, Demo/DemoLondon scope, `AcceptedForPlanning`, MarketDataOnly intent, and all runtime safety blockers still false. `PASS` means the planning data is safe and complete. It does not authorize an external run: `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false` must remain false for every instrument.

## Phase 6P Additional Snapshot Preflight Design

Build the future manual snapshot preflight envelope locally:

```powershell
.\scripts\build-lmax-readonly-additional-instrument-snapshot-preflights.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6n-manifest>.json `
  -SafetyGateManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6o-safety-gates>.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6P additional instrument snapshot preflight design"
.\scripts\check-lmax-readonly-runtime-phase6p-additional-snapshot-preflight-gate.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6n-manifest>.json `
  -SafetyGateManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6o-safety-gates>.json `
  -PreflightManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6p-preflights>.json
```

The preflight profile is `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, `MarketDepth=1`, with capped runtime, wait, and event limits. `PASS` means the preflight design is safe and complete, not executable. `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` must remain false for every instrument.

## Phase 6Q Approval Envelope

Create and review one planning-only approval envelope:

```powershell
.\scripts\new-lmax-readonly-additional-instrument-snapshot-approval-envelope.ps1 `
  -PreflightManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6p-preflights>.json `
  -Symbol GBPUSD `
  -RequestedByOperatorId "local-operator" `
  -ReviewedByOperatorId "local-operator" `
  -Reason "Phase 6Q planning envelope for one future GBPUSD manual read-only snapshot attempt" `
  -Decision AcceptedForPlanning `
  -ConfirmAllPlanningAttestations
.\scripts\review-lmax-readonly-additional-instrument-snapshot-approval-envelopes.ps1 `
  -PreflightManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6p-preflights>.json
.\scripts\check-lmax-readonly-runtime-phase6q-approval-envelope-gate.ps1 `
  -PreflightManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6p-preflights>.json `
  -ApprovalEnvelopeFile artifacts\lmax-readonly-runtime-securityid-planning\approval-envelopes\<envelope>.json
```

`PASS_WITH_KNOWN_WARNINGS` is expected before any envelope exists. `PASS` requires a valid `AcceptedForPlanning` envelope and still does not authorize a run. All run flags remain false.

## Phase 6R GBPUSD Dry-Run Report

Create a local dry-run report from the Phase 6N/6O/6P/6Q artifacts:

```powershell
.\scripts\new-lmax-readonly-additional-instrument-snapshot-dry-run-report.ps1 `
  -PlanningManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6n-manifest>.json `
  -SafetyGateManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6o-safety-gates>.json `
  -PreflightManifestFile artifacts\lmax-readonly-runtime-securityid-planning\<phase-6p-preflights>.json `
  -ApprovalEnvelopeFile artifacts\lmax-readonly-runtime-securityid-planning\approval-envelopes\<phase-6q-envelope>.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6R dry-run report for one future GBPUSD manual read-only snapshot attempt"
```

Review and gate with `review-lmax-readonly-additional-instrument-snapshot-dry-run-reports.ps1` and `check-lmax-readonly-runtime-phase6r-single-instrument-dryrun-gate.ps1`. `PASS` means local consistency only. No external run, snapshot, replay, scheduler, or order path is authorized.

### Phase 6S - GBPUSD Attempt Gate

Create the local-only gate artifact with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-single-instrument-snapshot-attempt-gate.ps1 `
  -PlanningManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-securityid-planning-manifest-20260509-135510.json `
  -SafetyGateManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-20260509-142938.json `
  -PreflightManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json `
  -ApprovalEnvelopeFile artifacts/lmax-readonly-runtime-securityid-planning/approval-envelopes/lmax-readonly-additional-snapshot-approval-GBPUSD-20260509-145836.json `
  -DryRunReportFile artifacts/lmax-readonly-runtime-securityid-planning/dry-run-reports/lmax-readonly-additional-snapshot-dryrun-GBPUSD-20260509-151404.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6S gate for one future GBPUSD manual read-only snapshot attempt"
```

Then run `scripts/check-lmax-readonly-runtime-phase6s-single-instrument-attempt-gate.ps1 -AttemptGateFile <generated file>`. This is local-only and does not run LMAX, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation.

### Phase 6T - GBPUSD Execution Plan

Create the planning-only execution plan with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-gbpusd-manual-snapshot-execution-plan.ps1 `
  -AttemptGateFile artifacts/lmax-readonly-runtime-securityid-planning/attempt-gates/lmax-readonly-single-instrument-snapshot-attempt-gate-GBPUSD-20260509-153256.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6T GBPUSD manual snapshot execution plan / kill-rollback plan"
```

Then run `scripts/check-lmax-readonly-runtime-phase6t-gbpusd-execution-plan-gate.ps1 -ExecutionPlanFile <generated file>`. Do not run the future command template in Phase 6T.

### Phase 6U - GBPUSD Operator Signoff

Create the non-executable signoff with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-gbpusd-manual-snapshot-operator-signoff.ps1 `
  -ExecutionPlanFile artifacts/lmax-readonly-runtime-securityid-planning/execution-plans/lmax-readonly-gbpusd-manual-snapshot-execution-plan-20260509-154546.json `
  -Phase6TGateReportFile artifacts/readiness/phase6t-gbpusd-execution-plan-gate.json `
  -RequestedByOperatorId "local-operator" `
  -SignedByOperatorId "local-operator" `
  -Reason "Phase 6U operator signoff for one future GBPUSD manual read-only snapshot attempt" `
  -SignoffDecision SignedForPlanning `
  -ConfirmAllPlanningAttestations
```

Review signoffs with `scripts/review-lmax-readonly-gbpusd-manual-snapshot-operator-signoffs.ps1`. Gate with `scripts/check-lmax-readonly-runtime-phase6u-gbpusd-operator-signoff-gate.ps1 -SignoffFile <generated file>`.

### Phase 6V - GBPUSD Final Readiness

Create final readiness with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-gbpusd-manual-snapshot-final-readiness.ps1 `
  -PlanningManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-securityid-planning-manifest-20260509-135510.json `
  -SafetyGateManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-20260509-142938.json `
  -PreflightManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json `
  -ApprovalEnvelopeFile artifacts/lmax-readonly-runtime-securityid-planning/approval-envelopes/lmax-readonly-additional-snapshot-approval-GBPUSD-20260509-145836.json `
  -DryRunReportFile artifacts/lmax-readonly-runtime-securityid-planning/dry-run-reports/lmax-readonly-additional-snapshot-dryrun-GBPUSD-20260509-151404.json `
  -AttemptGateFile artifacts/lmax-readonly-runtime-securityid-planning/attempt-gates/lmax-readonly-single-instrument-snapshot-attempt-gate-GBPUSD-20260509-153256.json `
  -ExecutionPlanFile artifacts/lmax-readonly-runtime-securityid-planning/execution-plans/lmax-readonly-gbpusd-manual-snapshot-execution-plan-20260509-154546.json `
  -OperatorSignoffFile artifacts/lmax-readonly-runtime-securityid-planning/operator-signoffs/lmax-readonly-gbpusd-manual-snapshot-operator-signoff-20260509-160336.json `
  -Phase6TGateReportFile artifacts/readiness/phase6t-gbpusd-execution-plan-gate.json `
  -Phase6UGateReportFile artifacts/readiness/phase6u-gbpusd-operator-signoff-gate.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6V final readiness for one future GBPUSD manual read-only snapshot attempt"
```

Then run `scripts/check-lmax-readonly-runtime-phase6v-gbpusd-final-readiness-gate.ps1 -FinalReadinessFile <generated file>`.

### Phase 6W - One Manual GBPUSD Snapshot Attempt

The operator command is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 `
  -FinalReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6W operator-approved one-time Demo GBPUSD read-only snapshot attempt"
```

Do not add retries, batches, scheduler/polling, orders, runtime shadow replay submit, or trading mutation. Validate any result with `scripts/check-lmax-readonly-runtime-phase6w-gbpusd-snapshot-result-gate.ps1 -ResultArtifactFile <result file>`.
## Phase 6X GBPUSD Empty-Book Review

Review the first GBPUSD result locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\review-lmax-readonly-gbpusd-snapshot-result.ps1 -ArtifactFile artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260509-191234.json
```

Then run the local gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6x-gbpusd-result-review-gate.ps1 -ArtifactFile artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260509-191234.json
```

`CompletedWithEmptyBook` means a MarketDataSnapshot was received with zero entries. It is a warning closure, not permission to retry, replay, schedule, submit orders, register a gateway, or mutate trading state.

## Phase 6Y GBPUSD Market-Hours Retry Preparation

Prepare the future market-hours retry locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-lmax-readonly-gbpusd-market-hours-retry.ps1 `
  -FinalReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json `
  -Phase6XReviewFile artifacts/readiness/phase6x-gbpusd-snapshot-result-review.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 6Y preparation for Monday market-hours GBPUSD retry after Saturday empty-book result"
```

Validate the preparation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6y-market-hours-retry-gate.ps1 -RetryReadinessFile <generated retry readiness file>
```

The script prints the future Phase 6Z command but does not run it. It adds no scheduler, polling, automatic retry, replay, orders, gateway registration, credential read, or mutation.
## Phase 6Z-A Additional-Instrument Planning Pipeline

Build the local non-executable planning pipeline with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-additional-instrument-planning-pipeline.ps1 `
  -PlanningManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-securityid-planning-manifest-20260509-135510.json `
  -SafetyGateManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-20260509-142938.json `
  -PreflightManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json `
  -RequestedByOperatorId "local-operator" `
  -ReviewedByOperatorId "local-operator" `
  -Reason "Phase 6Z-A non-executable additional instrument planning pipeline replication" `
  -All
```

Check it with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6za-additional-instrument-pipeline-gate.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/<generated-pipeline-manifest>.json
```

This runbook step is local-only. It must not connect to LMAX, run snapshots, run replay, start scheduler/polling, submit to shadow replay, submit orders, register a gateway, or mutate trading state. `PASS` means all four additional instruments have complete planning artifacts only; `executableCount` must remain `0`.

## Phase 6Z-C Additional-Instrument Planning Status

Show the read-only planning status with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\show-lmax-readonly-additional-instrument-planning-status.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json
```

Gate the status panel/API/script with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6zc-additional-instrument-status-panel-gate.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json
```

The script and endpoint are read-only. They do not connect to LMAX, run snapshots, run replay, read credentials, schedule work, submit orders, register gateways, or mutate trading state.

## Phase 6Z-D Additional-Instrument Planning Final Documentation Pack

Build the final non-executable documentation pack from the validated pipeline and operator status report:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-additional-instruments-planning-doc-pack.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json
```

Validate the freeze gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json `
  -DocPackFile <generated-doc-pack-json>
```

The pack freezes GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 as planning-only artifacts. It keeps `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`. It does not run LMAX, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation.

## Phase 6Z-E Market-Hours Next Action Card

Show the read-only market-hours next action summary:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\show-lmax-readonly-market-hours-next-action.ps1
```

Gate the action card/API/script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase6ze-market-hours-action-card-gate.ps1
```

The summary points to the prepared GBPUSD market-hours retry after the safe outside-market-hours `CompletedWithEmptyBook` result. It is visibility only: no LMAX connection, snapshot, replay, scheduler/polling, order submission, gateway registration, credential read, or trading mutation is performed.
## Phase 7A Next Boundary Check

Phase 7A is local planning only. To verify the current architecture decision boundary:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7a-next-boundary-gate.ps1
```

Expected result: `PASS`.

The gate reads the Phase 7A ADR/checklist and scans API/Worker startup surfaces. It does not connect to LMAX, run SecurityListRequest, request snapshots, replay evidence, read credentials, schedule work, submit orders, register gateways, or mutate trading state.

Recommended next phase: Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run.

## Phase 7B Controlled Manual Workflow Plan

Build the local planning artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-controlled-manual-multi-instrument-workflow-plan.ps1 `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningStatusReportFile artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7B controlled manual multi-instrument read-only snapshot workflow plan"
```

Gate it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7b-controlled-manual-workflow-plan-gate.ps1 `
  -WorkflowPlanFile <generated workflow plan file>
```

Both commands are local-only. They do not connect to LMAX, request snapshots, replay evidence, read credentials, or mutate runtime state.

## Phase 7C GBPUSD Closure Workflow

Do not use Phase 7C to run GBPUSD. It starts after a separate future operator command has produced a sanitized market-hours result artifact.

Review the artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1 `
  -ArtifactFile <gbpusd-result-artifact>
```

Map evidence preview if the review is safe:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\preview-lmax-readonly-gbpusd-market-hours-snapshot-evidence.ps1 `
  -ArtifactFile <gbpusd-result-artifact>
```

Optional replay is local and manual only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\replay-lmax-readonly-gbpusd-market-hours-evidence-preview.ps1 `
  -EvidencePreviewFile <preview-file> `
  -ConfirmLocalManualReplay
```

Build the closure manifest with the reviewed artifact and optional preview/replay reports. Gate the workflow with `scripts/check-lmax-readonly-runtime-phase7c-gbpusd-closure-gate.ps1`. No Phase 7C command connects to LMAX or requests snapshots.

## Phase 7D Post-GBPUSD Decision

Before GBPUSD has a market-hours closure, generate the current pending decision:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\decide-lmax-readonly-next-instrument-after-gbpusd.ps1 `
  -WorkflowPlanFile artifacts/lmax-readonly-runtime-securityid-planning/multi-instrument-workflow/lmax-readonly-controlled-manual-multi-instrument-workflow-plan-20260510-123311.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7D decision before GBPUSD market-hours closure exists"
```

Gate it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7d-next-instrument-decision-gate.ps1 `
  -DecisionFile <generated decision file>
```

With no closure supplied, the decision is `PendingGbpusdMarketHoursAttempt`. After a future Phase 7C closure, pass either `-GbpusdClosureManifestFile` or `-GbpusdReviewFile` to decide whether to proceed to EURGBP planning, retry GBPUSD later, or block for diagnostics. The script is local-only and does not run LMAX, snapshots, or replay.

## Phase 7E Market-Hours Execution Checklist Pack

Build the checklist pack:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-lmax-readonly-gbpusd-market-hours-execution-checklist-pack.ps1
```

Gate the pack:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7e-execution-checklist-gate.ps1 `
  -ChecklistPackFile <generated checklist pack json>
```

The pack records the future GBPUSD manual command with `DO NOT RUN UNTIL MARKET HOURS`. The builder and gate do not execute the command, connect to LMAX, request snapshots, run SecurityListRequest, replay evidence, schedule work, or use credentials.

## Phase 7E2 EURGBP Readiness Rehydration

After GBPUSD has a Phase 7C `CompletedWithBook` / `PASS` review and Phase 7D emits `ProceedToEurgbpPlanning`, rehydrate EURGBP readiness:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\rehydrate-lmax-readonly-eurgbp-manual-snapshot-readiness.ps1 `
  -Phase7DDecisionFile artifacts/lmax-readonly-runtime-securityid-planning/next-instrument-decisions/lmax-readonly-post-gbpusd-next-instrument-decision-20260511-114217.json `
  -PipelineManifestFile artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json `
  -PlanningManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-securityid-planning-manifest-20260509-135510.json `
  -SafetyGateManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-20260509-142938.json `
  -PreflightManifestFile artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7E2 EURGBP readiness rehydration after successful GBPUSD closure"
```

Then gate the readiness artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7e2-eurgbp-readiness-gate.ps1 `
  -EurgbpReadinessFile <generated EURGBP readiness json>
```

These commands do not run EURGBP, connect to LMAX, request SecurityList, request snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state.

## Phase 7F2 EURGBP Execution Checklist

After Phase 7E2 produces a `PASS` EURGBP readiness artifact, generate the planning-only execution checklist:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-eurgbp-manual-snapshot-execution-checklist.ps1 `
  -EurgbpReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-readiness/lmax-readonly-eurgbp-manual-snapshot-readiness-rehydration-20260511-120632.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7F2 EURGBP manual snapshot execution checklist / kill-rollback plan"
```

Then gate the checklist:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7f2-eurgbp-execution-checklist-gate.ps1 `
  -ChecklistFile <generated EURGBP checklist json>
```

The generated future command is explicitly marked `DO NOT RUN IN PHASE 7F2`. The checklist and gate do not connect to LMAX, request SecurityList, request snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state.

## Phase 7G2 EURGBP Final Pre-Run Gate

After Phase 7F2 produces a `PASS` EURGBP checklist artifact, build the final non-executable pre-run gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-eurgbp-final-pre-run-gate.ps1 `
  -Phase7DDecisionFile artifacts/lmax-readonly-runtime-securityid-planning/next-instrument-decisions/lmax-readonly-post-gbpusd-next-instrument-decision-20260511-114217.json `
  -EurgbpReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-readiness/lmax-readonly-eurgbp-manual-snapshot-readiness-rehydration-20260511-120632.json `
  -ExecutionChecklistFile artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-execution-checklists/lmax-readonly-eurgbp-manual-snapshot-execution-checklist-20260511-123308.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7G2 EURGBP final pre-run gate"
```

Then gate the final pre-run artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7g2-eurgbp-final-prerun-gate.ps1 `
  -FinalPreRunGateFile <generated EURGBP final pre-run gate json>
```

Phase 7G2 does not connect to LMAX, call external APIs, request SecurityList, request snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state. `PASS` means prerequisite consistency only; it does not authorize execution.

## Phase 7H Generic Additional Instrument Manual Workflow

Phase 7H introduces a reusable one-instrument manual wrapper for additional Demo read-only MarketData snapshots:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1 `
  -Symbol EURGBP `
  -FinalPreRunGateFile "artifacts\lmax-readonly-runtime-securityid-planning\eurgbp-final-prerun\lmax-readonly-eurgbp-final-prerun-gate-20260511-134130.json" `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 7H operator-approved EURGBP market-hours read-only snapshot attempt"
```

Do not run this command unless the operator explicitly chooses to run one Demo read-only market-hours attempt. The wrapper accepts one supported symbol only, refuses batch/multiple symbols, validates the final pre-run gate, and delegates to the isolated manual prototype path.

After any future manual result, use the generic Phase 7H review, evidence preview, optional local replay, closure manifest, and gate scripts. Phase 7H adds no scheduler, polling, automatic retry, runtime shadow replay submit, orders, gateway registration, or trading mutation.

## Phase 7H2 Generic Final Pre-Run Gate Builder

Before using the Phase 7H wrapper for USDJPY or AUDUSD, create a Phase 7H-compatible final pre-run gate from the existing non-executable readiness artifact:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-lmax-readonly-additional-instrument-final-pre-run-gate.ps1 `
  -Symbol USDJPY `
  -FinalReadinessFile artifacts\lmax-readonly-runtime-securityid-planning\final-readiness\lmax-readonly-additional-instrument-final-readiness-USDJPY-20260509-175849.json `
  -RequestedByOperatorId "local-operator" `
  -Reason "Phase 7H2 USDJPY final pre-run gate"
```

Then validate the generated gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase7h2-additional-instrument-final-prerun-gate.ps1 `
  -FinalPreRunGateFile <generated USDJPY final pre-run gate json>
```

Do not pass a Phase 6Z-A generic final-readiness artifact directly to the Phase 7H wrapper. It is intentionally rejected because it is not a Phase 7H final pre-run gate. Phase 7H2 does not connect to LMAX, run snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state.
