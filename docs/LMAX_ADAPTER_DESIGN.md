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

The dormant adapter foundation defines interfaces and DTOs in `QQ.Production.Intraday.Infrastructure.Lmax`:

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

These are contracts and pure normalization helpers only. They are not connected to dependency injection in the API or Worker.

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
