# LMAX Demo daily recap: offline fallbacks

This additive reporting command produces a recap even when report acquisition
fails. It does not start the downloader, the Worker, an API or a database. It
never sends email. It currently validates **EUR/USD Individual Trades only**;
the existing report-set importer remains responsible for trades/wallet matching.

Run after the existing report acquisition step, including when that step fails:

```powershell
node Build-LmaxDemoDailyRecap.mjs config.json D:\data\lmax-eod\recaps\NEW_RUN
node --test Build-LmaxDemoDailyRecap.test.mjs
```

The output parent must exist. Every run gets a new output directory and writes
`recap.json`, `recap.md`, `email-draft.txt`, and a SHA-256 `manifest.json`.
Reports always remain provisional until the existing EOD reconciliation is run.
This is a separately runnable reporting component, not a scheduled task or an
automatic hook into the trading launcher. Email scheduling/transport is deferred.

## Input contract

- `environment`: exactly `LMAX_DEMO`; `date`: ISO date; `account_id`: digits.
- `expected_order_ids`: all known day-order identifiers. Required operationally
  whenever orders are known, so an entry-only file cannot represent a closed day.
- `trade_candidates`: ordered array of `{path, sha256?}`. Put the fresh capture
  first, same-day retained export second, operator-supplied export third.
- Every candidate is checked for schema, exact account/date, nonempty execution
  identifiers, duplicates, numeric values, expected orders and optional hash.
  First valid candidate wins; overlapping exports are never concatenated.
  Header-only exports mean insufficient evidence, never zero account activity.
- `portal_observation` (optional): account/date, `observed_at_utc`,
  `source_reference`, numeric `gross_pnl_usd`, signed `entry_commission_usd`,
  signed `exit_commission_usd`, `position_units`, `working_orders`.
  This is a dated observation, never a live assertion or a ledger fill.
  Net is explicitly approximate because the closing fee is UI-rounded.
- `internal_evidence` (optional): preserved as separate evidence; use status
  `FIX_CONTINUITY_LOST` to emit the explicit unresolved break.
- `simulated_tca` is off by default. Requires `owner_authorized: true` and label
  `SIMULATED_NOT_FOR_ECONOMIC_VALIDATION`. Its orders contain `id`, signed
  `units`, positive `execution_price`, positive `benchmark_price`, and a
  nonnegative fee charge `fee_usd`. All these scenario inputs are fictional.

For EUR/USD, cost-positive shortfall is signed EUR quantity ×
(execution price − benchmark). All-in adds the positive commission charge.
USD/M divides by absolute EUR quantity × benchmark USD/EUR price. Aggregate
ratios use the sum of notionals, never the average of individual ratios.

## Recovery contract

1. Acquisition succeeds: consume exact-date/account export and retain its hash.
2. Ordinary acquisition failure: use already retained local files, if valid.
3. No complete valid export: publish a provisional recap using independently
   retained observations; list missing evidence and all failed candidates.
4. Missing real M15 context: keep economic TCA unavailable. An explicitly opted-in
   synthetic section may exercise report presentation without changing evidence.
5. Late official files: generate a new immutable run, then use the existing
   report-set import/reconciliation workflow. Never overwrite past evidence.

Security or access denials are terminal for acquisition; this command must not
be used to route a denied download through another browser, host, API or proxy.
No Databento requests, credentials, network or email transport exist in the code.

The unsent email draft includes date, status, observed PnL/position, official
coverage, breaks and an unmistakable synthetic TCA label when present. Email
recipients, transport, scheduling and delivery retries will be decided later.
