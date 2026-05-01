# QQ.Production.Intraday

Local simulator foundation for `QQ.Production.Intraday`, an institutional PMS/OMS/EMS-style intraday execution platform. This first version is intentionally local-only: it does not connect to LMAX, cannot send live orders, and registers only `FakeLmaxGateway`.

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

The `QQ.Production.Intraday.Infrastructure.Lmax` project contains only a placeholder `LmaxVenueGateway` that throws `NotImplementedException` and is not registered by the API or Worker.

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
- `scripts/run-ui.ps1`
- `scripts/run-local-stack.ps1`

See [docs/LOCAL_RUNBOOK.md](docs/LOCAL_RUNBOOK.md) for the full local workflow.

The smoke script uses dynamic UTC timestamps: it builds the previous completed 15-minute bar, adds fresh fake snapshots for execution freshness, creates a current model run, and processes it through `FakeLmaxGateway`. If a local API call fails, the script prints the endpoint, safe request body, HTTP status, and response body.

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

Development CORS allows only `http://localhost:5173` and `http://127.0.0.1:5173`; wildcard CORS is not enabled for production-like environments.

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
