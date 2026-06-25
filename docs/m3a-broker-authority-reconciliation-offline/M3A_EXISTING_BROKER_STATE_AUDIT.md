# M3A Existing Broker State Audit

Mode: OFFLINE_READ_ONLY. No LMAX session, no FIX logon, no broker traffic, no AccountAPI, no Databento, no DB apply.

## Existing contracts inspected

| Area | Existing source | Classification | Notes |
|---|---|---:|---|
| ExecutionReport / Fill | `src/QQ.Production.Intraday.Domain/DomainModels.cs`, `src/QQ.Production.Intraday.Infrastructure.Lmax/VenueAdapterContracts.cs` | EXISTS_AND_WIRED | Domain has fills and execution reports; LMAX adapter contracts normalize ExecutionReport and TradeCapture evidence. |
| ParentOrder / ChildOrder | `DomainModels.cs`, OMS/PMS foundation | EXISTS_AND_WIRED | Existing parent/child concepts are reused by M3A rather than duplicated. |
| PositionLedgerEvent | `DomainModels.cs` | EXISTS_AND_WIRED | Internal accounting lineage exists; M3A treats it as internal accounting, not broker truth. |
| ReconciliationBreak | `DomainModels.cs`, `EodServices.cs` | EXISTS_AND_WIRED | Generic and EOD reconciliation exist; M3A adds broker-authority break taxonomy for runtime readiness. |
| ExceptionCase | `DomainModels.cs` | EXISTS_AND_WIRED | Existing exception pattern can carry operator workflow. |
| Open-order state | Execution reports, order state machine, shadow replay | EXISTS_NOT_WIRED | Can be reconstructed from complete session state but no proven broker authoritative open-order snapshot source is wired. |
| LMAX execution/report transports | `src/QQ.Production.Intraday.Infrastructure.Lmax`, `tools/*Lmax*` | WIRED_ONLY_IN_LAB_OR_DEMO | Read-only market data exists; execution/report normalization exists; order-entry is outside M3A and remains forbidden. |
| Drop Copy / reports | Venue adapter contracts, EOD import, shadow replay | EXISTS_NOT_WIRED | Recovery evidence is modelled, but no production-authoritative runtime broker state source is activated. |
| Manual UI/export evidence | Prior R090/R093 pattern and M3A model | MANUAL_HANDOFF | Accepted as evidence refs only; never upgraded to authoritative fill or position source. |

## Authority conclusions

- Intent / target authority: `ModelRun` and `TargetWeight`.
- Internal order authority: OMS parent/child/order state.
- Execution authority: broker execution feed when source quality is AUTHORITATIVE or verified RECONSTRUCTED.
- Internal accounting position: `PositionLedgerEvent` / internal position snapshot lineage.
- Real account position: broker position snapshot or broker report, not available yet as an authoritative runtime source.
- Open orders: broker open-order snapshot or a proven complete session state. Current code can model it; the source still needs M3B integration.
- Breaks: comparison results. No silent overwrite of PMS or broker truth.

## M3A implementation scope

M3A adds `BrokerAuthorityModels` and a pure `BrokerAuthorityReconciler`. It consumes already-local evidence and returns breaks, remaining delta, and readiness. It does not connect to LMAX, does not send orders, and does not apply database state.
