# LMAX Demo day reports

`Run-LmaxDemoReports.mjs` is the report-only entry point for EC2AMAZ-1QPHTD8,
under its local Administrator identity. It calls the existing Core report
downloader and pins the isolated candidate in Core PR #61 (`6dce3375`).
That PR fixes the initial login redirect; it is not merged at this handoff.

Run from the Demo desktop terminal:

```powershell
node C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\Run-LmaxDemoReports.mjs --date 2026-09-16
node C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\Run-LmaxDemoReports.mjs --date 2026-09-16 --execute --interactive
```

The first command only plans the reports. The second opens the dedicated
Chrome profile and waits up to ten minutes for the owner to authenticate on
the official Demo portal for account `1754288005`. Enter credentials and any
MFA code in that browser. After a successful bootstrap, subsequent captures
can use `--execute` without `--interactive`. Omit `--date` for the current UTC
date. Run one capture at a time; the persistent Chrome profile must be free.

Each invocation retains separate `portal-downloads/<run-id>` and `logs/<run-id>`
directories under `D:\data\lmax-eod`. Its import staging directory is
`captures/<run-id>/inbox/1754288005/<report-date>` so the existing Intraday
account/date resolver sees the account immediately after `inbox`. Read the printed `command-result.json`
location and acquisition manifest before importing anything. The launcher
does not import reports into the database. Use the existing Intraday import
path only after validating the actual downloaded set and account mapping.

The first authenticated capture on 16 September 2026 completed at 13:51:50Z.
All five current-day CSVs contained only headers; a separate 15 September
control returned one row in each account-summary and currency-wallet report,
with the expected account. This establishes acquisition and date selection,
not a zero balance or the absence of working orders. Keep the header-only
capture as evidence and do not substitute prior-day values for current state.

This entry point does not implement or activate `Invoke-LmaxDemoFullCycle.ps1`.
That full-day launcher remains absent. Its Worker path needs the separately
reviewed continuing-session implementation described in Intraday #84,
comment 5697076664: original UI observation, journaled actual FIX activity,
known working orders, fill-derived positions, and ambiguous-restart blocking.
Repeated one-cycle Worker invocations or a refreshed attestation timestamp
do not provide that continuity. The historical 11:55:47Z observation remains
historical. Reports also cannot establish the absence of working orders.

The LocalDB diagnostic remains complete and its disabled task is unchanged.
This report launcher changes no scheduled task or PowerShell execution policy,
starts no Worker, accesses no Databento API and requests no AWS secret.
