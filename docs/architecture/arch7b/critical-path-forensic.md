# ARCH7B stale-position critical-path forensic

## Immutable failed run

- Run: `arch7b-core06-e2e-no-order-20260728T1315Z-909f5cc-e3c01e81-ac29f1b01b3d`
- Status: `ABORTED_POSITION_BRACKET_STALE_BEFORE_READY`
- Verdict retained: `NO_GO_ARCH7B_POSITION_BRACKET_STALE`
- Manifest SHA-256: `ff5914f80816b2bad0a87df42a6c8ef5e6b2603f27bffd29250ab3ec12fc04cb`
- ZIP SHA-256: `ecf2c7be90bb4e8ed4b2c757ed5b42cefb836fccdfb0df2a48ad6d8dda53f77d`
- Armed at PostgreSQL: `2026-07-28T13:11:51.383873Z`
- Broker P2 economic time: `2026-07-28T13:12:15Z`
- Stale decision at PostgreSQL: `2026-07-28T13:19:28Z`
- Age: 433 seconds; contract: 300 seconds; excess: 133 seconds.

The sealed manifest and ZIP are not changed. No ready marker, plan, apply, database write, market capture, FIX session, or order occurred.

## Exact observable timeline

| Stage | Start UTC | End UTC | Observed duration | Since P2 at end | Evidence |
|---|---:|---:|---:|---:|---|
| T0 | 13:12:13.538 | 13:12:13.570 | 32 ms | -1.430 s | attempt manifest |
| P1 | 13:12:13.581 | 13:12:13.612 | 31 ms | -1.388 s | attempt manifest |
| T1 | 13:12:13.622 | 13:12:13.653 | 31 ms | -1.347 s | attempt manifest |
| P2 | 13:12:13.666 | 13:12:13.695 | 29 ms | broker P2 = 0 | attempt manifest + broker Date |
| T2 | 13:12:13.705 | 13:12:13.738 | 33 ms | -1.262 s | attempt manifest |
| Complementary reports | 13:12:13.752 | 13:12:13.950 | 198 ms | -1.050 s | acquisition manifest |
| Core contract written | unrecorded | 13:12:13.951322 | n/a | -1.049 s | file creation |
| Core acquisition manifest written | unrecorded | 13:12:14.084564 | n/a | -0.915 s | file creation |
| Core tests | 13:17:10.069551 | 13:17:11.431314 | 1.362 s | +296.431 s | runner log bounds |
| Core final index written | unrecorded | 13:17:11.668909 | n/a | +296.669 s | file creation |
| First consumer tunnel | 13:17:45.416146 | 13:17:46.961652 | 1.546 s | +331.962 s | first SSM log |
| Second consumer tunnel | 13:18:36.173626 | 13:18:37.792206 | 1.619 s | +382.792 s | second SSM log |
| Pre-bundle combined upper bound | 13:18:36.173626 | 13:18:39.742827 | <=3.569 s | +384.743 s | tunnel start to first output |
| Full bundle write observed bound | 13:18:39.742827 | 13:18:39.793991 | 51 ms | +384.794 s | bundle file times |
| Consumer result complete | 13:18:39.835574 | 13:18:39.839792 | 4 ms | +384.840 s | result file bounds |
| Publish-ready tunnel/attempt | 13:19:27.054138 | 13:19:28.576961 | 1.523 s | +433.577 s | position-import SSM log |
| PostgreSQL stale decision | 13:19:28 | 13:19:28 | 0 | +433 s | blocker report |

The Core worker host clock ran about 1.3 seconds behind the LMAX broker Date header. The broker P2 timestamp remains the economic start authority. Filesystem times are diagnostic only.

## Missing original instrumentation

The original consumer did not record separate start/end or monotonic samples for:

- Core package validation;
- RDS universe read;
- 99-line snapshot build;
- smoke A;
- smoke B;
- determinism.

Those phases are therefore `NOT_SEPARATELY_RECORDED`, not estimated. Their combined upper bound, including second-tunnel startup, is 3.569 seconds before the first bundle file appeared. The corrective CLI instruments each phase with `Stopwatch`; historical replay evidence is kept outside the closed import package.

## Dominant cause

The full consumer was not the dominant 433-second cost. From broker P2:

- Core tests and final-index publication consumed 296.669 seconds.
- Core final to the second consumer tunnel added 84.505 seconds, including a replaced first tunnel.
- The full consumer completed about 3.666 seconds after the second tunnel began.
- Consumer completion to the publish-ready tunnel added another 47.214 seconds.

The dominant cause was post-P2 qualification plus serial process/tunnel orchestration. The corrective design moves build, tests, qualification, tunnel validation, clock qualification, and arm before P2, and keeps only canonical validation, one read-only universe read, the 99-line build, four-file write, ready, plan, and apply in the freshness window.

## Historical replay

Status: `PASS_READ_ONLY`.

Qualification root: `C:\tmp\arch7b-fast-path-historical-qualification-909f5cc`.

- Full total: 2,192.598 ms.
- Fast total: 1,839.317 ms.
- Fast Core validation: 64.001 ms.
- Fast RDS universe read: 1,661.298 ms.
- Fast snapshot build: 10.283 ms.
- Fast minimal package write: 62.561 ms.
- Full smoke A: 51.857 ms; smoke B: 10.585 ms.
- Full bundle write: 102.274 ms.
- Four-file full/fast byte parity: PASS.
- Fast package SLO 60 seconds: PASS.
- Margin to 300 seconds: 298,160.683 ms.
- Historical plan: `HISTORICAL_FIXTURE_NOT_IMPORT_ELIGIBLE`.
- Transaction read-only: true; pending model changes: false; database writes: zero.

The historical package remains a fixture only. All replay output is external; neither the sealed run nor PostgreSQL was mutated.