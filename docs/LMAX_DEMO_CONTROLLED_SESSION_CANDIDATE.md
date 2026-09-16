# Controlled LMAX Demo session: reconciliation candidate

This candidate addresses the state and durable evidence portion of
[#84, review 5697076664](https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5697076664).
It is **not wired into the Worker, an execution gateway, or a FIX socket**.
It does not make the existing one-cycle bridge suitable for a full trading day.
No new trading permission or broker observation is created by these tests.

## Implemented boundary

`LmaxDemoControlledSession` consumes the existing `Arch7bExecutionReportEvent`
contract. The candidate does not use the Arch7b PostgreSQL/PMS Shadow runtime.
The separate JSONL journal records normalized session, intent, socket-completion,
report, duplicate, rejection and fault facts, with a hash chain and a disk flush
before each append returns. Logon frames and credentials are not journal fields.
The hash chain detects accidental corruption; it is not broker authentication or
protection against someone who can rewrite the entire file.

Initial qualification is pinned to Demo account `1754288005`, an immutable UTC
observation, a declared instrument scope, flat/no-working-order observations and
exclusive order activity. Its maximum age remains capped at 900 seconds.
Continuing qualification depends on ordered inbound events, a bounded same-day
deadline, resolved sends/cancels and no latched fault. The original observation
is never refreshed. The current 90-second inbound-silence limit is a candidate
policy that still needs transport/heartbeat integration review.

For each symbol, the next proposed quantity is:

```text
target base quantity - fill-derived base position - signed known working leaves
```

Venue quantities are converted using the start-of-session contract-size binding.
A different target while an order remains live requires evidenced cancellation
before another child can be sent. Cancel completion is not assumed after a socket
write. Canceled leaves do not erase past fills or prove that the parent target is
complete. The same cycle/symbol keeps a stable economic parent across children.

Execution IDs are deduplicated on economic/report fields rather than the
transport sequence, PossDup flag or raw-message hash. Conflicting duplicates,
untracked identities, cumulative fill gaps, inconsistent leaves, sequence gaps,
unsupported states and lost continuity latch a submission blocker and retain
the rejected fact. New unique reports after a terminal child require recovery.
Only the documented D/F send intents and supported report transitions are
accepted; replace, cancel-reject and sequence-recovery flows are not implemented.

An intent must be recorded **before** attempting the transport write.
`RecordSendCompleted` proves neither acknowledgement nor execution. An unresolved
intent/cancel stops another submission. Reopening an existing journal is
inspection-only; a new process cannot resume writes or reset that journal to an
empty session. Final closure requires a fresh flat/no-working-order observation
after the last accepted lifecycle evidence, zero projected position and terminal
known orders. Closure is allowed after the submission deadline.

## Qualification

All fixtures set `Simulated = true`. No test contacts LMAX, a database, Databento,
or the Worker. Run the repository test project with the .NET 10 SDK:

```powershell
dotnet test tests/QQ.Production.Intraday.Tests.DemoSession/QQ.Production.Intraday.Tests.DemoSession.csproj --configuration Release
```

The 34 cases cover two cycles starting 16 and 32 minutes after the unchanged
initial observation; a nonzero position and partial working order; duplicate
reports; cancellation with a late fill; a later working child after an earlier
cancel; interrupted sends with/without socket completion; read-only restart
inspection preserving earlier fills and uncertain intents; rejection cases;
initial qualification; post-deadline closure; and corrupt/truncated journals.

EC2 qualification on 2026-09-16 compiled these two new C# files against the
Application and Domain assemblies already in the nonactive
`C:\deploy\IntradayPlatform\releases\lmax-demo-cycle-c81b739c` release, using SDK
10.0.400. Result: **34 passed, 0 failed, 0 skipped**. This was a narrowed harness,
not a full solution build or a validation of the runtime bridge. One xUnit
analyzer style warning (`xUnit2031`) remained.

Evidence is under
`C:\deploy\IntradayPlatform\staging\lmax-session-qualification-20260916`:

- `qualification-result-qualified.json`: identity, time, exit code and file hashes.
- `qualification-output-qualified.log`: compilation and test summary.
- `TestResults\simulated-session-qualified.trx`: individual test results.
- Earlier failed runs are retained. The first exposed Windows reader/writer
  sharing, which was corrected. The next build accidentally included the backed
  up sources; the harness was restricted to the two explicit input files.

The active Worker and the successful LocalDB diagnostic were not changed.

## Required before execution integration

1. A single account/session owner and fixed journal catalog must prevent a host
   from choosing a different filename to bypass an unresolved prior session.
   `FileMode.CreateNew` protects one path, not account-wide ownership. The current
   API relies on serialized calls from one event loop and is not thread-safe.
2. A continuously owned FIX receiver must validate framing, sequencing and
   account/instrument mapping, deliver every report, and record any receive,
   parser, journal or transport failure. The existing per-parent connection
   lifetime and buffer reader are not that receiver. Do not dispose it merely
   because a parent call has returned while an order may remain live.
3. The actual send boundary must enforce intent-before-write and fault on an
   ambiguous write. Existing repository execution/fill/position persistence must
   be integrated with durable journal replay and idempotency; this candidate
   does not make a filesystem append and a repository write atomic.
4. Gateway position/open-order seams and Worker scheduling must use this owned
   reconciled session across cycles. Known-order projection is not an external
   account-wide order enquiry. Keep existing execution limits and fresh target
   lineage checks, and qualify the real cancel/report mapping.
5. Review the connected path before enabling it. A real controlled Demo day
   still requires a fresh official UI start observation, actual FIX lifecycle
   and fill evidence, final UI reconciliation and EOD corroboration. Header-only
   reports do not establish flat/no-working-order state. Ambiguous restart
   recovery and unattended cold starts remain separate, unimplemented work.

The report-only launcher and portal session are independently qualified in
[Intraday PR #100](https://github.com/phu-qqb/IntradayPlatform/pull/100) and
[Core PR #61](https://github.com/phu-qqb/QQ.Production.Core/pull/61).
