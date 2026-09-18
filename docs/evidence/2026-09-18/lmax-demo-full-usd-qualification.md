# LMAX Demo full native USD qualification — 18 September 2026

This checkpoint follows the initial [netting readiness inspection](lmax-demo-usd-netting-readiness.md).
It records completed reference configuration, launcher/Worker deployment and
reporting qualification. **It is not a session-start receipt. No new order was sent.**

Scope is Administrator on EC2AMAZ-1QPHTD8, Demo account 1754288005 only.
The Control Tower and all four linked North Stars were reread before this work.
Philippe's full-universe instruction and standing Demo authority remain subject
to genuine account-state, reconciliation, ownership and runtime checks.

## Authentic contract reference and applied configuration

The public [LMAX Global UK page](https://www.lmax.com/global/uk) links the official
[London LD4 instrument CSV](https://assets.lmaxstatic.com/csv/LMAXGlobal-uk-Instruments-LD4.csv).
The file was retained on the authorized EC2 host at 2026-09-18T11:15:55.2372762Z:

- Path: `D:\data\lmax-demo-reference\public-LD4-20260918T111554Z.csv`.
- SHA-256: `5fe68bab4715034d85cb355a74f40f2d27b656acf7ff3c50418f1bdaf70be309`.
- All 14 contract multipliers are 10000, with published minimum order 0.1 contracts.
- QQ uses one published minimum lot as its conservative quantity quantum (0.1).
  The CSV does not separately publish an increment field. No fee terms are inferred.

| Native symbol | LMAX SecurityID | Tick size |
|---|---:|---:|
| EURUSD | 4001 | 0.00001 |
| GBPUSD | 4002 | 0.00001 |
| AUDUSD | 4007 | 0.00001 |
| NZDUSD | 100613 | 0.00001 |
| USDJPY | 4004 | 0.001 |
| USDCHF | 4010 | 0.00001 |
| USDCAD | 4013 | 0.00001 |
| USDHUF | 100501 | 0.001 |
| USDMXN | 100507 | 0.00001 |
| USDNOK | 100513 | 0.00001 |
| USDPLN | 100523 | 0.00001 |
| USDRON | 100931 | 0.00001 |
| USDSEK | 100529 | 0.00001 |
| USDZAR | 100547 | 0.00001 |

`verify-reference` first applied and read back the changes inside a serializable
transaction, then rolled back at 11:39:42.2926087Z. Actual `apply-reference` committed
46 audited changes at **11:43:17.0045993Z**, with no remaining configuration issues:
six instrument/mapping pairs enabled, seven native instruments/mappings/report
aliases added, and thirteen instrument risk bindings added. Each added risk rule
reuses the existing exact EURUSD policy: USD 500000 per trade, USD 1500000 instrument
exposure, minimum zero and 100 orders. Existing USD 2000000 gross exposure and all
numeric ceilings are unchanged. This extends policy coverage; it is not a bypass.

Audit ID: `3c454075-0301-464c-9392-b92b7f33608e`. Immutable receipts:
`D:\data\lmax-demo-reference\verify-full-scope-20260918.json` and
`D:\data\lmax-demo-reference\applied-full-scope-20260918.json`.
The deployed launcher's independent `inspect-reference` also passed all 14 legs.

## Qualified and deployed trading candidate

The focused C# suite passed **61/61 tests**, including the existing netting, USD-base
sizing, risk valuation and FIX continuity/parser tests, plus fourteen recovered-send
retirement cases. Recovery tool, launcher and Worker Release builds had zero errors.
Existing dependency/analyzer warnings remain; dependencies were not upgraded here.
The launcher passed **44 self-tests** without broker or database calls.

Deployment root:
`C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\usd-session-20260918`.

| Deployed component | SHA-256 |
|---|---|
| `worker\QQ.Production.Intraday.Worker.dll` | `794f3dc9abbf7843cfc3f269a572f50918d5b89c645b2e3837e8155a99948b78` |
| `worker-manifest.json` (152-file closure) | `8f1dad1c1a4e3cb391a5b29075e9acefd8be640be327c7f6e52d370f2d7f44fb` |
| `launcher\QQ.Production.Intraday.Tools.LmaxDemoDayLauncher.dll` | `867f9762d93612fd02330037269cb5519476650bb25663302838ea73ce2d81cd` |

The launcher checks the complete Worker closure, exact full observation scope,
reference/risk readiness and real per-leg market data before startup. Its final
scheduled reduction uses the same 14-leg scope. All manager signal rows still
enter the previously implemented Qubes/Anubis currency netting before execution
selection. No raw-cross execution, silent missing-currency exclusion or EURUSD-only
fallback is introduced.

Actual `inspect-worker` passed exact Demo identity, credentials-present and configuration
checks without opening any broker connection or accessing the database. Actual
`inspect-marketdata` passed **14/14 legs** between **11:46:26.4395317Z and
11:46:34.1222124Z**, with matching symbol/SecurityID and fresh bid/ask evidence.
Each check recorded no order connection, zero orders and no database access.
These are dated market-data observations, not proof of current positions or order-FIX logon.
Retained log: `C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\native-usd-marketdata-preflight.log`.

## Recovered actual-send retirement: implemented, not exercised

The exact-incident `inspect-retirement` readback at 11:39:47.3222732Z verified the
durable recovery and authoritative before/after audit: four recovered fills, four
ledger events, net zero, zero open children, zero nonzero positions, zero open
reconciliation breaks and zero independent historical FIX execution reports.

The new `retire-session` command additionally requires a retained authentic official
Demo UI inspection, exact account/origin, explicit flat/no-working-orders evidence,
matching capture hash/timestamp and age below 900 seconds. It holds the account
owner lease and a serializable read transaction while writing an immutable sidecar.
It cannot change journal bytes, book economic activity or fabricate FIX messages.
Historical sidecar validation never substitutes for a new session's fresh observation.

**No retirement certificate has been written.** At **11:55:32.0502372Z**, executable-
scoped process inspection found zero Worker and zero active `start-worker`/`run-day`
processes. The original actual-send journal remained 467585 bytes with SHA-256
`f133311e31c47aaf9fcbd817712b27d26e47ce3e30f3bedeb7c21118da83af49`;
`owner.lock` remained empty with SHA-256
`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

The available browser connection was inspected: only a remote cloud Chrome binding
was advertised, without any EC2/profile attachment capability. The control-browser
skill permits browser control only through that supported binding. No LMAX page was
opened there, no alternate-host login/download was attempted, and no positive UI
inspection was fabricated. This identifies a missing current observation path in
this chat; it does not prove that an approved local reader cannot be implemented.
The existing qualified Core PR61 adapter remains report-only and cannot attest the
live positions/working-orders screen. Its route guards were not weakened.

## Full-universe provisional reporting

`Build-LmaxDemoDailyRecap.mjs` now accepts the 14 native symbols, validates any
provided SecurityID, and preserves quantities per pair/base currency. It never
adds EUR units to USD or other base units. Monetary fields remain in the official
report currency; no conversion is invented from an execution price.

For non-EURUSD rows, currency attribution requires a matching official `trades.csv`
with the same account/day/symbol, consistent signed/gross contract volumes and
full-precision commission totals. Missing/conflicting evidence leaves economics
unavailable with an explicit break. The qualified historical EURUSD/USD convention
is retained. Simulated TCA remains isolated to explicitly marked EURUSD test reports.

Candidate reporting operator:
`C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-usd-20260918`.
Its three Node suites passed **20/20 tests**. Runtime hashes and the unchanged
retained portal-authentication qualification passed `--verify-only`.
The local-only **17 September historical** pipeline completed at **11:48:07.001Z**:
four official rows/fills, zero reconciliation breaks, zero independent FIX reports,
no economic-ledger mutation, no email and no trading. Reconciliation run:
`74c59400-8043-4481-9fe1-55b5858d5c49`.
Receipt directory:
`D:\data\lmax-eod\daily-runs\2026-09-17-2026-09-18T114801443Z-fa34e20c-00ab-4ae2-bd89-8c4d9ff8c74e`.

`Update-LmaxDemoUsdReporting.ps1` promotes only this qualified runtime and an exact
published commit, preserving the existing task principal/settings/triggers and
backing up its XML. The actual promotion/readback is recorded in the subsequent
#84 checkpoint. Schedule remains weekday 20:15 UTC and requires Administrator logged
on. Existing importer completeness checks fail closed at 500 rows; real M15 TCA,
logoff/reboot operation and unattended MFA are not qualified by this work.

## Remaining startup gate

Obtain the genuine current official account observation through the authorized EC2
profile/reader, then perform audited retirement. Obtain/bind a valid fresh full-scope
startup observation and use documented `start-worker --observation` followed by
supervised `run-day --session-id` with a new ID. Recheck live prerequisites and keep
the natural programme calendar; no replay or advanced first order. Require actual
FIX logon/start receipts before reporting trading as started.

No Production/PMS runtime access, IAM/OS/principal change, Databento request, email,
unrelated process termination, forced owner lock or portal-denial workaround occurred.
