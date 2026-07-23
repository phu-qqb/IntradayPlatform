# ARCH7B Demo known-order lifecycle runbook

Status: PREPARED, DISABLED, NOT AUTHORIZED, NOT APPLIED.

## Fixed boundaries

- TEST and LMAX Demo account `1754288005` only.
- Real account `921640160` is rejected in code and database constraints.
- One deterministic GBPUSD ChildOrder (`SecurityID=4002`, `SecurityIDSource=8`).
- Opening quantity `0.1`, one opening `35=D`, one flatten `35=D` only for executed quantity.
- One residual cancel `35=F`, zero replace `35=G`, at most four known-order `35=H`.
- `ExternalOrManualOrderCoverage=UNPROVEN`; exclusivity is a bounded operator declaration plus PostgreSQL advisory lease.

## Preflight order

1. Validate exact authorization packet SHA, Demo endpoint, account and explicit CLI confirmation.
2. Acquire the Demo-account advisory lease.
3. Read the selected ARCH7A ChildOrder and its TradeIntent/RiskDecision/ParentOrder lineage from PostgreSQL.
4. Prove latest qualifying ARCH6F revision, fresh slot, LMAX direct market lineage, initial flat position and zero other platform-known working order.
5. Bind current LMAX BBO bid/ask/time/SHA to opening and flatten limits and deterministic ClOrdIDs.
6. Persist the qualification run before FIX logon.

## Lifecycle

1. Arm kill switch before logon.
2. Persist each D/F/H send intent before writing bytes.
3. Persist every real `35=8` with raw message SHA, MsgSeqNum and PossDup.
4. On restart, never resend an existing D or F. Query only the known opening/flatten ClOrdID.
5. Cancel opening residual once, then flatten exactly cumulative executed opening quantity.
6. Fail closed on unknown ClOrdID/OrderID, account/instrument mismatch, sequence gap, duplicate conflict, budget breach or deadline.
7. Build Fill and PositionLedgerEvent only from valid fill/partial-fill ExecutionReports.
8. Require both known lifecycles terminal, zero known leaves, zero ledger quantity and zero critical break.
9. Persist final reconciliation with authority `LMAX_FIX_EXECUTION_REPORTS_KNOWN_ORDERS`.
10. Logout, dispose socket and release lease.

## Recovery

Each connection receives a unique persisted FIX-session instance ID. Recovery reuses the qualification run, packet, ClOrdIDs, send ledger and ExecutionReports. A stale source cannot authorize a new opening, while an already-sent opening may be recovered and flattened without resending it.

## Current non-actions

No PostgreSQL connection or migration apply, no FIX logon, no secret read, no Demo order, no real-account access and no production mutation were performed while preparing this branch.
