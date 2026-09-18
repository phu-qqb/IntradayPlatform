# Demo parent lifecycle correction — candidate, not activated

## Problem and change

The existing Demo strategy bridge used the latest FIX `LeavesQty` both as a
physical child's working quantity and as the logical parent's residual target.
A cancel acknowledgment with zero leaves could therefore erase an unfilled
target. Taking the maximum child `CumQty` also undercounted fills across a
canceled passive child and its replacement.

`LmaxDemoParentLifecycle` separates those facts. It adds fills across physical
children, checks their order identities and quantity consistency, deduplicates
identical execution replays, and refuses a replacement until the previous child
is terminal. Conflicting execution IDs or incomplete reports stop the path.
The existing `RawLmaxFixSessionClient.ExecuteStrategyParentAsync` uses this
projection, includes the original child quantity in cancellation requests, waits
for residual-child reports, and rejects new submissions after the target close.
A full fill during cancellation leaves no quantity to reprice.

The gateway derives FIX roots from persisted child IDs instead of the common
ModelRun ID. Two instruments in one run must have distinct FIX identities.

## Verification status

**Latest result: compilation and all 28 simulated tests pass**, with no skipped
tests, on `EC2AMAZ-1QPHTD8` under `Administrator`, SDK 10.0.400, at
**2026-09-16T15:51:38Z**. The tested source commit is
`367b2e3caf0073b5917109bdbea8bfa45a2bc04f`. The owner explicitly authorized the
fixture correction and qualification iterations for these six files in the same
isolated staging directory. No executable bridge source changed after the first
compilation. The full suite was rerun, without filtering out the failed case.

`git diff --check` also passed. After the owner's initial explicit approval, the six-file
payload from commit `b86fa6d5a225101bf0fb8596d51df1f5d1587e8f` was transferred and
all hashes verified on EC2. **Compilation succeeded; 27 of 28 tests passed** at
2026-09-16T15:28:47Z with SDK 10.0.400. That initial run is retained as history.
The proposed tests cover cancellation with residual quantity, partial fills
across children, late and complete fills during cancellation, missing cancel
acknowledgments, duplicate/conflicting executions, report inconsistencies, and
distinct identities across two instruments in one ModelRun. Existing bridge
tests are retained. The one failing case is
`TwoInstrumentsInOneModelRun_UseDifferentFixIdentities`: its fixture assumed
`SeedData` contained GBPUSD, but that seed contains only EURUSD. This revision
explicitly adds a second simulated instrument, its venue mapping and alias;
the original assertion remains unchanged. No executable bridge code changed.

The corrected test file's SHA-256 is
`5ed69038b179b9089a20d3016103ec217cabf4a4b5b7c00566e00413eb03fb6e`.
Automatic review initially rejected that fixture revision because its
content/hash differed from the original approved payload. No alternate transfer
or indirect edit followed the rejection. The owner's subsequent explicit
approval authorized the correction and further qualification iterations; the
revised fixture then transferred with a matching hash and passed the full suite.

Qualification receipts are retained in the staging directory:
`qualification-result-01.json`, `qualification-output-01.log`, and
`TestResults\parent-qualification.trx`, followed by the successful
`qualification-result-02.json`, `qualification-output-02.log`, and
`TestResults\parent-qualification-02.trx`. The successful run is recorded in
`docs/evidence/2026-09-16/lmax-demo-parent-qualification-pass.json`.
The initial result, artifact hashes and AWS
identity check are recorded in
`docs/evidence/2026-09-16/lmax-demo-parent-qualification-followup.json`.

Automatic review rejected both a project-directory copy and the subsequent
narrow transfer of five changed source/test files plus a test harness to EC2.
The stated reason was that the exact private-source payload and staging
destination lacked explicit authorization. No alternate transfer route was
used after the narrow-transfer rejection. The owner subsequently approved the
original six-file payload, allowing the run described above. There is no
repository CI workflow available in this checkout to provide another build.

The proposed isolated qualification is reviewable here:

- Destination: `C:\deploy\IntradayPlatform\staging\lmax-parent-qualification-20260916`.
- Payload: the five source/test files listed in the accompanying evidence JSON,
  plus `deploy/windows/lmax-demo/qualification/DemoParent.Qualification.csproj`.
  They would be staged with their basenames and their hashes verified.
- The harness reads existing ConnectivityLab source in the already present
  `qq84-demo-cycle-coordinator-f1692d8e-20260915T000000Z` checkout without modifying
  it. It excludes its `Program.cs` and original `DemoStrategyExecutionBridge.cs`.
- Application, Domain, Lmax and PostgreSql references are existing assemblies
  from the nonactive `lmax-demo-cycle-c81b739c` release.
- Command, from that staging directory:
  `C:\deploy\IntradayPlatform\operator\dotnet-10\dotnet.exe test DemoParent.Qualification.csproj --logger "trx;LogFileName=parent-qualification.trx"`.
- Only simulated/in-memory tests run. This neither starts the Worker nor opens
  a broker session, imports data, changes LocalDB or installs a scheduled task.
- A passing result would qualify this candidate against those installed
  dependencies; it would not constitute a full solution build or a Demo trade.

## AWS escalation and authorized identity result

Two metadata reads failed on the specified EC2 host:

1. `ssm describe-document --name QQ-LMAX-Demo-Anubis-Cycle-v1`: `AccessDenied`.
2. `ec2 describe-instances --instance-ids i-019ec3c94b9d234f6`: `UnauthorizedOperation`.

Both identified `arn:aws:iam::761018894194:user/qq-intraday-ec2`, whereas the
existing transport mandate in issue #84 comment 5684741676 names the EC2 role
`qq-role-ec2-intraday`. These failures do not prove that `S3 PutObject` or
`SSM SendCommand` is denied; neither was attempted in this continuation.
AWS calls stopped after the two failures, under the Control Tower/#84 escalation
rule. The owner subsequently approved the separate read-only identity check.
At **2026-09-16T15:29:48Z**, STS returned the expected identity:
`arn:aws:sts::761018894194:assumed-role/qq-role-ec2-intraday/i-05626133ca7892fb8`.
The temporary process environment was restored. No persistent credential,
profile, IAM policy or instance attachment changed. This confirms the role is
available on this host; it does not establish S3/SSM permissions or make the
default IAM-user environment a valid launcher binding.

The authorized check used the **already authorized EC2 role** on
`EC2AMAZ-1QPHTD8` / `i-05626133ca7892fb8`, in a temporary process environment
excluding the selected IAM-user profile and inheriting credentials solely from
the attached role. Only `aws sts get-caller-identity` in `eu-west-2` was invoked,
with the expected account, role and instance session checked. Only identity
metadata was exposed. No SSM command, GPU start or order was performed.

## Execution limits and next integration work

**This patch does not make day trading ready.** The bridge still owns a socket
per parent; shared receive framing, durable event persistence and reconciliation
after an ambiguous disconnect remain unresolved. This in-memory projection is
not a replacement for the disconnected journal candidate in PR #101. Neither
the 900-second startup observation limit nor its timestamp is extended.

The full capture → boundary normalization → fixed SSM Anubis V1 → existing
ExecDesk adapter → targeted cycle manifest launcher is still absent. The capture
scheduler currently only captures. The Worker is still single-cycle and the
existing gateway still returns an empty open-order list. Those integration
gaps and the continuing-session review in #84 comment 5697076664 must be resolved
before a natural Demo cycle can be launched and maintained safely. No synthetic
target, stale capture replay, timestamp refresh, blanket queue promotion or
forced order is proposed.

Successful LocalDB diagnosis and report acquisition remain acquired. Existing
report receipts in #84 comment 5698878239 are unchanged; header-only current-day
reports are not proof of a flat account or absence of working orders. No Worker
restart, database import, broker order, Production/PMS action or Databento API
request was performed in this continuation.
