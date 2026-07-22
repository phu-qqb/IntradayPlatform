# ARCH7A execution component reuse decision

ARCH7A reuses the current canonical domain records, Qubes FX currency netting, USD-pair selection policy, broker-authority formula, and CloseSeeking15m phase foundation. It adds only shadow envelopes, a deterministic coordinator, an offline FIX order-state reducer, and optional additive PostgreSQL persistence.

`ProcessModelRunService` is deliberately not invoked because its current path calls `IVenueExecutionGateway.SendOrderAsync` and can create ExecutionReport, Fill, and PositionLedgerEvent facts. R009 remains historical evidence only. API and Worker gateway registration is unchanged.

No second OMS, execution algorithm, LMAX gateway, working-order model, Fill model, or ledger model is introduced.
