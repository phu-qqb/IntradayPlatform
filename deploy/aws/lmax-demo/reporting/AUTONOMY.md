# LMAX Demo reporting autonomy

## Verified on 18 September 2026

Only EC2AMAZ-1QPHTD8 / Administrator / Demo account 1754288005 is in scope.
Control Tower QQ.Investment.Platform#100 and IntradayPlatform#84 remain authoritative.

Philippe approved and applied the exact `QQ-LMAX-Demo-PortalReports-Read-v1`
policy on `qq-role-ec2-intraday`. His IAM readback matched the proposed policy.
At 08:34:58 UTC the EC2 role independently succeeded in reading the exact Demo
secret at AWSCURRENT; its contract was valid. Only presence booleans were
recorded. This supersedes the 08:01:42 `NO_IDENTITY_BASED_ALLOW` failure.
No IAM management permission was added to EC2. The policy artifact retains its
`.proposed.json` name for traceability; this document records its applied state.

The existing Core PR #61 application, pinned to
`6dce3375aaaa7e8042c61a36467e81fe5e97cb49`, was invoked on its existing profile.
The PR remains open/unmerged. No alternate browser adapter, account API or
raw report endpoint was introduced. Source-manifest hashes are checked before
every invocation. Earlier download refusals must not be bypassed; a fresh
security denial remains terminal for the acquisition attempt.

At **08:51:33.506 UTC**, the real application recorded `session_already_active=false`,
`secret_fetched=true`, `login_performed=true`, and `mfa_mode=NOT_CHALLENGED`.
The account-scoped account-summary response was HTTP 200; its server date was
18 September 08:51:33 UTC. Closing and reopening the same profile then yielded
`AUTHENTICATED_REPORT_FORM_PRESENT`, with no secret read during the reopen probe.
Bootstrap receipt: `demo-reports-20260918T085026155Z-9b9a7105-75fb-4057-a93a-7081e3375eb0`.
Its acquisition-manifest SHA-256 is
`55cfbea5d0525596111e79136e01ec055b8f86bf5e8c109795bd6f662dbb65d8`.

At **08:52:55.502 UTC**, a separate normal invocation downloaded all six
reports for **17 September**: account summary, account statement, currency
wallets, trades, individual trades and open positions. Account, selected
date range, report forms, file paths, sizes and hashes were validated.
Receipt: `demo-reports-20260918T085251578Z-ebf4eb4c-22b4-4cca-aa6c-039f966f3b38`.
The individual-trades hash is the same as the previously obtained official
post-close export: `1f05ccf6f127f75e5d240aab9186f5f2c69a2fc4533f445f395b411a0553107b`.
These are historical-day reports acquired today, not an observation that the
account is currently flat or has no working orders.

Private receipts and manifests remain under `D:\data\lmax-eod\logs\<run-id>`;
raw captures remain under `D:\data\lmax-eod\captures`. Public GitHub contains
sanitized operational status only, never credentials or raw account reports.

## Daily integration and qualification gate

`Run-LmaxDemoReports.mjs` reuses that existing application and profile. Its
child-only environment removes legacy credentials/config references, then
requires the exact EC2 assumed-role identity before any authenticated run.
There is one acquisition attempt per run, no blind login retry. The exclusive
portal lock is never stolen. An uncertain child termination retains its lock
for inspection. Credentials stay within the existing downloader process.

`Run-LmaxDemoDailyEod.mjs` calls acquisition before choosing an import source.
Only complete, successful captures with a matching manifest and file hashes
are eligible as retained captures. An ordinary portal failure still produces
a provisional recap from valid same-date local data when available, with the
failure retained in its receipt and exit status. A partial failed download
cannot silently become a fallback source. No different-day data is substituted.

The runtime pin must include `portal_acquisition_qualified=true` and hashed
bootstrap/download receipt references. Startup validates the retained evidence,
including an actual secret-based login and the reopen proof. `--verify-only`
checks these references and runtime hashes without login, download or import.
`--local-only` retains the independent provisional reporting fallback.

For the initial portal-enabled upgrade, run the entire pinned daily
pipeline against the historical 17 September reports. Require an authenticated
acquisition and successful EOD import receipt; open reconciliation breaks must
remain visible. `Update-LmaxDemoDailyEod.ps1 -QualificationReceiptPath <receipt>`
requires that evidence, checks the old task target, saves its XML, and changes
only its action and description. It verifies unchanged principal, settings and
triggers after the update. The prior runtime remains available for rollback.
The deployment result and exact commit are recorded in issue #84 after readback.

The subsequent recovered-accounting upgrade uses
`Update-LmaxDemoRecoveredReporting.ps1`. It retains and verifies the same
authenticated acquisition qualification, then requires a successful local
historical pipeline receipt with four explicitly report-recovered fills, zero
breaks and zero FIX ExecutionReports. It changes only the task action and
description, preserving its principal, schedule and settings.

## Historical accounting recovery — 18 September 2026

At 09:59:06 UTC the separately authorized recovery tool committed the four
authentic 17 September executions: three fills of the durably sent entry and
one explicitly manual closing execution. The old model is marked recovered and
processed to prevent replay. Four signed ledger events net to zero. A fresh
reconciliation found zero breaks; twelve matching historical rows across three
prior runs were retained with audited Resolved status. No FIX reports were
invented and no journal or owner-lock bytes were changed.

The second-pass check exposed an EF mutation of the audit's before-state
projection. The immutable reviewed plan retained the original state. An exact,
append-only correction now links the audit to that plan; the original audit and
all economic rows were preserved. Repeating the operation subsequently returned
`ALREADY_APPLIED_VERIFIED`, without new writes. The regression suite has 25 tests.

The full daily pipeline succeeded at 10:10:21 UTC with zero breaks and explicit
`AUTHENTIC_OFFICIAL_REPORT_NOT_FIX` provenance for all four recovered fills.
This verifies the accounting recovery against its official source; it does not
provide independent FIX confirmation or a current account-position observation.

## Remaining limits

- The daily task is Administrator / Interactive / Limited, at 20:15 UTC on
  weekdays. It requires the host running and Administrator logged on. Windows
  logoff/reboot recovery is not qualified; no task-principal, OS account or
  credential-storage change is included.
- The Demo secret has no TOTP seed. More importantly, this pinned browser
  adapter does not submit TOTP: an actual MFA challenge stops acquisition.
  Adding a seed alone would not qualify unattended MFA.
- Core PR #61 and Intraday PR #103 remain reviewable candidates, not merged
  production releases. This deployment is the explicitly authorized Demo scope.
- Reporting does not certify the old actual-send session, qualify/deploy the corrected FIX Worker,
  or create a fresh official flat/no-orders observation. Trading stays blocked.
- Email remains a draft only. Real M15 TCA remains unavailable. Synthetic TCA
  is reserved for explicitly labelled test reports and is off in daily runs.
- Production credentials, PMS, Databento requests/downloads and unrelated
  processes are outside this workflow.
