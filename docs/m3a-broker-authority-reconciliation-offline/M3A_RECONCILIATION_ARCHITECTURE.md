# M3A Reconciliation Architecture

M3A is a pure offline function:

```text
BrokerAuthorityReconciliationInput
  -> BrokerAuthorityReconciler.Reconcile(...)
  -> BrokerReconciliationResult
```

## Inputs

- `BrokerAuthorityScope`: fund, portfolio, book, strategy, environment, account, venue.
- Source states: execution, position, open-order source quality and hashes.
- Internal evidence: fills, positions, working orders.
- Broker evidence: executions, positions, open orders, manual evidence refs.
- Target positions for remaining-delta readiness.

## Comparisons

- Internal fills vs broker executions.
- Internal positions vs broker positions.
- Internal working orders vs broker open orders.
- Duplicate broker execution ids and sequence gaps.
- Source freshness and source completeness.

## Remaining delta

```text
remaining_delta = target_position - reconciled_current_position - signed_reserved_working_leaves
```

`cancel_pending` leaves are still reserved. This prevents double-send while cancellation is unresolved.

## Output

- deterministic run id and input hash;
- break list with severity, blocking flag, resolution status, evidence refs, source hashes;
- per-instrument remaining delta;
- readiness decision: `CAN_TRADE`, `BLOCK_NEW_ORDERS`, or `EMERGENCY_STOP`.

The engine is local and append/read-only friendly. It does not mutate PMS, broker state, or DB.
