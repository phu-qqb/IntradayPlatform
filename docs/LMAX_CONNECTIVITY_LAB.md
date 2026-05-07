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

## FIX-Only Integration Path

The platform integration strategy is now FIX-first and EOD-file based:

- FIX Market Data for read-only live/demo market data investigation.
- FIX Trading for logon, read-only order/trade recovery requests, and future carefully gated trading work.
- LMAX EOD files as the official daily reconciliation source.

The Account REST API path is parked. The Account API lab commands remain in the repo as isolated diagnostics only, but they are no longer required for platform operation. BasicAuth against `https://account-api.london-demo.lmax.com` returned `401 Unauthorized` for likely discovery endpoints during exploration, so do not treat Account API availability as a dependency for this platform. No Account API command persists data or feeds the main runtime.

The lab validation feeds the dormant adapter design gate documented in `docs/LMAX_ADAPTER_DESIGN.md`. That design gate defines contracts, normalized DTOs, safety options, and shadow-mode comparison helpers only. It does not register LMAX connectivity in the API or Worker.

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
QQ_LMAX_ACCOUNT_API_AUTH_MODE
QQ_LMAX_ACCOUNT_API_USERNAME
QQ_LMAX_ACCOUNT_API_PASSWORD
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
QQ_LMAX_ACCOUNT_API_KEY_HEADER_NAME
QQ_LMAX_ACCOUNT_API_BEARER_TOKEN
QQ_LMAX_ACCOUNT_API_REQUEST_TIMEOUT_SECONDS
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
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-discover
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-positions-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-balances-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-open-orders-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- account-api-trade-history-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-session-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-market-data-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-order-logon-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-marketdata-logon-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-marketdata-snapshot-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-capabilities
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-trade-capture-smoke
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- fix-order-status-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo-dry-run
dotnet run --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab -- order-lifecycle-demo
```

The smoke commands skip safely unless `AllowExternalConnections=true` and required config exists. No external calls are made by default.

`fix-order-logon-smoke` and `fix-marketdata-logon-smoke` use a minimal raw FIX 4.4 logon/logoff implementation over TLS. They send only Logon and Logout. They do not submit orders and do not subscribe to market data. QuickFIX/n is not currently added; this keeps the lab dependency-free while still allowing a controlled Demo/UAT logon check.

`fix-marketdata-snapshot-smoke` logs on to the market data session, sends a read-only `MarketDataRequest` (`35=V`), parses `MarketDataSnapshotFullRefresh` (`35=W`), `MarketDataIncrementalRefresh` (`35=X`), or `MarketDataRequestReject` (`35=Y`), prints bid/ask/mid if received, then unsubscribes when needed and logs out. It does not persist data to LocalDB and is not integrated with the execution workflow.

## FIX Trading Read-Only Recovery

`fix-capabilities` scans `brokerFixTradingGateway-QuickFix-DataDictionary.xml` if that dictionary is present anywhere under the repo. If it is missing, the command returns `Skipped` and explains where to place it. The scanner performs no network calls.

The capability scanner reports:

- `OrderStatusRequest` (`35=H`)
- `ExecutionReport` (`35=8`)
- `TradeCaptureReportRequest` (`35=AD`)
- `TradeCaptureReportRequestAck` (`35=AQ`)
- `TradeCaptureReport` (`35=AE`)
- `OrderMassStatusRequest` (`35=AF`)
- `RequestForPositions` (`35=AN`)
- `PositionReport` (`35=AP`)

Uploaded LMAX package findings indicate that the trading dictionary supports `H`, `8`, `AD`, `AQ`, and `AE`, but does not include `AF`, `AN`, or `AP`. The lab therefore does not invent mass-status or position-report requests. If a future LMAX package adds custom equivalents, the scanner will make that visible before any command is implemented.

`fix-trade-capture-smoke` is a read-only FIX Trading command. It logs on, sends `TradeCaptureReportRequest` (`35=AD`) for a recent UTC window, reads `TradeCaptureReportRequestAck` (`35=AQ`) and zero or more `TradeCaptureReport` (`35=AE`) messages, then logs out. It submits no orders and persists nothing into LocalDB. An accepted ack with zero reports and `LastRptRequested=true` is a valid success.

The AD request uses a unique `568 TradeRequestID`, `569=1`, `263=0`, optional `1 Account`, and the LMAX date group shape with `580=2` plus two `60 TransactTime` values for start and end UTC. LMAX Demo enforces a maximum length of 16 characters for tag `568`, so the lab generates IDs as `TCyyMMddHHmmssNN`, for example `TC26050516395101`. The lab validates the ID locally before sending so malformed requests are not written to the session. Parsed report fields include `17 ExecID`, `527 SecondaryExecID`, `48 SecurityID`, `22 SecurityIDSource`, `55 Symbol`, `32 LastQty`, `31 LastPx`, `75 TradeDate`, `60 TransactTime`, optional `54 Side`, optional `1 Account`, and `912 LastRptRequested`.

The `AQ` ack parser reads `568 TradeRequestID`, `569 TradeRequestType`, `263 SubscriptionRequestType`, `748 TotNumTradeReports`, `749 TradeRequestResult`, `750 TradeRequestStatus`, and optional `58 Text`. If LMAX sends accepted ack fields `749=0` and `750=0` with `748=0`, the smoke completes successfully immediately with `ExpectedTradeReportCount=0`, `NoMoreReports=True`, and a message that no trade reports were returned for the requested window. If `748>0`, the lab waits for `AE` reports until the expected count, `912=Y`, `MaxReports`, or timeout. If `748` is missing, the lab waits for `AE`/`912=Y` or timeout and reports that the ack omitted `TotNumTradeReports`.

Session-level rejects (`35=3`) are parsed explicitly. If LMAX rejects an `AD` request, the result reports `RequestRejected=True`, `LastReceivedMsgType=3`, `RejectRefTagId`, `RejectRefMsgType`, `RejectReasonCode`, and `RejectText` instead of mislabeling the outcome as a timeout.

`fix-order-status-dry-run` builds a read-only `OrderStatusRequest` (`35=H`) and prints a sanitized FIX message. It does not open a socket. `fix-order-status-smoke` is implemented for explicit recovery scenarios where a known `ClOrdID` exists. `fix-order-mass-status-smoke` and `fix-position-report-smoke` return `Skipped` when the dictionary does not support those messages.

## FIX OrderStatusRequest Smoke

`fix-order-status-smoke` is read-only recovery tooling. It connects to the FIX Trading session, logs on, sends `OrderStatusRequest` (`35=H`), waits for `ExecutionReport` (`35=8`) or session reject (`35=3`), logs out, and prints normalized execution-report details. It submits no orders and persists nothing.

The command requires:

- `AllowExternalConnections=true`
- `EnvironmentName=Demo` or `UAT`
- `AllowLiveTrading=false`
- `AllowOrderSubmission=false`
- explicit `ClOrdID`
- LMAX demo/UAT FIX Trading host

Example using the validated tiny Demo order:

```powershell
.\scripts\lmax-lab-fix-order-status-smoke.ps1 `
  -AllowExternalConnections `
  -ClOrdID "DL26050607454402" `
  -LmaxInstrumentId 4001 `
  -Side Buy `
  -ShowFixMessages
```

The request sends only read-only recovery fields:

- `35=H`
- `11 ClOrdID`
- optional `48 SecurityID`
- optional `22 SecurityIDSource`, defaulting to `8` when `SecurityID` is supplied
- optional `54 Side`
- optional `1 Account`
- optional `790 OrdStatusReqID`

Troubleshooting:

- `35=3` session reject: inspect `RejectRefTagId`, `RejectRefMsgType`, and `RejectText`. The lab reports this as a structured reject, not a timeout.
- Timeout: confirm the `ClOrdID`, session target, and whether the order still exists in the Demo session recovery window.
- Logout before response: inspect `RejectText` / logout text and sequence-number state.
- No credentials are printed; password tags are stripped from diagnostics.

## FIX ExecutionReport Normalization

The lab can replay and normalize synthetic `ExecutionReport` (`35=8`) messages before any demo order lifecycle work. This is readiness tooling only: it does not submit orders, does not persist data, and does not wire execution reports into the main API/Worker runtime.

The parser extracts:

- `17 ExecID`
- `37 OrderID`
- `11 ClOrdID`
- `41 OrigClOrdID`
- `150 ExecType`
- `39 OrdStatus`
- `48 SecurityID`
- `22 SecurityIDSource`
- `55 Symbol`
- `54 Side`
- `38 OrderQty`
- `151 LeavesQty`
- `14 CumQty`
- `32 LastQty`
- `31 LastPx`
- `6 AvgPx`
- `44 Price`
- `99 StopPx`
- `59 TimeInForce`
- `40 OrdType`
- `60 TransactTime`
- `58 Text`
- `1 Account`

Common FIX values are normalized for operator readability. `ExecType` values such as `0 New`, `F Trade`, `8 Rejected`, `4 Canceled`, `C Expired`, `6 PendingCancel`, and `I OrderStatus` are mapped to named values. `OrdStatus` values such as `0 New`, `1 PartiallyFilled`, `2 Filled`, `4 Canceled`, `8 Rejected`, and `C Expired` are also mapped. Side `54=1` maps to `Buy`; `54=2` maps to `Sell`. Order type `40=1/2/3/4` maps to `Market`, `Limit`, `Stop`, and `StopLimit`. Time-in-force `59=0/1/3/4` maps to `Day`, `GTC`, `IOC`, and `FOK`.

Unknown enum values are preserved through the raw value fields and reported as warnings. Malformed decimal quantities/prices or malformed timestamps produce warnings rather than crashing the replay command. `SecurityID=4001` maps to `EURUSD` in the same lab-only readiness style used by trade capture normalization.

The lab also projects each normalized execution report into a conceptual internal order event:

- `OrderAck`
- `OrderReject`
- `Fill`
- `PartialFill`
- `CancelAck`
- `Expired`
- `Unknown`

This projection is for comparison and design readiness only. It is not persisted and is not consumed by `ProcessModelRunService` or the main execution engine.

Replay synthetic sanitized fixtures with:

```powershell
.\scripts\lmax-lab-fix-execution-report-replay.ps1
```

The replay fixture covers new acknowledgement, rejected order, full fill, partial fill, cancelled, expired, pending cancel, order-status response, and malformed value warning cases. It makes no network call.

## Controlled Demo Order Lifecycle Lab

The lab includes a controlled `NewOrderSingle` (`35=D`) demo lifecycle command for future LMAX Demo validation. This is lab-only and is not wired into the API, Worker, `ProcessModelRunService`, OMS UI controls, or any main trading workflow.

Start with the dry-run:

```powershell
.\scripts\lmax-lab-fix-demo-order-dry-run.ps1
```

Dry-run builds and prints a sanitized `35=D` message only. It opens no socket, submits no order, and persists nothing.

The default demo request is deliberately small:

- `EURUSD`
- `SecurityID=4001`
- `Market`
- `IOC`
- `VenueQuantity=0.1`
- `MaxNotionalUsd=5000`
- `DryRun=true`
- `ConfirmDemoOrder=false`

The `NewOrderSingle` builder sends:

- `35=D`
- `11 ClOrdID`
- `48 SecurityID`
- `22 SecurityIDSource` when configured, normally `8`
- `55 Symbol`
- `54 Side`, `1 Buy` or `2 Sell`
- `60 TransactTime`
- `38 OrderQty`
- `40 OrdType`, `1 Market` or `2 Limit`
- `44 Price` for limit orders
- `59 TimeInForce`, `3 IOC` or `4 FOK`
- `1 Account` when provided

LMAX Demo rejected `21 HandlInst` with a session-level `UnknownTag` reject (`35=3`, `371=21`, `372=D`), so the lab does not send tag `21` by default. A diagnostic `--include-handl-inst` option exists only for dictionary experiments and should not be used for the validated Demo path.

Live demo submission is blocked unless every gate passes:

- `EnvironmentName` is `Demo` or `UAT`
- `AllowExternalConnections=true`
- `AllowOrderSubmission=true`
- `AllowLiveTrading=false`
- `DryRun=false`
- `ConfirmDemoOrder=true`
- the command includes `--confirm-demo-order`
- FIX order host is an LMAX demo/UAT host
- quantity is positive and within the configured demo max quantity
- limit-order notional is within the configured max notional

The script also refuses a live run unless the required switches are present:

```powershell
.\scripts\lmax-lab-fix-demo-order-lifecycle.ps1 `
  -AllowExternalConnections `
  -AllowOrderSubmission `
  -ConfirmDemoOrder `
  -DryRun:$false
```

Do not run the live lifecycle unless you intentionally want to submit a tiny LMAX Demo order. If submitted, the lab logs on to the FIX Trading session, sends one `35=D`, reads `ExecutionReport` (`35=8`) messages until a terminal state or timeout, and logs out. The normalized execution reports are printed only; no live data is persisted into the main database.

## FIX Lifecycle Evidence Report

The lab has now validated the full LMAX Demo FIX lifecycle in isolation:

- `NewOrderSingle` (`35=D`) submitted in Demo.
- `ExecutionReport` (`35=8`) `New` received.
- `ExecutionReport` (`35=8`) `Trade/Filled` received.
- `OrderStatusRequest` (`35=H`) returned `ExecutionReport` (`35=8`) with `ExecType=I` and `OrdStatus=Filled`.
- `TradeCaptureReportRequest` (`35=AD`) was accepted.
- `TradeCaptureReport` (`35=AE`) recovered the fill.

`fix-demo-lifecycle-evidence` is a lab-only wrapper that can produce one structured report for that flow. In live Demo mode it submits the same tiny gated demo order, runs read-only order-status recovery by `ClOrdID`, runs read-only trade-capture recovery over a recent UTC window, then prints consistency checks. It does not persist anything into the main DB and it is not referenced by the API or Worker.

The command separates the order-capable phase from the recovery phase while keeping one FIX Trading session. The first phase uses the strict demo order gates (`AllowOrderSubmission=true`, explicit confirmation, `DryRun=false`, Demo/UAT only). After the terminal execution report is known, the recovery phase stays on the same logged-on session and sends only `OrderStatusRequest` (`35=H`) and `TradeCaptureReportRequest` (`35=AD`). It never sends a second `NewOrderSingle`, does not attempt a second recovery logon, and logs out once at the end.

Trade capture windows are computed after the fill is received. The lab uses the last fill `TransactTimeUtc` as the anchor, sets the start before that fill, and sets `EndUtc` after the fill (`max(now, fillTime + 1 minute)`). Diagnostics include `FillTransactTimeUtc`, `TradeCaptureStartUtc`, and `TradeCaptureEndUtc` so operators can verify the recovery window actually covers the fill.

The evidence report checks:

- `ClOrdID` matches across order submission and order-status recovery.
- Broker `OrderID` matches between execution reports and order-status recovery.
- `SecurityID` and side match across reports.
- `CumQty` and `LeavesQty` from the terminal execution report match order-status recovery.
- Fill `ExecID` from `ExecType=F` appears in `TradeCaptureReport` (`35=AE`).
- Fill quantity and price match TradeCapture `LastQty` and `LastPx`.
- Missing Trade UTI in FIX AE is reported as a warning only.

`ExecType=I` is status-only. It must not be counted as a fill; fill identity comes from `ExecType=F` / `Trade` execution reports and the corresponding TradeCapture `ExecID`.

If the order fills but read-only recovery is incomplete, the evidence command reports `PartiallySucceeded` and states which recovery pieces are missing. That means the order lifecycle itself succeeded, but the cross-channel evidence is incomplete.

Dry-run:

```powershell
.\scripts\lmax-lab-fix-demo-lifecycle-evidence.ps1
```

Live Demo evidence is intentionally gated and must not be run automatically:

```powershell
.\scripts\lmax-lab-fix-demo-lifecycle-evidence.ps1 `
  -AllowExternalConnections `
  -AllowOrderSubmission `
  -ConfirmDemoOrder `
  -DryRun:$false `
  -VenueQuantity 0.1 `
  -TradeCaptureLookbackMinutes 1440 `
  -MaxReports 20
```

The command remains Demo/UAT-only, requires explicit confirmation, keeps `AllowLiveTrading=false`, and never writes recovered FIX data into production/local trading tables.

## FIX Trade Capture Normalization

When `fix-trade-capture-smoke` receives `TradeCaptureReport` (`35=AE`) messages, the lab normalizes them into a lab-only DTO aligned with the shape needed for later EOD comparison. This is still read-only: reports are printed, not persisted, and the main API/Worker do not consume them.

The AE parser extracts:

- `568 TradeRequestID`
- `571 TradeReportID`
- `912 LastRptRequested`
- `17 ExecID`
- `527 SecondaryExecID`
- `48 SecurityID`
- `22 SecurityIDSource`
- `55 Symbol`
- `32 LastQty`
- `31 LastPx`
- `75 TradeDate`
- `60 TransactTime`
- `54 Side`
- `1 Account`
- `11 ClOrdID`
- `37 OrderID`

Normalization maps `SecurityID=4001` to `EURUSD` when the lab config/default mapping is available. FIX side `54=1` maps to `Buy`; `54=2` maps to `Sell`. Quantities and prices are parsed as `decimal`, and timestamps are parsed as UTC. Malformed decimals or timestamps produce warnings rather than crashing the replay or smoke command.

The lab also projects each normalized AE into an EOD-like comparison DTO:

- `ExecutionId`
- `MtfExecutionId`
- `TimestampUtc`
- `TradeQuantity`
- `TradePrice`
- `TradeDate`
- `InstrumentId` / `SecurityId`
- `Symbol`
- `InstructionId` / `ClOrdID`
- `OrderId`
- `AccountId`
- `UnitsBoughtSold`
- `NotionalValue`
- `TradeUti`

This is comparison-readiness only. It does not claim perfect equivalence with `individual-trades.csv`. Known differences include:

- EOD has Trade UTI; parsed FIX AE may not.
- Field names and cardinality differ.
- Some EOD fields may not exist in AE.
- Account, ClOrdID, OrderID, Symbol, or Trade UTI may be missing depending on the report.

Each normalized report includes `CanMapToEodIndividualTrade`, `MissingForEodComparison[]`, and `Warnings[]`.

Synthetic replay:

```powershell
.\scripts\lmax-lab-fix-trade-capture-replay.ps1
```

The replay reads synthetic sanitized FIX messages from `tools/QQ.Production.Intraday.Lmax.ConnectivityLab/fixtures/synthetic-trade-capture-ae.fix`, normalizes them, prints normalized reports and EOD-like projections, makes no network call, and persists nothing. The fixture covers buy/sell EURUSD, USDJPY, `912=Y`, missing `55`, missing `48`, malformed decimal, and malformed timestamp cases.

## Parked Account API Read-Only Discovery

The lab includes read-only Account REST API discovery for LMAX Demo/UAT, but this path is parked and diagnostic only. It is not part of the platform integration path. It does not persist account data into LocalDB, does not create broker positions, does not update wallets, does not submit orders, and is not referenced by `QQ.Production.Intraday.Api` or `QQ.Production.Intraday.Worker`.

Default Demo endpoints:

```text
AccountApiBaseUrl = https://account-api.london-demo.lmax.com
PublicDataApiBaseUrl = https://public-data-api.london-demo.lmax.com
```

Configure credentials with user-secrets or environment variables only:

```powershell
dotnet user-secrets set "LmaxConnectivityLab:AccountApiAuthMode" "Auto" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
dotnet user-secrets set "LmaxConnectivityLab:AccountApiUsername" "<demo-username>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
dotnet user-secrets set "LmaxConnectivityLab:AccountApiPassword" "<demo-password>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab

# If LMAX provides API-key auth instead:
dotnet user-secrets set "LmaxConnectivityLab:AccountApiKey" "<api-key>" --project .\tools\QQ.Production.Intraday.Lmax.ConnectivityLab
```

Supported auth modes:

- `Auto`: tries BasicAuth when username/password exist, then Bearer API key, then header API key.
- `BasicAuth`: sends `Authorization: Basic ...`.
- `BearerApiKey`: sends `Authorization: Bearer ...` using `AccountApiBearerToken` or `AccountApiKey`.
- `HeaderApiKey`: sends `AccountApiKeyHeaderName` with the API key; default header is `X-API-Key`.
- `UsernamePasswordForm`: intentionally not guessed; skipped unless a known safe auth endpoint is later implemented.
- `None`: no auth header, useful only for probing public metadata endpoints.

Read-only commands:

```powershell
.\scripts\lmax-lab-account-config-check.ps1
.\scripts\lmax-lab-account-smoke.ps1
.\scripts\lmax-lab-account-discover.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-positions-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-balances-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-open-orders-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
.\scripts\lmax-lab-account-trade-history-smoke.ps1 -AllowExternalConnections -AuthMode Auto -ShowResponseExcerpt
```

`account-api-discover` probes a controlled set of GET-only endpoints, including `/openapi.json`, `/account`, `/accounts`, `/v1/account/positions`, `/v1/account/balances`, `/working-orders`, `/instrument-positions`, `/wallets`, and `/trade-history`. It reports HTTP status, content type, top-level JSON field names, array counts where obvious, and a sanitized short excerpt only when `-ShowResponseExcerpt` is used. `404` is treated as discovery information, not infrastructure failure. `401`/`403` is reported as authentication or permission failure. BasicAuth exploration against the Demo host has returned `401` for likely endpoints, reinforcing that this is diagnostic only.

Troubleshooting:

- `401 Unauthorized`: wrong auth mode, username/password, bearer token, or API key.
- `403 Forbidden`: credentials are valid but the account may not have API permission.
- `404 Not Found`: endpoint path is probably wrong; try discovery and record working endpoints here once confirmed.
- TLS/proxy issues: outbound HTTPS to `account-api.london-demo.lmax.com:443` must be allowed.
- Docs vs contact guidance: public docs may require API-key auth even though LMAX contact said Demo credentials may work; keep `AccountApiAuthMode=Auto` until a working mode is confirmed.

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
.\scripts\lmax-lab-fix-capabilities.ps1
.\scripts\lmax-lab-fix-order-status-dry-run.ps1
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

Manual read-only FIX Trading recovery smoke:

```powershell
.\scripts\lmax-lab-fix-capabilities.ps1
.\scripts\lmax-lab-fix-trade-capture-smoke.ps1 -AllowExternalConnections -LookbackMinutes 1440 -MaxReports 20 -ShowFixMessages
.\scripts\lmax-lab-fix-order-status-dry-run.ps1 -ClOrdId "known-demo-client-order-id"
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
- whether any FIX custom message replaces unsupported `AF`/`AN`/`AP`
- exact trade-capture date-window constraints and retention
- how to map trade capture reports to EOD execution reports
- Account API key/authentication model only if diagnostics are resumed later
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

After logon works, next safe steps are read-only market data snapshot investigation and FIX trade-capture recovery. Account REST API exploration is parked. Any demo order lifecycle remains a separate future approval and is not implemented here.

## Live Read-Only Evidence Capture Lab

The Connectivity Lab now includes a lab-only read-only capture command for collecting a small amount of LMAX Demo FIX evidence and exporting it as sanitized JSON for local shadow replay.

```powershell
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1 -AllowExternalConnections -TradeCaptureLookbackMinutes 60 -MaxReports 20
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1 -AllowExternalConnections -ClOrdID "known-demo-client-order-id"
```

Without `-AllowExternalConnections`, the script exits as skipped and makes no network call. With the flag, the lab command can run only read-only actions: a FIX Market Data snapshot for EURUSD / `SecurityID=4001`, a bounded `TradeCaptureReportRequest`, and optionally an `OrderStatusRequest` when an explicit `ClOrdID` is supplied. It never builds or sends `NewOrderSingle`, requires `AllowOrderSubmission=false`, and writes no data to the main platform database.

Generated evidence files are written under `artifacts/lmax-lab/evidence/` by default with names like:

```text
lmax-readonly-evidence-YYYYMMDD-HHMMSS.json
```

Generated evidence uses schema `lmax-fix-lifecycle-evidence-v1` and contains normalized sections compatible with `POST /lmax-shadow/replay`: `executionReports`, `orderStatuses`, `tradeCaptureReports`, and `protocolRejects`. Metadata records `source=ConnectivityLab`, `captureMode=ReadOnly`, instrument details, capture timestamps, and redaction markers. Evidence files must not contain credentials, authorization headers, raw logon password tag `554`, or account secrets, and generated files are ignored through the repository `artifacts/` ignore rule.

Replay is a separate local-only step:

```powershell
.\scripts\replay-lmax-lab-evidence-file.ps1 -EvidenceFile .\artifacts\lmax-lab\evidence\lmax-readonly-evidence-YYYYMMDD-HHMMSS.json
```

The replay script posts only to the local API, does not connect to LMAX, verifies safe mutation counts when available, and prints replay and observation counts.

### Evidence Contract Validation

Read-only and lifecycle evidence files use schema `lmax-fix-lifecycle-evidence-v1`. Required replay arrays are `executionReports`, `orderStatuses`, `tradeCaptureReports`, and `protocolRejects`; generated evidence must use `orderStatuses`, not the legacy `orderStatusReports` name. TradeCapture `tradeDate` is normalized to `yyyy-MM-dd`, raw FIX side values `1`/`2` are normalized to `Buy`/`Sell`, and missing FIX TradeUTI is represented as explicit `tradeUti: null`.

Evidence validation checks schema version, replay-compatible metadata, array shapes, ISO timestamps, numeric quantity/price fields, normalized side values, redaction markers, and credential-like content. The replay helper refuses files with validation errors unless `-AllowInvalidEvidence` is used for diagnostics.

The validator also infers an evidence mode and writes it into normalized JSON as `evidenceMode`:

- `EmptyReadOnly`: all replay arrays are present and empty.
- `MarketDataOnly`: market-data snapshot context is present, with no execution/order/trade/reject evidence.
- `TradeCaptureOnly`: one or more `35=AE` trade-capture reports, with no other replay evidence.
- `OrderStatusOnly`: one or more status-only `ExecType=I` order-status reports; these are never fill evidence.
- `ProtocolRejectOnly`: one or more session-level `35=3` rejects.
- `MixedReadOnly`: read-only combinations such as market data plus order-status and trade-capture evidence.
- `SyntheticLifecycle`: lifecycle fixture or evidence containing execution, order-status, and trade-capture sections.

Market data is captured as context only and does not currently create shadow observations. TradeCapture AE is recovery evidence, not the official daily reconciliation source; EOD files remain authoritative for daily reconciliation and TradeUTI.

### Shadow Observation Policy

Replay classification is policy-driven:

- Empty and market-data-only evidence replays successfully with zero trading observations.
- TradeCapture-only AE that does not match an internal fill is a `Warning` in lab/read-only mode. It is recovery evidence for review, not an instruction to create a fill.
- OrderStatus-only `ExecType=I` is status-only. It is never treated as fill evidence; unknown internal orders from status-only evidence are non-blocking warnings in lab mode.
- Synthetic lifecycle fills missing internally are warnings in offline fixture context.
- Protocol rejects for read-only recovery requests such as `35=AD` or `35=H` are warnings. Protocol rejects for `35=D` or unknown order-path requests are blocking and create exception cases.
- Mixed read-only evidence applies these rules per observation and dedupes repeated logical observations by fingerprint.

Observation DTOs expose the policy code, evidence mode, source event type, rationale, suggested operator action, and whether an exception case is created.

Validate a file without API or LMAX access:

```powershell
.\scripts\validate-lmax-lab-evidence-file.ps1 -EvidenceFile .\tests\fixtures\lmax-shadow\lmax-fix-lifecycle-evidence-v1.json
.\scripts\validate-lmax-lab-evidence-file.ps1 -EvidenceFile .\artifacts\lmax-lab\evidence\lmax-readonly-evidence-YYYYMMDD-HHMMSS.json -WriteNormalizedCopy
```

Generated evidence files are local artifacts and should not be committed.

## Current Limitations

- No official LMAX client library is wired in.
- QuickFIX/n is not wired in; FIX logon and market-data snapshot smoke use a minimal raw FIX client.
- Public data smoke returns `Skipped` unless a future client is implemented.
- Account API discovery is parked and diagnostic only; the main path is FIX market data, FIX trading recovery, and LMAX EOD files.
- `OrderMassStatusRequest`, `RequestForPositions`, and `PositionReport` are treated as unsupported unless a future LMAX FIX dictionary says otherwise.
- Demo order and lifecycle evidence commands are gated, Demo/UAT-only, and dry-run by default. They submit nothing unless explicit safety flags and confirmation are supplied.
- Main API/Worker remain FakeLmax-only.
