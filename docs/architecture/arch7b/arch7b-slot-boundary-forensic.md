# ARCH7B 10:30Z slot-boundary forensic

Status: read-only historical analysis. No artifact, database row, manifest, or
economic revision was rewritten.

## Source

The inspected source is the closed 10:30Z LMAX Demo market-data-only run
`M2C1B_LMAX_DEMO_MD_20260724102358348` and its immutable slot
`pms-shadow-15m-20260724T1030Z`. The economic window is
`[2026-07-24T10:30:00Z, 2026-07-24T10:45:00Z]`.

The raw chunks contain 668 GBPUSD `BBO_UPDATED` events: 410 have a source
timestamp in the slot and 47 are post-close. The legacy manifest selected 49
symbols, of which 34 have a post-close source timestamp. All 49 selected rows
also have `SourceTimestampUtc > RecordedUtc`, exposing a separate local/source
clock-provenance violation that must remain fail-closed.

## GBPUSD close timeline

Last source timestamp in the economic window:

- event ID: `evt-000000053756`
- FIX sequence: `26834`
- source timestamp: `2026-07-24T10:44:58.637Z`
- recorded timestamp: `2026-07-24T10:44:57.5237679Z`
- bid/ask: `1.33239 / 1.33249`
- quote event ID:
  `lmax-md-M2C1B_CAPTURE-26834-4002-17f60f58af8b`

First source timestamp after close:

- event ID: `evt-000000053830`
- FIX sequence: `26871`
- source timestamp: `2026-07-24T10:45:00.125Z`
- recorded timestamp: `2026-07-24T10:44:59.0137045Z`
- bid/ask: `1.33240 / 1.33248`
- quote event ID:
  `lmax-md-M2C1B_CAPTURE-26871-4002-26a6d97907ee`

The legacy `last_bbo_by_symbol.GBPUSD` is the second event. The artifact was
snapshotted through recorded time `2026-07-24T10:44:59.6325629Z`, while the
recorder continued updating the book after close. Manifest construction used
the current book state without bounding it by source timestamp.

## Selection result

The legacy selection used the post-close `10:45:00.125Z` event. A
source-window-only cutoff would instead identify `10:44:58.637Z` as the last
GBPUSD event in the slot. The full authoritative contract additionally requires
`SourceTimestampUtc <= RecordedUtc`; therefore neither historical event is
qualifying and the immutable 10:30Z slot remains negative evidence.

The PostgreSQL dry-run must continue to fail with
`ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SLOT_MISMATCH`. No
epsilon, close-time clamp, second truncation, grace period, data correction, or
revision rewrite is permitted.

## Corrected future contract

Future finalization selects only valid LMAX events in the inclusive source
window. Ordering is source timestamp, recorded timestamp, FIX sequence, source
receive sequence, process sequence, and event ID. The existing 300-second
handoff boundary is reused only as a receipt/finalization deadline. It does not
extend the economic slot.

Post-close source events are counted and excluded from selection. A required
symbol with no valid in-window event makes the capture non-qualifying and
prevents ready-marker publication. The reader independently rejects a
post-close selected event with
`RAW_SLOT_BBO_SOURCE_TIMESTAMP_OUTSIDE_WINDOW`.

No secret, connection string, FIX credential, account credential, or
production identifier is included in this report.
