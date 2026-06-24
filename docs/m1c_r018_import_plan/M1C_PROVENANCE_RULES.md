# M1C Provenance Rules

Supported provenance classes:

- `RAW_FIX`
- `EOD_LMAX_REPORT`
- `ARTIFACT_LEDGER`
- `MANUAL_EXPORT`
- `MANUAL_UI`
- `DERIVED`
- `UNKNOWN`

Each normalized event carries source path, source file hash, row/message locator, parser version, authority class, and raw payload hash.

`MANUAL_UI` is observation-only. It can support an exception/evidence note, but it cannot create `ExecutionReport`, `Fill`, or `PositionLedgerEvent`.
