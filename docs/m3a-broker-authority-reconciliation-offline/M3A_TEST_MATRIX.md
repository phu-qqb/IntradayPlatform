# M3A Test Matrix

Command:

```text
dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --filter BrokerAuthorityReconciliationTests --no-restore
```

Result: Passed 17/17.

| Requirement | Test coverage |
|---|---|
| Partial then final | `PartialThenFinalBrokerFillIsIdempotentAndClean` |
| Duplicate ExecID exact | `DuplicateExactBrokerExecutionIsIdempotent` |
| Duplicate conflict | `DuplicateConflictingBrokerExecutionIsCriticalStop` |
| PossDup/out-of-order | `PossDupOutOfOrderExactReplayIsIdempotent` |
| Missing fill both sides | `MissingInternalFillBlocks`, `MissingBrokerFillBlocks` |
| Position mismatch | `PositionMismatchBlocks` |
| Open-order mismatch | `BrokerOpenOrderMissingInternallyBlocks`, `InternalWorkingMissingAtBrokerBlocks` |
| Cancel pending | `CancelPendingLeavesRemainReservedAndBlock` |
| Leaves mismatch | `LeavesMismatchBlocks` |
| Stale snapshot | `StaleBrokerSourceBlocks` |
| Demo/Live collision | `DemoLiveAndMultiAccountExecIdCollisionsStayScoped` |
| Multi-account collision | `DemoLiveAndMultiAccountExecIdCollisionsStayScoped` |
| Manual UI non-authoritative | `ManualEvidenceDoesNotBecomeAuthoritativeBrokerFill` |
| Restart/replay idempotent | duplicate/PossDup replay tests plus deterministic input hash path |
| Critical break blocks | duplicate conflict test |
| Clean state authorizes | `CleanAuthoritativeStateCanTradeAndComputesRemainingDelta` |
| Source absent no-go | `MissingSourceProducesNoGoNotSyntheticZero` |

No disposable DB was required.
