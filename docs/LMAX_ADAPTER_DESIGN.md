# LMAX Adapter Design Gate

This document captures the design boundary for a future real LMAX adapter. It is deliberately not a runtime enablement document.

The current API and Worker remain `FakeLmaxGateway` only. No `LmaxVenueGateway`, FIX session, market-data session, order gateway, or shadow-mode service is registered by the main runtime.

## Integration Path

The LMAX path for this platform is FIX-only plus EOD files:

- FIX Market Data for market-data snapshots and future streaming market-data ingestion.
- FIX Trading for order entry, `ExecutionReport` handling, `OrderStatusRequest` recovery, and `TradeCaptureReportRequest` recovery.
- LMAX EOD files as the official daily reconciliation source.

The Account REST API path is parked as lab-only diagnostics and is not required for platform operation.

## Validated Lab Facts

The isolated Connectivity Lab has validated:

- EURUSD uses `SecurityID=4001`.
- `SecurityIDSource=8`.
- `NewOrderSingle` (`35=D`) must not include `21 HandlInst`; LMAX Demo rejects it as `UnknownTag`.
- `TradeRequestID` (`568`) must be at most 16 characters.
- `ExecutionReport` (`35=8`) with `ExecType=F` is a fill.
- `ExecutionReport` (`35=8`) with `ExecType=I` is status-only and must not be counted as a fill.
- `OrderStatusRequest` (`35=H`) returns order-state recovery via `ExecutionReport`.
- `TradeCaptureReportRequest` (`35=AD`) returns fill recovery via `TradeCaptureReport` (`35=AE`).
- FIX `AE` can recover fills by `ExecID`.
- FIX `AE` does not currently provide Trade UTI; `individual-trades.csv` provides Trade UTI in EOD reconciliation.

## Adapter Contracts

The dormant adapter foundation defines interfaces, DTOs, and skeleton components in `QQ.Production.Intraday.Infrastructure.Lmax`:

- `ILmaxFixSession`
- `ILmaxFixMarketDataSession`
- `ILmaxFixTradingSession`
- `ILmaxFixOrderGateway`
- `ILmaxFixExecutionReportNormalizer`
- `ILmaxFixTradeCaptureRecoveryService`
- `ILmaxFixOrderStatusRecoveryService`
- `ILmaxInstrumentMapper`
- `ILmaxFixSafetyGate`
- `ILmaxShadowModeService`

The concrete skeleton classes are inert by default:

- `LmaxFixOrderMessageBuilder`
- `LmaxFixExecutionEventMapper`
- `LmaxFixTradeCaptureMapper`
- `LmaxFixOrderStatusMapper`
- `LmaxFixMarketDataMapper`
- `LmaxFixRejectMapper`
- `LmaxVenueGatewaySkeleton`
- `LmaxFixAdapterRuntimeSafetyValidator`
- `LmaxFixAdapterOptions`

These are contracts, pure builders, pure mappers, and safety helpers only. They are not connected to dependency injection in the API or Worker. They do not open sockets, read credentials, submit orders, or persist data.

## Normalized Models

Normalized DTOs are shaped to align future LMAX data with existing internal concepts without writing to internal tables:

- `LmaxNormalizedMarketDataSnapshot`
- `LmaxNormalizedExecutionReport`
- `LmaxNormalizedOrderStatus`
- `LmaxNormalizedTradeCaptureReport`
- `LmaxNormalizedOrderLifecycleEvidence`
- `LmaxNormalizedFixReject`

The normalizers preserve broker identifiers and keep warnings when equivalence is imperfect. Missing `TradeUTI` in FIX trade capture is a warning only; EOD remains the official daily reconciliation source.

## Safety Gate

`LmaxAdapterSafetyOptions` defaults are inert:

```text
Enabled = false
ShadowModeEnabled = false
AllowExternalConnections = false
AllowOrderSubmission = false
AllowLiveTrading = false
RequireGovernanceApproval = true
```

The safety gate rejects:

- `AllowLiveTrading=true`
- production/non-Demo/non-UAT environments
- external-connection intents without explicit external-connection allowance
- non-Demo/non-UAT hosts
- order submission from non-lab adapter code
- missing governance approval for any future order-submission path

For this design gate, order submission through the adapter is blocked even if an option is set. The only validated order submission path remains the isolated Connectivity Lab with explicit Demo safety flags.

## Proven FIX Message Shapes

The adapter skeleton builders encode the LMAX rules learned in the lab:

- `NewOrderSingle` (`35=D`) includes `11`, `48`, `22=8`, optional `55`, `54`, `60`, `38`, `40`, optional `44`, `59`, and optional `1`.
- `NewOrderSingle` does not include `21 HandlInst` by default.
- `TradeCaptureReportRequest` (`35=AD`) uses `568` with max length 16, `569=1`, `263=0`, `580=2`, and two `60 TransactTime` values for start/end.
- `OrderStatusRequest` (`35=H`) uses `11`, optional `48`, `22=8` when `48` is present, optional `54`, and optional `1`.
- `MarketDataRequest` (`35=V`) supports the validated EURUSD security-id shape: `48=4001`, `22=8`, bid `269=0`, and offer `269=1`.

The builders return sanitized message strings for tests and diagnostics only. They do not send anything.

## Shadow Mode

Future LMAX runtime work must start in shadow mode.

Shadow mode accepts normalized LMAX events and compares them to internal orders/fills, producing observations only:

- `ExecutionReportMatchesInternalFill`
- `ExecutionReportMissingInternalFill`
- `TradeCaptureMatchesInternalFill`
- `TradeCaptureMissingInternalFill`
- `OrderStatusMatchesInternalOrder`
- `OrderStatusMismatch`
- `MarketDataSnapshotReceived`
- `UnknownLmaxExecution`

Shadow mode must not:

- send orders
- update orders
- update fills
- update positions
- trigger execution
- bypass risk, governance, reconciliation, or audit

The initial implementation is in-memory and test-only. It does not persist observations.

## Runtime Boundary

The main runtime must continue to show:

- API registers `IVenueExecutionGateway -> FakeLmaxGateway`.
- Worker registers `IVenueExecutionGateway -> FakeLmaxGateway`.
- API/Worker do not reference the Connectivity Lab.
- API/Worker do not register LMAX FIX sessions or `ILmaxShadowModeService`.

Any future change that alters those boundaries must be treated as a separate safety-critical design gate.

## Remaining Runtime Integration Gates

Before any real LMAX runtime integration can be considered, the platform still needs:

- shadow-mode observation persistence
- explicit configuration gates
- governance approval flow for adapter activation
- risk approval enforcement for any future order path
- daily operations/runbook rehearsals
- failover and recovery rehearsals
- production certification and operational sign-off

## Shadow Observation Store

Shadow mode now has a local observation store and replay harness. The store accepts normalized LMAX-like execution reports, order statuses, trade capture reports, and protocol rejects through local replay endpoints. It compares those normalized events against internal child orders and fills, then writes LMAX shadow observations and replay run history only. Shadow replay does not mutate orders, fills, positions, risk decisions, or reconciliation state.

Blocking shadow observations create operator-visible exception cases. Warning observations remain review items. This is replay infrastructure only; live shadow mode remains a future activation gate and the API/Worker remain FakeLmax-only.

The isolated Connectivity Lab can export a sanitized lifecycle evidence file with schema `lmax-fix-lifecycle-evidence-v1`. That file contains normalized execution reports, `ExecType=I` order-status reports, trade-capture reports, consistency checks, and warnings, but no credentials, authorization headers, raw FIX logon password tags, or main-database persistence. `scripts/replay-lmax-lab-evidence.ps1` converts the evidence JSON into a local `POST /lmax-shadow/replay` request using `inputSource=LabEvidenceFile`; `scripts/smoke-lmax-shadow-local.ps1` replays the synthetic fixture under `tests/fixtures/lmax-shadow` to verify the loop without any FIX/network call.

