# LMAX Read-Only Runtime — First Transport Prototype Preflight

## Phase 6A Planning Boundary

Phase 6A is the post-Phase-5 planning boundary. It does not alter the first transport prototype or add any runtime behavior.

The validated Phase 5 workflow is frozen as manual Demo read-only MarketData:

- 3 successful Demo EURUSD snapshots.
- 3 sanitized artifacts.
- 3 `MarketDataOnly` evidence previews.
- 3 explicit manual local replays.
- 0 observations.
- Mutation guards unchanged.
- Phase 5V audit pack `PASS`.
- Phase 5W operational signoff `PASS`.
- Phase 5X status `FrozenManualReadOnly`.

Phase 6A recommends `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run` as the next safest boundary. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, production/UAT use, multi-instrument execution, or trading-state mutation.

## 1. Purpose

This document gates the first future runtime socket prototype for the LMAX read-only runtime path.

Phase 5A does not implement a socket. It defines what must be true before the first Demo-only, manual, read-only socket prototype can be coded or run in a later phase.

## 2. Current Approved State

- Phase 4A-4P are closed.
- The final no-socket release gate passed.
- API and Worker remain `FakeLmaxGateway` only.
- No runtime LMAX connectivity exists.
- No credential values are read, used, stored, logged, or returned.
- No order submission exists.
- No scheduler auto-run exists.
- No shadow replay submit from runtime exists.
- No trading-state mutation exists.

## 3. Future Phase 5B Scope, Not Implemented Here

Future Phase 5B may only be considered as a separate explicit prompt. Its intended boundary is:

- First external read-only transport prototype.
- Demo only.
- Manual only.
- No order submission.
- No scheduler.
- No trading mutation.
- No shadow replay submit initially.
- No execution gateway registration.
- No Worker auto-start.
- No production or UAT.
- Limited runtime duration.
- Limited event count.
- Explicit local command only.
- Explicit reason required.

## 4. Phase 5A Non-Goals

- No socket.
- No FIX logon.
- No credential read.
- No live connection.
- No message send.
- No event capture.
- No persistence.
- No runtime activation.

## 5. Entry Criteria for Phase 5B

- Final no-socket release gate is `PASS`.
- Operational readiness gate is `PASS` or `PASS WITH KNOWN WARNINGS` only for accepted warnings.
- Backend and frontend tests are green.
- External preflight smoke is green when the local API is available.
- Fake preview smoke is green when the local API is available.
- Documentation is current.
- Operator understands Demo/manual/read-only-only constraints.
- Credential handling plan is approved.
- Kill/rollback plan is reviewed.
- Separate explicit manual approval exists to start Phase 5B.

## 6. Hard Runtime Safety Gates for Future Phase 5B

- `EnvironmentName` must be `Demo`.
- `VenueProfileName` must be `DemoLondon` or another approved Demo label.
- `CredentialProfileName` remains a label only; resolver work is a separate gated future step.
- CredentialProfileName remains a label only in Phase 5A documents, scripts, and future-entry criteria.
- `AllowExternalConnections` may be true only in a dedicated local Phase 5B script.
- `AllowCredentialUse` remains blocked unless a later credential-read phase explicitly enables a safe read boundary.
- `AllowOrderSubmission=false`.
- `SchedulerEnabled=false`.
- `PersistToTradingTables=false`.
- `SubmitToShadowReplay=false`.
- `DryRun=true` or an approved read-only proof mode.
- `MaxRuntimeSeconds` is capped.
- `MaxEventsPerRun` is capped.
- Manual reason is required.
- Manual operator id is required.
- API/Worker remain `FakeLmaxGateway`.

## 7. Kill / Rollback Plan

If a future Phase 5B prototype is stopped or aborted:

1. Stop the local prototype process.
2. Remove or clear Phase 5B environment variables.
3. Revert to default `run-api.ps1` startup.
4. Verify `/health` still reports `FakeLmaxGateway`.
5. Run `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`.
6. Run `scripts/smoke-lmax-readonly-runtime-external-preflight-local.ps1` when the API is available.
7. Inspect logs for no credential values, no order path, and no mutation.
8. Retain generated artifacts only when sanitized.
9. Do not perform DB rollback unless a future violation is found; Phase 5B must not mutate trading state.
10. Escalate to operator, developer, and risk/approver if any abort condition is triggered.

## 8. Abort Conditions

Abort immediately if:

- Any order-submission path appears.
- Any credential value appears in logs, API responses, or evidence.
- Any host, user, or password value leaks into runtime API DTOs.
- Scheduler starts automatically.
- API or Worker gateway changes from `FakeLmaxGateway`.
- Any mutation guard changes.
- Shadow replay submit occurs from runtime.
- Non-Demo environment is selected.
- Unknown protocol reject appears in a future run.
- Sequence/logon behavior is unclear.

## 9. Required Observability

Future Phase 5B must report:

- Run id.
- Reason.
- Operator id.
- Environment label.
- Venue profile label.
- Credential profile label only.
- Max runtime and max events.
- No order submission.
- No scheduler.
- No mutation.
- Event counts.
- Error counts.
- No-sensitive-content flag.
- Final outcome.
- Stop/abort reason.

## 10. Required Evidence Handling

Future Phase 5B may only produce sanitized preview artifacts initially:

- No raw credential or logon data.
- No trading table writes.
- No shadow replay submit initially.
- Evidence validator must pass before any later replay phase.

## 11. Sign-Off Requirements

- Operator signoff.
- Developer signoff.
- Risk/approver signoff when applicable.
- Signoff still cannot authorize execution by itself.
- Final manual command remains separate.

## 12. Phase 5B Exit Criteria, Future

- First read-only Demo socket can be opened only manually.
- No order path exists.
- No mutation occurs.
- No credential values leak.
- No scheduler runs.
- No shadow replay submit occurs.
- Stop/rollback is tested.
- Logs are reviewed.
- Readiness gate passes after the run.

## 13. Phase 5B Implemented Outcome

Phase 5B is implemented as the first dedicated external read-only transport prototype boundary, but it remains blocked before any socket, logon, credential read, or external connection attempt.

Implemented artifacts:

- `LmaxReadOnlySocketPrototypeTransport`
- `LmaxReadOnlySocketPrototypeOptions`
- `LmaxReadOnlySocketPrototypeResult`
- `scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1`
- `scripts/check-lmax-readonly-runtime-phase5b-prototype-gate.ps1`

The manual script requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty `-Reason`, prints rollback instructions, and returns a sanitized blocked result. Because the credential resolver remains disabled/no-op and no credential values may be read yet, the script and prototype refuse before socket/logon. No external run is attempted in Phase 5B.

Phase 5B does not register the prototype in API or Worker. API/Worker remain `FakeLmaxGateway` only. No order submission, scheduler, shadow replay submit, trading-state mutation, live gateway registration, or runtime persistence is added.

## 14. Phase 5C Implemented Outcome

Phase 5C adds credential availability checking and redaction only. It does not open a socket, perform FIX logon, start a session, register a gateway, submit orders, schedule work, submit to shadow replay, or mutate trading state.

Credential availability is checked through environment key labels only:

- `LMAX_DEMO_FIX_USERNAME`
- `LMAX_DEMO_FIX_PASSWORD`
- `LMAX_DEMO_SENDER_COMP_ID`
- `LMAX_DEMO_TARGET_COMP_ID`

The resolver and scripts report labels, present/missing booleans, missing label counts, and `RedactionStatus=Redacted`. They never return actual values. Abort immediately if any credential value appears in console output, logs, readiness reports, evidence, API payloads, or docs.

Safe checks:

```powershell
.\scripts\check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck
.\scripts\check-lmax-readonly-runtime-phase5c-credential-gate.ps1
```

Phase 5D has now implemented the first isolated manual Demo market-data snapshot socket prototype. It is Demo-only, manual-only, EURUSD / SecurityID `4001` only, and requires explicit `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and `-Reason`. It remains outside API/Worker, keeps `FakeLmaxGateway`, and adds no order submission, scheduler, shadow replay submit, gateway replacement, trading-table persistence, or trading-state mutation.

The next recommended phase is **Phase 5E - Manual Demo Snapshot Evidence Artifact Preview** if a snapshot succeeds, or **Phase 5E - Transport Failure/Retry Hardening** if connection or credential setup remains incomplete.

## 15. Phase 5E Implemented Outcome

Phase 5E hardens the Phase 5D manual prototype's failure and retry reporting. The prototype now classifies missing credentials, invalid safety gates, invalid environment, unsafe venue, order-submission flag, connection failure, logon rejection, logon timeout, snapshot timeout, logout failure, max-runtime exceeded, and max-events exceeded.

Phase 5E does not automatically connect in gates/tests and does not add automatic retry. Retry metadata remains disabled with `RetryEnabled=false`, `RetryAllowed=false`, and `MaxAttempts=1`. Operators receive sanitized guidance only.

## 16. Phase 5F Implemented Outcome

Phase 5F adds sanitized result capture for the operator-approved manual Demo EURUSD / SecurityID `4001` snapshot attempt. It does not automate external runs in tests or gates. The manual script still requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty `-Reason`.

The script now prints planned safety flags before any external attempt and writes a sanitized JSON result under `artifacts/lmax-readonly-runtime-demo-snapshot/`. The artifact is not shadow-replay evidence, is ignored by git, and must contain `noSensitiveContent=true`, `redactionStatus=Redacted`, `credentialValuesReturned=false`, `orderSubmissionAttempted=false`, `shadowReplaySubmitAttempted=false`, `tradingMutationAttempted=false`, and `schedulerStarted=false`.

The Phase 5F gate masks credential labels and verifies the missing-credential blocked path, redaction markers, artifact output boundary, no order surface, no gateway registration, no hosted service, no scheduler, no shadow replay submit, and no trading mutation references without making an external attempt.

## 17. Phase 5G Implemented Outcome

Phase 5G adds sanitized transport diagnostics for the first manual Demo snapshot outcome: logon succeeded, snapshot request was sent, logout succeeded, but no market-data snapshot arrived before timeout.

The prototype now records request mode, non-secret request metadata, message-type counters, reject/session classifications, timeout timing, and sanitized warnings/errors. Diagnostic request modes remain manual-only and read-only: `SecurityIdOnly`, `SlashSymbolOnly`, `SymbolOnly`, and `AutoSequence`. The Phase 5G gate verifies these diagnostics without making an external attempt.

## 18. Phase 5H Implemented Outcome

Phase 5H hardens request compatibility after LMAX Demo rejects. The runtime now models known rejected request profiles and blocks them locally by default. `SnapshotOnly` / `263=0` is known rejected with `ValueOutOfRange`, symbol encodings containing tag `55` are known rejected/risky diagnostics, and `InternalSymbol` is known rejected with a repeating-group mismatch around tag `146`.

The safe default for a future manual diagnostic attempt is `SnapshotPlusUpdates` plus `SecurityIdOnly`. This keeps the request read-only, uses EURUSD / SecurityID `4001`, sends `263=1`, includes `48` and `22=8`, omits `55`, and requires unsubscribe/logout after snapshot or timeout.

## 19. Phase 5J Implemented Outcome

Phase 5J adds sanitized Demo MarketData logon/session diagnostics. The current blocker is no confirmed MarketData FIX logon: the runtime prototype and Connectivity Lab have reached TCP/TLS and sent Logon, but observed session behavior includes `MsgType=5` Logout before logon confirmation.

The manual prototype now records profile labels, credential/comp-id presence and lengths only, BeginString, EncryptMethod, HeartBtInt, ResetSeqNumFlag, Logon sequence number, first inbound message type, sanitized Logout/Reject text, logon wait duration, TCP/TLS flags, and runtime-vs-Connectivity-Lab profile comparison. The new gate is `scripts/check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1` and does not make an external attempt.

## 20. Phase 5L Implemented Outcome

Phase 5L closes the first successful Demo read-only EURUSD snapshot milestone. The successful sanitized artifact validates Demo logon, one EURUSD / SecurityID `4001` snapshot, bid/ask/mid values, logout, no secret leakage, no order submission, no scheduler, no shadow replay submit, and no trading mutation.

The artifact validator is local-only and does not connect to LMAX:

```powershell
.\scripts\validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
.\scripts\check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1 -ArtifactFile .\artifacts\lmax-readonly-runtime-demo-snapshot\lmax-readonly-demo-snapshot-result-20260508-132646.json
```
## Phase 5M Evidence Preview Mapping

Phase 5M is a local preview step after the first successful Demo read-only snapshot closure. It maps the validated sanitized artifact to `lmax-fix-lifecycle-evidence-v1` with evidence mode `MarketDataOnly`, validates the preview, and writes only ignored sanitized JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/`.

This phase does not run another socket attempt, submit to shadow replay, create observations, register a gateway, start a scheduler, submit orders, persist live FIX data, or mutate trading state. The next safest phase is either a manual MarketDataOnly replay dry-run that is not submitted from runtime, or a repeated manual snapshot stability check.

## Phase 5N Manual Replay Dry-Run

Phase 5N validates the Phase 5M preview through the existing local shadow replay API only. It is not a runtime submit path. A valid `MarketDataOnly` preview should replay as `Completed` with zero observations and no mutation guard changes. This confirms the preview is safe for manual offline shadow analysis without enabling runtime shadow replay submit.

## Phase 5O Repeated Manual Snapshot Stability Check

Phase 5O adds a controlled manual stability workflow for a small number of Demo EURUSD read-only snapshots. It requires explicit operator flags and a capped `AttemptCount`; it has no scheduler, no automatic polling, and no automatic retry. Each attempt reuses the existing manual prototype, validates successful sanitized artifacts, maps them to `MarketDataOnly` previews, and writes a sanitized ignored stability summary.

The default Phase 5O workflow does not replay previews. If the operator explicitly supplies `-ReplayEvidencePreviews`, replay still uses the manual Phase 5N script and local API only; runtime code does not submit to shadow replay.

## Phase 5P Stability Readiness Decision

Phase 5P reviews the Phase 5O stability summary and closes the repeated manual snapshot milestone. The reviewed run completed `3/3` manual Demo EURUSD snapshots successfully, produced sanitized artifacts and `MarketDataOnly` previews, and kept all no-order/no-mutation/no-runtime-submit flags closed.

The Phase 5P PASS decision is not an activation approval. It does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, broader instruments, or production use.

## Phase 5Q Controlled Manual MarketData Evidence Workflow

Phase 5Q hardens the artifact-to-preview-to-optional-replay workflow as local metadata only. The workflow manifest records sanitized snapshot artifact paths, artifact validation results, evidence preview paths and validation results, optional explicit manual replay results, no-order/no-mutation/no-secret flags, and a final `PASS`, `PASS_WITH_WARNINGS`, or `FAIL` decision.

Default Phase 5Q behavior does not replay previews, does not require API availability, does not connect to LMAX, and does not call the runtime prototype. Optional replay remains a separate explicit local API step and is recorded only when the operator provides replay flags. A Phase 5Q `PASS` or `PASS_WITH_WARNINGS` does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, broader instruments, or production use.

## Phase 5R Manual Replay Review

Phase 5R uses the Phase 5Q workflow manifest path with explicit local manual replay. Replay is still not runtime behavior: the operator must supply `-ReplayEvidencePreviews -ConfirmLocalManualReplay`, the script requires a localhost API, and each preview is replayed through the existing manual `/lmax-shadow/replay` path only.

The expected result for every `MarketDataOnly` preview is `Completed` with zero observations and mutation guard `Unchanged`. A successful replay-reviewed manifest can close the Phase 5Q replay-omitted warning as `PASS`, but it still does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, external snapshot automation, trading mutation, broader instruments, or production use.

## Phase 5S Controlled Manual Release Gate

Phase 5S creates the release gate for the controlled manual MarketData workflow. It validates the closed Phase 5O stability summary, the Phase 5L sanitized snapshot artifacts, the Phase 5M `MarketDataOnly` previews, and optional explicit Phase 5R replay results. The output manifest is local artifact metadata only and lives under the ignored workflow artifact directory.

The gate can pass with warnings when replay is skipped. This warning means the release artifacts/previews are valid but optional local replay was not performed. A PASS or PASS_WITH_WARNINGS still does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, external snapshot automation, trading mutation, broader instruments, or production use.

## Phase 5T Controlled Manual Workflow Runbook Freeze

Phase 5T freezes the current controlled manual MarketData workflow as the approved documented process. It validates that the Phase 5S manifest/report are present and `PASS` or `PASS_WITH_WARNINGS`, that any warning is the optional local replay skipped warning, and that the workflow remains manual-only with no runtime shadow replay submit, scheduler, polling, order path, gateway registration, or trading mutation.

The freeze gate is:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1
```

This gate is local-only, requires no API by default, does not connect to LMAX, and does not perform optional local replay.

## Phase 5V Final Audit Pack

Phase 5V builds the final audit pack for the controlled manual Demo MarketData workflow. The pack is local JSON/Markdown under `artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack/` and references the successful stability summary, sanitized artifacts, `MarketDataOnly` previews, replay-enabled workflow manifest, replay results, and safety confirmations.

The final audit-pack gate is local-only and does not connect to LMAX or perform replay:

```powershell
.\scripts\check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1 -AuditPackFile <audit-pack>
```

`PASS` means the controlled manual Demo MarketData workflow has an auditable artifact pack. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, broader instruments, or trading mutation.
## Phase 5W Operational Signoff Boundary

Phase 5W freezes the validated controlled manual Demo MarketData workflow through an operational signoff over the Phase 5V audit pack. The signoff is local reporting only and does not add new runtime behavior.

The expected signoff result is `PASS` with three artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, unchanged mutation guards, `runtimeShadowReplaySubmit=false`, `externalConnectionAttempted=false` for the audit/signoff workflow, and `credentialValuesReturned=false`.

This does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.

## Phase 5X Visibility Boundary

Phase 5X is the first operator-facing frozen-workflow status panel. It reads Phase 5W signoff metadata only and is visibility/reporting-only. The status endpoint and script do not run LMAX, do not read credentials, do not replay, do not schedule, and do not mutate state.
