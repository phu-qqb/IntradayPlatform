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
.\scripts\lmax-lab-account-smoke.ps1
.\scripts\lmax-lab-fix-dry-run.ps1
.\scripts\lmax-lab-fix-order-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1
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
