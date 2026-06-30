# M3 Real Account Download Readiness

Status: NOT READY FOR REAL IMPORT WITHOUT EXPLICIT GATE.

## Current Mapping State

Approved mapping:

- `921640160` -> `Quantum QB Master Fund Limited`
- allowed use: `EOD_REPORT_IMPORT` and dry-run report evidence only
- order entry authority: false
- live broker-state authority: false
- pre-trade authority: false

Test-only mapping:

- `1754288005` -> `LMAX_TEST_EOD_ONLY`

## Preconditions Before Any Real Account Download Gate

- Browser automation must be limited to report download pages.
- No trading page automation.
- No AccountAPI, Java client, .NET client, FIX logon, Order Entry, Databento, DB apply, AWS, or Terraform change.
- Download manifest must capture selected account id, report date, timestamp UTC, file names, sizes, hashes, and row counts.
- Staging must use `data/lmax-eod/inbox/<broker_account>/<yyyy-MM-dd>/<report_type>.csv`.
- Real files must remain quarantine or dry-run evidence until a dedicated import gate approves scope.

## M3G Status

`M3G` remains blocked for any account without explicit internal fund/account scope mapping. Do not infer mapping from filenames or portal labels.
