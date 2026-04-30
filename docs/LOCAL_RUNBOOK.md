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

Reset the local database:

```powershell
.\scripts\reset-local-db.ps1 -SeedDemoData
```

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

## Run Worker

```powershell
.\scripts\run-worker.ps1
```

Worker bar building is configurable under `MarketDataBars`.

## Smoke Test

Start the API first, then run:

```powershell
.\scripts\smoke-local.ps1 -BaseUrl http://localhost:5000
```

The smoke test calls local API endpoints only. It creates fake EURUSD snapshots, builds 15-minute bars, creates a local model run, processes it through FakeLmax, and queries orders, fills, positions, and reconciliation breaks.

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

## Troubleshooting LocalDB

- If `sqllocaldb` is not found, install SQL Server Express LocalDB.
- If database connection fails, run `sqllocaldb start MSSQLLocalDB`.
- If schema is stale, run `.\scripts\update-local-db.ps1`.
- For a clean development reset, run `.\scripts\reset-local-db.ps1`.

## NuGet Advisory

The vulnerability audit currently reports transitive `System.Security.Cryptography.Xml 9.0.0` advisories through SQL Server/EF infrastructure dependencies. Directly pinning available .NET 10 package versions did not clear the advisory in the current package graph, so it remains documented rather than hidden with an unstable override.
