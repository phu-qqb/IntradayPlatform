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
QQ_LMAX_SLASH_SYMBOL
QQ_LMAX_FIX_SECURITY_ID_SOURCE
QQ_LMAX_MARKET_DEPTH
QQ_LMAX_MARKET_DATA_REQUEST_MODE
QQ_LMAX_CONNECT_TIMEOUT_SECONDS
QQ_LMAX_LOGON_TIMEOUT_SECONDS
QQ_LMAX_MARKET_DATA_MAX_WAIT_SECONDS
QQ_LMAX_MARKET_DATA_MAX_MESSAGES
QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE
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

`fix-marketdata-snapshot-smoke` logs on to the market data session, sends a read-only `MarketDataRequest` (`35=V`), parses `MarketDataSnapshotFullRefresh` (`35=W`), `MarketDataIncrementalRefresh` (`35=X`), or `MarketDataRequestReject` (`35=Y`), prints bid/ask/mid if received, then unsubscribes when needed and logs out. It does not persist data to LocalDB and is not integrated with the execution workflow.

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

Manual read-only market data snapshot smoke:

```powershell
.\scripts\lmax-lab-fix-marketdata-snapshot-smoke.ps1 -AllowExternalConnections -Instrument EURUSD -LmaxInstrumentId 4001 -SlashSymbol "EUR/USD" -MarketDepth 1 -RequestMode Auto -SymbolEncodingMode SecurityId -MaxWaitSeconds 15
```

Required safety conditions:

- `AllowExternalConnections=true`
- `EnvironmentName=Demo` or `UAT`
- `AllowLiveTrading=false`
- `AllowOrderSubmission=false`
- required host/port/target/sender/username/password are configured

The commands return `Ok`, `Skipped`, or `Failed` with `SessionType`, `Connected`, `LoggedOn`, `StartedAtUtc`, and `CompletedAtUtc`.

The market data snapshot command returns `Connected`, `LoggedOn`, `RequestSent`, `RequestRejected`, reject reason/text when present, message count, entries, `BestBid`, `BestAsk`, and `Mid`.

It also reports phase-level diagnostics: `TcpConnected`, `TlsHandshakeCompleted`, `FixLogonSent`, `FixLoggedOn`, `MarketDataRequestSent`, `MarketDataSnapshotReceived`, `MarketDataRejectReceived`, `LogoutSent`, `LastReceivedMsgType`, safe host/port/target/sender details, and separate connect/logon/market-data wait timeouts. Passwords are never printed.

Use `-ShowFixMessages` to print sanitized lab-only FIX wire diagnostics. SOH is rendered as `|`, `49` is masked, and credential tags such as `553` and `554` are removed. The trace includes the outgoing `35=V` MarketDataRequest, request group diagnostics (`262`, `263`, `264`, `267/269`, `146`, `48`, `55`, `22`), received message types, rejects, and bid/ask entries where present.

`RequestMode=Auto` tries `SnapshotPlusUpdates` first and then `SnapshotOnly`. Each attempt opens a clean market-data FIX session, logs on, sends one request, waits for `35=W`, `35=X`, `35=Y`, reject, logout, or timeout, then logs out and closes the socket. No data is persisted.

## Validated Demo Market Data Smoke

The isolated lab has successfully validated a read-only LMAX Demo FIX market data snapshot for:

```text
Internal symbol: EURUSD
LMAX instrument id: 4001
SymbolEncodingMode: SecurityId
MsgType received: 35=W MarketDataSnapshotFullRefresh
```

Observed safe output shape:

```text
Command: fix-marketdata-snapshot-smoke
Status: Ok
Connected: True
LoggedOn: True
MarketDataRequestSent: True
MarketDataSnapshotReceived: True
LastReceivedMsgType: W
BestBid: 1.17361
BestAsk: 1.17368
Mid: 1.173645
Entry: Type=0 Price=1.17361 Size=50 SecurityId=4001
Entry: Type=1 Price=1.17368 Size=200 SecurityId=4001
LogoutSent: True
```

No order was submitted, no market data was persisted to LocalDB, and no main platform service consumed the Demo data. This remains isolated from `QQ.Production.Intraday.Api` and `QQ.Production.Intraday.Worker`, which continue to register `FakeLmaxGateway` only.

Symbol encoding modes:

- `SecurityId`: sends `48=<LmaxInstrumentId>` and optional `22=<FixSecurityIdSource>`
- `SecurityIdNoIdSource`: sends `48=<LmaxInstrumentId>` without `22`
- `SecurityIdAndSymbolWithIdSource`: sends `48=<LmaxInstrumentId>`, `22=<FixSecurityIdSource>`, and `55=EUR/USD`
- `SecurityIdAndSymbolNoIdSource`: sends `48=<LmaxInstrumentId>` and `55=EUR/USD` without `22`
- `SlashSymbol`: sends `55=EUR/USD`
- `InternalSymbol`: sends `55=EURUSD`
- `SecurityIdAndSymbol`: sends `48=<LmaxInstrumentId>`, `55=EUR/USD`, and optional `22=<FixSecurityIdSource>`
- `Auto`: tries `SecurityIdAndSymbolNoIdSource`, `SecurityIdNoIdSource`, `SlashSymbol`, `SecurityIdAndSymbolWithIdSource`, `SecurityId`, then `InternalSymbol`, reporting each attempt

`FixSecurityIdSource` defaults to `8`. If LMAX rejects this, change the setting and rerun the smoke. The reject text is printed instead of being hidden.

## Instrument Mapping

The default investigation mapping is:

```text
Internal symbol: EURUSD
LMAX slash symbol: EUR/USD
LMAX instrument id: 4001
```

If public/demo market data is later reachable, the lab can be extended to print bid/ask/mid. It does not persist market data to the platform database.

The read-only snapshot smoke now prints bid/ask/mid when LMAX returns them. It still does not persist market data to the platform database.

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
- MarketDataRequestReject: inspect `RejectReason` and `RejectText`; try a different symbol encoding mode if the symbol is unknown.
- Unknown symbol: confirm `InstrumentSymbol`, `LmaxInstrumentId`, `LmaxSlashSymbol`, and `FixSecurityIdSource`.
- Insufficient permissions: confirm the Demo account is enabled for FIX market data.
- Unsupported market depth: retry with `-MarketDepth 1`.
- Timeout after logon: use `-RequestMode Auto -SymbolEncodingMode Auto -ShowFixMessages`, increase `-MaxWaitSeconds`, and inspect whether LMAX sends heartbeats, test requests, rejects, or no market-data response.
- TestRequest/heartbeat: the lab responds to `35=1` TestRequest with a `35=0` Heartbeat and ignores ordinary `35=0` Heartbeat while waiting for market data.

After logon works, next safe steps are read-only market data snapshot investigation, account/position API discovery, and only later a demo order lifecycle under a separate explicit approval.

## Current Limitations

- No official LMAX client library is wired in.
- QuickFIX/n is not wired in; FIX logon and market-data snapshot smoke use a minimal raw FIX client.
- Public data smoke returns `Skipped` unless a future client is implemented.
- Account API smoke returns `Skipped` unless a future client is implemented.
- Demo order command is gated and returns `Skipped`; it does not submit orders.
- Main API/Worker remain FakeLmax-only.
