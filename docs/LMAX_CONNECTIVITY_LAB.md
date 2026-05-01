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

The source-controlled `appsettings.json` contains only non-secret Demo defaults:

```text
FixOrderHost = fix-order.london-demo.lmax.com
FixOrderPort = 443
FixOrderTargetCompId = LMXBD
FixMarketDataHost = fix-marketdata.london-demo.lmax.com
FixMarketDataPort = 443
FixMarketDataTargetCompId = LMXBDM
UseTls = true
```

Store credentials outside the repo:

```powershell
dotnet user-secrets set "LmaxConnectivityLab:FixSenderCompId" "<demo-sender-comp-id>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
dotnet user-secrets set "LmaxConnectivityLab:FixUsername" "<demo-username>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
dotnet user-secrets set "LmaxConnectivityLab:FixPassword" "<demo-password>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
```

Equivalent environment variables are also supported. `FixPassword`, `FixUsername`, `FixSenderCompId`, and API keys are masked by `print-config`.

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
QQ_LMAX_FIX_ORDER_TARGET_COMP_ID
QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID
QQ_LMAX_FIX_TARGET_COMP_ID
QQ_LMAX_FIX_USERNAME
QQ_LMAX_FIX_PASSWORD
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
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-order-logon-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-marketdata-logon-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-marketdata-snapshot-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo
```

The smoke commands skip safely unless `AllowExternalConnections=true` and required config exists. No external calls are made by default.

`fix-order-logon-smoke` and `fix-marketdata-logon-smoke` use a minimal raw FIX 4.4 logon/logoff implementation over TLS. They send only Logon and Logout. They do not submit orders and do not subscribe to market data. QuickFIX/n is not currently added; this keeps the lab dependency-free while still allowing a controlled Demo/UAT logon check.

## Scripts

Run `dotnet restore` and `dotnet build` first; the scripts execute the already-built lab with `--no-build --no-restore` so they do not perform implicit restore/build work or network/package operations.

```powershell
.\scripts\lmax-lab-print-config.ps1
.\scripts\lmax-lab-public-data-smoke.ps1
.\scripts\lmax-lab-account-smoke.ps1
.\scripts\lmax-lab-fix-dry-run.ps1
.\scripts\lmax-lab-fix-order-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1
.\scripts\lmax-lab-order-dry-run.ps1
```

These scripts do not contain secrets and default to no external network calls.

Manual Demo FIX logon smoke, after credentials are configured:

```powershell
.\scripts\lmax-lab-fix-order-logon-smoke.ps1 -AllowExternalConnections
.\scripts\lmax-lab-fix-marketdata-logon-smoke.ps1 -AllowExternalConnections
```

Required safety conditions:

- `AllowExternalConnections=true`
- `EnvironmentName=Demo` or `UAT`
- `AllowLiveTrading=false`
- `AllowOrderSubmission=false`
- required host/port/target/sender/username/password are configured

The commands return `Ok`, `Skipped`, or `Failed` with `SessionType`, `Connected`, `LoggedOn`, `StartedAtUtc`, and `CompletedAtUtc`.

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

## Troubleshooting FIX Logon

- TLS/SSL: Demo endpoints use port 443 with TLS enabled.
- SenderCompID: must match the LMAX Demo value supplied for the account.
- TargetCompID: use `LMXBD` for Broker FIX Trading and `LMXBDM` for Broker FIX Market Data.
- Username/password: keep them in user-secrets or environment variables only.
- Firewall/proxy: outbound TLS to the Demo hosts must be allowed.
- Sequence numbers: the lab sends sequence number `1` with `141=Y` for reset on logon.
- Timeout: increase `LmaxConnectivityLab:RequestTimeoutSeconds` if the session is slow to respond.

After logon works, next safe steps are read-only market data snapshot investigation, account/position API discovery, and only later a demo order lifecycle under a separate explicit approval.

## Current Limitations

- No official LMAX client library is wired in.
- QuickFIX/n is not wired in; FIX logon smoke uses a minimal raw Logon/Logout client.
- Public data smoke returns `Skipped` unless a future client is implemented.
- Account API smoke returns `Skipped` unless a future client is implemented.
- Demo order command is gated and returns `Skipped`; it does not submit orders.
- Main API/Worker remain FakeLmax-only.
