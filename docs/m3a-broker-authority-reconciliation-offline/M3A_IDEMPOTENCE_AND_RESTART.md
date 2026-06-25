# M3A Idempotence And Restart

## Identity scope

Broker execution identity is scoped by:

```text
environment/account/venue/broker_execution_id
```

This prevents Demo/Live and multi-account collisions.

## Duplicate handling

- Same id and same payload: idempotent, no break.
- Same id and different payload: critical `DUPLICATE_EXECUTION` break.
- PossDup/out-of-order exact replay: idempotent.

## Sequence health

Sequence gaps are surfaced as `SEQUENCE_GAP` and block new orders until resolved.

## Restart/replay behavior

The reconciler is a pure replay over supplied evidence. Replaying the same scoped evidence yields the same input hash, breaks, remaining deltas, and readiness. No fill is double-applied by the engine.

## Cancel pending

Internal cancel-pending orders continue to reserve leaves until terminal broker state is supplied. This blocks double-send into unresolved residual.
