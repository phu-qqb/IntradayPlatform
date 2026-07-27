# ARCH7B PostgreSQL Database Clock Authority

## Failed Live Run

The append-only verdict remains:

`NO_GO_ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_AUTHORITY_CONVERSION_INVALID`

Run `fresh-no-order-pms-slot-20260727T1830Z-bc662887` stopped before any
LMAX acquisition or database write. Its evidence remains immutable at:

`D:\QQFund\ARCH7B\fresh-no-order-pms-slot-20260727T1830Z-bc662887`

- manifest SHA-256:
  `127bc418573669a605932acf3956f9b4bab82704736a30f39f9e757cae63ed60`;
- ZIP SHA-256:
  `8cfd13a080c93af5819810bf4249bb1c83b8ce9328c5722f89da3da4bbda1f02`.

The former reader converted Npgsql's typed timestamp to an invariant string.
Because that string contained no offset, `DateTimeOffset.Parse` interpreted it
with the Windows host timezone. On a UTC+02 host, database time `18:09:38 UTC`
was therefore reported as `16:09:38 UTC`.

## Contract

Contract `arch7b_postgresql_database_clock_authority_v1` uses one PostgreSQL
instant:

```sql
WITH sample AS
(
    SELECT clock_timestamp() AS database_clock
)
SELECT
    database_clock,
    EXTRACT(EPOCH FROM database_clock)::numeric,
    pg_typeof(database_clock)::text,
    current_setting('TimeZone')
FROM sample;
```

`DbDataReader` must return either:

- `DateTime` with `Kind == Utc`; or
- `DateTimeOffset` with `Offset == 00:00`.

The PostgreSQL type must be `timestamp with time zone`, the session timezone
must be `UTC`, and the typed value must differ from the same-instant epoch by
no more than one microsecond. No timestamp is parsed from text. Diagnostic host
timestamps never participate in freshness.

`arm-import`, `publish-ready`, and `apply-import` consume only
`Arch7bPostgreSqlClockSample.DatabaseUtc`.

## Read-Only Qualification

Mode `qualify-database-clock`:

- validates the exact `ARCH7B_RDS_TEST` target and fingerprint;
- uses TLS `VerifyFull`;
- opens a repeatable-read transaction and executes
  `SET TRANSACTION READ ONLY`;
- reads three independent monotonic samples;
- emits `sample-a.json`, `sample-b.json`, `sample-c.json`, `report.md`,
  `manifest.json`, and a deterministic ZIP;
- creates no armed state, owner lock, ready marker, application row, LMAX
  acquisition, FIX session, order, Fill, or ledger event.

## Blockers

- `ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_CLR_MAPPING_INVALID`
- `ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_EPOCH_MISMATCH`
- `ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_TYPE_INVALID`
- `ARCH7B_POSITION_IMPORT_DATABASE_TIMEZONE_NOT_UTC`

The corrective qualification does not resume the failed live run and does not
select a replacement slot.
