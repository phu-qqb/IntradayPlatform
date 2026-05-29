# QQ.Production.Intraday

Local simulator foundation for `QQ.Production.Intraday`, an institutional PMS/OMS/EMS-style intraday execution platform. This first version is intentionally local-only: it does not connect to LMAX, cannot send live orders, and registers only `FakeLmaxGateway`.

## Documentation

Start with these guides:

- [Developer Guide](docs/DEVELOPER_GUIDE.md) - architecture, projects, API routes, persistence, UI, scripts, LMAX evidence/shadow behavior, and extension rules.
- [Operator Manual](docs/OPERATOR_MANUAL.md) - plain-language daily workflow, page guide, safety rules, exception handling, governance, and escalation.
- [Operational Readiness Checklist](docs/OPERATIONAL_READINESS_CHECKLIST.md) - release/readiness gate, validation commands, safety checklist, and next-phase criteria.
- [Local Runbook](docs/LOCAL_RUNBOOK.md) - local setup, database reset/update, API/UI startup, smoke scripts, and operational procedures.
- [LMAX Connectivity Lab](docs/LMAX_CONNECTIVITY_LAB.md) - isolated lab-only LMAX FIX diagnostics and evidence capture.
- [LMAX Read-Only Runtime Adapter Design](docs/LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md) - future read-only shadow reader design, activation levels, safety gates, and non-mutating evidence flow. Design-only; no runtime connectivity.
- [LMAX Read-Only Runtime Adapter Implementation Plan](docs/LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md) - phased future delivery plan with entry/exit gates, tests, smokes, rollback criteria, and next eligible phase.
- [LMAX Read-Only Runtime Phase Gates](docs/LMAX_READONLY_RUNTIME_PHASE_GATES.md) - concise quick-review checklist for each future phase.
- [LMAX Read-Only Runtime First Transport Preflight](docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md) - Phase 5A preflight, kill/rollback, abort conditions, and entry criteria before any future socket prototype.
- [LMAX Read-Only Demo MarketData Workflow Final Doc](docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md) - Phase 5Y final documentation pack for the frozen manual Demo MarketData workflow.
- [LMAX Read-Only Runtime Phase 6 Operationalization Plan](docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md) - Phase 6A planning boundary and recommended next safe frontier.
- [Adapter Contracts](docs/ADAPTER_CONTRACTS.md) - neutral venue contract, FakeLmax parity, and future LMAX adapter requirements.
- [Documentation Index](docs/INDEX.md) - audience-oriented map of the documentation set.

## Scope

- Modular monolith targeting .NET 10 (`net10.0`)
- FX spot only, seeded with `EURUSD`
- One fund and one broker account in seed data, with fund/account IDs modeled for later expansion
- Venue-neutral execution abstractions with LMAX treated as one future venue implementation
- SQL Server LocalDB connection string included for local development
- EF Core SQL Server infrastructure project with DbContext, repository, constraints, and seed-friendly model shape
- Worker and minimal API wired to local simulator services only

## Safety Invariants

- No position match = no trading
- No risk approval = no order
- No persisted fill = no position update
- No duplicate broker execution id
- No duplicate model run processing
- No ambiguous active reference data = no trading
- No live LMAX connectivity exists in the registered application path
- API and Worker register only `FakeLmaxGateway`

The `QQ.Production.Intraday.Infrastructure.Lmax` project contains a placeholder `LmaxVenueGateway` that throws `NotImplementedException` and is not registered by the API or Worker. It also contains dormant adapter design contracts, normalized DTOs, safety-gate helpers, and in-memory shadow-mode comparison helpers. These are architecture/readiness types only; the API and Worker do not register them.

## LMAX Adapter Design Gate

The isolated Connectivity Lab has validated the LMAX Demo FIX lifecycle: market-data snapshot, trading logon, tiny Demo `NewOrderSingle`, `ExecutionReport` New/Trade, `OrderStatusRequest`, `TradeCaptureReportRequest`, and `TradeCaptureReport` recovery. No lab data is persisted into the main database.

The platform integration path is FIX-only plus LMAX EOD files:

- FIX Market Data for future market-data ingestion.
- FIX Trading for future order entry, execution reports, order-status recovery, and trade-capture recovery.
- LMAX EOD files as the official daily reconciliation source.

The real adapter is not wired into runtime. Future LMAX work must start in shadow mode, comparing normalized LMAX events to internal orders/fills without mutating orders, fills, positions, or execution state. See `docs/LMAX_ADAPTER_DESIGN.md` and `docs/ADAPTER_CONTRACTS.md`.

The adapter contract parity gate is documented in `docs/ADAPTER_CONTRACTS.md`. It defines the neutral venue event model used to compare internal execution behavior, `FakeLmaxGateway` simulator output, and future LMAX FIX-normalized events before any real adapter can be considered for runtime registration.

The LMAX adapter skeleton now includes inert FIX message builders, mappers, runtime safety validation, and a blocked `LmaxVenueGatewaySkeleton`. These components are not registered in API or Worker. `FakeLmaxGateway` remains the only runtime execution gateway.

The live shadow reader skeleton is also disabled by default. It exposes local status/blocking diagnostics for future read-only LMAX shadow work, but it does not open sockets, use credentials, call the Connectivity Lab, submit orders, or write to trading tables. The future read-only runtime adapter design and implementation plan are documented separately. Phase 1 adds inert interfaces and disabled/no-op behavior only. Phase 2 adds a service-level fake/in-memory fixture preview only. Phase 3 adds local diagnostic endpoints under `/lmax-readonly-runtime/*`; they are disabled/blocked by default, fixture-only when explicitly fake-enabled, and still add no runtime connectivity. Phase 3.5 proves the explicit fake-enabled path in integration tests while keeping default `appsettings.json` disabled/design-only and keeping `SubmitToShadowReplay` blocked.

Phase 4 preflight locks the future external read-only boundary before any socket code exists. It adds documentation, tests, config-gate checks, and a local preflight script only. Phase 4A adds external-session contracts and a disabled stub only. Phase 4B adds an in-memory fake transport harness only. Phase 4C adds sanitized fake-event evidence preview mapping only, with no shadow replay submit. Phase 4D exposes the fake transport preview through a local manual endpoint, still fake-only/no-shadow-submit/no-persistence. Phase 4E adds a hard-disabled external-session skeleton only, with no socket activation or FIX logon. Phase 4F adds a guarded transport interface and disabled transport only, still with no network implementation. Phase 4G adds a typed configuration envelope and inactive sample only, with no credential values. Phase 4H adds a disabled credential-profile boundary only; `CredentialProfileName` is a label and no credential values are read, used, stored, logged, or returned. Phase 4I adds a disabled venue-profile boundary only; `VenueProfileName` is a label and no host, port, user, account, endpoint, session, or credential values are exposed. Phase 4J adds a validate-only run-intent envelope; it requires a manual reason and operator id but starts no session. Phase 4K exposes that envelope through `POST /lmax-readonly-runtime/external-run-intent/validate`; it validates only and always reports no session start, no external connection, no credential read, no shadow replay submit, and no trading mutation. Phase 4L adds `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`; it aggregates the same intent validation with options, venue, credential resolver, guarded transport, skeleton, safety-gate, expected-outcome, and operator-guidance status while still starting no session and attempting no external connection, credential read, shadow replay submit, or trading mutation. Phase 4M adds `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`; it validates manual signoff metadata and attestations, but cannot authorize execution and still starts no session or external action. Phase 4N adds `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`; it validates the intent/report/signoff audit envelope, but cannot authorize execution and still starts no session or external action. Phase 4O adds `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`; it aggregates the full blocked chain into one snapshot, but cannot start a session or external action. Phase 4P adds the final no-socket release gate at `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`; passing it only means the no-socket boundary is ready to consider a separately prompted future socket prototype. Phase 5A adds first-transport preflight, kill/rollback, abort-condition, and operator-control planning only through `docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md` and `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1`; it still adds no socket capability. Functional external transport implementation has not started.

Shadow Reader Quality Gate #1 hardens that skeleton against dangerous or contradictory configuration. The reader now reports explicit safety gates with status, observed value, expected safe value, and message. Even if options are set toward a future live reader, the current implementation remains blocked by `ImplementationMode` and does not execute.

## Quality Gate #1

Quality Gate #1 hardened the local simulator foundation without changing the architecture:

- Added deterministic `IClock` usage for workflow, simulator timestamps, and tests
- Persisted target positions and drift snapshots idempotently
- Added pre-trade and post-trade reconciliation phases
- Ensured post-trade mismatches persist blocking reconciliation breaks without corrective trading
- Tightened IOC execution report sequences and parent/child order terminal states
- Expanded tests across target calculation, risk, reconciliation, idempotency, fills, ledger updates, IOC behavior, and FakeLmax-only startup safety
- Configured EF decimal precision and restricted cascade delete behavior to protect audit history

## Build And Test

```powershell
dotnet restore QQ.Production.Intraday.sln --configfile NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
dotnet build QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
dotnet test QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false
dotnet list QQ.Production.Intraday.sln package --vulnerable --include-transitive
```

## Local Persistence

Runtime persistence is configurable:

- `Persistence:Provider = InMemory`
- `Persistence:Provider = SqlServerLocal`

Development defaults to `SqlServerLocal` with SQL Server LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Persistence mode does not change the safety boundary: API and Worker still register `FakeLmaxGateway` and `FakeMarketDataProvider`.

Startup database settings:

- `Database:ApplyMigrationsOnStartup`
- `Database:SeedReferenceDataOnStartup`
- `Database:SeedDemoDataOnStartup`

Normal startup never drops or resets the database.

## Reference Data Integrity

Prompt #4.2 adds a reference data integrity gate. Duplicate active reference rows are dangerous because they can change venue mapping, risk-limit, trading-window, or enabled reference selection. The platform now checks reference data before processing a model run and at startup by default.

If a blocking integrity issue exists:

- processing returns `Blocked` with `ReferenceDataAmbiguous` or `ReferenceDataInvalid`
- no trade intents are created when ambiguity is detected before intent creation
- no orders are created
- no fills are created
- no position ledger updates are made

The read-only admin endpoint reports the current check:

```powershell
curl http://localhost:5050/admin/reference-data/integrity
.\scripts\check-reference-data.ps1 -BaseUrl http://localhost:5050
```

Startup settings:

- `ReferenceDataIntegrity:CheckOnStartup`
- `ReferenceDataIntegrity:FailStartupOnBlockingIssues`

Development defaults fail startup on blocking issues. If an old local database contains duplicate reference rows from early non-idempotent seed runs, apply the new migration to a clean database:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
```

Production/RDS remediation is not implemented yet. Future production migrations must use a controlled duplicate-detection and remediation plan, not blind deletion.

## EF Migrations

The LocalDB schema migrations include:

- `InitialLocalSqlServerSchema`
- `EnforceReferenceDataUniqueness`

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/QQ.Production.Intraday.Infrastructure.SqlServer --startup-project src/QQ.Production.Intraday.Api
```

The reference seed is idempotent and includes the fund, account, LMAX venue metadata, EURUSD, venue mapping, NAV, conservative risk configuration, trading window, kill switch, start-of-day position, and seed market data. Demo seed data is opt-in and adds deterministic fake snapshots only. Local model runs are created through `POST /model-runs` or `scripts/smoke-local.ps1`, not by persistent seed data.

Prompt #6 adds `AddModelWeightSourceTables` for DB-staged model weights:

- `ModelWeightBatches`
- `ModelWeightRows`
- `ModelWeightValidationIssues`

Daily Operations adds `AddDailyOperationsJobControl` for persistent local operational job history:

- `OperationalJobDefinitions`
- `OperationalJobRuns`
- `OperationalJobSteps`
- `OperationalJobRunEvents`

Daily Operations job status semantics distinguish operational results from infrastructure failures. `Succeeded` means the wrapper completed as expected, `Skipped` means intentionally no work, `PartiallySucceeded` means completed with business warnings or handled blocks, and `Failed` is reserved for infrastructure/programming failures or critical controls such as blocking reference-data integrity. EOD reconciliation can succeed while reporting blocking breaks because those breaks remain reconciliation/exception workflow objects. Retries create new linked job runs with `RetryOfJobRunId`, require a reason, and are audited.

Operational runbooks add `AddOperationalRunbooksAndLocalScheduler` for Start-of-Day, Intraday Cycle, End-of-Day, and disabled-by-default local schedule metadata:

- `OperationalRunbookDefinitions`
- `OperationalRunbookStepDefinitions`
- `OperationalRunbookRuns`
- `OperationalRunbookStepRuns`
- `OperationalScheduleDefinitions`

The seeded runbooks orchestrate existing local jobs only. Start of Day runs reference-data, bar-build, active-risk, exception, and manual confirmation checks. Intraday Cycle promotes ready weights, processes pending model runs through FakeLmax only, builds bars, and checks exceptions. End of Day uses fake/generated LMAX EOD reports, local import, reconciliation, USD PnL, EOD exception checks, and manual confirmation. The local scheduler foundation is disabled by default (`LocalScheduler:Enabled=false`), has no cloud dependency, does not install a Windows Service, and never calls real LMAX or Connectivity Lab network commands.

## Scripts

- `scripts/check-env.ps1`
- `scripts/restore-build-test.ps1`
- `scripts/update-local-db.ps1`
- `scripts/reset-local-db.ps1`
- `scripts/run-api.ps1`
- `scripts/run-worker.ps1`
- `scripts/check-reference-data.ps1`
- `scripts/smoke-local.ps1`
- `scripts/create-fake-weight-batch.ps1`
- `scripts/promote-ready-weight-batches.ps1`
- `scripts/smoke-db-weights-local.ps1`
- `scripts/smoke-governance-local.ps1`
- `scripts/run-ops-job.ps1`
- `scripts/smoke-daily-ops-local.ps1`
- `scripts/run-runbook.ps1`
- `scripts/smoke-runbooks-local.ps1`
- `scripts/smoke-lmax-shadow-local.ps1`
- `scripts/smoke-lmax-shadow-reader-local.ps1`
- `scripts/run-ui.ps1`
- `scripts/run-local-stack.ps1`

See [docs/LOCAL_RUNBOOK.md](docs/LOCAL_RUNBOOK.md) for the full local workflow.

The smoke scripts use dynamic UTC timestamps and local-only APIs. `smoke-local.ps1` builds the previous completed 15-minute bar, adds fresh fake snapshots for execution freshness, creates a current model run, and processes it through `FakeLmaxGateway`. `smoke-daily-ops-local.ps1` validates Daily Operations summary/checklist/job history, exercises a safe retry, verifies audit events, and skips EOD reconciliation clearly when no local LMAX EOD import run exists. `smoke-runbooks-local.ps1` validates default runbook definitions, manual gates, linked job runs, runbook audit events, and that the local scheduler remains disabled by default. If a local API call fails, the scripts print the endpoint, safe request body, HTTP status, and response body.

## Run API

```powershell
.\scripts\run-api.ps1
```

Endpoints include:

- `GET /health`
- `GET /model-runs`
- `POST /model-runs`
- `POST /model-runs/{id}/process`
- `GET /model-weight-batches`
- `GET /model-weight-batches/{id}`
- `GET /model-weight-batches/{id}/rows`
- `GET /model-weight-batches/{id}/validation-issues`
- `POST /model-weight-batches/fake`
- `POST /model-weight-batches/{id}/validate`
- `POST /model-weight-batches/{id}/promote`
- `POST /model-weight-batches/promote-ready`
- `GET /positions/internal`
- `GET /positions/broker`
- `GET /reconciliation/breaks`
- `GET /trade-intents`
- `GET /orders`
- `GET /fills`
- `GET /admin/reference-data/integrity`
- `POST /admin/kill-switch`
- `POST /admin/kill-switch/clear`

`GET /health` reports application name, environment, persistence provider, database reachability, pending migrations count, execution gateway, market data mode, live trading flag, external connections flag, and UTC server time. It does not expose secrets.

`GET /orders` returns API DTOs with plain string IDs (`id`, `tradeIntentId`, `parentOrderId`, `venueId`, `instrumentId`, `clientOrderId`, `brokerOrderId`) rather than nested value-object shapes.

`POST /model-runs/{id}/process` returns a structured process result. Normal operational blocks such as stale model run, stale market data, position mismatch, kill switch active, trading window closed, risk rejection, no target weights, no market data, or no drift return HTTP 200 with a status such as `Blocked`, `AlreadyProcessed`, or `NoActionRequired`. HTTP 500 is reserved for real infrastructure or programming failures; inspect API logs in Development for the stack trace.

## Run UI

Prompt #5 adds a local-only React/TypeScript operator cockpit:

```text
src/QQ.Production.Intraday.Ui
```

The UI is a monitoring and local development control surface. The backend remains the source of truth; the UI does not calculate positions, risk, reconciliation, drift, orders, or fills.

Default URLs:

- API: `http://localhost:5050`
- UI: `http://localhost:5173`

Configure the API URL with `VITE_API_BASE_URL` if needed:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5050"
```

Run locally:

```powershell
.\scripts\run-api.ps1
.\scripts\run-ui.ps1
```

The cockpit shows live trading status, external connection status, execution gateway, market data mode, persistence provider, database reachability, pending migrations, and reference data integrity. It shows critical warnings if anything violates the local-only FakeLmax safety boundary.

The cockpit is now organized as a PMS/OMS/EMS operator shell:

- Command Center: safety, health, latest batches/runs, open orders, fills, EOD breaks, and wallet/PnL summary
- PMS: internal/broker positions, target positions, drift, currency wallets, and USD PnL
- Model Weights: DB-staged batches, rows, validation issues, validation, and promotion
- OMS: model runs, trade intents, risk decisions, parent/child orders, and fills
- EMS: execution gateway status, child orders, fills, latest local market data, and `MarketImmediate`
- Market Data: fake/local snapshots and 15-minute bars
- Exceptions: operator workflow for breaks and operational exceptions
- Reconciliation: intraday and EOD breaks
- Daily Operations: local job runs, daily checklist, retryable job history, and operational timeline
- LMAX EOD: import runs, validation issues, individual trades, trade summaries, currency wallets, PnL, and EOD breaks
- Risk Control Center: active risk profile, versioned limit sets, trading windows, instrument/venue controls, kill switch, and recent explainable risk decisions
- Audit Journal: append-only operator/system action journal with correlation IDs
- Connectivity Lab: read-only script guidance only; no credential forms or live controls

The UI comfort pass keeps the same local-only backend behavior while making the cockpit calmer and easier to read for intraday use. The top safety bar groups runtime, safety, data, and reference-integrity state and shows `SAFE LOCAL` only when the FakeLmax boundary is intact. Tables now prioritize human-readable fields, shorten long technical IDs, keep headers sticky, and move full record detail into the right-side drawer. The drawer groups summary, IDs, timestamps, details, and raw JSON so operators can inspect records without turning every blotter into a wall of identifiers.

Long-running UI actions now show explicit operator feedback. Mutating buttons switch to labels such as `Creating...`, `Promoting...`, or `Processing...`, show a spinner, and disable themselves while the request is pending. The shell also shows a calm operation toast with success/failure state, elapsed “still working” feedback after a short delay, and expandable API error details when a request fails.

Development CORS allows only `http://localhost:5173` and `http://127.0.0.1:5173`; wildcard CORS is not enabled for production-like environments.

## Operator Audit Trail

The platform persists an append-only `OperatorAuditEvents` journal for safety-relevant local operator and system actions. Audited actions include DB weight batch creation/validation/promotion, model-run creation and processing, kill-switch activation/clear, reference-data integrity blocking checks, fake LMAX EOD generation/import, EOD reconciliation runs, permission denials, and approval workflow decisions.

Each event records UTC time, actor type/name, event type, severity, result, entity reference, correlation/request IDs, source, description, optional reason, and sanitized JSON metadata. The local operator context is attribution only, not production authentication: API requests default to the configured local operator (`local-admin` in development) and may set `X-Operator-Id` and `X-Correlation-Id`.

Audit APIs are read-only:

```powershell
curl "http://localhost:5050/audit/events?limit=100"
curl "http://localhost:5050/audit/events/by-entity?entityType=ModelRun&entityId=<id>"
curl "http://localhost:5050/audit/events/by-correlation/<correlationId>"
```

Audit metadata is sanitized before persistence for keys containing `password`, `secret`, `token`, or `apiKey`. There are no update/delete endpoints for audit events. The current limitation is that the local operator identity is a development governance context, not real authentication.

## Local Governance and Approvals

The local runtime now seeds operator identities and roles for maker/checker testing: `local-viewer`, `local-operator`, `local-risk`, `local-approver`, `local-admin`, and `system`. The UI has a local operator selector that stores the selected operator in browser storage and sends it as `X-Operator-Id`. This is deliberately labelled as local development context only; there is no login form, password capture, external identity provider, or production authentication.

Roles map to local permissions such as viewing dashboards, creating/promoting model weights, processing model runs, managing exceptions, drafting/activating risk config, clearing the kill switch, running EOD reconciliation, and managing approvals. `Admin` has all local permissions. Disabled or unknown operators are rejected for sensitive actions.

Four-eyes approval is enabled by default for safety-critical local actions. The first operator creates a pending `ApprovalRequest`; a different approver/admin must approve it with a reason; the approved request is then executed once. The requester cannot approve their own request, and rejected/cancelled/executed requests cannot be executed again.

Approval-gated actions currently include risk limit set activation, risk limit set retirement, kill-switch clear, waiving blocking/critical exceptions, marking blocking/critical exceptions false positive, and resolving blocking/critical exceptions. The Governance page shows current operator permissions, pending approvals, approval history, and approve/reject/execute actions. These workflows do not enable live trading or external connectivity.

To rerun the local maker/checker validation after resetting and starting the API:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1

# In another terminal:
.\scripts\smoke-governance-local.ps1
```

The smoke confirms health remains FakeLmax-only, seeded operators resolve through `X-Operator-Id`, `local-risk` can create a risk activation approval request, requester self-approval is blocked, `local-approver` can approve/execute once, kill-switch clear remains pending until checker execution, and audit/approval records are written. It only calls `http://localhost:5050` by default and never uses credentials, LMAX, live trading, or external connections.

## Exception Management

The platform now persists `ExceptionCases` for warning/blocking operational breaks. Blocking and warning intraday reconciliation breaks and EOD reconciliation breaks create or update an exception case idempotently; informational breaks do not create cases by default. The original break remains intact, and the exception case becomes the operator workflow object.

Operators can acknowledge, assign, mark investigating, resolve, mark false positive, waive with reason, reopen, and add notes. Resolution, waiver, and false-positive actions require a reason. Every action is written to the append-only action history and also creates an `OperatorAuditEvent` with correlation metadata.

Read/write local APIs are under `/exceptions`; there are no delete endpoints:

```powershell
curl "http://localhost:5050/exceptions?limit=100"
curl "http://localhost:5050/exceptions/<id>/actions"
curl -X POST "http://localhost:5050/exceptions/<id>/acknowledge" -H "Content-Type: application/json" -d "{\"reason\":\"Reviewed locally\"}"
curl -X POST "http://localhost:5050/exceptions/<id>/resolve" -H "Content-Type: application/json" -d "{\"reason\":\"Source break was remediated\"}"
```

The cockpit has an Exceptions page plus Command Center exception counts. Blocking/critical waiver, false-positive, and resolution actions can require four-eyes approval; normal acknowledge, investigate, assign, and note actions remain immediate local workflow actions with audit.

## Risk Control Center

The platform now exposes a local-only Risk Control Center so risk configuration is visible, versioned, and audited before any future live integration. Risk limit sets support `Draft`, `Active`, `Retired`, and `Archived` lifecycle states. Only one active set is selected per fund/model; activating a draft retires the prior active set for that scope.

Risk/config mutations require a reason and create `OperatorAuditEvent` records with before/after metadata where practical. The cockpit shows the active risk profile, global limits, instrument limits, venue limits, trading windows, instrument/venue control flags, kill switch state, and recent risk decisions. Active and archived profiles are treated as read-only from the UI; clone an active set to a draft before editing and then activate the draft with a reason.

Risk decisions now retain the active `RiskLimitSetId` and check-level details such as observed value, limit value, unit, check name, and message. A rejected decision can therefore explain outcomes like `MaxTradeNotionalUsd exceeded: observed > limit` without turning normal risk blocks into HTTP 500s.

Approved decisions also keep passed check rows. The UI shows a summary observed/limit pair in the Risk table and a check-detail table when a decision is selected. Older historical rows that predate detail persistence may show “No check details available for this historical decision.”

Instrument controls distinguish execution and reporting permissions. `IsTradingEnabled` gates order generation, while `IsReportImportEnabled` allows historical LMAX EOD import for known aliases even when an instrument is not tradable. Current limitations: local identity is not production authentication, there is no four-eyes multi-party authentication provider yet, and risk changes affect only the local/FakeLmax runtime.

## DB Model Weight Source

Model weights are now staged in the database before they become canonical `ModelRun` and `TargetWeight` records. This is the future integration point for Qubes/GA output, but no Qubes/GA writer is implemented yet.

Current local flow:

1. Fake/local generator creates a `ModelWeightBatch` and `ModelWeightRows`.
2. Validation checks fund/model metadata, timestamps, NAV, frequency, row count, duplicate rows, enabled instruments, and reference-data integrity.
3. Promotion creates one `ModelRun` plus `TargetWeights`.
4. Promotion does not process the model run or send orders.
5. Existing `POST /model-runs/{id}/process` remains the explicit execution workflow and still enforces reconciliation and risk.

Idempotency:

- `SourceSystem + ExternalBatchId` is unique.
- Reusing the same explicit external batch id with identical content returns the existing batch.
- Reusing the same external batch id with different content is rejected.
- Re-promoting an already promoted batch returns the existing promoted model run id.

Local examples:

```powershell
.\scripts\create-fake-weight-batch.ps1
.\scripts\promote-ready-weight-batches.ps1
.\scripts\smoke-db-weights-local.ps1
```

The cockpit includes a Model Weight Batches panel for creating fake batches, viewing rows/issues, validating, and promoting. Promotion is deliberately separate from model-run processing.

## Run Worker

```powershell
.\scripts\run-worker.ps1
```

The default poll interval is 15 minutes. Development configuration can process immediately on startup.

## LocalDB

Default connection string:

```text
Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Run EF migrations against LocalDB:

```powershell
.\scripts\update-local-db.ps1
```

Recommended clean local sequence after Prompt #4.2:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\smoke-local.ps1
```

Recommended local UI workflow:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\run-ui.ps1
```

Then use the UI to create fake snapshots, build bars, create a model run, process it, and inspect positions, drift, risk decisions, orders, fills, and reconciliation breaks.

DB-weight local workflow:

```powershell
cd C:\Users\phili\source\repos\QQ.Production.Intraday
.\scripts\reset-local-db.ps1 -SeedDemoData
.\scripts\run-api.ps1
.\scripts\check-reference-data.ps1
.\scripts\smoke-db-weights-local.ps1
```

In the UI, create a fake model weight batch, validate/promote it, then process the generated model run from the Model Runs panel.

## Target Quantity Modes

`PortfolioBaseCurrencyNotional`:

- `targetNotionalUsd = weight * navUsd`
- For `EURUSD`, base quantity is `targetNotionalUsd / midPrice`
- Venue quantity is base quantity divided by venue contract size
- Venue quantity is rounded to the nearest venue quantity step using `MidpointRounding.AwayFromZero`, preserving sign
- Example: NAV `1,000,000`, weight `-0.10`, mid `1.10000`, contract size `10,000`, step `0.1` gives notional `-100,000`, unrounded base `-90,909.090909...`, rounded venue `-9.1`, rounded base `-91,000`

`FxBaseCurrencyQuantity`:

- `targetBaseQuantity = weight * navUsd`
- `targetNotionalUsd = targetBaseQuantity * midPrice`
- Venue quantity is base quantity divided by venue contract size
- Venue quantity is rounded to the nearest venue quantity step using `MidpointRounding.AwayFromZero`, preserving sign
- Example: NAV `1,000,000`, weight `-0.10`, mid `1.10000`, contract size `10,000`, step `0.1` gives base `-100,000`, notional `-110,000`, venue `-10.0`

## Execution

The first execution algorithm is `MarketImmediate`, creating market IOC child orders and sending them only to `FakeLmaxGateway`. The fake gateway supports deterministic full fill, partial fill, reject, and no-fill outcomes for tests.

IOC semantics:

- Full fill: `OrderAck`, `Fill`; child `Filled`, parent `Filled`, fill and ledger event persisted
- Partial fill: `OrderAck`, `PartialFill`, `Expired`; child `Expired`, parent `PartiallyFilled`, fill and ledger event only for executed quantity, no open order remains
- Reject: `OrderReject`; child `Rejected`, parent `Rejected`, no fill, no ledger update
- No fill: `OrderAck`, `Expired`; child `Expired`, parent `Expired`, no fill, no ledger update

## Market Data Store

The platform stores both raw market data snapshots and derived bars.

Snapshots preserve the observed bid/ask/mid/spread with source metadata, source timestamp, received timestamp, optional sequence number, synthetic flag, and creation timestamp. The execution workflow still uses the latest snapshot for target position calculation and stale market data checks.

Bars are derived from persisted snapshots and are intended for future model-weight research and generation. Bar building does not affect trading decisions yet.

15-minute bar convention:

- UTC only
- Half-open intervals: `[BarStartUtc, BarEndUtc)`
- `09:15:00` belongs to the `09:15-09:30` bar
- `09:29:59.999` belongs to the `09:15-09:30` bar
- `09:30:00` belongs to the `09:30-09:45` bar

Stored bar fields include bid OHLC, ask OHLC, mid OHLC, spread open/high/low/close/average, observation count, first/last snapshot timestamps, completeness, quality status, build run id, builder version, and creation timestamp.

Bar quality:

- `Complete`: interval is in the past and has at least the configured minimum observation count
- `SparseData`: interval is in the past but below the configured minimum observation count
- `Incomplete`: interval end is in the future
- `NoData`: supported by the builder when no-data bar creation is explicitly enabled; default behavior skips no-data bars

Only `FifteenMinutes` bar construction is implemented. Other timeframes are modeled but not built yet.

Fake/local market data only:

- `FakeMarketDataProvider` creates deterministic synthetic EURUSD snapshots
- No live market data connectivity exists
- No live LMAX market data connectivity exists
- API and Worker still use local/fake services only

API examples:

```powershell
curl -X POST http://localhost:5050/market-data/fake-snapshots `
  -H "Content-Type: application/json" `
  -d '{"instrumentSymbol":"EURUSD","venueName":"LMAX","startUtc":"2026-04-29T09:15:00Z","intervalSeconds":60,"count":15,"bid":1.1000,"ask":1.1002,"bidStep":0.00001,"askStep":0.00001}'

curl -X POST http://localhost:5050/market-data/build-bars `
  -H "Content-Type: application/json" `
  -d '{"venueName":"LMAX","timeframe":1,"startUtc":"2026-04-29T09:15:00Z","endUtc":"2026-04-29T09:30:00Z"}'

curl "http://localhost:5050/market-data/bars?instrument=EURUSD&venue=LMAX&timeframe=FifteenMinutes"
```

## LMAX EOD Reports

The platform can import the actual local LMAX EOD report schemas without connecting to LMAX:

- `individual-trades.csv`: primary execution source of truth, including `Execution ID`, `Order ID`, `Instruction ID`, `Trade UTI`, quantities, price, commission, notional, and account id
- `trades.csv`: order/trade rollup control report; it has no execution id, order id, instruction id, or UTI, so it is reconciled by conservative totals and per-symbol totals
- `currency-wallets.csv`: wallet/cash/PnL by currency, not an instrument position report

Parsing uses invariant-culture decimals. `individual-trades.csv` timestamps use `dd-MM-yyyy HH:mm:ss.fff`; trade dates use `dd-MM-yyyy`; `trades.csv` uses `M/d/yyyy HH:mm`. `LmaxEodReports:TimestampTimeZone` defaults to `UTC`; local machine timezone is not assumed.

LMAX slash symbols are resolved through `InstrumentAlias` rows with source `LMAX_REPORT`. The LocalDB reference seed includes the received report aliases for `AUD/USD`, `EUR/USD`, `GBP/USD`, `NZD/USD`, `USD/CAD`, `USD/CHF`, and `USD/JPY`. An instrument can be disabled for trading and still be valid for EOD report import; trading enablement is an execution permission, not a historical-report permission.

Currency wallets convert every wallet/PnL component to USD as `value * Rate to Base CCY`. `TotalNetPnlUsd = TotalProfitLossUsd + TotalCommissionUsd + TotalDividendsUsd + TotalFinancingUsd`. This is broker wallet/PnL summary, not full strategy attribution.

EOD reconciliation compares internal fills against `LmaxIndividualTrades` by `Fill.BrokerExecutionId = Execution ID`. A normal no-fill order is not a break just because it is absent from `individual-trades.csv`; only actual internal fills missing at LMAX, or LMAX executions missing internally, are blocking.

Fake report generation writes actual LMAX-shaped CSVs under `data/lmax-eod/generated`. Mutation modes include dropped/unknown executions, quantity/price/side changes, summary changes, and wallet balance/rate changes for local validation.

Synthetic anonymized parser fixtures live under `tests/fixtures/lmax-eod`. Real LMAX report files should be placed only in ignored local folders such as `data/lmax-eod/incoming` and passed explicitly to import scripts; they must not be committed.

Useful local commands:

```powershell
.\scripts\generate-fake-lmax-eod-report.ps1
.\scripts\import-generated-lmax-eod-report.ps1
.\scripts\run-eod-reconciliation.ps1
.\scripts\get-eod-pnl-summary.ps1
.\scripts\smoke-lmax-eod-local.ps1
```

The API endpoints are under `/lmax-eod/*`, `/eod-reconciliation/*`, and `/eod-pnl/summary`. They are local-only and do not perform external calls.

## LMAX Connectivity Lab

An isolated CLI lab lives at `tools/QQ.Production.Intraday.Lmax.ConnectivityLab`. It is for manual/demo/UAT connectivity exploration only and is not referenced by the API, Worker, Domain, or Application projects.

The lab defaults to disabled, dry-run, no external connections, no order submission, and no live trading. It can print masked configuration, validate public-data/account/FIX settings, and run dry-run command paths without network calls. Real external calls remain skipped unless `AllowExternalConnections=true` is explicitly provided, and order submission remains blocked unless demo/UAT-only gates and explicit confirmation are satisfied. No real LMAX adapter is registered in the main platform.

The lab also has gated Demo FIX logon/logoff smoke commands for Broker FIX Trading and Broker FIX Market Data. Credentials must be supplied through user-secrets or environment variables and are never stored in source-controlled appsettings.

It also includes a read-only Demo FIX market data snapshot smoke command. The command sends `35=V`, parses `35=W`, `35=X`, or `35=Y`, prints bid/ask/mid if available, and does not persist data into the main database or execution workflow.

The isolated lab has validated LMAX Demo FIX market data snapshot retrieval for `EURUSD` using `SecurityId` mode with LMAX instrument id `4001`. This does not change the main runtime: API and Worker remain FakeLmax-only, no orders are submitted, and Demo market data is not persisted into LocalDB.

The LMAX integration strategy is now FIX-only plus EOD files. The lab path is FIX Market Data, FIX Trading read-only recovery, and LMAX EOD report reconciliation. Account REST API discovery remains parked as diagnostic-only code; BasicAuth against `https://account-api.london-demo.lmax.com` returned `401` for likely endpoints, and Account API access is not required for platform operation.

The lab includes read-only FIX Trading recovery tools: `fix-capabilities` scans the LMAX trading dictionary for supported messages, `fix-trade-capture-smoke` sends `35=AD` with a 16-character-or-shorter `568 TradeRequestID` and reads `35=AQ`/`35=AE` or session-level `35=3` rejects, and `fix-order-status-dry-run` builds a sanitized `35=H` request without opening a socket. Accepted `AQ` with `748=0` is treated as a successful zero-report response, not a timeout. The uploaded LMAX dictionary findings support `H`, `8`, `AD`, `AQ`, and `AE`; `AF`, `AN`, and `AP` are treated as unsupported unless future LMAX documentation provides equivalents.

`fix-trade-capture-replay` replays synthetic sanitized `35=AE` messages, normalizes them into lab-only trade capture DTOs, and projects an EOD-like comparison shape. This is comparison-readiness only: no live FIX call, no order submission, and no persistence.

`fix-execution-report-replay` replays synthetic sanitized `35=8` messages, normalizes acknowledgements/rejections/fills/cancels/expiries, and projects conceptual internal order events such as `OrderAck`, `OrderReject`, `Fill`, `PartialFill`, and `CancelAck`. This remains lab-only readiness tooling and is not wired into the main execution engine.

`fix-demo-order-lifecycle` is a controlled LMAX Demo-only `NewOrderSingle` (`35=D`) lab command. Its dry-run script prints a sanitized tiny EURUSD Market IOC demo order without opening a socket. The default LMAX Demo order shape omits `21 HandlInst` because Demo rejected it as an unknown tag. Live demo submission is blocked unless external connections, order submission, `DryRun=false`, explicit confirmation, Demo/UAT host checks, and quantity/notional limits all pass. This remains outside the API/Worker runtime.

`fix-order-status-smoke` is now unparked for explicit recovery cases with a known `ClOrdID`. It is read-only, requires `AllowOrderSubmission=false`, sends `35=H`, parses `35=8` through the lab ExecutionReport normalizer, and reports session-level `35=3` rejects structurally.

`fix-demo-lifecycle-evidence` is a lab-only evidence wrapper for the validated Demo FIX lifecycle. In dry-run it submits nothing. In live Demo mode, after all explicit demo-order gates pass, it opens one FIX Trading session, submits the tiny `35=D`, collects `35=8`, keeps that session logged on, sends read-only `35=H` order-status recovery, computes the trade-capture window after the fill timestamp is known, sends read-only `35=AD`/`35=AE` trade-capture recovery, then logs out once. `ExecType=I` is treated as status-only, not a fill; fill identity comes from `ExecType=F` and matching TradeCapture `ExecID`. Nothing is persisted into the main DB and API/Worker remain FakeLmax-only.

Useful dry-run commands:

```powershell
.\scripts\lmax-lab-print-config.ps1
.\scripts\lmax-lab-fix-dry-run.ps1
.\scripts\lmax-lab-fix-order-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1
.\scripts\lmax-lab-fix-capabilities.ps1
.\scripts\lmax-lab-fix-order-status-dry-run.ps1
.\scripts\lmax-lab-fix-trade-capture-replay.ps1
.\scripts\lmax-lab-fix-execution-report-replay.ps1
.\scripts\lmax-lab-fix-demo-order-dry-run.ps1
.\scripts\lmax-lab-fix-demo-lifecycle-evidence.ps1
.\scripts\lmax-lab-order-dry-run.ps1
```

See [docs/LMAX_CONNECTIVITY_LAB.md](docs/LMAX_CONNECTIVITY_LAB.md) for configuration, safety gates, credential guidance, and open questions for LMAX.

## Known Limitations

- No real LMAX connectivity
- No live broker connectivity
- No live market data connectivity
- No external market data connectivity
- Only FX spot `EURUSD` is actively seeded for local trading; EOD report alias support is currently local/reference-data oriented
- Only one fund and one broker account are seeded
- Only `MarketImmediate` is implemented
- Only fake/local market data is implemented
- Only 15-minute bar building is implemented
- No historical market data import yet
- No file manifest/CSV model-weight ingestion; weights are staged through local DB tables
- Qubes/GA database writer is not implemented yet
- UI is a local operator cockpit only; no authentication or production UI hardening yet
- RDS is not configured
- Real LMAX report acquisition is not implemented; only local file import/generation exists
- No final official instrument position report is available yet; EOD position checks use execution-derived position deltas
- Advanced execution algos are not implemented
- Production/RDS duplicate reference-data remediation is not implemented yet
- Old local databases may contain stale demo model runs from earlier seed behavior; `scripts/reset-local-db.ps1 -SeedDemoData` recreates a clean local database where demo seed contains fake snapshots only.
- NuGet advisory audit currently reports `System.Security.Cryptography.Xml` as a vulnerable transitive package through the SQL Server infrastructure dependency graph. Directly pinning available .NET 10 package versions did not clear the advisory, so this is documented rather than masked with an unstable package workaround.

### LMAX Read-Only Runtime Phase 5B Prototype Boundary

Phase 5B adds a dedicated Demo/manual read-only prototype boundary and scripts, but it remains blocked before any socket/logon attempt because credential resolver hardening is still required. The manual script is:

```powershell
.\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "manual demo read-only prototype check"
```

It prints sanitized diagnostics and rollback instructions, returns blocked, and does not connect, read credentials, submit orders, schedule work, submit to shadow replay, register a gateway, or mutate trading state. Verify the boundary with:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5b-prototype-gate.ps1
```

Phase 5C adds a local credential availability check. It checks only whether required environment labels are present and never prints, stores, returns, or logs credential values:

```powershell
.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck
.\scripts\check-lmax-readonly-runtime-phase5c-credential-gate.ps1
```

Required labels are `LMAX_DEMO_FIX_USERNAME`, `LMAX_DEMO_FIX_PASSWORD`, `LMAX_DEMO_SENDER_COMP_ID`, and `LMAX_DEMO_TARGET_COMP_ID`. Keep values in the local shell/user profile or a future approved user-secret flow only. Do not commit values to appsettings, docs, evidence, reports, or logs.

Phase 5D adds the first isolated manual Demo read-only socket prototype for a single EURUSD / SecurityID `4001` market-data snapshot. It is not registered in API/Worker and keeps `FakeLmaxGateway` as the execution gateway. It requires explicit operator flags and local credential labels:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "manual demo snapshot"
```

Without complete credential labels it returns sanitized `Blocked` output and `externalConnectionAttempted=false`. With complete labels it may attempt the Demo market-data logon/snapshot/logout path, but still submits no orders, starts no scheduler, submits no shadow replay, writes no trading tables, and mutates no trading state.

Phase 5E hardens the prototype failure paths and retry metadata. Missing credentials classify as `BlockedMissingCredentials`; connection/logon/snapshot/logout failures classify as failed-safe statuses. Retry metadata is disabled (`retryEnabled=false`, `retryAllowed=false`, `maxAttempts=1`) and no gate or test makes an external attempt.

Phase 5F adds operator-approved manual result capture. The same script may attempt the Demo EURUSD / SecurityID `4001` market-data snapshot only when the operator deliberately supplies the required flags and local Demo credential labels. It prints planned safety flags, keeps `retryEnabled=false`, writes only sanitized JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/`, and never registers a gateway, starts a scheduler, submits to shadow replay, or mutates trading state. Validate the boundary without a live attempt:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1
```

Phase 5G adds sanitized transport diagnostics after the first operator-approved Demo run logged on and logged out but timed out waiting for a snapshot. Results/artifacts now include request mode, request metadata, message-type counters, response classification, timeout timing, and sanitized session warnings/errors. Optional request modes are `SecurityIdOnly`, `SlashSymbolOnly`, `SymbolOnly`, and `AutoSequence`; all remain read-only market-data only and manual-only. Validate diagnostics without a live attempt:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1
```

Phase 5H hardens LMAX MarketDataRequest compatibility after observed Demo rejects. The safe default is now `SnapshotPlusUpdates` with `SecurityIdOnly`, which sends `263=1`, `48` present, `22=8`, and omits `55`. Known rejected profiles such as `SnapshotOnly` / `263=0`, symbol encodings with tag `55`, and `InternalSymbol` are marked as known-rejected and block locally unless `-AllowKnownRejectedDiagnostics` is explicitly supplied. Validate compatibility without a live attempt:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1
```

Phase 5J adds sanitized MarketData logon/session diagnostics after both runtime and Connectivity Lab reached TCP/TLS but did not confirm FIX Logon. Add `-ShowSanitizedLogonDiagnostics` to the manual script to print presence/length-only credential and comp-id diagnostics, FIX session settings, first inbound message type, sanitized Logout/Reject text, and runtime-vs-lab profile-label comparison. It does not print credential values, comp-id values, or raw sensitive FIX. Validate the diagnostics boundary without a live attempt:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1
```

Phase 5L closes the first successful Demo read-only EURUSD snapshot artifact. Validate the successful sanitized artifact locally:

```powershell
.\scripts\validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

The closure validates Demo logon, EURUSD snapshot, logout, sanitized artifact status, no secret leakage, no order submission, no scheduler, no shadow replay submit, and no trading mutation. It does not enable scheduler, register a gateway, submit to shadow replay, persist live FIX data, or generalize toward orders.

Phase 5M maps the validated successful Demo snapshot artifact into a sanitized `MarketDataOnly` evidence preview using the existing `lmax-fix-lifecycle-evidence-v1` contract. This is preview-only: it validates the artifact first, writes only ignored sanitized preview JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/`, and does not submit to shadow replay or create observations.

```powershell
.\scripts\preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```

The preview contains Demo EURUSD / SecurityID `4001` market data, empty execution/order/trade/reject arrays, `noSensitiveContent=true`, and `redactionStatus=Redacted`. It still adds no order submission, gateway registration, scheduler, shadow replay submit, live FIX persistence, or trading-state mutation.

Phase 5N adds a manual/offline replay dry-run for that `MarketDataOnly` preview. This uses only the existing local shadow replay API or script path; runtime still does not call shadow replay. Expected result is `Completed` with zero observations and unchanged orders/fills/positions:

```powershell
.\scripts\replay-lmax-readonly-demo-snapshot-evidence-preview.ps1 -EvidencePreviewFile .\artifacts\lmax-readonly-runtime-demo-snapshot\evidence-preview\<preview-file>.json
.\scripts\check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1 -EvidencePreviewFile .\artifacts\lmax-readonly-runtime-demo-snapshot\evidence-preview\<preview-file>.json
```

Phase 5O adds a manual repeated-snapshot stability workflow. It is not a scheduler and not polling: the operator must provide `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, `-ConfirmRepeatedManualSnapshots`, a non-empty `-Reason`, and an explicit capped `-AttemptCount` of 1..5. Each planned attempt reuses the existing manual Demo EURUSD snapshot prototype, validates successful artifacts, maps them to `MarketDataOnly` previews, and writes only a sanitized ignored summary under `artifacts/lmax-readonly-runtime-demo-snapshot/stability/`. Evidence replay remains off by default and is available only through the explicit `-ReplayEvidencePreviews` manual flag.

```powershell
.\scripts\check-lmax-readonly-runtime-phase5o-stability-gate.ps1
.\scripts\run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -AttemptCount 3 -DelaySeconds 2 -Reason "Phase 5O operator-approved repeated Demo EURUSD read-only snapshot stability check"
```

Phase 5O adds no scheduler, automatic polling, runtime shadow replay submit, order submission, gateway registration, live FIX persistence, or trading-state mutation. API and Worker remain `FakeLmaxGateway` only.

Phase 5P reviews the operator-run 3/3 stability summary and closes the repeated manual snapshot milestone. It validates the stability summary, referenced snapshot artifacts, and referenced `MarketDataOnly` previews without connecting to LMAX or calling the runtime prototype:

```powershell
.\scripts\review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
.\scripts\check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

The Phase 5P decision is `PASS`, meaning the project is ready to consider a separate Phase 5Q prompt for controlled manual MarketData evidence workflow hardening. It does not authorize scheduler, polling, order submission, gateway registration, runtime shadow replay submit, trading mutation, broader instruments, or production use.

Phase 5Q hardens the complete manual MarketData evidence workflow without adding runtime power. The workflow review script accepts the Phase 5O stability summary or explicit sanitized snapshot artifacts, validates each Phase 5L artifact, confirms or regenerates `MarketDataOnly` previews, validates those previews, and writes a sanitized ignored workflow manifest under `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/`. Replay is off by default; optional replay requires explicit local replay flags and uses only the existing manual local shadow replay path.

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
.\scripts\check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json
```

Expected default decision is `PASS_WITH_WARNINGS` when replay is omitted intentionally. Phase 5Q still adds no scheduler, automatic polling, runtime shadow replay submit, order submission, gateway registration, live FIX persistence, or trading-state mutation. API and Worker remain `FakeLmaxGateway` only.

Phase 5R keeps the same workflow but closes the optional replay warning when the operator explicitly requests local manual replay. Replay requires localhost API availability and `-ReplayEvidencePreviews -ConfirmLocalManualReplay`; the workflow replays each `MarketDataOnly` preview through the existing local `/lmax-shadow/replay` script/API, records replay run ids and zero-observation results in the manifest, and expects `FinalDecision=PASS` when all previews replay as `Completed` with unchanged mutation guards.

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json -ReplayEvidencePreviews -ConfirmLocalManualReplay
.\scripts\check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1 -WorkflowManifestFile .\artifacts\lmax-readonly-runtime-demo-snapshot\workflow\<workflow-manifest>.json
```

Phase 5R still adds no external socket attempt, scheduler, automatic polling, runtime shadow replay submit, order submission, gateway registration, live FIX persistence, or trading-state mutation. API and Worker remain `FakeLmaxGateway` only.

Phase 5S adds the controlled manual workflow release gate. It validates the Phase 5O stability summary, the Phase 5L snapshot artifacts, and the Phase 5M `MarketDataOnly` previews, then writes a fixed sanitized release manifest at `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json`. Replay is optional and still requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay`; skipped replay produces `PASS_WITH_WARNINGS`.

```powershell
.\scripts\run-lmax-readonly-marketdata-manual-workflow-release.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -ConfirmRepeatedManualSnapshots -ReplayEvidencePreviews -ConfirmLocalManualReplay -AttemptCount 3 -DelaySeconds 5 -Reason "Phase 5S manual workflow release test"
.\scripts\check-lmax-readonly-runtime-phase5s-release-gate.ps1
```

If the workflow is stopped or fails, clear any Phase 5S shell variables, verify `/health` still reports `FakeLmaxGateway`, and rerun the Phase 5O and 5S gates. Phase 5S does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, trading mutation, broader instruments, or production use.

Phase 5T freezes that controlled manual workflow as an operator/developer/risk runbook. It adds only documentation and a local gate:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1
```

The freeze gate validates the Phase 5S manifest/report, confirms `PASS` or `PASS_WITH_WARNINGS`, verifies the replay-skipped warning reason when applicable, and checks that runtime still has no scheduler/polling, shadow replay submit, order path, gateway registration, trading mutation, or credential-value exposure. It does not connect to LMAX and does not perform manual replay. The frozen runbook is `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md`.

Phase 5V creates the final local audit pack for the controlled manual Demo MarketData workflow. It gathers the Phase 5O stability summary, sanitized snapshot artifacts, `MarketDataOnly` previews, replay-enabled workflow manifest, replay results, safety confirmations, and gate references into ignored JSON/Markdown reports:

```powershell
.\scripts\build-lmax-readonly-marketdata-workflow-audit-pack.ps1 -StabilitySummaryFile .\artifacts\lmax-readonly-runtime-demo-snapshot\stability\lmax-readonly-demo-snapshot-stability-20260508-144517.json -WorkflowManifestFile .\artifacts\lmax-readonly-runtime-demo-snapshot\workflow\lmax-readonly-marketdata-workflow-20260508-162327.json
.\scripts\check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1 -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\<audit-pack>.json
```

`PASS` means the controlled manual Demo MarketData workflow is validated as an audit package. It still does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, UAT/production use, multi-instrument expansion, or trading-state mutation.

Phase 5W adds the operational signoff over the Phase 5V audit pack. It is local signoff/reporting only and does not run LMAX, snapshots, replay, scheduler, orders, gateway registration, or mutation paths:

```powershell
.\scripts\signoff-lmax-readonly-marketdata-workflow.ps1 `
  -AuditPackFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.json `
  -AuditPackMarkdownFile .\artifacts\lmax-readonly-runtime-demo-snapshot\audit-pack\lmax-readonly-marketdata-workflow-audit-pack-20260508-163430.md `
  -SignoffBy "local-operator" `
  -Role "Operator" `
  -Reason "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
.\scripts\check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1 -SignoffFile .\artifacts\readiness\<signoff-file>.json
```

`PASS` authorizes only recognition that the controlled manual Demo read-only MarketData workflow has been validated. It does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, UAT/production use, multi-instrument expansion, automatic execution, or trading-state mutation. The detailed signoff runbook is `docs/LMAX_READONLY_RUNTIME_OPERATIONAL_SIGNOFF.md`.

Phase 5X exposes that frozen status to operators without adding runtime capability. Use the local script or the read-only API/UI panel:

```powershell
.\scripts\show-lmax-readonly-marketdata-workflow-status.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
.\scripts\check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1 -SignoffFile .\artifacts\readiness\lmax-readonly-marketdata-operational-signoff-20260508-165858.json
```

The endpoint is `GET /lmax-readonly-runtime/marketdata-workflow/status`, and the cockpit panel appears on the LMAX Shadow page as `LMAX Read-Only Demo MarketData Workflow`. This is status/reporting only: no scheduler, polling, runtime replay submit, orders, gateway registration, production/UAT, multi-instrument expansion, or mutation is authorized.

Phase 6A adds the planning boundary after the frozen Phase 5 workflow. It creates `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`, `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`. The recommended next phase is `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run`. Phase 6A adds no runtime capability: no external run, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading-state mutation.

Phase 6B adds the manual additional MarketData instrument allowlist design only. `LmaxReadOnlyInstrumentAllowlist` documents planning candidates GBPUSD, USDJPY, EURGBP, and AUDUSD with Demo-only metadata and confirmation-required SecurityID labels. The allowlist validator requires `MarketDataOnly`, blocks external-run approval, and keeps scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential values, and trading mutation disabled. Run `scripts/check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1`; it writes `artifacts/readiness/phase6b-instrument-allowlist-gate.json` and does not connect to LMAX or replay anything.

Phase 6D adds the local SecurityID discovery manifest for those candidate instruments. `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest` stores placeholder candidate values for GBPUSD, USDJPY, EURGBP, and AUDUSD, and every entry remains `IsApprovedForExternalRun=false`. Run `scripts/check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1`; it writes `artifacts/readiness/phase6d-securityid-discovery-gate.json` and does not connect to LMAX, call external APIs, run snapshots, replay evidence, schedule/poll, submit orders, register a gateway, expose credentials, or mutate trading state.

Phase 6E adds the SecurityID source evidence review process before any Phase 6D placeholder can be replaced by an accepted planning value. `LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator` requires allowlisted symbols, source references, reviewer metadata for accepted records, High/Confirmed confidence, no sensitive content, and `IsApprovedForExternalRun=false`. The default state is `NeedsMoreEvidence`, so `scripts/check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1` currently writes `artifacts/readiness/phase6e-securityid-evidence-review-gate.json` with `PASS_WITH_KNOWN_WARNINGS`. It does not authorize external runs.

Phase 6F adds the local SecurityID confirmation record workflow. Use `scripts/new-lmax-readonly-securityid-confirmation-record.ps1` to write sanitized planning records under ignored artifacts, `scripts/review-lmax-readonly-securityid-confirmation-records.ps1` to summarize them, and `scripts/check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1` for the phase gate. Missing records are a known warning; accepted records still keep `IsApprovedForExternalRun=false` and do not authorize external runs, snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation.

Phase 6G hardens that record-entry workflow. `scripts/new-lmax-readonly-securityid-confirmation-record-template.ps1 -Symbol All -Force` generates ignored per-symbol templates, creation supports `-WhatIfPreview`, and `scripts/check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1` validates the workflow without requiring real records. Current expected state is `PASS_WITH_KNOWN_WARNINGS` while accepted records are missing.

Phase 6H implements local-only entry and review for real, trusted, sanitized SecurityID confirmation records under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`. Use `scripts/new-lmax-readonly-securityid-confirmation-record.ps1 -WhatIfPreview` before writing, then create records only from operator-approved evidence; `scripts/review-lmax-readonly-securityid-confirmation-records.ps1` reviews the real directory by default and `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1` writes `artifacts/readiness/phase6h-real-confirmation-records-gate.json`. `PASS` means all four candidates have valid `AcceptedForPlanning` records; `PASS_WITH_KNOWN_WARNINGS` means records remain missing/pending but safe; `FAIL` means unsafe, conflicting, sensitive, or externally approved content. `AcceptedForPlanning` is planning-only: `IsApprovedForExternalRun=false` remains mandatory and no snapshot, replay, scheduler, order path, gateway registration, or trading mutation is authorized. Next recommended phase is Phase 6I to apply accepted planning values while still keeping every candidate non-executable, or remain pending evidence if records are missing.

Phase 6I adds a manual Demo-only FIX `SecurityListRequest` discovery path for the four additional Phase 6 candidates. The operator command is `scripts/run-lmax-readonly-runtime-demo-securitylist-discovery.ps1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason "<reason>"`; validation gates never run it automatically. The script sends only SecurityListRequest on the Demo market-data FIX session, writes sanitized planning artifacts under `artifacts/lmax-readonly-runtime-securityid-discovery/`, and records candidate matches/unmatched instruments with `IsApprovedForExternalRun=false`. `scripts/check-lmax-readonly-runtime-phase6i-securitylist-discovery-gate.ps1` is local-only and returns `PASS_WITH_KNOWN_WARNINGS` until a valid discovery artifact exists. Phase 6I adds no market-data snapshot, replay, scheduler/polling, order message, gateway registration, credential exposure, or trading mutation. Next recommended phase is Phase 6J to prepare confirmation records from successful discovery, or Phase 6J security-list failure diagnostics if discovery fails.

Phase 6J adds SecurityList failure diagnostics and request-profile compatibility hardening after the first manual attempt failed safely. `LmaxReadOnlySecurityListDiscoveryArtifactValidator` and `LmaxReadOnlySecurityListFailureDiagnostics` parse sanitized failure artifacts, classify reject details, and verify all safety flags. The manual script now exposes `AllSecurities`, `ProductFx`, `SymbolExact`, `SecurityTypeFx`, `CandidateSymbolsOneByOne`, `MinimalRequest`, `LabCompatibleFallback`, and `AutoSequence` profiles plus `-AllowKnownRejectedDiagnostics`. `AutoSequence` skips known-rejected profiles by default. `scripts/check-lmax-readonly-runtime-phase6j-securitylist-diagnostics-gate.ps1` validates the failed artifact and the profile model without connecting to LMAX. Next recommended phase is Phase 6K for an operator-approved AutoSequence discovery attempt, or VendorSupportConfirmation if LMAX Demo does not support SecurityListRequest.

Phase 6L analyzes the operator-approved Phase 6K AutoSequence failure artifact locally, without another LMAX request. `scripts/review-lmax-readonly-runtime-securitylist-discovery-failure.ps1 -DiscoveryArtifactFile <artifact>` writes `artifacts/readiness/phase6l-securitylist-fallback-decision.json`, reporting attempted profiles, sanitized reject diagnostics if present, unmatched candidates, and a non-authorizing fallback decision. `scripts/check-lmax-readonly-runtime-phase6l-securitylist-fallback-gate.ps1` validates the fallback report and safety boundaries. The Phase 6K artifact had zero candidate matches and no attempt-level reject tag/text, so the recommended fallback is vendor/support or other official manual confirmation; all candidates remain `IsApprovedForExternalRun=false`, and no snapshots, replay, scheduler/polling, orders, gateway registration, or trading mutation are authorized. Next recommended phase is Phase 6M - VendorSupportConfirmation Record Preparation, No External Run.

Phase 6M adds local extraction and record creation from uploaded LMAX instrument CSVs. Use `scripts/new-lmax-readonly-securityid-records-from-instrument-csv.ps1 -InstrumentCsvFile <LMAX-Instruments.csv> -SecondaryCsvFile <LMAX-NewYork-Instruments.csv> -VenueProfileName DemoLondon -CapturedBy "local-operator" -ReviewedBy "local-operator" -ReviewReason "Phase 6M accepted planning values from uploaded LMAX instrument CSVs" -ConfirmPlanningOnly` to create four `AcceptedForPlanning` records. The selected DemoLondon/NewYork IDs are GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007; Tokyo 600x IDs are documented but not selected for the current DemoLondon profile. The records use `OfficialLmaxDocument`, `Confirmed`, and `IsApprovedForExternalRun=false`. This phase is planning-only and adds no external run, SecurityListRequest, snapshot, replay, scheduler/polling, order path, gateway registration, credential exposure, or trading mutation. Next recommended phase is Phase 6N - Apply Accepted SecurityID Planning Values to Planning Manifest, Still IsApprovedForExternalRun=false.

Phase 6N applies those accepted record values to a local planning manifest under `artifacts/lmax-readonly-runtime-securityid-planning/` using `scripts/apply-lmax-readonly-securityid-planning-values.ps1`. The manifest contains GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007 with `securityIdSource=8`, `environmentName=Demo`, `venueProfileName=DemoLondon`, and `IsApprovedForExternalRun=false`. Validate it with `scripts/check-lmax-readonly-runtime-phase6n-planning-values-gate.ps1 -PlanningManifestFile <manifest>`. The manifest is non-executable and authorizes no external run, snapshot, replay, scheduler/polling, orders, gateway registration, or trading mutation. Next recommended phase is Phase 6O - Manual Additional Instrument Snapshot Preflight Design, No External Run, or Phase 6O - Per-Instrument Safety Gate Design, No External Run.

Phase 6O defines the per-instrument safety gate for the additional DemoLondon instruments. Use `scripts/build-lmax-readonly-additional-instrument-safety-gates.ps1 -PlanningManifestFile <phase-6n-manifest>` to write `artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-safety-gates-*.json`, then validate it with `scripts/check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1 -PlanningManifestFile <phase-6n-manifest> -SafetyGateManifestFile <phase-6o-manifest>`. The gate checks GBP/USD=4002, EUR/GBP=4003, USD/JPY=4004, and AUD/USD=4007 with `securityIdSource=8`, Demo/DemoLondon scope, MarketDataOnly intent, no scheduler/polling, no runtime shadow replay submit, no order capability, and no trading mutation. `PASS` means planning data is safe and complete, not executable; `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false` remain mandatory. Next recommended phase is Phase 6P - Manual Additional Instrument Snapshot Preflight Design, No External Run, or fix planning records if Phase 6O fails.

Phase 6P defines the future one-off manual additional-instrument snapshot preflight envelope without authorizing a run. Use `scripts/build-lmax-readonly-additional-instrument-snapshot-preflights.ps1 -PlanningManifestFile <phase-6n-manifest> -SafetyGateManifestFile <phase-6o-manifest> -RequestedByOperatorId "local-operator" -Reason "Phase 6P additional instrument snapshot preflight design"` to create `lmax-readonly-additional-instrument-snapshot-preflights-*.json`, then validate it with `scripts/check-lmax-readonly-runtime-phase6p-additional-snapshot-preflight-gate.ps1`. The preflight profile remains `SnapshotPlusUpdates`, `SecurityIdOnly`, `SecurityIDSource=8`, `MarketDepth=1`, and capped runtime/wait/event limits. `PASS` means the preflight design is safe, not executable: `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory. Next recommended phase is Phase 6Q - Manual Additional Instrument Snapshot Attempt Approval Envelope, No External Run, or Phase 6Q - Single-Instrument Manual Snapshot Dry-Run Report, No External Run.

Phase 6Q adds a non-executable approval envelope for one future single-instrument manual Demo read-only MarketData snapshot attempt. Create an envelope with `scripts/new-lmax-readonly-additional-instrument-snapshot-approval-envelope.ps1 -PreflightManifestFile <phase-6p-manifest> -Symbol GBPUSD -RequestedByOperatorId "local-operator" -ReviewedByOperatorId "local-operator" -Reason "<planning reason>" -Decision AcceptedForPlanning -ConfirmAllPlanningAttestations`, review it with `scripts/review-lmax-readonly-additional-instrument-snapshot-approval-envelopes.ps1`, and gate it with `scripts/check-lmax-readonly-runtime-phase6q-approval-envelope-gate.ps1`. `AcceptedForPlanning` means the envelope is complete for planning only; it does not authorize a run, and `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory. Next recommended phase is Phase 6R - Single-Instrument Manual Snapshot Dry-Run Report, No External Run.

Phase 6R creates a non-executable GBPUSD single-instrument dry-run report from Phase 6N planning, Phase 6O safety gate, Phase 6P preflight, and Phase 6Q approval envelope artifacts. Use `scripts/new-lmax-readonly-additional-instrument-snapshot-dry-run-report.ps1` with those source files, then review with `scripts/review-lmax-readonly-additional-instrument-snapshot-dry-run-reports.ps1` and gate with `scripts/check-lmax-readonly-runtime-phase6r-single-instrument-dryrun-gate.ps1`. `PASS` means report consistency only, not executable authorization; `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, and `IsApprovedForExternalRun=false` remain mandatory. Next recommended phase is Phase 6S - Single-Instrument Manual Snapshot Attempt Gate, Still No External Run, or Phase 6S - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run.

### LMAX Shadow Observation Store

The platform includes a local LMAX shadow replay store for normalized lab evidence. Replay creates auditable observations and optional blocking exception cases without mutating orders, fills, positions, risk, or reconciliation state. This is not live LMAX integration; API and Worker remain FakeLmax-only.

Lifecycle evidence from the isolated Connectivity Lab can now be exported as sanitized JSON and replayed through the local shadow API. Use `scripts/lmax-lab-fix-demo-lifecycle-evidence.ps1 -OutputJsonPath .\artifacts\lmax\evidence.json` to write the lab report, then `scripts/replay-lmax-lab-evidence.ps1 -Path .\artifacts\lmax\evidence.json` while the local API is running. Replay calls localhost only, never opens FIX sessions, and does not contain credentials or raw logon messages. A synthetic fixture smoke is available at `scripts/smoke-lmax-shadow-local.ps1`.

The Connectivity Lab also has a read-only evidence capture path for future LMAX shadow analysis. `scripts/lmax-lab-fix-readonly-evidence-capture.ps1` skips by default; with explicit `-AllowExternalConnections` it can collect only read-only FIX market-data/trade-capture evidence and optional order-status evidence for a supplied `ClOrdID`. It never sends orders, requires order submission to stay disabled, writes sanitized JSON under `artifacts/lmax-lab/evidence/`, and can be replayed locally with `scripts/replay-lmax-lab-evidence-file.ps1`.

Evidence files use schema `lmax-fix-lifecycle-evidence-v1` and are validated before replay. The contract requires replay arrays, `orderStatuses`, normalized TradeCapture dates, explicit null `tradeUti` when absent, and no credential-like content. `scripts/validate-lmax-lab-evidence-file.ps1` validates a file without API or LMAX access.

Read-only evidence coverage now includes empty, market-data-only, TradeCapture-only, OrderStatus-only, protocol-reject-only, mixed read-only, and synthetic lifecycle fixtures. Empty and market-data-only evidence replay with zero observations; OrderStatus `ExecType=I` is status-only; TradeCapture AE is recovery evidence, while EOD files remain the official daily reconciliation source. `scripts/smoke-lmax-evidence-coverage-local.ps1` validates and replays the coverage fixtures through the local shadow API without any live FIX call.

Shadow observations are classified by an explicit policy. DTOs expose policy code, evidence mode, source event type, rationale, suggested operator action, and exception behavior. TradeCapture-only missing internal fills and OrderStatus-only unknown orders are lab/read-only warnings, not blocking mutations. Order-path protocol rejects are blocking and create exception cases; read-only recovery rejects are warnings.

### LMAX Shadow Reader Skeleton

The live shadow reader skeleton is present only as a disabled safety shell. `GET /lmax-shadow-reader/status` reports the configured gates, and `POST /lmax-shadow-reader/run` returns blocked by default. The skeleton has no credential fields, no host/password/user DTOs, no FIX client, no Connectivity Lab dependency, no scheduler auto-run, and no order-submission path.

Default `LmaxShadowReader` settings keep `Enabled=false`, `AllowExternalConnections=false`, `AllowCredentialUse=false`, `ReadOnly=true`, `AllowOrderSubmission=false`, `PersistRawFixMessages=false`, `PersistToTradingTables=false`, and `DryRun=true`. Future activation would require a separate governance/runbook/config gate; today the smoke script `scripts/smoke-lmax-shadow-reader-local.ps1` verifies the reader stays disabled and non-mutating.

Quality Gate #1 validates the skeleton under unsafe config combinations such as order submission enabled, trading-table persistence enabled, non-read-only mode, dry-run disabled, invalid event limits, and contradictory gate settings. Blocked run attempts create sanitized audit events with failed gate names. No exceptions, orders, fills, positions, model runs, risk decisions, or reconciliation state are changed.

Shadow observations now include deterministic fingerprints. Duplicate observations are deduped within a replay run, while repeated replays preserve new run history with the same fingerprints for grouping. Replay summaries show input, unique, duplicate, warning, blocking, and total observation counts. Blocking observations create operator exception cases once per replay/fingerprint; warning observations remain review-only by default.

### Phase 6S - Single-Instrument Manual Snapshot Attempt Gate

Phase 6S is implemented as a local-only, non-executable gate for a future GBPUSD Demo read-only MarketData snapshot attempt. It aggregates the Phase 6N planning manifest, Phase 6O safety gate, Phase 6P preflight, Phase 6Q approval envelope, and Phase 6R dry-run report, and produces a consistency decision only.

`PASS` means the pre-execution planning artifacts are internally consistent for future consideration; it does not authorize an external run. `IsApprovedForExternalRun`, `eligibleForManualSnapshotAttempt`, and `canRunExternalSnapshot` remain `false`. Phase 6S performs no LMAX connection, SecurityListRequest, snapshot, replay, scheduler/polling, order submission, gateway registration, or trading-state mutation.

Next recommended phase: Phase 6T - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run, or Phase 6T - Manual GBPUSD Snapshot Execution Plan / Kill-Rollback Plan, Still No External Run.

### Phase 6T - GBPUSD Manual Snapshot Execution Plan

Phase 6T is implemented as a planning-only execution plan and kill/rollback checklist for a future GBPUSD Demo read-only MarketData snapshot attempt. The plan documents the future command template, abort criteria, rollback steps, and post-run validation requirements, but marks the command `DO NOT RUN IN PHASE 6T`.

The Phase 6T plan does not authorize execution. `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain enforced, with no LMAX connection, snapshot, replay, scheduler/polling, orders, real gateway registration, or trading mutation.

Next recommended phase: Phase 6U - Operator Signoff for One Future GBPUSD Manual Snapshot Attempt, Still No External Run, or Phase 6U - GBPUSD Execution Plan Review / Final Pre-Run Gate, Still No External Run.

### Phase 6U - GBPUSD Operator Signoff

Phase 6U is implemented as a final non-executable operator signoff envelope confirming review of the Phase 6T execution plan and kill/rollback checklist. `SignedForPlanning` means the operator reviewed the plan only; it does not authorize execution.

All run eligibility flags remain false: `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`. Phase 6U performs no LMAX connection, snapshot, replay, scheduler/polling, order submission, gateway registration, or trading-state mutation.

Next recommended phase: Phase 6V - Final Manual GBPUSD Snapshot Execution Readiness Gate, Still No External Run, or Phase 6V - Operator-approved Manual GBPUSD Snapshot Attempt, if and only if a final readiness gate is explicitly passed later.

### Phase 6V - GBPUSD Final Readiness Gate

Phase 6V is implemented as the final non-executable readiness aggregation for the GBPUSD chain. It checks planning, safety, preflight, approval, dry-run, attempt gate, execution plan, Phase 6T gate, operator signoff, and Phase 6U gate artifacts in one readiness artifact.

`PASS` means complete pre-execution readiness only. It does not authorize execution. `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain enforced.

Next recommended phase: Phase 6W - Operator-approved Manual GBPUSD Snapshot Attempt, if and only if the operator explicitly chooses to execute, or stop here with all readiness closed.

### Phase 6W - Operator-Approved Manual GBPUSD Snapshot Attempt

Phase 6W is implemented as a single-attempt, manual-only GBPUSD wrapper around the existing isolated Demo snapshot prototype path. The wrapper hardcodes GBPUSD / GBP/USD, SecurityID `4002`, SecurityIDSource `8`, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`, and requires the Phase 6V final readiness artifact plus explicit operator flags.

The wrapper does not loop or retry, and no scheduler, runtime shadow replay submit, order submission, gateway registration, trading-table persistence, or trading-state mutation is added. Validation/gates remain local-only unless the operator explicitly runs the wrapper command.

Next recommended phase after an operator run: Phase 6X - GBPUSD Snapshot Artifact Review / Evidence Preview Mapping if the snapshot succeeds, or Phase 6X - GBPUSD Snapshot Failure Diagnostics if it fails.

### Phase 6X - GBPUSD Snapshot Artifact Review / Empty Book Diagnostics

Phase 6X is implemented as a local-only review of the first operator-approved GBPUSD Demo read-only snapshot result. The artifact closed as `CompletedWithEmptyBook`: logon succeeded, a MarketDataSnapshot was received, reject counts were zero, and the book contained no entries.

`CompletedWithEmptyBook` is accepted as `PASS_WITH_KNOWN_WARNINGS`, not as an order/reject/mutation and not as authorization for another run. The review and gate scripts do not connect to LMAX, request snapshots, replay evidence, start scheduler/polling, submit to shadow replay, register a gateway, or mutate trading state. Empty-book evidence preview mapping is supported as `MarketDataOnly` with `snapshotReceived=true`, `entryCount=0`, null bid/ask/mid, and a warning.

Next recommended phase: Phase 6Y - Optional second operator-approved GBPUSD snapshot attempt at a different time, or Phase 6Y - GBPUSD EmptyBook Evidence Preview Mapping / Manual Replay.

### Phase 6Y - GBPUSD Market-Hours Retry Preparation

Phase 6Y is implemented as a local-only preparation plan for one future GBPUSD market-hours retry after the Saturday `CompletedWithEmptyBook` result. The empty book is interpreted as expected outside FX market hours: the request reached Demo MarketData and received one empty MarketDataSnapshot with zero rejects, no orders, no credential leakage, and no mutation.

The preparation script writes a non-executable retry readiness artifact and prints the future Phase 6Z command template marked `DO NOT RUN FROM THIS SCRIPT`. It adds no scheduler, polling, background job, automatic retry, runtime shadow replay submit, order surface, gateway registration, or trading mutation.

Next recommended phase: Phase 6Z - Operator-approved GBPUSD Market-Hours Snapshot Attempt, if explicitly run during market hours, or remain paused until market hours.

### Phase 6Z-A - Additional Instruments Planning Pipeline Replication

Phase 6Z-A is implemented as a local-only replication of the non-executable GBPUSD planning chain for EURGBP, USDJPY, and AUDUSD. The pipeline now summarizes GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 with SecurityIDSource `8`, Demo/DemoLondon scope, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1`.

The aggregate pipeline manifest records approval envelope, dry-run, attempt gate, execution plan, operator signoff, and final readiness coverage for all four additional instruments. `PASS` means planning pipeline completeness only. `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` remain mandatory for every instrument.

Phase 6Z-A adds no external run, SecurityListRequest, snapshot, replay, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, trading-table persistence, or trading-state mutation. Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for One Selected Additional Instrument, one instrument at a time only, or stop with all planning closed.

### Phase 6Z-C - Additional Instruments Operator Console Summary

Phase 6Z-C adds read-only operator visibility for the additional-instrument planning pipeline. `GET /lmax-readonly-runtime/additional-instruments/planning-status`, `scripts/show-lmax-readonly-additional-instrument-planning-status.ps1`, and the LMAX Shadow console panel summarize GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007.

The panel shows aggregate `PASS`, `executableCount=0`, per-instrument pipeline decisions, and safety flags. It has no controls to connect, run snapshots, replay, schedule, enter credentials, configure host/port, register gateways, submit orders, or mutate trading state.

Next recommended phase: Phase 6Z-D - Additional Instruments Documentation Pack / Final Planning Freeze, or wait for market hours and explicitly choose a one-instrument Phase 6Z-B manual attempt.

### Phase 6Z-D - Additional Instruments Documentation Pack / Final Planning Freeze

Phase 6Z-D freezes the additional-instrument planning state as documentation and audit evidence. `docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md`, `scripts/build-lmax-readonly-additional-instruments-planning-doc-pack.ps1`, and `scripts/check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1` summarize the GBPUSD/EURGBP/USDJPY/AUDUSD artifact chain and confirm the state is non-executable.

The frozen state remains `executableCount=0`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false` for every additional instrument. Phase 6Z-D does not run LMAX, SecurityListRequest, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, credential exposure, or trading-state mutation.

Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for one selected additional instrument, only when the market is open and the operator explicitly chooses, or stop with planning frozen.

### Phase 6Z-E - Market-Hours Next Action Card

Phase 6Z-E adds read-only operator visibility for the prepared GBPUSD market-hours retry. `GET /lmax-readonly-runtime/market-hours-next-action`, `scripts/show-lmax-readonly-market-hours-next-action.ps1`, and the LMAX Shadow console panel show the next recommended action: wait for market hours, then use a separate manual operator command for one GBPUSD read-only snapshot attempt.

The card records GBPUSD / GBP/USD / SecurityID `4002`, the previous `CompletedWithEmptyBook` result outside market hours, Phase 6V final readiness `PASS`, Phase 6Y retry readiness `PASS`, and Phase 6Z-D planning freeze `PASS`. It is visibility only and adds no UI execution button, replay button, scheduler button, credential field, host/port field, order control, gateway registration, or mutation capability.

Next recommended phase: Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for GBPUSD during market hours, or stop until the market opens.

### Phase 7A - Read-Only Runtime Next Boundary Decision

Phase 7A is implemented as an architecture decision and boundary checklist only. `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`, `docs/LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase7a-next-boundary-gate.ps1` formalize the next safe frontier after the frozen EURUSD workflow and additional-instrument planning pipeline.

The recommended next phase is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. Phase 7A explicitly keeps scheduler/polling, runtime shadow replay submit, order path, production/UAT, real gateway registration, and multi-instrument batch execution rejected for now. It adds no runtime power.

### Phase 7B - Controlled Manual Multi-Instrument Workflow Plan

Phase 7B is implemented as a planning-only workflow plan for future additional-instrument read-only snapshot attempts. It creates a controlled sequence for GBPUSD, EURGBP, USDJPY, and AUDUSD while enforcing one instrument at a time, one attempt per instrument, market-hours only, and retry-by-new-phase.

`PASS` means the workflow plan is safe and complete as documentation/artifact planning only. `executableCount=0`, `batchExecutionAllowed=false`, `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false` remain enforced. Phase 7B adds no LMAX connection, snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

Next recommended phase: Phase 7C - GBPUSD Market-Hours Manual Snapshot Attempt Closure / Evidence Workflow if a future GBPUSD market-hours attempt is run, or wait for market hours.

### Phase 7C - GBPUSD Market-Hours Closure Workflow

Phase 7C adds local-only closure tooling for the next future operator-approved GBPUSD market-hours snapshot result. It does not run GBPUSD. The workflow reviews a supplied sanitized result artifact, classifies `CompletedWithBook`, `CompletedWithEmptyBook`, `FailedSafe`, or `UnsafeFail`, maps safe results to MarketDataOnly evidence preview when appropriate, supports explicit script-only local replay, and builds a closure manifest.

The Phase 7C gate is `scripts/check-lmax-readonly-runtime-phase7c-gbpusd-closure-gate.ps1`. With no market-hours artifact supplied, the expected result is `PASS_WITH_KNOWN_WARNINGS` because the closure workflow is ready but no new GBPUSD result exists. Runtime shadow replay submit remains disabled; API/Worker remain `FakeLmaxGateway`; no scheduler/polling, order surface, gateway registration, external run, automatic replay, or trading mutation is added.

Next recommended phase: wait for market hours and, only if the operator explicitly chooses, run the existing GBPUSD wrapper manually; then use Phase 7C review, evidence preview, optional manual replay, and closure manifest scripts.

### Phase 7D - Post-GBPUSD Next Instrument Decision

Phase 7D adds a local decision framework for choosing what happens after the future GBPUSD market-hours closure. It reads the controlled manual workflow plan and optional GBPUSD Phase 7C closure/review artifacts, then writes a non-executable decision artifact.

Current expected decision, with no GBPUSD market-hours closure artifact supplied, is `PendingGbpusdMarketHoursAttempt`. A future `CompletedWithBook` / `PASS` GBPUSD closure may proceed to `EURGBP` planning. A safe empty-book warning requires a controlled GBPUSD retry phase, and failed-safe or unsafe results block the sequence for diagnostics.

Phase 7D preserves `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, `batchExecutionAllowed=false`, and `executableCount=0`. It adds no external run, snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

### Phase 7E - GBPUSD Market-Hours Execution Checklist Pack

Phase 7E adds the final operator runbook pack for the future GBPUSD market-hours manual snapshot attempt. The checklist document is `docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_EXECUTION_CHECKLIST.md`; the builder is `scripts/build-lmax-readonly-gbpusd-market-hours-execution-checklist-pack.ps1`; the gate is `scripts/check-lmax-readonly-runtime-phase7e-execution-checklist-gate.ps1`.

The pack records the exact future manual command, clearly marked `DO NOT RUN UNTIL MARKET HOURS`, plus pre-run checks, one-attempt/no-retry monitoring, kill switch, post-run Phase 7C closure sequence, Phase 7D decision step, result interpretation, rollback, and non-authorizations.

Phase 7E does not run anything and does not authorize automation. It adds no external connection, snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

### Phase 7E2 - EURGBP Readiness Rehydration

Phase 7E2 rehydrates EURGBP / EUR/GBP / SecurityID `4003` planning readiness after the corrected GBPUSD `CompletedWithBook` / `PASS` closure and Phase 7D `ProceedToEurgbpPlanning` decision. It consumes the frozen multi-instrument pipeline, planning manifest, safety gate manifest, and preflight manifest, then writes an EURGBP-specific readiness artifact.

This phase is planning-only. EURGBP remains `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false`; `executableCount=0`, `batchExecutionAllowed=false`, and one-instrument-at-a-time control remain enforced. No external run, SecurityListRequest, snapshot, replay, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, credential exposure, or trading mutation is authorized.

### Phase 7F2 - EURGBP Execution Checklist / Kill-Rollback Plan

Phase 7F2 adds the EURGBP manual snapshot execution checklist and kill/rollback plan as a planning-only artifact. The checklist consumes the Phase 7E2 EURGBP readiness artifact, preserves the GBPUSD `PASS` / Phase 7D `ProceedToEurgbpPlanning` bridge, and documents the future EURGBP command template as `DO NOT RUN IN PHASE 7F2`.

EURGBP remains `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false`. The checklist keeps `batchExecutionAllowed=false`, `oneInstrumentAtATime=true`, and API/Worker `FakeLmaxGateway` only. Phase 7F2 adds no external run, SecurityListRequest, snapshot, replay, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, or trading mutation.

### Phase 7G2 - EURGBP Final Pre-Run Gate

Phase 7G2 adds the final non-executable pre-run gate before any future operator-approved EURGBP Demo read-only MarketData snapshot attempt. It aggregates the Phase 7D `ProceedToEurgbpPlanning` decision, Phase 7E2 EURGBP readiness `PASS`, and Phase 7F2 execution checklist `PASS`.

`PASS` means pre-run prerequisites are consistent only. It does not authorize execution. EURGBP remains `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false`; one-instrument-at-a-time remains enforced; batch execution remains disabled. Phase 7G2 adds no external run, SecurityListRequest, snapshot, replay, scheduler/polling, runtime shadow replay submit, order surface, gateway registration, or trading mutation.

### Phase 7H - Generic Additional Instrument One-Shot Workflow

Phase 7H adds a reusable manual-only wrapper and local closure workflow for exactly one supported additional MarketData instrument per invocation: GBPUSD `4002`, EURGBP `4003`, USDJPY `4004`, or AUDUSD `4007`. The generic path is still Demo-only, read-only, operator-approved, one-instrument-at-a-time, and no batch/no loop/no retry.

The immediate selected instrument remains EURGBP because Phase 7D returned `ProceedToEurgbpPlanning` and Phase 7G2 is `PASS`. The future manual command is `scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1 -Symbol EURGBP -FinalPreRunGateFile artifacts\lmax-readonly-runtime-securityid-planning\eurgbp-final-prerun\lmax-readonly-eurgbp-final-prerun-gate-20260511-134130.json -AllowExternalConnections -ConfirmDemoReadOnly -Reason "<operator reason>"`.

Phase 7H also adds generic review, evidence preview, optional local replay, closure manifest, and gate scripts. It does not run LMAX automatically, does not authorize scheduler/polling, does not submit to shadow replay from runtime, does not add orders, does not register a gateway, and does not mutate trading state. API/Worker remain `FakeLmaxGateway` only.

### Phase 7H2 - Generic Final Pre-Run Gate Builder

Phase 7H2 adds a generic final pre-run gate builder for supported additional instruments before they are passed to the Phase 7H one-shot wrapper. The builder creates Phase 7H-compatible local safety artifacts for USDJPY `4004` and AUDUSD `4007` from their existing non-executable planning readiness, while preserving one-instrument-at-a-time control.

The Phase 7H wrapper must be given a Phase 7H-compatible final pre-run gate, not a generic Phase 6Z-A final-readiness artifact. Generic final-readiness artifacts remain rejected by wrapper validation. Phase 7H2 does not authorize execution: `externalRunAuthorized=false`, `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, `batchExecutionAllowed=false`, and API/Worker remain `FakeLmaxGateway` only.
