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
- Shadow replay observations have deterministic fingerprints and duplicate fingerprints are collapsed within one replay run.

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

The current `LmaxVenueGatewaySkeleton` is intentionally inert. It is a named placeholder for future adapter shape, not an executable gateway. It is not registered in API or Worker, and its order-submission method returns a blocked result.

## Shadow Replay Harness

The shadow replay harness is the observable side of the adapter contract gate. It accepts normalized contract-shaped evidence and produces observations such as matching fills, missing internal fills, order status mismatches, duplicate executions, and protocol rejects. TradeCapture AE evidence is recovery/evidence and must not double-count fills. OrderStatus ExecType=I remains status-only.

Replay is local JSON/API driven and does not connect to LMAX. Observation actions require a reason and are audited.

Repeated replay of the same evidence creates a separate replay run and separate observations, but the fingerprints remain stable. That preserves the audit/history trail while making recurring observations groupable. Replay summaries expose input, unique, duplicate, warning, blocking, and total observation counts.

Shadow replay may write only replay runs, observations, audit events, and configured exception cases for blocking observations. It must not update internal orders, fills, positions, risk decisions, model runs, target positions, drift snapshots, or reconciliation state.

The read-only Connectivity Lab evidence capture command writes the same replay-compatible normalized contract shape to local JSON. It can include market-data metadata, TradeCapture AE recovery evidence, optional OrderStatus `ExecType=I` reports for a supplied `ClOrdID`, and protocol rejects. It cannot submit orders, does not persist raw FIX into trading tables, and is replayed through the same `/lmax-shadow/replay` contract with `inputSource=LabEvidenceFile`.

Evidence schema `lmax-fix-lifecycle-evidence-v1` requires `executionReports`, `orderStatuses`, `tradeCaptureReports`, and `protocolRejects` arrays. Contract normalization converts compact FIX trade dates to `yyyy-MM-dd`, raw FIX side values to `Buy`/`Sell`, legacy `orderStatusReports` to `orderStatuses`, missing arrays to empty arrays, and absent TradeUTI to explicit `tradeUti: null`. Validation errors stop replay before `POST /lmax-shadow/replay` unless explicitly overridden for diagnostics.

Supported evidence modes are explicit in the validator: empty read-only evidence, market-data-only context, TradeCapture-only recovery, OrderStatus-only recovery, protocol-reject-only diagnostics, mixed read-only evidence, and synthetic lifecycle evidence. Market data is contextual and does not create observations today. OrderStatus `ExecType=I` is status-only. TradeCapture AE is recovery evidence and must not double-count fills; EOD files remain the official daily reconciliation source.

## Shadow Observation Policy

LMAX Shadow Observation Policy Hardening #1 makes observation classification explicit. Each observation now carries policy metadata in its difference payload and API DTO: `policyCode`, `evidenceMode`, `sourceEventType`, `rationale`, `suggestedOperatorAction`, and whether the policy creates an exception case.

| Evidence / event | Policy semantics | Severity | Exception |
| --- | --- | --- | --- |
| `EmptyReadOnly` | No execution/order/trade/reject events; replay records zero observations. | None | No |
| `MarketDataOnly` | Market data is context only and creates no trading observation. | None | No |
| `TradeCaptureOnly` missing internal fill | AE is recovery evidence in lab/read-only mode; investigate without mutating state. | Warning | No |
| `OrderStatusOnly` missing internal order | `ExecType=I` is status-only and never a fill. Missing internal order is a review item. | Warning | No |
| `SyntheticLifecycle` missing internal fill | Offline lab fixture may be ahead of internal state; replay records mismatch. | Warning | No |
| `ProtocolRejectOnly` for `35=D` or unknown path | Order-path or unknown protocol rejects block future adapter activation until reviewed. | Blocking | Yes |
| Protocol reject for read-only `35=AD`, `35=H`, or market data | Recovery/read-only request rejected; review request shape. | Warning | No |
| Mixed read-only evidence | Applies the max severity of individual observations and dedupes by fingerprint. | Max applicable | Blocking only |

Warnings are audit-visible operator review items and do not create exception cases by default. Blocking observations create/link exception cases with replay id, observation id, fingerprint, policy code, and evidence mode. Shadow replay remains non-mutating for orders, fills, positions, model state, risk state, and reconciliation state.

## Live Shadow Reader Boundary

The live shadow reader skeleton is a blocked, read-only shell around the replay contract. It exists so future work can route normalized LMAX evidence into the same observation pipeline, but it is disabled by default and is not a live FIX connection.

The reader skeleton must preserve the same contract rules as replay:

- no order submission
- no raw FIX persistence into trading tables
- no mutation of orders, fills, positions, risk, model, or reconciliation state
- no credential input through API/UI
- no scheduler auto-run
- API and Worker remain `FakeLmaxGateway` only

Until a later explicit activation gate, the reader status/run APIs are expected to return `Disabled` or `Blocked` and expose safety-check diagnostics only.

Shadow Reader Quality Gate #1 adds a dangerous-configuration matrix around this boundary. Contradictory settings produce multiple failed gates rather than hiding behind the first failure, blocked run attempts are audited, and mutation guards verify that only audit can change during a blocked diagnostic run.

