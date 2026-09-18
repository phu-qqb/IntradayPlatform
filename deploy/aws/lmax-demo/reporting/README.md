# LMAX Demo daily acquisition and recap

## Autonomous position-report reader

`node Read-LmaxDemoPositionReports.mjs --execute` reuses the installed, pinned
Core PR61 downloader and the same approved EC2/profile/secret. Its existing
bracket mode reads trades/positions/trades/positions/trades, followed by the
complementary account reports. This requires no interactive trading-screen reader.
The command keeps the existing portal lease, owner/role checks, one acquisition
invocation and terminal security-denial behavior. No alternative host or account
API is used. Five focused contract tests cover empty reports, account/date scope,
staleness, tampering, source safety and the separate working-order authority.

Every result is retained in `D:\data\lmax-eod\logs\<run-id>\position-reader-receipt.json`;
the original CSVs and broker timestamp interval are retained under `position-snapshots`.
A successful receipt means the official position reports were read automatically.
It does not attest absence of unfilled orders or start trading. The existing report
contract's unproven explicit timezone is preserved; a historical report is never
retimestamped as a current observation.

For morning preparation, use official positions as the opening-position source,
then reconcile with the previous close and executions. Separately require terminal
status for every previous order, exclusive account activity and no unaccounted-for
activity between the report boundary and startup. A failed/incomplete report is
never interpreted as zero positions. This reader introduces no new trading gate
override: replacing the launcher's UI-only observation with report provenance must
be wired and qualified explicitly. Runtime qualification is recorded in #84.

The standalone `Build-LmaxDemoDailyRecap.mjs` produces a recap even when report acquisition
fails. It does not start the downloader, the Worker, an API or a database. It
never sends email. It validates the 14 native USD execution pairs described in
`LmaxDemoUsdExecutionUniverse`; the existing report-set importer remains responsible
for trades/wallet matching. Quantities remain per pair/base currency. Mixed-pair
quantities are never added into a fictitious aggregate position. Monetary values
remain in their official report currency, without inferred FX conversion.

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

Runtime: `C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-usd-20260918` (published candidate; consult the latest task-update receipt).
Inputs: `D:\data\lmax-eod\inbox\1754288005\YYYY-MM-DD\individual-trades.csv`,
or verified successful capture-run inboxes with the same exact account/date. Optional
`trades.csv` and `currency-wallets.csv` must accompany the selected individual
file in the same directory. They feed the existing report-set importer.
The latest verified capture precedes an operator-supplied inbox file; exports are never concatenated. Header-only
reports do not prove zero activity. Do not overwrite retained input files;
use a new capture directory for a subsequent export.

The daily runner invokes the existing, pinned Core PR #61 downloader through
`Run-LmaxDemoReports.mjs`. The wrapper verifies source hashes and the intended
EC2 role, enables its existing AWS Secrets session recovery, and validates the
real six-report manifest before import. Actual secret login, profile reopen and
historical report acquisition were qualified on 18 September; see
[retained evidence and remaining limits](AUTONOMY.md). No model-visible secrets,
new browser adapter, account API or raw-endpoint fallback is introduced.
Any security denial is terminal for that acquisition attempt.

Daily startup requires hashed qualification receipt references in the runtime
pin. `--verify-only` verifies those and runtime hashes without executing the
pipeline. `--local-only` skips acquisition and keeps provisional reporting
available. `Update-LmaxDemoDailyEod.ps1` upgrades the existing task only after
a successful authenticated acquisition/import receipt for the exact script.
It preserves principal, settings, triggers and the previous task XML.
`Update-LmaxDemoUsdReporting.ps1` similarly promotes the qualified 14-pair runtime
from the recovered-report runtime, requiring the exact published commit and retained
historical replay receipt; it does not refresh any broker-account observation.

Each invocation uses exclusive daily and portal reporting locks (never forcibly cleared),
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
immutable bundles are under `daily-runs`. The downloader has a five-minute
deadline and the importer a three-minute deadline, within the existing
ten-minute task limit. An uncertain downloader termination retains its portal
lock for inspection; no unrelated process is killed. There is one acquisition
attempt, with no blind credential retries. Exit 2 means evidence or reconciliation
remains incomplete, even if the report was successfully written. Exit 1 means
an execution/integrity problem; inspect the retained logs and scheduler result.

Qualification commands:
`node --test Build-LmaxDemoDailyRecap.test.mjs Run-LmaxDemoDailyEod.test.mjs Run-LmaxDemoReports.test.mjs`;
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

For non-EUR/USD executions, the recap requires a matching official sibling
`trades.csv` to establish report currency: exact account/day/symbol, consistent
signed/gross contract volumes and full-precision commission totals. Without this
proof, raw amounts are retained but USD economics remain unavailable and a break
is emitted. The previously qualified EUR/USD-to-USD convention is retained.
No simulated TCA is enabled in scheduled runs; the test fixture remains EUR/USD only.
The existing importer fails closed at its 500-row completeness boundary rather
than claiming reconciliation of a truncated day.

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
No Databento requests or email transport exist in these commands. The standalone
recap builder remains entirely offline; only the separately qualified downloader
performs portal access and reads the exact Demo credential secret.

The unsent email draft includes date, status, observed PnL/position, official
coverage, breaks and an unmistakable synthetic TCA label when present. Email
recipients, transport, scheduling and delivery retries will be decided later.
