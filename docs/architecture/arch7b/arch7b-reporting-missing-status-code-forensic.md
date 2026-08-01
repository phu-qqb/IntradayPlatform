# ARCH7B reporting status-code forensic

## Scope

The scanner reported nine source occurrences introduced by commit `1f1eaeec7ec02284c3dc04b8db4a2b1558e44e2e`. They represent eight unique exact codes because `CLEANUP_FAILURE_SUPPRESSED` is emitted by both the pinned-open lifecycle and the pinned-session cleanup.

## Classification

| Exact code | Occurrences | Classification | Severity | Authority |
|---|---:|---|---|---|
| `ARCH7B_PINNED_POSTGRESQL_PRIMARY_OPEN_FAILURE` | 1 | blocker | critical | primary failure |
| `ARCH7B_PINNED_POSTGRESQL_CLEANUP_FAILED_WITHOUT_PRIMARY_FAILURE` | 1 | blocker | critical | cleanup terminal failure |
| `ARCH7B_PINNED_POSTGRESQL_PRIMARY_FAILURE_PRESERVATION_FAILED` | 1 | blocker | critical | failure authority unreliable |
| `CLEANUP_FAILURE_SUPPRESSED` | 2 | secondary blocker evidence | error | primary failure remains authoritative |
| `CLEANUP_FAILED_WITHOUT_PRIMARY` | 1 | blocker | critical | cleanup terminal failure |
| `OPEN_FAILED` | 1 | lifecycle transition | error | associated primary failure |
| `UNKNOWN_TYPE` | 1 | diagnostic | info | none |
| `UNKNOWN_METHOD` | 1 | diagnostic | info | none |

The lifecycle contract and tests establish that cleanup never replaces a primary failure. Cleanup is terminal only when no primary failure exists. The two `UNKNOWN_*` values occur only while sanitizing stack frames and carry no economic or operational decision independently.

## Inventory correction

The reporting catalogue adds exact entries only. No wildcard, prefix family, regex auto-classification, generic unknown fallback, or economic calculation is introduced.

After the correction, source inventory is complete with zero missing codes. T27 and T32 pass.
