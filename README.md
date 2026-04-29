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
dotnet restore QQ.Production.Intraday.sln --configfile NuGet.Config
dotnet build QQ.Production.Intraday.sln --no-restore
dotnet test QQ.Production.Intraday.sln --no-build
dotnet list QQ.Production.Intraday.sln package --vulnerable --include-transitive
```

## Run API

```powershell
dotnet run --project src/QQ.Production.Intraday.Api/QQ.Production.Intraday.Api.csproj
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

## Run Worker

```powershell
dotnet run --project src/QQ.Production.Intraday.Worker/QQ.Production.Intraday.Worker.csproj
```

The default poll interval is 15 minutes. Development configuration can process immediately on startup.

## LocalDB

Default connection string:

```text
Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

If EF migrations are added, run them against the SQL Server infrastructure project:

```powershell
dotnet ef migrations add InitialCreate --project src/QQ.Production.Intraday.Infrastructure.SqlServer --startup-project src/QQ.Production.Intraday.Api
dotnet ef database update --project src/QQ.Production.Intraday.Infrastructure.SqlServer --startup-project src/QQ.Production.Intraday.Api
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

## Known Limitations

- No real LMAX connectivity
- No live broker connectivity
- No EF migrations yet
- No UI yet
- Only FX spot `EURUSD` is seeded
- Only one fund and one broker account are seeded
- Only `MarketImmediate` is implemented
- NuGet advisory audit currently reports `System.Security.Cryptography.Xml` as a vulnerable transitive package through the SQL Server infrastructure dependency graph. Directly pinning available .NET 10 package versions did not clear the advisory, so this is documented rather than masked with an unstable package workaround.
