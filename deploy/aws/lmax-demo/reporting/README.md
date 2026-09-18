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
The recurring entry point is now `Run-LmaxDemoDailyEod.mjs`, backed by the
report-only `QQ.Production.Intraday.Tools.LmaxDemoEod` executable. The original
standalone recap command remains available. Email delivery is deferred.

## Recurring Demo installation (18 September)

`Install-LmaxDemoDailyEod.ps1` registers `QQ-LMAX-Demo-Daily-EOD` at 20:15 UTC
(22:15 Swiss summer / 21:15 winter), with an explicit weekday check. This is
after the latest existing programme close and does not alter order timing.
The task uses Administrator's interactive logon, without stored credentials;
the machine must be running and Administrator must remain logged on. A
disconnected RDP session may remain logged on; logging off prevents the task.
No claim of unattended service operation while logged off is made.

Runtime: `C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-20260918`.
Inputs: `D:\data\lmax-eod\inbox\1754288005\YYYY-MM-DD\individual-trades.csv`,
or existing capture-run inboxes with the same exact account/date. Optional
`trades.csv` and `currency-wallets.csv` must accompany the selected individual
file in the same directory. They feed the existing report-set importer.
The latest valid candidate wins; exports are never concatenated. Header-only
reports do not prove zero activity. Do not overwrite retained input files;
use a new capture directory for a subsequent export.

**Acquisition remains `LOCAL_EXPORTS_ONLY_UNATTENDED_PORTAL_NOT_QUALIFIED`.**
The task does not call the remote downloader, another browser, an account API
or credentials. Existing report-launcher captures are consumed when available;
the successful cloud export does not qualify a different unattended browser
session. Do not route earlier security refusals through the remote downloader.
This is recurring import/reconciliation/reporting, not a completed autonomous
portal-acquisition chain.

Each invocation uses an exclusive reporting lock (never forcibly cleared),
an immutable dated run directory and hashed source/runtime evidence. The EOD
executable validates the account, dates and source hashes, previews before
writing, imports/reconciles in one database transaction and rejects conflicting
existing execution or wallet evidence. No schema migration runs automatically.
Execution IDs deduplicate repeated imports. Internal fills, FIX reports, owner
journals and trading readiness remain unchanged. A receipt is written only
after commit; a crash around commit is ambiguous and requires inspection.

Every ordinary missing-export/import failure produces a PROVISIONAL recap
and unsent email draft. No fabricated current observation or synthetic TCA is
enabled in scheduled runs. Real M15 TCA remains unavailable without benchmarks.
The latest operational receipt is `D:\data\lmax-eod\latest-daily-eod.json`;
immutable bundles are under `daily-runs`. Exit 2 means evidence or reconciliation
remains incomplete, even if the report was successfully written. Exit 1 means
an execution/integrity problem; inspect the retained logs and scheduler result.

Qualification commands:
`node --test Build-LmaxDemoDailyRecap.test.mjs Run-LmaxDemoDailyEod.test.mjs`;
`node Run-LmaxDemoDailyEod.mjs --date 2026-09-17`.
The latter is a historical replay/import, never a current-state attestation.

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
