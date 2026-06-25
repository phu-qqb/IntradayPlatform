# M3A Kill Readiness

Readiness decisions:

- `CAN_TRADE`: all required source states are fresh/usable and no blocking break exists.
- `BLOCK_NEW_ORDERS`: source is missing/stale/incomplete or a blocking break exists.
- `EMERGENCY_STOP`: a critical break exists, such as conflicting duplicate execution evidence.

Operational gate:

- `GO` only for `CAN_TRADE`.
- `NO_GO` for all blocking or emergency states.

M3A emits readiness only. It never sends an order, cancel, replace, FIX logon, AccountAPI call, Databento request, or DB apply.
