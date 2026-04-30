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

## EF Migrations

The LocalDB schema migration is `InitialLocalSqlServerSchema`.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/QQ.Production.Intraday.Infrastructure.SqlServer --startup-project src/QQ.Production.Intraday.Api
```

The reference seed is idempotent and includes the fund, account, LMAX venue metadata, EURUSD, venue mapping, NAV, conservative risk configuration, trading window, kill switch, start-of-day position, and seed market data. Demo seed data is opt-in and adds fake snapshots plus a sample model run.

## Scripts

- `scripts/check-env.ps1`
- `scripts/restore-build-test.ps1`
- `scripts/update-local-db.ps1`
- `scripts/reset-local-db.ps1`
- `scripts/run-api.ps1`
- `scripts/run-worker.ps1`
- `scripts/smoke-local.ps1`

See [docs/LOCAL_RUNBOOK.md](docs/LOCAL_RUNBOOK.md) for the full local workflow.

## Run API

```powershell
.\scripts\run-api.ps1
```

Endpoints include:

- `GET /health`
- `GET /model-runs`
- `POST /model-runs`
- `POST /model-runs/{id}/process`
- `GET /positions/internal`
- `GET /positions/broker`
- `GET /reconciliation/breaks`
- `GET /trade-intents`
- `GET /orders`
- `GET /fills`
- `POST /admin/kill-switch`
- `POST /admin/kill-switch/clear`

`GET /health` reports application name, environment, persistence provider, database reachability, pending migrations count, execution gateway, market data mode, live trading flag, external connections flag, and UTC server time. It does not expose secrets.

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
curl -X POST http://localhost:5000/market-data/fake-snapshots `
  -H "Content-Type: application/json" `
  -d '{"instrumentSymbol":"EURUSD","venueName":"LMAX","startUtc":"2026-04-29T09:15:00Z","intervalSeconds":60,"count":15,"bid":1.1000,"ask":1.1002,"bidStep":0.00001,"askStep":0.00001}'

curl -X POST http://localhost:5000/market-data/build-bars `
  -H "Content-Type: application/json" `
  -d '{"venueName":"LMAX","timeframe":1,"startUtc":"2026-04-29T09:15:00Z","endUtc":"2026-04-29T09:30:00Z"}'

curl "http://localhost:5000/market-data/bars?instrument=EURUSD&venue=LMAX&timeframe=FifteenMinutes"
```

## Known Limitations

- No real LMAX connectivity
- No live broker connectivity
- No live market data connectivity
- No external market data connectivity
- No UI yet
- Only FX spot `EURUSD` is seeded
- Only one fund and one broker account are seeded
- Only `MarketImmediate` is implemented
- Only fake/local market data is implemented
- Only 15-minute bar building is implemented
- No historical market data import yet
- RDS is not configured
- EOD LMAX report import is not implemented
- Advanced execution algos are not implemented
- NuGet advisory audit currently reports `System.Security.Cryptography.Xml` as a vulnerable transitive package through the SQL Server infrastructure dependency graph. Directly pinning available .NET 10 package versions did not clear the advisory, so this is documented rather than masked with an unstable package workaround.
