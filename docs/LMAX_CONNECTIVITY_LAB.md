# LMAX Connectivity Lab

The LMAX Connectivity Lab is an isolated command-line tool for manual/demo/UAT investigation. It is not part of the production execution workflow.

The main platform remains local-only:

- `QQ.Production.Intraday.Api` registers `FakeLmaxGateway` only.
- `QQ.Production.Intraday.Worker` registers `FakeLmaxGateway` only.
- `LmaxVenueGateway` is not registered in API or Worker.
- No live trading support is enabled.
- No credentials are stored in repository configuration.

Project path:

```text
tools/QQ.Production.Intraday.Lmax.ConnectivityLab
```

## Safety Defaults

The lab defaults are deliberately inert:

```text
Enabled = false
AllowExternalConnections = false
AllowOrderSubmission = false
AllowLiveTrading = false
DryRun = true
```

`AllowLiveTrading=true` is rejected. Order submission is blocked unless all gates pass:

- `AllowExternalConnections=true`
- `AllowOrderSubmission=true`
- `AllowLiveTrading=false`
- `DryRun=false`
- `EnvironmentName` is `Demo` or `UAT`
- explicit `--confirm-demo-order` flag is present

Even if those gates pass, this version does not include a real LMAX order submission implementation, so the command returns `Skipped`.

## Configuration

Use environment variables, user-secrets, or command-line arguments. Do not put secrets in `appsettings.json`.

Environment variables:

```text
QQ_LMAX_ENVIRONMENT
QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS
QQ_LMAX_ALLOW_ORDER_SUBMISSION
QQ_LMAX_ALLOW_LIVE_TRADING
QQ_LMAX_DRY_RUN
QQ_LMAX_ACCOUNT_API_BASE_URL
QQ_LMAX_PUBLIC_DATA_API_BASE_URL
QQ_LMAX_FIX_ORDER_HOST
QQ_LMAX_FIX_ORDER_PORT
QQ_LMAX_FIX_MARKET_DATA_HOST
QQ_LMAX_FIX_MARKET_DATA_PORT
QQ_LMAX_FIX_SENDER_COMP_ID
QQ_LMAX_FIX_TARGET_COMP_ID
QQ_LMAX_FIX_USERNAME
QQ_LMAX_ACCOUNT_API_KEY
QQ_LMAX_INSTRUMENT_SYMBOL
QQ_LMAX_INSTRUMENT_ID
```

Secrets are masked by `print-config`.

## Commands

```powershell
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- print-config
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- check-public-data-config
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- public-data-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-session-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-market-data-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo
```

The smoke commands skip safely unless `AllowExternalConnections=true` and required config exists. No external calls are made by default.

## Scripts

Run `dotnet restore` and `dotnet build` first; the scripts execute the already-built lab with `--no-build --no-restore` so they do not perform implicit restore/build work or network/package operations.

```powershell
.\scripts\lmax-lab-print-config.ps1
.\scripts\lmax-lab-public-data-smoke.ps1
.\scripts\lmax-lab-account-smoke.ps1
.\scripts\lmax-lab-fix-dry-run.ps1
.\scripts\lmax-lab-order-dry-run.ps1
```

These scripts do not contain secrets and default to no external network calls.

## Instrument Mapping

The default investigation mapping is:

```text
Internal symbol: EURUSD
LMAX slash symbol: EUR/USD
LMAX instrument id: 4001
```

If public/demo market data is later reachable, the lab can be extended to print bid/ask/mid. It does not persist market data to the platform database.

## LMAX Questions

Before real connectivity work:

- Demo/UAT account credentials
- .NET API library availability
- FIX 4.4 credentials
- market data access method
- Account API key and authentication model
- how to retrieve positions
- how to retrieve balances
- how to retrieve open orders
- how to retrieve trade history
- EOD report acquisition method
- certification/conformance requirements before live

## Current Limitations

- No official LMAX client library is wired in.
- No QuickFIX/n session is wired in.
- Public data smoke returns `Skipped` unless a future client is implemented.
- Account API smoke returns `Skipped` unless a future client is implemented.
- Demo order command is gated and returns `Skipped`; it does not submit orders.
- Main API/Worker remain FakeLmax-only.
