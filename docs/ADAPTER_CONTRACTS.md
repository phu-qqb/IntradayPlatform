# Adapter Contracts

This document defines the venue adapter contract gate for simulator parity and future LMAX adapter work.

The contract is deliberately normalized. It is independent of FIX tags, independent of `FakeLmaxGateway` internals, and independent of any future live adapter implementation. API and Worker remain registered to `FakeLmaxGateway` only.

## Normalized Venue Events

Adapter contract tests use pure venue events:

- `VenueOrderSubmissionRequest`
- `VenueOrderSubmissionResult`
- `VenueExecutionEvent`
- `VenueExecutionEventType`
- `VenueExecutionEventStatus`
- `VenueExecutionLifecycle`
- `VenueAdapterContractScenario`
- `VenueAdapterContractResult`

Supported event types:

- `OrderAccepted`
- `OrderRejected`
- `Fill`
- `PartialFill`
- `Expired`
- `Cancelled`
- `CancelReject`
- `OrderStatus`
- `ProtocolReject`
- `DuplicateExecution`
- `Unknown`

These events describe lifecycle evidence only. They do not submit orders, register a real adapter, persist live data, or connect to LMAX.

## FakeLmax Parity Role

`FakeLmaxGateway` remains the only runtime execution gateway registered by the API and Worker. Its existing simulator reports are mapped into the same normalized venue event contract used by LMAX normalized FIX DTOs.

Simulator parity scenarios cover:

- market IOC full fill
- market IOC partial fill then expired
- market IOC reject
- market IOC no fill / expired
- duplicate broker execution handling

The purpose is to keep internal execution lifecycle behavior stable before any future real adapter is allowed near runtime registration.

## LMAX Normalized FIX Mapping

LMAX FIX messages are first normalized into DTOs, then mapped to the venue contract.

ExecutionReport mapping:

| LMAX normalized input | Venue event |
| --- | --- |
| `ExecType=0` / New | `OrderAccepted` |
| `ExecType=F` / Trade and `LeavesQty > 0` | `PartialFill` |
| `ExecType=F` / Trade and `LeavesQty = 0` | `Fill` |
| `ExecType=8` / Rejected | `OrderRejected` |
| `ExecType=C` / Expired | `Expired` |
| `ExecType=4` / Cancelled | `Cancelled` |
| `ExecType=I` / OrderStatus | `OrderStatus` |

Session reject mapping:

- FIX session reject against `35=D` maps to `ProtocolReject`.
- A protocol reject never creates a fill or position ledger delta.

## ExecutionReport vs TradeCapture

`ExecutionReport` is the primary intraday lifecycle feed for order acknowledgements, rejects, fills, partial fills, cancels, expiries, and status-only updates.

`TradeCaptureReport` is recovery evidence. It may prove that an execution exists, but it must not create a second fill when the same `ExecID` already exists internally. A TradeCapture event with a missing internal fill creates a shadow or contract observation.

`OrderStatus` is status-only. `ExecType=I` must not be treated as a fill, even when `OrdStatus=Filled`.

## Idempotency Rules

- Duplicate `BrokerExecutionId` values do not create duplicate fills.
- Duplicate TradeCapture reports for an existing `ExecID` create a match observation, not another fill.
- ExecutionReport fills and TradeCapture fills with the same `ExecID` are correlated.
- Protocol rejects and OrderStatus reports never create fills.
- Partial fill plus expired creates one fill and an expired child order state.

## Shadow Mode Observations

Shadow mode compares normalized LMAX evidence to internal references and never mutates orders, fills, positions, or execution state.

Expected observation patterns:

- LMAX ExecutionReport fill matches internal fill.
- LMAX ExecutionReport fill is missing internally.
- LMAX TradeCapture fill matches internal fill.
- LMAX TradeCapture fill is missing internally.
- LMAX OrderStatus Filled matches internal child status.
- LMAX OrderStatus Filled without internal filled state creates a warning.

Shadow mode is evidence comparison only. It is not a live trading path.

## Runtime Registration Gate

A real LMAX adapter must pass these contracts before any runtime registration is considered:

- normalized LMAX ExecutionReport mapping
- normalized LMAX TradeCapture mapping
- session reject mapping
- FakeLmax parity scenarios
- state machine parity
- idempotency and duplicate handling
- shadow-mode observation behavior
- API/Worker default-registration safety checks

Passing this gate does not authorize live trading. It only proves that normalized venue lifecycle evidence can be interpreted consistently with the simulator and internal execution state model.
