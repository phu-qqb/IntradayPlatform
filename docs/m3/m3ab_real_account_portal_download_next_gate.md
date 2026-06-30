# M3AB Real Account Portal Download Next Gate

This is the proposed next gate after M3AA.

## Gate Name

`M3AB_REAL_ACCOUNT_PORTAL_DOWNLOAD_DRY_RUN_NO_PROD_DB`

## Scope

- Portal report download only.
- Real account `921640160` may be used only under its approved dry-run EOD/report evidence mapping.
- No production DB mutation.
- DB APPLY = false.
- No Order Entry.
- No AccountAPI / Java / .NET client.
- No FIX logon.
- No Databento API/download.
- No AWS/Terraform changes.

## Required Reports

- individual-trades.csv
- trades.csv
- currency-wallets.csv
- account summary export or metadata manifest if available
- open-positions.csv as candidate EOD evidence only

## Required Validation

- selected portal account id captured before download;
- exact staging path account matches selected account;
- PDF account number, if present, matches selected account;
- source SHA-256 and row-level provenance recorded;
- repeated dry-run import idempotent;
- cross-account merge forbidden;
- open-positions remains non-authority pending contract closure.

## Expected Decision

The gate may approve dry-run evidence capture/import only. It must not approve live broker-state authority, pre-trade authority, open-order authority, Order Entry, or production DB apply.
