# LMAX Read-Only Runtime Adapter - Implementation Plan

## Phase 6A — Operationalization Plan / Next Boundary

Phase 6A is implemented as a planning-only boundary after the validated Phase 5 manual Demo EURUSD read-only MarketData workflow.

Created:

- `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`
- `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`
- `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`

Phase 6A confirms the Phase 5 workflow remains frozen as `FrozenManualReadOnly`: three successful manual Demo snapshots, three sanitized artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, unchanged mutation guards, audit pack `PASS`, operational signoff `PASS`, API/Worker `FakeLmaxGateway` only, and runtime shadow replay submit still absent.

Recommended next phase: **Phase 6B — Manual Additional MarketData Instrument Allowlist Design, No External Run**.

Rationale: this extends the read-only MarketData planning surface while keeping the next step design-only, manual-only, Demo-only, and free of scheduler, polling, runtime shadow replay submit, orders, gateway registration, and trading-state mutation.

This plan translates the read-only runtime adapter design into a phased delivery path. It is not live trading, not order submission, not production activation, and not runtime LMAX enablement. It defines how a future read-only runtime shadow adapter could be introduced safely, with phase gates, tests, smokes, documentation, and rollback criteria.

## 1. Purpose

The future read-only runtime adapter would collect LMAX FIX evidence, normalize it into the existing evidence contract, and feed the shadow replay/observation pipeline. This plan describes the safe implementation sequence.

The plan preserves these boundaries:

- no order submission
- no trading-state mutation
- no credential UI
- no scheduler auto-run
- no runtime LMAX gateway registration until a separate future gate explicitly allows it
- all read-only evidence flows through validation, normalization, shadow replay, observations, audit, and exception policy

## 2. Current Approved State

The current approved state is:

- API and Worker register `FakeLmaxGateway` only.
- Connectivity Lab is the only external FIX-capable area.
- Shadow Replay is local, non-mutating, audited, fingerprinted, and validated.
- Shadow Reader Skeleton is disabled/no-op and blocked by default.
- Evidence Contract, Evidence Coverage, and Observation Policy are validated.
- Operational Readiness Gate exists.
- Read-only runtime adapter contracts are design-only and inert.

No runtime LMAX connectivity, sockets, credentials, scheduler activation, order submission, or trading-state mutation exists.

## 3. Non-Negotiable Safety Principles

- No `NewOrderSingle` in the read-only runtime adapter.
- No direct writes to orders, fills, positions, wallets, risk decisions, model runs, reconciliation state, or trading tables.
- No credentials in UI, API DTOs, evidence files, logs, or generated reports.
- No scheduler auto-run.
- No real LMAX gateway registration without a future explicit activation gate.
- No API/Worker replacement of `FakeLmaxGateway`.
- Every evidence event must pass through the evidence contract and shadow replay pipeline.
- Every activation level requires tests, smokes, docs updates, rollback steps, and sign-off.

## 4. Phase Overview

### Phase 0 - Readiness Baseline

Goal:

- Confirm the current system is ready before implementation starts.

Entry criteria:

- Operational readiness gate passes.
- Developer Guide, Operator Manual, Local Runbook, LMAX docs, and Adapter Contracts are current.
- Backend, frontend, shadow/evidence smokes, and evidence fixture validation are green.

Allowed code changes:

- None.

Forbidden code changes:

- Any runtime LMAX connectivity, DI registration, credential handling, scheduler activation, or trading-state mutation.

Exit criteria:

- Baseline report recorded.
- Known warnings documented.

### Phase 1 - Inert Runtime Interface Layer

Goal:

- Create runtime-facing interfaces and contracts only.
- Reuse safety gate concepts from the design contracts.
- Add no socket implementation and no active DI registration.

Status:

- Implemented as an inert interface layer.
- Added runtime-facing interfaces, status/evidence preview DTOs, disabled evidence sink, no-op run store, and disabled adapter implementation.
- No API endpoint was added in Phase 1; this avoids creating new runtime surface area before the fake/in-memory adapter phase.
- No API/Worker DI registration was added.

Potential files:

- `ILmaxReadOnlyRuntimeAdapter`
- `ILmaxReadOnlyRuntimeRunStore`
- `LmaxReadOnlyRuntimeRunRequest`
- `LmaxReadOnlyRuntimeRunResult`
- `LmaxReadOnlyRuntimeStatusDto`
- safety gate evaluator extensions

Allowed code changes:

- Pure interfaces, DTOs, options, validators, and tests.
- Documentation links and checklist updates.

Forbidden code changes:

- sockets
- credentials
- FIX logon implementation
- runtime LMAX DI registration
- order submission
- scheduler auto-run
- trading-table writes

Required tests:

- defaults disabled
- run blocked by default
- unsafe activation levels blocked
- no credential fields in DTOs
- API/Worker remain FakeLmax-only

Exit criteria:

- contracts compile
- tests pass
- no runtime service can connect externally
- default run returns `Disabled`/`Blocked`
- disabled evidence sink does not submit shadow replay evidence
- no-op run store does not persist run history

### Phase 2 - Fake/InMemory Runtime Adapter

Goal:

- Add a fake read-only runtime adapter that consumes fixture evidence only.
- Feed fixture evidence into the shadow replay path in a controlled local-only way.
- Prove the run is observable and non-mutating.

Status:

- Implemented as a service-level fake/in-memory adapter using local evidence fixtures only.
- Phase 3 adds the manual diagnostic API surface; Phase 2 itself remains service-level.
- Replay submission is preview-only in Phase 2. `SubmitToShadowReplay=true` remains blocked until a later phase explicitly wires a safe local path.
- Supported fixture source defaults to `tests/fixtures/lmax-shadow/lmax-mixed-readonly-evidence-v1.json` when available.

Allowed code changes:

- fake/in-memory adapter implementation
- local evidence fixture reader
- run status DTOs
- tests and UI status display if needed

Forbidden code changes:

- external connections
- credentials
- real FIX session
- order submission
- trading-table writes

Required tests:

- fake run creates evidence batch/replay observations
- mutation guards remain green
- audit created for run
- API/Worker still FakeLmax-only

Exit criteria:

- fake run is local-only
- no sockets or credentials
- operator UI can distinguish fake run from live read-only connectivity
- valid fixtures produce evidence-mode/event-count previews
- missing or invalid fixtures fail clearly
- no shadow replay observations are created in preview-only mode

### Phase 3 - Manual Runtime Endpoint, Still No External Connection

Goal:

- Add a local endpoint to trigger fake/read-only adapter run only.
- Endpoint remains disabled/blocked by default.
- No external connection, credentials, or scheduler.

Status:

- Implemented as a manual local diagnostic API surface.
- Endpoints:
  - `GET /lmax-readonly-runtime/status`
  - `POST /lmax-readonly-runtime/run`
  - `GET /lmax-readonly-runtime/runs`
  - `GET /lmax-readonly-runtime/runs/{id}`
- Default configuration remains disabled/blocked.
- `POST /run` requires a reason and can only preview local fixture evidence when test/local configuration explicitly enables the fake in-memory adapter.
- Fixture selection accepts known file names from `tests/fixtures/lmax-shadow` only; path traversal, nested paths, and absolute paths are rejected.
- `SubmitToShadowReplay=true` remains blocked/deferred in Phase 3.
- The API references only inert/fake `Infrastructure.Lmax` runtime contracts for this diagnostic endpoint. It does not register a real LMAX gateway, FIX session, hosted service, socket client, or order pathway.

Allowed code changes:

- local API status/run endpoints for fake-only execution
- audit for blocked/allowed fake runs
- UI status panel

Forbidden code changes:

- real LMAX host fields in request DTOs
- credential DTOs
- external connections
- order submission
- scheduler auto-run

Required tests:

- blocked by default
- explicit safe config required for fake-only execution
- reason is required
- fixture selector rejects path traversal and unknown files
- `SubmitToShadowReplay=true` remains blocked
- mutation guards green
- local-only URL checks

Exit criteria:

- smoke proves default blocked behavior
- fake-only run cannot be confused with live FIX
- API/Worker remain `FakeLmaxGateway` for venue execution

### Phase 3.5 - Explicit Fake-Enabled Endpoint Test Config

Goal:

- Prove the manual endpoint is disabled/blocked by default but can complete a fake/in-memory fixture preview under explicit test-only safe configuration.
- Exercise the endpoint across supported local fixture evidence modes without any external connection, sockets, credentials, scheduler, order submission, shadow replay submit, or trading-state mutation.

Status:

- Implemented in integration tests only through an explicit test host configuration.
- Default `appsettings.json` remains `Enabled=false` and `ImplementationMode=DesignOnly`.
- Fake-enabled test configuration sets `ImplementationMode=FakeInMemory`, `AllowExternalConnections=false`, `AllowCredentialUse=false`, `AllowOrderSubmission=false`, `PersistToTradingTables=false`, `PersistRawFixMessages=false`, `SchedulerEnabled=false`, `DryRun=true`, and `SubmitToShadowReplay=false`.
- Endpoint preview responses include fixture file name, evidence mode, per-source event counts, total event count, validation issue counts, run mode, run id, safety gates, and a message confirming no external connection, no shadow replay submit, and no trading-state mutation.
- `SubmitToShadowReplay=true` remains blocked/deferred.

Required tests:

- default config remains disabled/blocked
- fake-enabled test config completes `MixedReadOnly` fixture preview
- all supported fixture modes preview through the endpoint
- preview counts match fixture contents
- run summaries are visible through `GET /lmax-readonly-runtime/runs` and `GET /lmax-readonly-runtime/runs/{id}`
- mutation guard remains unchanged
- sensitive field scan remains clean
- no Connectivity Lab, hosted service, socket, credential, or order path is invoked

Exit criteria:

- fake-enabled preview is proven only in explicit test/local configuration
- default runtime remains disabled/design-only
- `SubmitToShadowReplay` remains blocked
- API/Worker remain `FakeLmaxGateway` for venue execution

### Phase 4 - External Read-Only Session Prototype Behind Compile/Config Gate

Goal:

- Introduce a real read-only session implementation behind hard disabled gates.
- Keep it unregistered by default.
- No scheduler.
- No order submission.
- No trading table writes.

Phase 4 preflight is complete as a boundary lock only. The preflight adds no sockets, credentials, external connection, FIX session, shadow replay submit, scheduler, gateway registration, or trading mutation. See [LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md](LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md).

Phase 4A is implemented as an external-session contract/stub only. It adds `ILmaxReadOnlyExternalSession` and related request/result/status/event/counter records plus a disabled session implementation. It does not add sockets, FIX logon/logout, credentials, Connectivity Lab calls, evidence creation, shadow replay submit, scheduler, gateway registration, or trading mutation. The disabled session always blocks and reports that the external-session implementation has not started.

Phase 4B is implemented as an external-session fake transport harness with no network. It adds in-memory fake transport scripts/messages/results and a fake session that can emit deterministic read-only events and counters under test-only fake options. It does not add sockets, FIX logon/logout, credentials, Connectivity Lab calls, evidence creation, shadow replay submit, scheduler, gateway registration, or trading mutation.

Phase 4C is implemented as fake transport to sanitized evidence preview mapping. It maps in-memory fake read-only events into `lmax-fix-lifecycle-evidence-v1` JSON, validates compatibility through existing preview/fixture validation tests, and remains no-shadow-submit/no-persistence. It does not add sockets, FIX logon/logout, credentials, Connectivity Lab calls, shadow replay submit, scheduler, gateway registration, or trading mutation.

Phase 4D is implemented as a local manual fake transport preview endpoint. It adds `POST /lmax-readonly-runtime/fake-transport-preview`, which is blocked by default and can run only predefined in-memory fake scenarios when the API is explicitly launched with local fake-preview configuration. It returns evidence mode/count/validation summaries and records an in-memory run summary only. It does not accept raw FIX, paths, host/user/password fields, credentials, order controls, live connection controls, scheduler controls, or `SubmitToShadowReplay=true`.

Phase 4E is implemented as a hard-disabled external read-only session skeleton. It adds `LmaxReadOnlyExternalSessionSkeleton`, `LmaxReadOnlyExternalSessionSkeletonFactory`, and `LmaxReadOnlyExternalSessionSkeletonSafetyReport`. The skeleton always returns disabled/blocked and reports `SkeletonOnly`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, `ShadowReplaySubmitImplemented=false`, and `TradingMutationImplemented=false`. It does not instantiate network clients, read credentials, perform FIX logon/logout, call Connectivity Lab, create evidence, submit to shadow replay, register a gateway, schedule work, or mutate trading state.

Phase 4F is implemented as a guarded transport interface and disabled transport only. It adds `ILmaxReadOnlyGuardedTransport`, `ILmaxReadOnlyGuardedTransportFactory`, `LmaxReadOnlyGuardedTransportDisabled`, and transport request/result/status/capability/safety records. The interface names future read-only responsibilities such as `ConnectReadOnlyAsync`, `ReadEventsAsync`, and `DisconnectAsync`, but the disabled implementation always blocks, returns no events, opens no socket, attempts no FIX logon, uses no credentials, creates no evidence, submits nothing to shadow replay, and mutates no trading state.

Phase 4G is implemented as a typed configuration envelope and validator only. It adds external-session options, environment/limits/credential-profile records, structured validation issues, and an inactive sample JSON file. The envelope uses profile labels only and contains no credential values, no host/user/password fields, no socket activation, no real transport, no FIX logon/logout, no order submission, no scheduler activation, no shadow replay submit, and no trading mutation.

Phase 4H is implemented as a credential-profile boundary and disabled resolver only. It adds resolver contracts, safe profile descriptors, safety/status/result records, and `LmaxReadOnlyCredentialProfileResolverDisabled`. `CredentialProfileName` is a non-secret label only. The disabled resolver does not read user-secrets, environment variables, appsettings values, vaults, or credential material; it does not use, store, log, or return credential values. Credential use remains blocked because the resolver is disabled.

Phase 4I is implemented as a non-secret venue-profile boundary and disabled/static registry only. It adds venue profile labels, descriptors, validation results, and `LmaxReadOnlyVenueProfileRegistryDisabled`. `VenueProfileName` is a non-secret label only. `DemoLondon` is recognized as an inactive future prototype label, `LmaxDemoReadOnly` remains an inactive legacy demo label, and `Uat`/`Production` are blocked for the current Phase 4 path. No host, port, user, endpoint URL, account ID, sender/target comp ID, session value, or credential value is exposed.

Phase 4J is implemented as a run-intent envelope and validator only. It adds manual intent/request records, an intent mode enum, validation result/summary records, and `LmaxReadOnlyExternalSessionRunIntentValidator`. Manual reason and operator id are required. `FutureExternalReadOnlyManual` remains blocked because implementation has not started. `ValidateOnly` and `PreviewOnly` validate the intent only; no endpoint was added, no session starts, no credential values are read, no shadow replay submit occurs, and no trading state is mutated.

Phase 4K is implemented as a validate-only manual preflight endpoint: `POST /lmax-readonly-runtime/external-run-intent/validate`. It validates the Phase 4J run-intent envelope, returns structured issues/safety gates/operator guidance, and always reports that no session can start. It does not persist a run, start a session, open sockets, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4L is implemented as a no-network dry-run report endpoint: `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`. It uses the same safe intent envelope, aggregates intent/options validation, venue profile status, disabled credential resolver status, disabled guarded transport status, blocked skeleton status, safety gates, expected outcome, blocked reason, and operator guidance. It always reports that no session can start and that no external connection, credential read, shadow replay submit, or trading mutation was attempted.

Phase 4M is implemented as a manual signoff envelope endpoint: `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`. It validates signoff metadata and required attestations for a dry-run report, enforces maker/checker checks for risk/approver roles, and always returns `canAuthorizeExecution=false`. It does not persist an approval, start a session, open sockets, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4N is implemented as a pre-activation audit envelope endpoint: `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`. It validates an intent/dry-run/signoff summary chain, stable blockers, and no-attempt flags, and always returns `canAuthorizeExecution=false`. It does not persist execution authorization, start a session, open sockets, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4O is implemented as a readiness snapshot endpoint: `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`. It aggregates intent validation, dry-run report, signoff, pre-activation audit, options/config, venue profile, credential profile, guarded transport, skeleton, safety gates, blockers, and final decision into one no-network snapshot. It always returns `canStartSession=false` and does not persist execution authorization, start a session, open sockets, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4P is implemented as the final no-socket release gate: `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`, with the institutional decision record in `docs/LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md`. It validates the Phase 4A-4O safety envelope and writes a local ignored report, but it does not add socket capability or execution capability.

Phase 5A is implemented as first-transport prototype preflight planning: `docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md`, `docs/LMAX_READONLY_RUNTIME_PHASE5A_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1`. It defines kill/rollback, abort conditions, observability, evidence handling, and entry criteria for a future Phase 5B, but it does not add socket capability or execution capability.

The functional transport implementation has not started. It must not start until Phases 0-3.5, Phase 4 preflight, Phase 4A, Phase 4B, Phase 4C, Phase 4D, Phase 4E, Phase 4F, Phase 4G, Phase 4H, Phase 4I, Phase 4J, Phase 4K, Phase 4L, Phase 4M, Phase 4N, Phase 4O, Phase 4P, and Phase 5A pass, the release gate explicitly approves the work, and a separate explicit prompt authorizes implementation.

Allowed code changes:

- isolated read-only FIX session implementation
- parser/sanitizer tests
- fake FIX session tests
- compile/config gate

Forbidden code changes:

- default activation
- API/Worker automatic startup
- order submission methods
- credential UI
- trading-state mutation

Required tests:

- blocked default config
- no runtime activation unless all gates pass
- read-only message whitelist
- evidence contract validation
- mutation guards

Exit criteria:

- no default activation
- manual lab/demo test only
- all evidence replay remains non-mutating

### Phase 5 - Manual Demo Read-Only Runtime Capture

Goal:

- Allow a manual read-only connection to LMAX Demo through the runtime path after explicit gates.
- Capture events into evidence.
- Submit to shadow replay only if explicitly enabled.
- No scheduler.

Allowed code changes:

- manual run control
- bounded Demo/UAT config
- evidence artifact export
- optional local shadow replay submit

Forbidden code changes:

- production activation
- scheduler
- order submission
- trading-table writes

Required tests/smokes:

- manual Demo read-only run captured
- replay observations created
- no mutation
- audit and UI visible
- rollback tested

Exit criteria:

- operator runbook validated
- no unexplained mutation or exception behavior
- disable path tested

### Phase 6 - Soak / Rehearsal

Goal:

- Run multiple manual Demo/UAT read-only sessions.
- Compare shadow replay against LMAX EOD files.
- Monitor reconnects, duplicates, sequence behavior, protocol rejects, and observation policy outcomes.

Exit criteria:

- no unexplained observations
- no mutation
- stable dedupe/fingerprints
- operator playbook updated
- incident and rollback playbooks rehearsed

### Phase 7 - Scheduled Read-Only Shadow, Future Only

Goal:

- Optional scheduled read-only shadow mode.
- Still no trading mutation.

This phase requires separate governance approval, scheduler quality gate, operational sign-off, and release gate. It is not in current scope.

### Phase 8 - Trading Adapter, Separate Future Project

Trading adapter work is explicitly out of scope. It requires a separate design, certification, governance, operational, risk, and production-readiness program.

## 5. Detailed Phase Gates

Every phase must define:

- entry criteria
- allowed code changes
- forbidden code changes
- required tests
- required smoke scripts
- required docs updates
- rollback criteria
- exit criteria
- sign-off checklist

No phase may start if the prior phase has unresolved safety failures. A phase may not expand its scope to include order submission, scheduler activation, credentials in UI, or trading-state mutation.

## 6. Test Matrix

Required categories:

- safety gate tests
- no-mutation tests for orders, fills, positions, wallets, model runs, risk state, reconciliation state, and scheduler state
- evidence contract validator tests
- evidence normalizer tests
- observation policy tests
- audit tests
- exception-case tests
- UI status tests
- smoke scripts
- operational readiness gate
- local-only URL checks
- no credential/sensitive-content scans
- API/Worker FakeLmax-only DI guard tests

No test should require live LMAX in CI.

## 7. API/DI Registration Rules

API and Worker must remain `FakeLmaxGateway` until a separate explicit future activation phase.

Any future DI registration must be behind:

- config gate
- environment gate
- implementation mode gate
- activation level gate
- operational readiness gate
- governance gate
- read-only gate
- no-order-submission gate

Worker must not start the reader automatically. Scheduler activation is a separate future phase.

## 8. Configuration Plan

Future config sections should include disabled defaults only:

```text
Enabled=false
ImplementationMode=DesignOnly
ActivationLevel=Level1DisabledSkeleton
AllowExternalConnections=false
AllowCredentialUse=false
ReadOnly=true
AllowOrderSubmission=false
PersistRawFixMessages=false
PersistToTradingTables=false
SubmitToShadowReplay=false
DryRun=true
MaxRuntimeSeconds=30
MaxEventsPerRun=100
EnvironmentName=Local
SchedulerEnabled=false
```

Credential config must use profile names only. Secret values belong in user-secrets or environment variables after a future credential handling gate, never in appsettings or UI DTOs.

## 9. Evidence Flow Plan

Future runtime captures must flow through:

```text
FIX message
  -> sanitized event envelope
  -> normalized evidence event
  -> evidence batch
  -> contract validator/normalizer
  -> shadow replay request
  -> observations/audit/exception cases
  -> UI drilldown
```

The adapter must never write directly to orders, fills, positions, wallets, risk state, model runs, or reconciliation state.

## 10. Observability Plan

Runtime reader observability should include:

- run id
- status
- activation level
- implementation mode
- safety gate results
- evidence batch id
- event counts
- validation issue counts
- duplicate counts
- observation counts by severity and policy code
- audit event ids
- exception case ids for blocking observations
- last successful run
- last error
- no secrets in logs or DTOs

## 11. Rollback / Kill Plan

Rollback should be simple because the reader is non-mutating:

- set `Enabled=false`
- set `AllowExternalConnections=false`
- set `SubmitToShadowReplay=false`
- keep scheduler disabled
- stop any manual run
- retain sanitized evidence artifacts for diagnostics
- review observations/audit/exception cases
- escalate if protocol rejects or unexplained observations appear

Rollback must not require database repair of trading state because the reader must never mutate trading state.

## 12. Operational Runbook Requirements

Before live read-only runtime activation, runbooks must exist for:

- manual start
- manual stop
- failed logon
- protocol reject
- sequence reset/reconnect
- duplicate evidence
- unexpected blocking observation
- shadow replay failure
- EOD mismatch
- credential rotation
- disable/rollback
- operator escalation

## 13. Documentation Requirements

Each phase must update:

- Developer Guide
- Operator Manual
- Local Runbook
- Operational Readiness Checklist
- LMAX Adapter Design
- LMAX Read-Only Runtime Adapter Design
- Adapter Contracts
- API docs if endpoints change
- UI docs if pages or operator workflows change

Docs must be updated in the same change set as endpoint, config, policy, evidence, or UI changes.

## 14. Open Questions / Decisions Required

- Should the future runtime reader live in API, Worker, or a separate service?
- How should runtime reader run state be stored?
- Should sanitized evidence batches be persisted, or only replay runs/observations?
- What is the artifact retention policy?
- How should Demo/UAT/Production credential profiles be separated?
- Does LMAX require conformance/certification for read-only FIX usage?
- Which credential provider should be used?
- What exact governance approval is required for each activation level?
- Should scheduled read-only shadow ever be allowed?
- What observation history and EOD comparison are required before production readiness?
- What constitutes production readiness for a reader that remains non-mutating?

## 15. Current Recommendation

Phase 1 is implemented as inert contracts and no-op behavior only. Phase 2 is implemented as service-level fake/in-memory fixture preview only. Phase 3 is implemented as manual local diagnostic endpoints for disabled/default-blocked, fixture-only preview.

The next eligible technical phase is **Phase 4 - External Read-Only Session Prototype Behind Compile/Config Gate** only, and only after a separate explicit prompt, readiness gate pass, and governance/runbook review.

Recommended constraints for the next phase:

- real read-only FIX session code may exist only behind hard disabled compile/config gates
- no default activation
- no real API/Worker LMAX gateway registration
- no credentials
- no order submission
- no scheduler
- no trading-state mutation
- fixture-only endpoint remains default blocked until an explicit fake test/local configuration enables preview
- `SubmitToShadowReplay` remains blocked unless a future phase explicitly enables a safe local path
- tests must prove blocked defaults, local-only behavior, no mutation, and FakeLmax-only runtime

Do not proceed beyond Phase 4A until Phase 3 endpoint behavior exits cleanly, the Phase 4 preflight remains green, and the operational readiness gate remains green.

Phase 5B is implemented as a dedicated Demo/manual prototype boundary, but it refuses before socket/logon because the credential resolver remains disabled/no-op. The implementation adds `LmaxReadOnlySocketPrototypeTransport`, a manual script, and a Phase 5B gate. It does not register in API/Worker, submit orders, schedule work, submit to shadow replay, persist live FIX data, or mutate trading state.

Phase 5C is implemented as credential availability checking and redaction only. `LmaxReadOnlyCredentialProfileResolverEnvironment` checks required environment labels for presence and returns only sanitized labels, booleans, counts, and redaction status. `scripts/check-lmax-readonly-runtime-demo-credentials.ps1` requires `-ConfirmCredentialAvailabilityCheck` and prints no values. `scripts/check-lmax-readonly-runtime-phase5c-credential-gate.ps1` verifies the boundary.

Phase 5D is implemented as the first isolated manual Demo market-data socket prototype. It supports only a single EURUSD / SecurityID `4001` market-data snapshot attempt behind the manual script and credential availability gate. The prototype may attempt a FIX market-data logon and logout only when all required local environment labels are present and the operator supplies `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty `-Reason`.

Phase 5D remains outside API/Worker DI, keeps API/Worker on `FakeLmaxGateway`, adds no order messages, no scheduler, no shadow replay submit, no gateway replacement, no trading-table persistence, and no trading-state mutation. The next recommended phase is **Phase 5E - Manual Demo Snapshot Evidence Artifact Preview** if a snapshot succeeds, or **Phase 5E - Transport Failure/Retry Hardening** if connection or credential setup remains incomplete.

Phase 5E is implemented as transport failure, missing-credential, and retry hardening. The prototype now reports explicit statuses such as `BlockedMissingCredentials`, `BlockedSafetyGate`, `BlockedInvalidEnvironment`, `BlockedUnsafeVenue`, `BlockedOrderSubmissionFlag`, `FailedSafeConnectionError`, `FailedSafeLogonRejected`, `FailedSafeLogonTimeout`, `FailedSafeSnapshotTimeout`, `FailedSafeLogoutError`, `FailedSafeMaxRuntimeExceeded`, `FailedSafeMaxEventsExceeded`, `Completed`, and `CompletedWithWarnings`.

The retry policy is descriptive only: `RetryEnabled=false`, `RetryAllowed=false`, and `MaxAttempts=1`. It may recommend `FixCredentialsThenRetry`, `ReviewFailureThenRetry`, `DoNotRetry`, or `NoRetry`, but no automatic external retry exists. `scripts/check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1` verifies the taxonomy, no-auto-retry posture, redaction coverage, no order surface, no gateway registration, no hosted service, no shadow replay submit, and no trading mutation references.

Phase 5F is implemented as operator-approved manual Demo snapshot result capture. The manual script still requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and a non-empty `-Reason`; it prints the planned safety flags before any external attempt, verifies credential label availability first, and returns `BlockedMissingCredentials` with `externalConnectionAttempted=false` when labels are absent.

When the operator deliberately provides local Demo credentials and runs the script, the only allowed external action remains one EURUSD / SecurityID `4001` market-data snapshot attempt followed by logout. Output and the optional JSON result artifact under `artifacts/lmax-readonly-runtime-demo-snapshot/` are sanitized, include `noSensitiveContent=true` and `redactionStatus=Redacted`, and never include credential values or raw sensitive FIX. `scripts/check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1` verifies the capture boundary without making an external attempt.

Phase 5G is implemented as sanitized snapshot-timeout diagnostics. The first operator-approved Demo read-only run successfully logged on and logged out but timed out before receiving a snapshot, so the prototype now records request mode, request metadata, message-type counters, reject/session classifications, timeout timing, and sanitized warnings/errors. Additional manual request modes are available for explicit diagnostics only: `SecurityIdOnly`, `SlashSymbolOnly`, `SymbolOnly`, and `AutoSequence`. No automatic retry, scheduler, order path, shadow replay submit, gateway registration, or trading mutation is added.

Phase 5H is implemented as MarketDataRequest compatibility hardening. It encodes observed LMAX Demo rejects as request-profile metadata: `SnapshotOnly` / `263=0` is known rejected with `ValueOutOfRange`, tag `55` symbol encodings are known-risk/known-rejected diagnostics, and `InternalSymbol` is known rejected with a repeating-group mismatch around tag `146`. The default profile is now `SnapshotPlusUpdates` plus `SecurityIdOnly`, with sanitized field summary `263=1`, `48 present`, `22=8`, and `55 omitted`. Known rejected profiles block locally unless `AllowKnownRejectedDiagnostics` is explicit.

Phase 5J is implemented as Demo MarketData logon diagnostics and session-profile alignment. The current blocker is no confirmed MarketData FIX logon: runtime and Connectivity Lab have reached TCP/TLS and sent Logon, but observed `MsgType=5` Logout/session behavior before logon confirmation. The prototype now records sanitized logon diagnostics: profile labels, credential/comp-id presence and lengths only, FIX BeginString, EncryptMethod, HeartBtInt, ResetSeqNumFlag, first inbound message type, sanitized Logout/Reject text, logon wait duration, TCP/TLS flags, and a runtime-vs-lab profile-label comparison. The manual script supports `-ShowSanitizedLogonDiagnostics`; the Phase 5J gate verifies diagnostics, tests, no order surface, no gateway registration, no scheduler, no shadow replay submit, and no trading mutation without making an external attempt.

Phase 5L is implemented as successful Demo snapshot artifact review and closure. The first successful operator-approved Demo read-only EURUSD / SecurityID `4001` snapshot artifact validates as `Completed`, with logon/snapshot/logout success, best bid/ask/mid present, no secret leakage, `CredentialValuesReturned=false`, no order submission, no scheduler, no shadow replay submit, and no trading mutation. The new artifact validator and Phase 5L gate do not connect externally or add runtime behavior. The next recommended phase is **Phase 5M - Manual Demo Snapshot Evidence Preview Mapping, No Shadow Submit** or **Phase 5M - Repeated Manual Snapshot Stability Check**, still manual-only with no scheduler and no shadow replay submit.
## Phase 5M - Manual Demo Snapshot Evidence Preview Mapping

Phase 5M is implemented as a local mapper/script/gate only. The mapper accepts a Phase 5L validated successful Demo EURUSD snapshot artifact and produces a sanitized `MarketDataOnly` `lmax-fix-lifecycle-evidence-v1` preview with empty execution, order-status, trade-capture, and protocol-reject arrays.

Exit checks:
- `scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1 -ArtifactFile <artifact>` validates the artifact, maps preview JSON, and validates the evidence contract.
- `scripts/check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1 -ArtifactFile <artifact>` confirms mapper/script/tests exist and no order/gateway/scheduler/shadow-submit/mutation surface was added.

This phase does not submit to shadow replay, create observations, connect externally, register a gateway, start a scheduler, or mutate trading state. Next recommended phase: Phase 5N - Manual MarketDataOnly Evidence Replay Dry-Run / No Runtime Submit, or Phase 5N - Repeated Manual Snapshot Stability Check.

## Phase 5N - Manual MarketDataOnly Evidence Replay Dry-Run / No Runtime Submit

Phase 5N is implemented as a manual script/gate only. It validates a Phase 5M preview, confirms `evidenceMode=MarketDataOnly`, confirms all replay arrays are empty, and uses the existing local `/lmax-shadow/replay` API only when the operator runs the script against a local API.

Exit checks:
- Replay result is `Completed`.
- Observation, blocking-observation, and warning-observation counts are all zero.
- Order/fill/internal-position counts are unchanged.
- Runtime prototype and mapper do not call shadow replay.
- API/Worker remain `FakeLmaxGateway` only.

## Phase 5O - Repeated Manual Snapshot Stability Check

Phase 5O is implemented as a manual script/gate and summary validator only. The stability script requires `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, `-ConfirmRepeatedManualSnapshots`, non-empty `-Reason`, and an explicit `-AttemptCount` capped to 1..5. `DelaySeconds` is capped to 1..10 and is only a bounded pause between planned manual attempts, not scheduler or polling behavior.

Each planned attempt reuses the existing Demo EURUSD / SecurityID `4001` manual snapshot prototype. Successful artifacts are validated with the Phase 5L validator and mapped to Phase 5M `MarketDataOnly` evidence previews. Optional preview replay remains separate and explicit through `-ReplayEvidencePreviews`; the runtime prototype still does not submit to shadow replay.

Exit checks:
- `scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1` passes without an external attempt.
- Stability summary artifacts are ignored, sanitized, and validated by `LmaxReadOnlyDemoSnapshotStabilitySummaryValidator`.
- No scheduler, automatic polling, automatic retry, order submission, gateway registration, runtime shadow replay submit, or trading-state mutation is added.
- API/Worker remain `FakeLmaxGateway` only.

Next recommended phase: Phase 5P - Stability Results Review / Readiness Decision, or Phase 5P - Controlled Manual MarketData Evidence Workflow Hardening.

## Phase 5P - Stability Results Review / Readiness Decision

Phase 5P is implemented as a closure validator, local review script, gate, and decision document only. It reviews the Phase 5O stability summary, validates referenced successful snapshot artifacts, validates referenced `MarketDataOnly` previews, and records the readiness decision.

The operator-run summary `artifacts/lmax-readonly-runtime-demo-snapshot/stability/lmax-readonly-demo-snapshot-stability-20260508-144517.json` closes with:
- AttemptCountRequested = `3`
- AttemptCountCompleted = `3`
- SuccessCount = `3`
- FailedSafeCount = `0`
- SnapshotReceivedCount = `3`
- no order submission, no runtime shadow replay submit, no scheduler, no trading mutation, and no credential values returned

Exit checks:
- `scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1 -StabilitySummaryFile <summary>` returns `PASS`.
- `scripts/check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1 -StabilitySummaryFile <summary>` returns `PASS`.
- No external socket attempt is made by review/gate scripts.
- API/Worker remain `FakeLmaxGateway` only.

Next recommended phase: Phase 5Q - Controlled Manual MarketData Evidence Workflow Hardening, or Phase 5Q - Manual MarketData Snapshot + Manual Replay Batch Review. Do not recommend scheduler yet.

## Phase 5Q - Controlled Manual MarketData Evidence Workflow Hardening

Phase 5Q is implemented as a local workflow manifest validator, review script, and gate only. It accepts the Phase 5O stability summary or explicit sanitized snapshot artifacts, validates each artifact with the Phase 5L validator, validates or regenerates Phase 5M `MarketDataOnly` previews, optionally records explicitly requested Phase 5N manual replay results, and writes an ignored sanitized workflow manifest.

Exit checks:
- `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1 -StabilitySummaryFile <summary>` writes a manifest with artifact and preview counts matching the summary.
- Default review has `ManualReplayCount=0` and `FinalDecision=PASS_WITH_WARNINGS` only because replay is intentionally omitted.
- Optional replay requires `-ReplayEvidencePreviews -ConfirmLocalReplay` and remains manual local API replay only.
- `scripts/check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1 -StabilitySummaryFile <summary>` passes without external connection.
- Runtime still does not submit to shadow replay, and no scheduler, polling, order submission, gateway registration, or trading mutation is added.

Next recommended phase: Phase 5R - Manual MarketData Workflow Review with Optional Replay, or Phase 5R - Read-Only Runtime Demo MarketData Operational Runbook Hardening. Do not recommend scheduler yet.

## Phase 5R - Manual MarketData Workflow Review with Optional Replay

Phase 5R is implemented by extending the Phase 5Q workflow review path and adding a replay-review gate. Replay remains optional, explicit, local API only, and separate from runtime. `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1` now requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay` and localhost API availability before invoking the existing Phase 5N replay script for each `MarketDataOnly` preview.

Exit checks:
- Default workflow review remains no replay and `PASS_WITH_WARNINGS`.
- Replay workflow manifests record `replayRequested`, `replayPerformed`, `manualReplayCount`, and `manualReplayResults`.
- Successful replay manifests require replay count to match preview count.
- Each replay result must be `Completed`, zero-observation, mutation-guard `Unchanged`, and sanitized.
- `scripts/check-lmax-readonly-runtime-phase5r-manual-replay-review-gate.ps1 -WorkflowManifestFile <manifest>` validates replay-reviewed manifests.
- Runtime still does not submit to shadow replay, and no external snapshot attempt, scheduler, polling, order submission, gateway registration, or trading mutation is added.

Next recommended phase: Phase 5S - Read-Only Runtime Demo MarketData Operational Runbook Hardening, or Phase 5S - Controlled Manual Workflow Release Gate. Do not recommend scheduler yet.

## Phase 5S - Controlled Manual Workflow Release Gate

Phase 5S is implemented as a release validator, local release script, and gate. The release workflow accepts the closed Phase 5O stability summary, validates referenced Phase 5L artifacts and Phase 5M `MarketDataOnly` previews, optionally records explicit Phase 5R local replay results, and writes `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json`.

Exit checks:
- `scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1` requires operator acknowledgement flags, a non-empty reason, and capped attempt/delay values.
- The release manifest has artifact count equal preview count.
- Referenced artifacts validate through the Phase 5L validator.
- Referenced previews validate as `MarketDataOnly`.
- If replay is skipped, the release decision is `PASS_WITH_WARNINGS`.
- If replay is requested, it requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay`, local API availability, replay count equal preview count, all replays `Completed`, zero observations, and unchanged mutation guards.
- `scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1` writes `artifacts/readiness/phase5s-manual-release-gate.json`.
- Runtime still does not submit to shadow replay, and no external snapshot attempt, scheduler, polling, order submission, gateway registration, or trading mutation is added.

Next recommended phase: Phase 5T - Read-Only Runtime Demo MarketData Operational Runbook Hardening, or Phase 5T - Controlled Manual Workflow Release Review. Do not recommend scheduler yet.

## Phase 5T - Controlled Manual Workflow Release Review / Runbook Freeze

Phase 5T is implemented as a documentation and local gate pass. It adds `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md` and `scripts/check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1`.

Exit checks:
- The Phase 5S manifest exists and reports `PASS` or `PASS_WITH_WARNINGS`.
- The Phase 5S gate report exists and any warning is limited to optional replay skipped.
- The frozen runbook documents prerequisites, workflow commands, optional replay, rollback, stop conditions, and decision semantics.
- Runtime still does not submit to shadow replay.
- No scheduler, polling, order path, gateway registration, external socket attempt, trading mutation, or credential-value exposure is added.

Next recommended phase: Phase 5U - Optional Local Replay Completion to Convert `PASS_WITH_WARNINGS` to `PASS`, or Phase 5U - Read-Only MarketData Workflow Operational Signoff.

## Phase 5V - Controlled Manual MarketData Workflow Final Release Gate / Audit Pack

Phase 5V is implemented as a local audit-pack validator, builder script, final gate, and documentation update. It packages the validated Phase 5O stability summary, sanitized snapshot artifacts, `MarketDataOnly` previews, replay-enabled workflow manifest, replay results, gate reports, and safety confirmations into ignored JSON/Markdown artifacts.

Exit checks:
- The audit pack final decision is `PASS`.
- Artifact, preview, and manual replay counts are present and equal.
- All replay results are `Completed`, zero-observation, and mutation-guard `Unchanged`.
- Runtime shadow replay submit remains false.
- No scheduler, polling, order path, gateway registration, external socket attempt, trading mutation, or credential-value exposure is added.

Next recommended phase: Phase 5W - Operational Signoff / Demo Read-Only MarketData Workflow Freeze, or Phase 5W - Controlled Manual MarketData Workflow UI/Operator Summary.
## Phase 5W - Operational Signoff / Demo Read-Only Workflow Freeze

Status: implemented.

Phase 5W adds `LmaxReadOnlyMarketDataOperationalSignoffValidator`, `scripts/signoff-lmax-readonly-marketdata-workflow.ps1`, `scripts/check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1`, and `docs/LMAX_READONLY_RUNTIME_OPERATIONAL_SIGNOFF.md`.

It validates the Phase 5V audit pack and signs off only the controlled manual Demo read-only MarketData workflow: three sanitized artifacts, three `MarketDataOnly` previews, three explicit manual local replays, zero observations, unchanged mutation guards, and no credential exposure. It does not run LMAX, run snapshots, perform replay, register a gateway, schedule work, submit orders, submit runtime shadow replay, or mutate trading state.

Next recommended phase: Phase 5X - Optional Operator Console Summary / Read-Only Workflow Status Panel, or a planning gate for future manual runtime shadow replay submit if ever desired. Neither is authorized by Phase 5W.

## Phase 5X - Optional Operator Console Summary / Read-Only Workflow Status Panel

Status: implemented.

Phase 5X adds `LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator`, a read-only status endpoint, a local status script, a status panel on the LMAX Shadow page, and `scripts/check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1`.

This phase surfaces Phase 5W signoff/audit-pack status only. It adds no runtime execution, no scheduler or polling, no runtime shadow replay submit, no order submission, no gateway registration, no credential value flow, no production/UAT activation, and no trading-state mutation.

Next recommended phase: Phase 5Y - Final Demo Read-Only MarketData Workflow Documentation Pack, or a planning-only gate for future manual runtime shadow replay submit if desired later.

## Phase 6D - SecurityID Discovery Planning Boundary

Phase 6D is implemented as a local planning manifest and gate only. `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest` records candidate placeholder SecurityID values for GBPUSD, USDJPY, EURGBP, and AUDUSD, and `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator` confirms every allowlist symbol is present while every `IsApprovedForExternalRun` flag remains false.

The Phase 6D gate is:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1
```

It writes `artifacts/readiness/phase6d-securityid-discovery-gate.json`. Phase 6D does not call LMAX, run a snapshot, run replay, schedule or poll, submit orders, register a gateway, expose credentials, or mutate trading state.

## Phase 6E - SecurityID Source Evidence Review Boundary

Phase 6E is implemented as an evidence-review model, default pending manifest, validator, tests, and local gate. It defines what evidence is required before Phase 6D placeholder values can be replaced by accepted planning values.

Accepted planning evidence requires:

- an allowlisted symbol,
- non-placeholder proposed SecurityID,
- evidence source type and reference,
- reviewer and reviewed timestamp,
- High or Confirmed confidence,
- `IsApprovedForExternalRun=false`,
- `noSensitiveContent=true`.

The current default manifest leaves GBPUSD, USDJPY, EURGBP, and AUDUSD as `NeedsMoreEvidence`, so the expected gate decision is `PASS_WITH_KNOWN_WARNINGS`.

Run:

```powershell
.\scripts\check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1
```

The gate writes `artifacts/readiness/phase6e-securityid-evidence-review-gate.json`. Phase 6E does not call LMAX, run a snapshot, run replay, schedule or poll, submit orders, register a gateway, expose credentials, or mutate trading state.

## Phase 6F - Manual SecurityID Confirmation Records

Phase 6F is implemented as a local confirmation record model, record validator, record creation script, review script, sample JSON template, tests, and gate script.

Files:

- `LmaxReadOnlyInstrumentSecurityIdConfirmationRecord`
- `scripts/new-lmax-readonly-securityid-confirmation-record.ps1`
- `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`
- `scripts/check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1`
- `docs/examples/lmax-readonly-securityid-confirmation-record.sample.json`

Accepted records are still planning-only. They require non-placeholder SecurityIDs and reviewer metadata, but they cannot set `IsApprovedForExternalRun=true`.

Current expected decision is `PASS_WITH_KNOWN_WARNINGS` until confirmation records exist for all four candidate instruments.

## Phase 6G - Manual Record Entry Workflow Hardening

Phase 6G hardens Phase 6F without adding runtime behavior.

Additions:

- `scripts/new-lmax-readonly-securityid-confirmation-record-template.ps1`
- `scripts/check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1`
- `docs/LMAX_READONLY_SECURITYID_CONFIRMATION_OPERATOR_CHECKLIST.md`
- creation-script preview/no-overwrite behavior
- richer review-script per-instrument summaries

The gate generates templates under ignored artifacts, reviews current records, and returns `PASS_WITH_KNOWN_WARNINGS` when accepted records are still missing. It does not call LMAX, run snapshots, run replay, schedule or poll, submit orders, register a gateway, expose credentials, or mutate trading state.

## Phase 6H - Real Confirmation Records, Local Only

Phase 6H adds the real-record entry convention and gate:

- `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`
- `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1`
- review defaults to the real directory
- creation supports `-OutputDirectory`, `-OutputFile`, `-WhatIfPreview`, and `-Force`

This phase still does not call LMAX, run snapshots, run replay, schedule or poll, submit orders, register a gateway, expose credentials, or mutate trading state. `AcceptedForPlanning` records are planning inputs only and must keep `IsApprovedForExternalRun=false`. Next phase is Phase 6I to apply accepted planning values while still non-executable, if accepted records exist.

## Phase 6I - SecurityList Discovery

Phase 6I adds a manual discovery tool, not a runtime adapter. The implementation parses SecurityList metadata, filters the four Phase 6 candidates, writes sanitized planning artifacts, and keeps all execution flags false.

The manual script sends only FIX `SecurityListRequest` on Demo market-data credentials after explicit operator flags. It does not enter API/Worker, does not request market data snapshots, and does not create confirmation records automatically. Follow-up is Phase 6J record preparation or failure diagnostics.

## Phase 6J - SecurityList Diagnostics

Phase 6J adds diagnostics after the first failed-safe SecurityList attempt. It introduces artifact validation, reject classification, request-profile compatibility metadata, and an `AutoSequence` operator mode for a future manually approved attempt.

This is still not a runtime adapter. API/Worker remain FakeLmax-only, and no scheduler, order path, replay submit, gateway registration, or trading mutation is introduced.

## Phase 6L - SecurityList Fallback Decision

Phase 6L adds local analysis for the Phase 6K AutoSequence failure artifact. The review script extracts sanitized attempt diagnostics when present, reports missing reject diagnostics when absent, summarizes unmatched candidates, and writes a fallback decision under `artifacts/readiness/phase6l-securitylist-fallback-decision.json`.

The implementation does not retry LMAX, run snapshots, run replay, schedule/poll, submit orders, register a gateway, expose credentials, or mutate trading state. With the current failed-safe artifact, the recommended next phase is Phase 6M - VendorSupportConfirmation Record Preparation, No External Run.

## Phase 6M - CSV SecurityID Record Preparation

Phase 6M adds an offline CSV extractor and a record generation script for uploaded LMAX instrument CSVs. The implementation validates the required CSV columns, selects the DemoLondon/NewYork 400x IDs, rejects missing/conflicting/unexpected values, and writes one `AcceptedForPlanning` confirmation record per candidate instrument.

The selected values are GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007. Tokyo 600x IDs are explicitly not applied to the current profile. This phase still does not connect to LMAX, run SecurityListRequest, request snapshots, run replay, schedule/poll, submit orders, register a gateway, expose credentials, or mutate trading state.

## Phase 6N - Planning Manifest Application

Phase 6N applies the accepted Phase 6M confirmation records to a local planning manifest artifact. It does not update API/Worker runtime registration and does not make any instrument executable. The manifest stores the confirmed DemoLondon SecurityIDs, SecurityIDSource=8, Demo/DemoLondon scope, evidence references, and confirmation record IDs.

This phase replaces planning placeholders with confirmed planning values only. It still does not connect to LMAX, run SecurityListRequest, request snapshots, run replay, schedule/poll, submit orders, register a gateway, expose credentials, persist live FIX, or mutate trading state.

## Phase 6O - Per-Instrument Safety Gate Design

Phase 6O adds a planning-only safety gate layer over the Phase 6N manifest. It creates per-instrument results and an aggregate safety gate manifest for GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007.

The implementation deliberately keeps `eligibleForManualSnapshotAttempt=false`. A passing gate means the planning state is internally complete and safe for a later preflight design phase, not that an external Demo snapshot may be attempted. API/Worker registration remains unchanged and FakeLmax-only; no scheduler, polling, runtime replay submit, order path, gateway registration, credential exposure, external connection, snapshot, replay, or trading mutation is added.

## Phase 6P - Additional Snapshot Preflight Design

Phase 6P adds a local operator intent envelope and preflight result manifest for potential future one-off manual Demo read-only MarketData snapshot attempts. It consumes the Phase 6N planning manifest and Phase 6O safety gates, but does not update runtime registration and does not run a snapshot.

The request shape is fixed to `SnapshotPlusUpdates`, `SecurityIdOnly`, SecurityIDSource=8, `MarketDepth=1`, safe runtime/wait/event caps, `allowExternalConnections=false`, `allowOrderSubmission=false`, `schedulerEnabled=false`, `submitToShadowReplay=false`, `persistToTradingTables=false`, `isApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, and `canRunExternalSnapshot=false`. The next implementation phase must still create a separate approval envelope or dry-run report before any external attempt can be considered.

## Phase 6Q - Approval Envelope

Phase 6Q adds a local approval-envelope layer for one selected additional instrument. The envelope references a PASS Phase 6P preflight result and captures operator/reviewer ids, reason, instrument details, request profile, and mandatory attestations. It is intentionally not wired into API/Worker runtime.

No envelope can set `canRunExternalSnapshot`, `eligibleForManualSnapshotAttempt`, or `isApprovedForExternalRun` true. `AcceptedForPlanning` means the approval metadata is complete for planning only.

## Phase 6R - Dry-Run Report

Phase 6R adds a local dry-run report for GBPUSD. The report joins the Phase 6N planning value, Phase 6O safety gate, Phase 6P preflight, and Phase 6Q approval envelope, then records the future step that would still be required before any manual attempt can be considered.

The dry-run report is not a command and not runtime configuration. It forces all run and attempt flags false and documents that Phase 6R does not authorize an external snapshot.
### Phase 6S Implementation Note

Phase 6S adds a planning-only GBPUSD attempt gate and does not change runtime adapter behavior. API and Worker continue to use `FakeLmaxGateway` only. No real gateway registration, scheduler, snapshot execution path, replay submit path, order path, or trading-state mutation is added.

### Phase 6T Implementation Note

Phase 6T adds only a planning model, script, gate, and docs for the GBPUSD execution plan. It does not add adapter execution behavior or runtime registration changes.

### Phase 6U Implementation Note

Phase 6U adds only an operator signoff model, scripts, gate, tests, and docs. It does not add adapter execution behavior, real gateway registration, scheduler, snapshot execution, replay submit, orders, or trading mutation.

### Phase 6V Implementation Note

Phase 6V adds only a final readiness model, scripts, gate, tests, and docs. It does not add adapter execution behavior, real gateway registration, scheduler, snapshot execution, replay submit, orders, or trading mutation.

### Phase 6W Implementation Note

Phase 6W adds a one-shot manual wrapper around the isolated prototype path and a result validator/gate. It does not register a real gateway in API or Worker and does not introduce scheduler, order, replay submit, or mutation behavior.
## Phase 6X GBPUSD Empty-Book Handling

The GBPUSD result validator now treats `CompletedWithEmptyBook` as a safe warning state when the artifact shows a successful logon, attempted request, received snapshot, zero entries, null bid/ask/mid, one MarketDataSnapshot, zero rejects, no errors, and all unsafe flags false. `Completed` with bid/ask remains `PASS`; unsafe or sensitive artifacts remain `FAIL`.

Empty-book evidence preview mapping is supported as `MarketDataOnly` with empty execution/order/trade/reject arrays and warning text. No runtime shadow replay submit path is added.

## Phase 6Y Market-Hours Retry Readiness

Phase 6Y adds a local readiness model for the Monday/market-hours retry. The validator requires `CompletedWithEmptyBook` from Phase 6X, marks the previous attempt as outside market hours, keeps retry manual-only and single-attempt, and rejects automatic run, scheduler/polling, runtime shadow replay submit, orders, trading mutation, sensitive content, or non-`FakeLmaxGateway` API/Worker mode.
## Phase 6Z-A Implementation Note

Phase 6Z-A adds the additional-instrument planning pipeline model, builder, gate, and tests without changing runtime adapter execution. The pipeline is an artifact aggregation layer only: it summarizes planning, safety, preflight, approval, dry-run, attempt-gate, execution-plan, operator-signoff, and final-readiness artifacts for GBPUSD, EURGBP, USDJPY, and AUDUSD.

No adapter registration changes are part of this phase. API and Worker remain `FakeLmaxGateway`; no scheduler/polling, runtime shadow replay submit, order surface, gateway registration, external connection, snapshot, replay, or trading mutation is introduced.

The implementation invariant is `executableCount=0` with every run eligibility flag false for every instrument.

## Phase 6Z-C Implementation Note

Phase 6Z-C adds read-only visibility over the Phase 6Z-A planning pipeline. The endpoint and UI panel do not call LMAX, do not read credentials, do not request snapshots, do not replay evidence, and do not mutate runtime or trading state.

## Phase 6Z-D Implementation Note

Phase 6Z-D adds the final additional-instrument planning documentation pack and freeze gate. The builder reads the Phase 6Z-A aggregate pipeline manifest and Phase 6Z-C planning status report, writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/`, and preserves `executableCount=0`.

This phase is documentation/reporting only. It adds no socket, no external API call, no SecurityListRequest, no MarketData snapshot, no replay, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no credential exposure, and no trading mutation. Future market-hours execution remains a separate explicit operator phase.

## Phase 6Z-E Implementation Note

Phase 6Z-E adds the market-hours next-action summary model, read-only API endpoint, local script, gate, UI panel, and tests. The implementation reads local sanitized artifacts only and projects the prepared GBPUSD retry state to operators.

The endpoint is `GET /lmax-readonly-runtime/market-hours-next-action`. It returns GBPUSD=4002, previous `CompletedWithEmptyBook` outside market hours, readiness decisions, `executableCount=0`, and false run flags. It does not start sessions, read credentials, connect to LMAX, run snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state.

The adapter implementation remains unchanged: API and Worker stay on `FakeLmaxGateway`, and the new status surface only projects local sanitized artifact state.

## Phase 7A Implementation Note

Phase 7A adds no adapter implementation. It creates the ADR, boundary checklist, and local source-scan gate for the next read-only runtime boundary.

The recommended next phase is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run. Scheduler/polling, runtime shadow replay submit, order path, real gateway registration, production/UAT, and multi-instrument batch execution remain explicitly deferred. API and Worker remain `FakeLmaxGateway` only.

## Phase 7B Implementation Note

Phase 7B adds only a planning model, builder script, gate script, tests, and documentation for a controlled manual multi-instrument workflow. It does not add adapter execution behavior or runtime registration changes.

The model enforces the sequence GBPUSD, EURGBP, USDJPY, AUDUSD while keeping `batchExecutionAllowed=false`, `executableCount=0`, and all run eligibility flags false. API and Worker remain `FakeLmaxGateway`; no scheduler/polling, runtime shadow replay submit, order path, real gateway registration, production/UAT, batch execution, or trading mutation is introduced.

## Phase 7C Implementation Note

Phase 7C adds local closure scripts and a small validator for post-run GBPUSD market-hours artifacts. It does not add an adapter runtime path, gateway registration, scheduler, polling loop, hosted service, or shadow replay submit integration.

The implementation remains artifact-driven: review a supplied sanitized result, map safe results to MarketDataOnly preview, optionally replay through an explicit manual local script, and build a closure manifest. API and Worker remain `FakeLmaxGateway` only.

## Phase 7D Implementation Note

Phase 7D adds only a local decision model, decision script, gate, tests, and documentation for the post-GBPUSD branch point. It does not add adapter execution behavior or runtime registration changes.

The decision artifact can remain pending, proceed to EURGBP planning after GBPUSD `CompletedWithBook/PASS`, require a controlled GBPUSD retry after safe empty-book, or block for diagnostics after failed-safe/unsafe closure. API and Worker remain `FakeLmaxGateway`; no scheduler/polling, runtime shadow replay submit, order path, real gateway registration, batch execution, or trading mutation is introduced.

## Phase 7E Implementation Note

Phase 7E adds a checklist-pack validator, builder, gate, tests, and documentation for the future GBPUSD market-hours manual procedure. It records the future command as text only and does not execute it.

The implementation is documentation/reporting-only. It introduces no adapter runtime path, scheduler, polling loop, hosted service, shadow replay submit path, order surface, gateway registration, or trading mutation. API and Worker remain `FakeLmaxGateway` only.

## Phase 7E2 Implementation Note

EURGBP readiness rehydration consumes existing planning artifacts and the corrected Phase 7D decision. It adds no adapter runtime capability and preserves FakeLmaxGateway-only API/Worker registration, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, and no trading mutation.

## Phase 7G2 Implementation Note

Phase 7G2 adds the EURGBP final pre-run gate artifact, builder script, gate script, model, and tests. It aggregates Phase 7D, Phase 7E2, and Phase 7F2 into one consistency report while keeping EURGBP non-executable.

This phase adds no adapter runtime capability, no real gateway registration, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no external connection, no snapshot, no replay, and no trading mutation.

## Phase 7H Implementation Note

Phase 7H adds generic one-instrument scripts for the additional MarketData workflow: manual wrapper, artifact review, evidence preview mapping, optional local replay, closure manifest, and gate. The isolated prototype is extended only to recognize the exact additional-instrument allowlist and write per-symbol sanitized artifacts.

## Phase 7H2 Implementation Note

Phase 7H2 adds a generic additional-instrument final pre-run gate builder and validator. It supports GBPUSD/EURGBP/USDJPY/AUDUSD identity mapping, rejects Tokyo 600x variants for DemoLondon, and keeps all execution eligibility flags false.

The implementation preserves the Phase 7H wrapper boundary: generic Phase 6Z-A final-readiness artifacts are still rejected, and the wrapper still requires a Phase 7H-compatible final pre-run gate plus explicit operator flags before any future one-instrument Demo read-only attempt.

The implementation remains outside API/Worker runtime registration. It adds no scheduler/polling, no batch execution, no automatic retry, no runtime shadow replay submit, no order surface, no gateway registration, no live FIX persistence, and no trading-state mutation.
