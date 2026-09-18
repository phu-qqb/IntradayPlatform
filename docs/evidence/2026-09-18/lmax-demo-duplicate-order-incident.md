# LMAX Demo: multi-pair order-ID collision, 18 September 2026

## Effective state

The 15:30Z startup was real, but the first natural 15:45Z cycle stopped at
15:50:24Z before FIX dispatch. Worker8512 retains account ownership with
`COORDINATOR_RECONCILIATION_REQUIRED`; run-day exited. No replacement session,
local order expiry or retirement has been applied in this qualification.

[Authoritative diagnosis](https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5732567094).

## Root cause and correction

`ProcessModelRunService` used order-list counts from its initial SQL snapshot
to allocate client IDs. SQL persistence does not mutate those detached lists,
so the second prepared leg reused the first leg's suffix. The in-memory test
repository shared live lists and hid the failure.

Demo batch parent and child IDs now derive from each persisted trade intent's
unique identifier. The non-Demo branch and all economic risk limits are unchanged.
Regression tests emulate detached SQL snapshots and enforce unique client IDs
for both two-pair and fourteen-pair batches. Both tests failed on the old code
with `TEST_SQL_UNIQUE_CLIENT_ORDER_ID_VIOLATION`, then passed with the fix.

The final focused suite passed **136/136** tests, including the exact-incident
retirement guards. The final launcher passed **44/44** self-checks and real
Demo configuration inspection without opening a broker connection. All fills in these tests are explicitly simulated fixtures;
none were inserted into the operating Demo database.

## Retained actual evidence

| Item | Actual value |
| --- | --- |
| Session | `lmax-demo-20260918-gmv-152437` |
| Active Worker | PID8512, parent14000, created 15:30:21.809Z |
| Failed model | `6fa561c1-454d-49a2-bd58-a11f16b1ba70` |
| Promoted batch | `46914de6-0650-417e-9c84-d7c25268e249` |
| Prepared manifest SHA256 | `79ef7bbbf4eb8f9fd58a9f923fd80cbeaf83bc0e0e96306b143a5d2674ae99de` |
| Original start anchor | `246544E7935C2AB0179E992849F67E825A91803DDCBE34D0283025F965D3EFC4` |
| Fault anchor, index42 | `6A71A11EFFF76D1EC7EA1ED170128C10EDF68BD4B41917B8FCFEE26F044ECCE4` |
| Unsent parent | `f63dba82-e46a-4a38-b773-92edd01dadfa`, Created |
| Unsent child | `4f900cc0-ace7-42f7-be9a-ebb03ea8f95f`, PendingNew |
| Parent's trade intent | `c265840f-ba66-4190-a8ea-b48896d70a2a` |

The actual recovery SELECT was qualified read-only at16:18:32Z: 14 weights,
2 targets, 2 intents, 2 approved risk decisions, 1 parent/child, 0 execution
reports, 0 fills, 1 internal open child and 0 nonzero ledger positions.
The prepared GBPUSD sell3000 and USDJPY buy1000 both passed risk.
The journal contains no SendIntent/SendCompleted/Report or Cycle entry.
The PendingNew child is an unsent internal record, not broker order evidence.

## Prepared exact recovery

`retire-duplicate-id` in the existing NoSendRetirement tool is scoped to this
session, its start/fault anchors, model, parent, child and trade-intent IDs.
It acquires the normal exclusive owner lease and a serializable SQL transaction.
It requires a genuine owner confirmation under900seconds, explicitly labelled
`OWNER_CONFIRMED_ACCOUNT_STATE`, bound to the same GitHub owner approval reference.
A confirmation after the incident may precede the final heartbeat: the verified
zero-send journal bridges that interval without changing its observation time.

Only after the actual16:00Z model deadline and all checks pass may the exact
parent/child become locally Expired. A durable before/after intent precedes the
two-row transaction; post-change readback precedes the immutable retirement
certificate. No FIX cancellation, fill, position or broker acknowledgement is
invented. The original journal, model, risk decisions and prior economic facts
remain. The failed model stays unprocessed and must never be replayed.
Failure after commit but before certification remains blocked, without an
automatic retry. The certificate is checked by normal session ownership.

After a new, explicit instruction to stop **8512**, verify its exact creation
time, command line and deployed binary before stopping only that process.
Preserve its frozen journal and record its hash; never force owner.lock.
The earlier permission for11736 does not cover8512. No stop has been performed.

With that approval and genuine current confirmation recorded, the prepared
command is:

```powershell
& $dotnet $retirementDll retire-duplicate-id `
  --observation $genuineOwnerObservationPath `
  --expected-journal-sha256 $frozenOriginalJournalHash `
  --approval-reference $newExplicitOwnerApprovalReference
```

These arguments must come from actual evidence after the missing instruction;
they are not placeholders to fill with a refreshed historical declaration.
The dated owner source must also be bound for the next start while preserving
the already-used15:24 source and historical certificate validation. Then use
the documented start-worker/run-day commands with a new session and the next
natural cycle. Do not replay15:45Z or claim fills from qualification tests.

## Staged candidate, not activated

Root: `C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\id-collision-20260918`.

| Artifact | SHA256 |
| --- | --- |
| Worker DLL | `a144f8b91486f80f3522f7137d71028ed72bed9e717679801e7de1f651211853` |
| Application DLL | `01a70acba76967405c3a80052debec25574ffcfac08709fbe8c6aa41daa51c58` |
| 152-file Worker manifest | `5c492a157ab193da125a262c333383f8706c43a248f42dfc1b2c901234081a35` |
| Retirement DLL | `b99b32c6106e7d3bb2fb33cf40a259469ef3a6fae953b75e3a5d62c6367ccc97` |
| Launcher DLL | `8bab1dbca20635b5c20521450a2b84b10e2994adcdaa6ed7e5a194cb332e946b` |

The applied seven-day / USD2M-position / USD10M-portfolio profile remains.
Natural programme timing, fourteen native USD legs, report provenance and
Production/IAM/OS/Databento/email restrictions remain. Daily EOD reporting is
available on its existing schedule. This candidate alone does not resolve the
current owner's fault or establish autonomous next-day report-based opening.
