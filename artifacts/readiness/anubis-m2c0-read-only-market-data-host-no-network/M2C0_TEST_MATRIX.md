# M2C0 Test Matrix

Targeted command passed 103 tests:

`	ext
dotnet test tests/QQ.Production.Intraday.Tests.Unit/QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CanonicalRecorderM2A|FullyQualifiedName~CanonicalShadowOfflineM2B|FullyQualifiedName~CanonicalRecorderM2B|FullyQualifiedName~CanonicalRecorderM2C0" /p:UseSharedCompilation=false
`

Result: PASS, Failed 0, Passed 103, Skipped 0, Total 103.

Coverage against ticket groups:

| Group | Status |
|---|---|
| Provenance | PASS via baseline capture/artifacts; mismatch documented |
| Parity independent hashes | PASS |
| Source/recorded/replay tamper | PASS through parity/replay/failure injection tests |
| Timing effective_from/deadline | PASS |
| Nominal no TARGET_REVISED | PASS |
| Real revision versioning | PASS |
| Risk shadow non-authoritative | PASS |
| Nonzero read-only position drift | PASS |
| EURUSD/AUDUSD market coverage | PASS |
| Sizing vs execution BBO distinct | PASS |
| Stale/missing quote fail-closed | PASS |
| Recorder recovery/replay/hash/DQ closeout | PASS in M2A/M2B/M2C0 targeted tests |
| Read-only source forbidden capabilities | PASS |
| State machine gap/stale/recovery/PossDup/invalid book/multi-instrument | PASS |
| Safety no FIX/no socket/no AccountAPI/no Databento/no DB/no R009/R018/R216 run | PASS by construction and static scans |
| End-to-end offline playback -> recorder -> replay/parity | PASS |

Exploratory full unit suite was also attempted. It failed because this isolated worktree does not contain historical readiness fixture artifacts required by unrelated execution-sim/execution-algo/execution-live tests. That run is documented as non-gate in validation_commands.log.
