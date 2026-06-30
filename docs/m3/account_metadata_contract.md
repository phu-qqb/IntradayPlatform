# M3AA Account Metadata Contract

Status: NO-LIVE hardening contract.

## Purpose

LMAX portal account summary and report pages may expose monetary account fields whose labels contain the word `Account`. Those fields must never be interpreted as broker account identifiers.

## Authoritative Inputs For Import Scoping

Allowed account identity evidence, in priority order:

1. Portal selected account id captured by the acquisition manifest.
2. Exact `Account Id` column from reports that contain that header.
3. Staging path account segment under `data/lmax-eod/inbox/<broker_account>/<yyyy-MM-dd>/...`.
4. PDF account number only as supporting evidence.

PDF evidence is not primary authority because it may be exported separately from CSV files. A mismatch between PDF account number and selected/report/staging account blocks the import.

## Explicit Non-Inputs

The following are monetary fields and must not be detected as account ids:

- Account Value
- Cash Balance
- Margin
- Margin on Open Position
- Open Profit / Loss
- Net Liquidation Value

## Blocking Classifications

- `BLOCKED_AMBIGUOUS_ACCOUNT_METADATA`: no selected account id and no exact report `Account Id` metadata.
- `BLOCKED_ACCOUNT_MISMATCH`: selected/report/staging/PDF account evidence disagrees.
- `QUARANTINE_UNKNOWN_ACCOUNT`: account id is present but has no approved mapping.

## Safety

This contract does not perform browser login, broker network access, AccountAPI, FIX, Order Entry, Databento, or DB apply.
