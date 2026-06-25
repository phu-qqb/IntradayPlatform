# M3A Break Taxonomy

Implemented broker-authority break types:

- `MISSING_INTERNAL_FILL`
- `MISSING_BROKER_FILL`
- `DUPLICATE_EXECUTION`
- `POSITION_QUANTITY_MISMATCH`
- `POSITION_INSTRUMENT_MISMATCH`
- `OPEN_ORDER_MISSING_INTERNAL`
- `OPEN_ORDER_MISSING_BROKER`
- `LEAVES_MISMATCH`
- `TERMINAL_STATE_MISMATCH`
- `STALE_BROKER_SNAPSHOT`
- `SEQUENCE_GAP`
- `UNRESOLVED_CANCEL_PENDING`
- `UNKNOWN_ACCOUNT_SCOPE`

Each break carries scope, instrument, as-of, source hashes, severity, blocking flag, resolution status, evidence refs, and deterministic description.

Severity policy:

- Critical duplicate conflict or unresolved authority gap -> `EMERGENCY_STOP`.
- Blocking reconciliation mismatch -> `BLOCK_NEW_ORDERS`.
- Manual evidence and informational observations -> no authority override.

Manual UI/export evidence remains `MANUAL_EVIDENCE`; it is never converted into a broker fill or position snapshot by the reconciler.
