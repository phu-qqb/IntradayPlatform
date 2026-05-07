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

The replay implementation persists local replay runs and shadow observations only. It does not persist raw FIX, credentials, or live LMAX data into trading tables, and it does not mutate orders, fills, positions, risk decisions, or reconciliation state.

## Live Shadow Reader Skeleton

The live shadow reader skeleton is a disabled foundation for future read-only LMAX evidence collection. It is not live shadow mode.

Default options are inert:

```text
Enabled = false
AllowExternalConnections = false
AllowCredentialUse = false
ReadOnly = true
AllowOrderSubmission = false
PersistRawFixMessages = false
PersistToTradingTables = false
DryRun = true
```

The no-op reader evaluates safety gates and returns `Disabled` or `Blocked` unless every explicit future gate is satisfied. Today it never opens sockets, never reads credentials, never calls the Connectivity Lab, never sends `NewOrderSingle`, never persists raw FIX, and never writes orders, fills, positions, model runs, risk state, reconciliation state, or trading tables.

Shadow Reader Quality Gate #1 formalizes those gates. Each gate reports name, status, observed value, expected safe value, and operator-readable message. The current skeleton always remains blocked by implementation mode even if future-looking options are toggled, so dangerous config tests cannot accidentally turn it into an executable reader.

The local endpoints are diagnostics only:

- `GET /lmax-shadow-reader/status`
- `POST /lmax-shadow-reader/run`

There are no credential DTOs, no host/password/user input fields, no UI enable button, and no scheduler auto-run. The UI panel shows the disabled state and gate reasons so operators can distinguish validated replay infrastructure from not-yet-enabled live shadow reading.

## Runtime Boundary

The main runtime must continue to show:

- API registers `IVenueExecutionGateway -> FakeLmaxGateway`.
- Worker registers `IVenueExecutionGateway -> FakeLmaxGateway`.
- API/Worker do not reference the Connectivity Lab.
- API/Worker do not register LMAX FIX sessions or `ILmaxShadowModeService`.

Any future change that alters those boundaries must be treated as a separate safety-critical design gate.

## Remaining Runtime Integration Gates

Before any real LMAX runtime integration can be considered, the platform still needs:

- live shadow-mode activation gates and operational controls
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

Each observation has a deterministic fingerprint based on observation type, broker execution/order ids, client order id, instrument/symbol, internal references, and key differences. Duplicate observations with the same fingerprint are collapsed within a single replay run. Replaying the same evidence file later creates a new replay run and new observations with the same fingerprints, preserving replay history while allowing grouping and comparison.

Replay summaries include input event count, unique event count, duplicate event count, observation count, blocking count, and warning count. Blocking observations create at most one exception case per replay/fingerprint; warning observations do not create exception cases by default.

## Read-Only Lab Evidence Capture

The isolated Connectivity Lab can also produce read-only evidence files for shadow replay without adding runtime LMAX integration. The capture command is explicit and lab-only:

```powershell
.\scripts\lmax-lab-fix-readonly-evidence-capture.ps1 -AllowExternalConnections -TradeCaptureLookbackMinutes 60 -MaxReports 20
```

The command may collect a FIX Market Data snapshot, bounded TradeCapture recovery, and optional OrderStatus recovery for a provided `ClOrdID`. It never sends `NewOrderSingle`, requires `AllowOrderSubmission=false`, writes only a sanitized JSON evidence file under `artifacts/lmax-lab/evidence/`, and does not persist anything into trading tables.

Replay stays local:

```powershell
.\scripts\replay-lmax-lab-evidence-file.ps1 -EvidenceFile .\artifacts\lmax-lab\evidence\lmax-readonly-evidence-YYYYMMDD-HHMMSS.json
```

The replay script posts to `/lmax-shadow/replay` on localhost, converts normalized evidence sections to the existing replay DTO, checks available mutation counts, and makes no FIX/network call. This closes a lab-to-shadow loop while preserving the hard boundary: API and Worker remain FakeLmax-only, and future live shadow mode is still not implemented.

Evidence Contract Hardening #1 formalizes schema `lmax-fix-lifecycle-evidence-v1`. Generated evidence uses `orderStatuses`, ISO `yyyy-MM-dd` TradeCapture trade dates, explicit `tradeUti: null` when FIX AE does not provide TradeUTI, and JSON arrays even for one-item sections. The validator rejects credential-like content, unsupported schema versions, invalid dates/timestamps, raw non-normalized sides, and missing required metadata before replay posts to the API.

LMAX Read-Only Evidence Coverage #1 expands the same contract across `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, and `SyntheticLifecycle` fixtures. Market-data-only and empty evidence are valid and replay with zero observations. `ExecType=I` order-status evidence is status-only and must not become fill evidence. Protocol rejects create the configured shadow observation path. All replay remains local and non-mutating; generated evidence files stay under ignored lab artifact directories and must not be committed.

Shadow Observation Policy Hardening #1 adds explicit classification metadata to every shadow observation. The policy records `policyCode`, evidence mode, source event type, rationale, suggested operator action, and exception-case behavior. TradeCapture-only missing internal fills are `Warning` in lab/read-only mode, not `Blocking`, because AE is recovery evidence and replay must not mutate the internal book. OrderStatus-only `ExecType=I` is also `Warning` or `Info` status evidence, never a fill. Market-data-only files create no trading observations. Protocol rejects are context-sensitive: read-only request rejects are warnings, while order-path or unknown rejects are blocking and create exception cases.

