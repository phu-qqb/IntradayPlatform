# M3A Authority Matrix

| Domain | Authority | Accepted quality | Blocking if missing? | M3A behavior |
|---|---|---|---:|---|
| Target intent | ModelRun / TargetWeight | AUTHORITATIVE internal model output | Yes | Used only as input to remaining delta. |
| Internal orders | OMS parent/child state | AUTHORITATIVE internal state | Yes for working leaves | Working leaves reserve quantity, including cancel-pending. |
| Broker executions | LMAX ExecutionReport / recovery TradeCapture where complete | AUTHORITATIVE or RECONSTRUCTED | Yes | Compared to internal fills by scoped broker execution id. |
| Internal fills | PMS/OMS fill ledger | AUTHORITATIVE internal state | Yes | Missing broker/internal side creates blocking break. |
| Internal positions | Position ledger snapshot | AUTHORITATIVE internal state | Yes | Compared to broker position snapshot. |
| Broker positions | Broker position snapshot/report | AUTHORITATIVE | Yes | Missing or stale source yields NO_GO, never synthetic zero. |
| Broker open orders | Broker open-order snapshot or proven complete session lifecycle | AUTHORITATIVE or RECONSTRUCTED | Yes | Compared to internal working orders and leaves. |
| Manual UI/export | Operator evidence | MANUAL_EVIDENCE | Cannot authorize alone | Produces evidence/warning only. |
| Breaks | Reconciler output | Deterministic local computation | Yes if blocking/critical | Drives BLOCK_NEW_ORDERS or EMERGENCY_STOP. |

## Source-quality rule

`AUTHORITATIVE`, `RECONSTRUCTED`, `MANUAL_EVIDENCE`, `STALE`, `INCOMPLETE`, `UNKNOWN` are explicit states. Manual evidence can help an operator review a case, but it cannot become broker authority inside the engine.
