# ARCH7B capture clock authority

Status: corrective offline contract. No capture, FIX connection, database
mutation, system clock change, or production access was performed.

## LMAX source timestamp

`source_timestamp_utc` comes from FIX header `SendingTime(52)` for both
`35=W MarketDataSnapshotFullRefresh` and
`35=X MarketDataIncrementalRefresh`.

The supplied LMAX FIX 4.4 dictionary defines tag 52 as a required
`UTCTIMESTAMP`: the UTC time the message was transmitted. The parser accepts
`yyyyMMdd-HH:mm:ss.fff` and `yyyyMMdd-HH:mm:ss`, so the implemented precision
is one millisecond when LMAX sends fractional seconds. Optional
`MDEntryDate(272)` and `MDEntryTime(273)` are parsed as entry metadata but do
not populate `source_timestamp_utc`.

Tag 52 is produced by the external LMAX FIX sender. `RecordedUtc` and
`SocketReceiveUtc` come from the local capture host. Missing or invalid tag 52
fails closed with `LMAX_MARKET_DATA_SENDING_TIME_MISSING_OR_INVALID`; socket
time is never substituted as external source time.

## Versioned snapshot

Contract: `pms_shadow_capture_clock_authority_v1`.

Every non-secret snapshot contains:

- capture UTC, host and reference clock sources;
- measured reference-minus-host offset, uncertainty and round-trip;
- sample count and the limits used;
- qualification status, synchronization state and leap indicator;
- last successful synchronization UTC;
- non-secret host identity, process ID and full repository commit;
- SHA-256 over all preceding canonical fields.

No credential, token, connection string or secret is permitted.

A qualifying snapshot requires:

- synchronized service and leap indicator 0;
- known host and reference sources;
- at least three samples;
- absolute measured offset at most 100 ms;
- uncertainty at most 100 ms;
- age at most 60 seconds;
- last successful synchronization no older than 15 minutes;
- exact host, repository commit and snapshot SHA.

`ARCH7B_CAPTURE_HOST_CLOCK_NOT_QUALIFIED` is the single fail-closed blocker.

## Slot evidence

`prearm-and-import` validates an initial snapshot before
`IMPORT_WATCHER_PREARMED`. `assert-prearmed` requires a second independent
snapshot immediately before capture. `publish-ready` requires another
independent snapshot immediately after close.

The capture and post-close snapshots must use the same host and reference
sources. Their offset delta may not exceed 100 ms. Each snapshot is copied
atomically as non-secret evidence and bound into the final manifest by
SHA-256. The ready marker carries both SHA values and is also bound to the
manifest SHA.

The command-line arguments are:

- `--clock-authority-preflight-snapshot` for `prearm-and-import`;
- `--clock-authority-capture-snapshot` for `assert-prearmed`;
- `--clock-authority-post-close-snapshot` for `publish-ready`.

## Cross-clock comparison

Raw `SourceTimestampUtc` and `RecordedUtc` are never changed.

Economic selection remains exactly:

`SlotStartUtc <= SourceTimestampUtc <= SlotEndUtc`.

Cross-clock causality uses `MEASURED_ENVELOPE_V1`. Maximum source lead is
derived from the larger measured reference-minus-host offset, measurement
uncertainty and one-millisecond FIX timestamp precision across the two
contemporary snapshots. No historical 1.2-second tolerance exists.

The manifest may expose a corrected recorded timestamp for validation, but
the raw recorded timestamp remains canonical provenance. Clock evidence never
moves source time, widens the slot or admits a post-close source quote.

Receipt/finalization is limited to two seconds after close, with measured
offset and uncertainty used only for validation. This is distinct from the
existing 300-second absolute import-start deadline and remains below the
10-second ready-marker SLO.

## Current host measurement

Read-only observation on `LAPTOP-PHU-QQB` on 2026-07-24:

- Windows Time service: running, manual start;
- synchronization: false;
- leap indicator: 3, not synchronized;
- stratum: 0;
- last successful sync: unspecified;
- effective host source: `Local CMOS Clock`;
- configured peer: `time.windows.com`, pending;
- direct three-sample reference: `time.windows.com` at
  `51.145.123.29:123`;
- offsets: +1721.7926 ms, +1722.5375 ms and +1725.1759 ms;
- round trips: 24.3254 ms, 24.6169 ms and 30.1628 ms;
- NTP server leap indicator 0, stratum 4.

This is operational `NO_GO`: offset is about +1.72 seconds and the Windows
service itself is unsynchronized. It must not be masked. No resynchronization
was attempted in this PR. A separately authorized operation may inspect
configuration, run `w32tm /resync`, then take new independent measurements;
capture remains prohibited until those measurements pass the contract.

## Historical evidence

The immutable 10:30Z slot has an approximately +1.11-second source/local lead,
34/49 legacy post-close selections, and no contemporary clock snapshots. It
remains non-qualifying. An offset measured today may not reinterpret or rewrite
that artifact, manifest, database row or economic revision.
