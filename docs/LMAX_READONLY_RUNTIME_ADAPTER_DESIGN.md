# LMAX Read-Only Runtime Adapter Design

## Phase 6A Operationalization Boundary

Phase 6A does not change the runtime design. It records the next boundary after the frozen Phase 5 manual Demo MarketData workflow.

The current approved state remains:

- Manual Demo EURUSD MarketData workflow only.
- Sanitized artifact and `MarketDataOnly` evidence preview pipeline.
- Optional explicit local manual replay only.
- API/Worker `FakeLmaxGateway` only.
- Runtime shadow replay submit absent.
- Scheduler and polling absent.
- Order surface absent.
- Trading-state mutation absent.

The recommended next design boundary is `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run`. Any instrument expansion must define an allowlist, manual flags, evidence expectations, artifact validation, replay validation, rollback, abort conditions, and an explicit gate before any new external run is considered.

This document defines the future read-only LMAX runtime shadow adapter. It is a design and contract document only. It does not enable live connectivity, does not register a runtime adapter, does not submit orders, and does not change the current FakeLmax-only API/Worker boundary.

The phased delivery plan is documented in [LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md](LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md), with a concise review table in [LMAX_READONLY_RUNTIME_PHASE_GATES.md](LMAX_READONLY_RUNTIME_PHASE_GATES.md). Those documents must be followed before any future runtime-facing code is added.

## 1. Purpose

The future LMAX read-only runtime adapter would collect LMAX FIX evidence in a controlled runtime context, normalize that evidence into the existing `lmax-fix-lifecycle-evidence-v1` contract, and submit it into the LMAX shadow replay and observation pipeline.

The component is intended to:

- consume read-only LMAX FIX evidence
- sanitize and normalize the evidence
- validate the evidence contract before replay
- create shadow observations, audit events, and configured exception cases
- provide operators with comparison evidence between broker-side LMAX data and internal state

It must never:

- mutate orders, fills, positions, wallets, risk decisions, model runs, reconciliation state, or trading state
- submit orders
- replace the official LMAX EOD reconciliation process
- bypass risk, governance, audit, or operational readiness gates

## 2. Non-Goals

This design does not introduce:

- order submission
- live trading
- OMS or EMS mutation
- position, wallet, PnL, or ledger mutation
- reconciliation mutation
- credential entry in the UI
- scheduler auto-run
- a replacement for LMAX EOD files as the official daily reconciliation source
- a bypass around governance, four-eyes approval, risk controls, audit, or exception management

Runtime trading adapter work, if ever pursued, is a separate project and a separate safety gate.

## 3. Current Boundary

The approved current state remains:

- Main API and Worker register `FakeLmaxGateway` only.
- No real `LmaxVenueGateway` is registered in API or Worker.
- No runtime LMAX FIX reader exists.
- LMAX FIX connectivity exists only under `tools/QQ.Production.Intraday.Lmax.ConnectivityLab`.
- The Connectivity Lab can connect externally only through explicit lab scripts and flags.
- Shadow replay is offline, local, and non-mutating.
- The live shadow reader skeleton is disabled/no-op.
- Operational Readiness Checklist & Release Gate #1 exists and must remain green before any next phase.

This document describes future architecture. It does not move the platform beyond that boundary.

## 4. Proposed Future Architecture

Future read-only runtime evidence flow:

```text
LMAX FIX read-only session
  -> raw event envelope
  -> sanitizer/redactor
  -> normalized evidence event
  -> evidence batch/run
  -> evidence contract validator
  -> shadow replay service
  -> observations/audit/exception cases
  -> operator UI
```

The write boundary is intentionally narrow:

```text
Allowed writes:
  LmaxShadowReplayRuns
  LmaxShadowObservations
  OperatorAuditEvents
  ExceptionCases for configured blocking observations

Forbidden writes:
  orders
  fills
  positions
  wallets
  risk decisions
  model runs
  reconciliation runs/breaks
  scheduler state
  production trading tables
```

No evidence path may directly update the internal book. Any mismatch becomes an observation and, when policy says so, an exception case for operator review.

## 5. Component Boundaries

Future component names and responsibilities:

| Component | Responsibility | Current status |
| --- | --- | --- |
| `ILmaxReadOnlyRuntimeAdapter` / `LmaxReadOnlyRuntimeAdapterDisabled` | Defines status, safety evaluation, and run contract for a bounded read-only run. Phase 1 implementation always returns disabled/blocked. | Inert Phase 1 contract/no-op. |
| `LmaxReadOnlyRuntimeAdapterFakeInMemory` | Loads local evidence fixtures, validates fixture shape, and produces preview counts without external connectivity. | Phase 2 service-level fake/in-memory preview; Phase 3 exposes it through disabled/default-blocked local diagnostic endpoints. |
| `LmaxFixReadOnlySession` | Owns logon/logout, sequence numbers, heartbeat, and read-only FIX requests. | Not implemented in runtime. Lab-only equivalents exist in Connectivity Lab. |
| `LmaxFixMessageParser` | Parses FIX messages into sanitized normalized DTOs. | Normalizers exist in `Infrastructure.Lmax` and lab code; runtime parser is not active. |
| `LmaxEvidenceBatchBuilder` | Builds `lmax-fix-lifecycle-evidence-v1` batches from normalized events. | Lab evidence builder exists; runtime batcher is not active. |
| `LmaxEvidenceSanitizer` | Removes credentials, password tags, auth headers, and sensitive metadata. | Evidence validator/sanitizer concepts exist. Runtime use is future work. |
| `ILmaxReadOnlyRuntimeEvidenceSink` / `LmaxReadOnlyRuntimeEvidenceSinkDisabled` | Future evidence submission boundary. Phase 1 sink rejects evidence and does not submit to shadow replay. | Inert Phase 1 contract/no-op. |
| `LmaxReadOnlyRuntimeSafetyGate` | Evaluates explicit safety gates and blocks unsafe configurations. | Design-only contract exists in `Infrastructure.Lmax`. |
| `ILmaxReadOnlyRuntimeRunStore` / `LmaxReadOnlyRuntimeRunStoreNoOp` | Future run attempt/status store. Phase 1 no-op store records nothing. | Inert Phase 1 contract/no-op. |
| `LmaxReadOnlyRuntimeRunStoreInMemory` | Records fake run results in memory only for service-level tests and manual diagnostic endpoint summaries. | Phase 2/3 in-memory only; no DB migration. |
| Hosted service | Optional future scheduler/manual worker wrapper. | Not implemented and must remain disabled by default. |

The current design-only and Phase 1/2/3 inert/fake contracts live in `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyRuntimeAdapterDesign.cs`, `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyRuntimeInterfaces.cs`, and `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyRuntimeFakeInMemory.cs`. Phase 3 adds local API diagnostic endpoints at `/lmax-readonly-runtime/*`; they remain disabled/blocked by default and never accept credentials, host fields, order controls, scheduler controls, or external connection settings. Phase 4 preflight adds only boundary documentation, stricter gates, tests, and a local preflight script; it does not add socket/session implementation.

Phase 4A adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionContracts.cs` as a contract/stub boundary only. The future external-session surface is limited to read-only events: market data snapshots, trade capture reports, order status reports, protocol rejects, session warnings, and session errors. It intentionally has no `NewOrderSingle`, cancel, replace, or submit-order surface. The disabled stub opens no socket, performs no FIX logon/logout, uses no credentials, creates no evidence batch, submits nothing to shadow replay, and mutates no trading state.

Phase 4B adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionFakeTransport.cs` as a no-network fake harness only. It accepts in-memory scripts of read-only messages, emits deterministic external-session events, enforces max-event caps, and returns counters. It does not create evidence batches yet; event-to-evidence preview mapping is deferred to Phase 4C.

Phase 4C adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionEvidencePreviewMapper.cs` as a preview-only mapper. It converts fake transport events to sanitized `lmax-fix-lifecycle-evidence-v1` JSON with replay-compatible arrays and metadata, then reports mode/count/validation summary. It does not submit to shadow replay, persist evidence, connect externally, or mutate trading state.

Phase 4D exposes that fake transport preview path through `POST /lmax-readonly-runtime/fake-transport-preview`. The endpoint is manual and local diagnostic only. It is blocked by default, can complete only under explicit fake-preview configuration, and accepts only predefined scenario names: `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, `WarningOnly`, and `ErrorOnly`. It returns fake event counters, evidence counts, validation counts, safety gates, and `submitToShadowReplay=false`. It does not accept raw FIX, paths, host/user/password fields, credentials, external URLs, live connection controls, order controls, or scheduler controls.

Phase 4E adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionSkeleton.cs` as the future real-session class boundary, still hard-disabled. It exists so the next design slice has a named skeleton to guard, but it has no socket activation, no FIX logon/logout, no credential use, no order submission, no shadow replay submit, no scheduler, no gateway registration, and no trading mutation. It always reports disabled/blocked and `ExternalSessionImplementationMode=SkeletonOnly`.

Phase 4F adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGuardedTransport.cs` as the future transport boundary, still hard-disabled. It defines `ConnectReadOnlyAsync`, `ReadEventsAsync`, and `DisconnectAsync` as read-only transport contract methods, but `LmaxReadOnlyGuardedTransportDisabled` always blocks them. It reports `NetworkTransportImplemented=false`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, and `ReadOnlyOnly=true`.

Phase 4G adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionOptions.cs` as the future external-session configuration envelope. It defines disabled options, environment labels, limits, non-secret profile names, and a validator that returns structured issues. It adds the inactive sample `docs/examples/lmax-readonly-external-session-options.sample.json`. There are no credential values, no host/user/password fields, no socket activation, and no live controls.

Phase 4H adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyCredentialProfile.cs` as the future credential-profile boundary. It defines resolver contracts, safe descriptor/status/result records, and `LmaxReadOnlyCredentialProfileResolverDisabled`. `CredentialProfileName` is a label only. The disabled resolver reads no user-secrets, environment variables, appsettings values, vaults, or credential material; it returns no credential values and reports `ResolverMode=Disabled`, `CredentialReadImplemented=false`, `CredentialUseImplemented=false`, `SensitiveMaterialReturned=false`, and `RedactionRequired=true`.

Phase 4I adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyVenueProfile.cs` as the future non-secret venue-profile boundary. `VenueProfileName` is a label only. `DemoLondon` is recognized as an inactive future prototype label, `LmaxDemoReadOnly` remains an inactive legacy demo label, and `Uat`/`Production` remain blocked for the current Phase 4 path. Venue descriptors expose no host, port, user, password, endpoint URL, sender/target comp ID, account ID, session value, or credential value.

Phase 4J adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionRunIntent.cs` as the future manual run-intent envelope. It captures only operator intent, reason, profile labels, requested mode, dry-run flags, bounded limits, and explicit safety expectations. The validator requires a manual reason, blocks `FutureExternalReadOnlyManual` because implementation has not started, keeps `SubmitToShadowReplay=false`, and does not start any session. It introduces no endpoint, host/port/user/account/session/endpoint fields, raw FIX input, credential use, order controls, scheduler, persistence, or trading mutation.

Phase 4K exposes that validator through `POST /lmax-readonly-runtime/external-run-intent/validate`. The endpoint is local/manual and validate-only: it returns structured issues, safety gates, `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`. It does not persist a run, start a session, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4L adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionDryRunReport.cs` and exposes `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`. The report is local/manual and no-network: it aggregates intent validation, options validation, venue profile status, disabled credential resolver status, disabled guarded transport status, blocked external-session skeleton status, safety gates, expected outcome, blocked reason, and next operator action. It does not persist a report, start a session, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4M adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionSignoff.cs` and exposes `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`. The signoff is metadata-only: it captures signer role, reason, dry-run/intent references, and attestations that the future path is read-only, no-order, no-mutation, no-scheduler, no-shadow-submit, no-credential-exposure, Demo-only, and dry-run-reviewed. It cannot authorize execution and does not persist approval, start a session, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4N adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionPreActivationAudit.cs` and exposes `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`. The audit envelope is metadata-only: it captures the intent id, dry-run report id, signoff id, operator labels, stable blockers, and no-attempt flags. It cannot authorize execution and does not persist execution authorization, start a session, read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4O adds `src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExternalSessionReadinessSnapshot.cs` and exposes `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`. The snapshot is metadata-only: it aggregates intent validation, dry-run report, signoff, pre-activation audit, options/config, venue profile, credential profile, guarded transport, skeleton, safety gates, and blockers. It cannot start a session and does not read credentials, connect externally, submit orders, submit to shadow replay, or mutate trading state.

Phase 4P adds `docs/LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md` and `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`. The gate is local-only and verifies the Phase 4A-4O no-socket boundary. It does not add a socket transport, FIX logon, credential read, execution capability, shadow replay submit, scheduler, gateway registration, or trading mutation.

## 6. Configuration and Safety Gates

Future runtime options must default to inert values:

```text
Enabled = false
ImplementationMode = DesignOnly
AllowExternalConnections = false
AllowCredentialUse = false
ReadOnly = true
AllowOrderSubmission = false
PersistRawFixMessages = false
PersistToTradingTables = false
SubmitToShadowReplay = false
MaxEventsPerRun = 100
MaxRuntimeSeconds = 30
RequireOperationalReadinessPass = true
RequireGovernanceApproval = true
RequireLocalOnlyApi = true
DryRun = true
```

The safety gate must block:

- `Enabled=false`
- `ImplementationMode=DesignOnly`
- `AllowExternalConnections=false` for any future external run
- `AllowCredentialUse=false` before any credential path
- `ReadOnly=false`
- `AllowOrderSubmission=true`
- `PersistRawFixMessages=true` until a separate sanitized-retention gate exists
- `PersistToTradingTables=true`
- `DryRun=false` at the current design stage
- Production environment
- missing operational readiness pass
- missing governance approval for activation beyond disabled skeleton
- non-local API submission path where local-only is required
- `MaxEventsPerRun` or `MaxRuntimeSeconds` outside safe bounds
- activation levels beyond the currently approved design/lab boundary

The gate reports every failed gate, not just the first failure. This is important for auditability and operator clarity.

## 7. Activation Levels

The staged activation model is:

| Level | Name | Meaning | Current status |
| --- | --- | --- | --- |
| 0 | Design only | Documentation and inert contracts only. | Allowed. |
| 1 | Disabled skeleton/no-op | Runtime-visible status and blocked run diagnostics only. | Current runtime reader skeleton. |
| 2 | Local manual run, no external connection | Local dry-run evidence validation only. | Future explicit gate. |
| 3 | Lab external read-only capture to file | Explicit Connectivity Lab scripts may connect and write sanitized evidence files. | Current lab-only capability. |
| 4 | Runtime manual read-only connection, no replay submit | Runtime process may connect manually but does not submit replay. | Not implemented. |
| 5 | Runtime manual read-only connection with shadow replay submit | Runtime process may connect and submit local replay evidence. | Not implemented. |
| 6 | Scheduled read-only shadow | Scheduled read-only shadow runs, still non-mutating. | Not implemented. |
| 7 | Future trading adapter design | Separate trading adapter work. | Out of scope. |

This project must remain at Level 1 for runtime and Level 3 for explicit Connectivity Lab scripts. Levels 4 and above require a future design, governance, operational readiness, and implementation gate.

Phase 2 adds a service-level fake/in-memory fixture preview using existing evidence fixtures under `tests/fixtures/lmax-shadow`. It is still not runtime connectivity. It does not submit evidence to shadow replay; `SubmitToShadowReplay=true` is blocked until a later explicit phase.

## 8. FIX Session Design

A future read-only FIX session would need:

- logon/logout with sanitized diagnostics
- sequence-number management
- heartbeat and TestRequest handling
- explicit logout on timeout/failure
- bounded reconnect strategy
- resend/reset behavior
- duplicate message handling by broker ids and replay fingerprints
- `TradeCaptureReportRequest` windows anchored to known event times when applicable
- `OrderStatusRequest` only for known `ClOrdID` when explicitly allowed
- market-data snapshot/subscription when explicitly allowed
- session reject parsing
- max event limits
- max runtime limits
- no `NewOrderSingle`

The read-only adapter must not contain an order-submission method. If a future shared FIX session component exists, order message builders must remain outside the read-only runtime path.

## 9. Evidence Batching

Runtime events would be converted into evidence batches with:

- batch id
- capture window start/end
- inferred evidence mode
- input/unique/duplicate event counts
- market-data context if present
- normalized execution reports
- normalized order-status reports
- normalized trade-capture reports
- normalized protocol rejects
- validation issues
- redaction marker
- environment/capture mode metadata

Evidence must validate against `lmax-fix-lifecycle-evidence-v1` before any shadow replay submit. Generated runtime evidence must contain no credentials, no raw password tags, no authorization headers, and no unsanitized raw FIX logon messages.

Raw FIX retention is disabled by default. Any future raw retention needs a separate retention, redaction, encryption, and access-control design.

## 10. Idempotency / Deduplication

The existing shadow replay model uses deterministic observation fingerprints. The runtime reader must preserve those rules:

- duplicate FIX execution events with the same broker execution id dedupe within a replay run
- duplicate TradeCapture AE events for the same `ExecID` dedupe within a replay run
- duplicate OrderStatus events dedupe by stable status identity
- repeated reader runs create separate replay runs
- repeated runs preserve stable fingerprints for grouping and trend analysis
- duplicate blocking observations within one replay do not create duplicate exception cases
- replay history is preserved

Idempotency must be implemented in the evidence/replay layer, not by mutating the internal order/fill store.

## 11. Error Handling and Protocol Rejects

Protocol reject policy:

- read-only request rejects become warning observations unless the policy classifies the context as blocking
- order-path rejects should never occur in the read-only runtime adapter; if they appear, classify as blocking/critical and stop the run
- unknown reject context is conservative and may become blocking
- session logout/reject messages must be sanitized
- timeout and max-event conditions end the run cleanly
- repeated failures should not trigger automated reconnect loops without a future operations gate

Stop conditions include:

- safety gate failure
- max runtime exceeded
- max events exceeded
- session-level logout
- protocol reject that policy says is blocking
- explicit operator stop in a future manual-run design

## 12. Observability

The runtime reader must expose:

- run status
- current activation level
- safety-gate results
- last successful read time
- last error
- input event count
- unique event count
- duplicate event count
- replay submit status
- observation count by severity and policy code
- audit event ids/correlation id

Logs and DTOs must not expose credentials, password fields, auth headers, or unsanitized FIX logon payloads.

## 13. Security and Credential Handling

Credential policy:

- no credentials in UI
- no credential forms
- no credentials in evidence files
- no credentials in logs
- no credentials in API responses
- user-secrets/environment variables only for lab and future explicitly approved runtime profiles
- separate Demo/UAT/Production credential profiles
- rotation path documented before use
- minimal read-only permissions requested where LMAX supports them

The design-only contracts avoid password/secret/token/API-key fields entirely.

## 14. Operational Controls

Future operational controls must include:

- manual start/stop before any scheduler work
- no scheduler by default
- bounded runtime duration
- bounded event count
- kill-switch interaction defined before activation
- operational readiness gate pass required
- governance approval required before activation
- clear UI wording that the component is read-only and non-mutating
- no order controls
- no live trading controls

Any future scheduled shadow reader requires a separate scheduler quality gate.

## 15. Testing Strategy

Required future test layers:

- unit tests for safety gates and options defaults
- contract tests for evidence batches
- fake FIX session tests for heartbeat, reject, logout, duplicates, and timeouts
- evidence validator tests
- shadow replay service tests
- mutation guard tests for orders, fills, positions, model runs, risk state, reconciliation state, and wallets
- UI tests for disabled/read-only status
- smoke tests for local replay
- lab Demo tests for explicit read-only capture only
- operational rehearsal tests before activation

No CI test should require live LMAX.

## 16. Certification / Production Readiness Path

Before production-style use, a future project would need:

- Demo/UAT soak
- LMAX conformance/certification if applicable
- comparison of runtime shadow evidence against EOD files
- monitoring and alerting
- incident playbook
- rollback plan
- backup/recovery plan
- credential rotation procedure
- operator training
- governance and risk sign-off
- documented support escalation path

This design does not certify or enable production connectivity.

## 17. Safety Checklist Before Implementation

Before any implementation beyond this design:

- Operational readiness gate passes.
- Developer Guide, Operator Manual, Local Runbook, LMAX docs, and Adapter Contracts are current.
- Evidence contract is stable and tested.
- Shadow observation policy is stable and tested.
- No-mutation tests are green.
- FakeLmax-only DI guard tests are green.
- Governance approval model is ready.
- Credential handling is reviewed.
- Runbook is created.
- UI clearly marks disabled/read-only state.
- Kill-switch behavior is defined.
- Scheduler remains disabled unless a separate scheduler gate approves it.
- The Connectivity Lab remains isolated.
- API/Worker still register only `FakeLmaxGateway`.

Until that checklist is satisfied in a future gate, this document remains a design boundary, not a runtime activation plan.

## 18. Phase 5B Prototype Boundary

Phase 5B introduces `LmaxReadOnlySocketPrototypeTransport` and a manual script for the future Demo read-only snapshot path. The prototype is isolated from API/Worker and is not registered as an execution gateway or hosted service.

The prototype remains blocked before any socket/logon attempt because `LmaxReadOnlyCredentialProfileResolverDisabled` is still the only credential resolver. Results are sanitized and always report no session start, no external connection attempt, no credential read, no order submission, no scheduler start, no shadow replay submit, and no trading mutation.

The next design step is credential resolver hardening/redaction. It must remain separate from order submission, gateway registration, scheduler activation, replay submission, and trading mutation.

## 19. Phase 5C Credential Availability Boundary

Phase 5C adds a credential availability resolver, not a credential value resolver. `LmaxReadOnlyCredentialProfileResolverEnvironment` checks only whether required Demo environment labels are present:

- `LMAX_DEMO_FIX_USERNAME`
- `LMAX_DEMO_FIX_PASSWORD`
- `LMAX_DEMO_SENDER_COMP_ID`
- `LMAX_DEMO_TARGET_COMP_ID`

Results contain key labels, present/missing status, missing label count/list, `RedactionStatus=Redacted`, `SensitiveMaterialReturned=false`, and `CredentialValuesReturned=false`. No actual value is returned, logged, written to artifacts, added to evidence, or exposed through API/UI.

## 20. Phase 5D Manual Demo Snapshot Socket Prototype

Phase 5D introduces the first isolated socket-capable prototype path for a manual Demo read-only market-data snapshot. The only supported external action is FIX market-data logon, one EURUSD / SecurityID `4001` snapshot request, and logout. It is available only through `scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1` with explicit `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and `-Reason`.

The implementation remains isolated from API/Worker gateway registration. It does not submit orders, does not implement `NewOrderSingle`, cancel, replace, trade capture, or order status requests, does not start a scheduler, does not submit to shadow replay, does not persist live FIX data into trading tables, and does not mutate orders, fills, positions, model runs, risk, reconciliation, wallet, or reconciliation state.

Credential values are read only from the required local environment labels after all manual gates pass. They are used internally to build FIX logon and are never returned, logged, written to evidence, written to readiness reports, or exposed through API/UI. Result output contains sanitized booleans, counters, best bid/ask/mid when available, and `CredentialValuesReturned=false`.

## 21. Phase 5E Failure and Retry Hardening

Phase 5E makes the manual prototype's non-happy paths explicit and machine-checkable. Missing credentials return `BlockedMissingCredentials` before any connection/logon/snapshot attempt. Unsafe gates classify as `BlockedSafetyGate`, `BlockedInvalidEnvironment`, `BlockedUnsafeVenue`, or `BlockedOrderSubmissionFlag`.

Runtime failure classifications include `FailedSafeConnectionError`, `FailedSafeLogonRejected`, `FailedSafeLogonTimeout`, `FailedSafeSnapshotTimeout`, `FailedSafeLogoutError`, `FailedSafeMaxRuntimeExceeded`, and `FailedSafeMaxEventsExceeded`. All results continue to report whether connection, credential read, logon, snapshot request, logout, order submission, shadow replay submit, scheduler start, and trading mutation were attempted.

Retry metadata is intentionally descriptive. `RetryEnabled=false`, `RetryAllowed=false`, and `MaxAttempts=1` in Phase 5E. Recommendations such as `FixCredentialsThenRetry` and `ReviewFailureThenRetry` are operator guidance only and never cause automatic external retries.

## 22. Phase 5F Sanitized Manual Snapshot Capture

Phase 5F keeps the same manual Demo EURUSD / SecurityID `4001` market-data snapshot boundary and adds sanitized result capture. The manual script prints planned safety flags before any external attempt, verifies credential label availability first, and writes sanitized JSON under `artifacts/lmax-readonly-runtime-demo-snapshot/`.

The artifact is operational diagnostics only, not shadow replay evidence. It must not include credential values, raw sensitive FIX, host/user/password values, account identifiers, or order-capable messages. It includes safe status, attempt booleans, retry metadata with `RetryEnabled=false` and `RetryAllowed=false`, and best bid/ask/mid only if a snapshot is received.

The API and Worker remain `FakeLmaxGateway` only. Phase 5F does not add gateway registration, scheduler activation, shadow replay submit, trading-table persistence, or trading-state mutation.

## 23. Phase 5G Snapshot Timeout Diagnostics

Phase 5G responds to the first manual Demo run outcome: FIX logon succeeded, the EURUSD market-data snapshot request was sent, no snapshot arrived before timeout, and logout succeeded. The design now captures sanitized transport diagnostics around that flow.

Diagnostics include a non-secret request id/hash, request mode, instrument/security id, `SecurityIDSource=8`, market depth, snapshot-only subscription type, requested bid/offer entry types, request/first-response/timeout timing, message-type counters, and a response classification. Supported classifications include snapshot timeout, market-data request reject, business reject, session reject, symbol-encoding reject, unexpected logout, empty book, and completed.

Diagnostic request modes remain manual-only and read-only: `SecurityIdOnly`, `SlashSymbolOnly`, `SymbolOnly`, and `AutoSequence`. They do not add trade capture, order status, order messages, replay submit, persistence, gateway registration, scheduler activation, or trading mutation.

## 24. Phase 5H MarketDataRequest Compatibility

Phase 5H separates market-data request style from symbol encoding. Request mode describes subscription behavior: `SnapshotPlusUpdates`, `SnapshotOnly`, or `AutoSequence`. Symbol encoding describes instrument fields: `SecurityIdOnly`, `SecurityIdAndSymbolWithIdSource`, `SecurityIdAndSymbolNoIdSource`, `SlashSymbol`, `InternalSymbol`, or `Auto`.

The default profile is `SnapshotPlusUpdates` plus `SecurityIdOnly`, because LMAX Demo rejected `SnapshotOnly` / `263=0` with `ValueOutOfRange`, rejected some tag `55` shapes with `UnknownTag`, and rejected `InternalSymbol` with a repeating-group mismatch around tag `146`. Known rejected profiles are represented as metadata with sanitized rejection reasons and are blocked locally unless explicitly allowed for diagnostics.

The runtime still supports only Demo EURUSD / SecurityID `4001`, market-data read-only. No order message, trade capture, order status request, scheduler, gateway registration, shadow replay submit, persistence, or trading-state mutation is introduced.

## 25. Phase 5J Demo MarketData Logon Diagnostics

Phase 5J shifts the immediate diagnostic focus to MarketData FIX logon/session profile alignment. Runtime and Connectivity Lab have both reached TCP/TLS and sent Logon, but logon confirmation is not reliable and observed behavior includes `MsgType=5` Logout before the market-data request can be sent.

The prototype now emits `LmaxReadOnlyFixLogonDiagnostics`: environment/profile labels, credential/comp-id presence booleans, credential/comp-id lengths only, BeginString, EncryptMethod, HeartBtInt, ResetSeqNumFlag, Logon sequence number, first inbound message type, sanitized Logout/Reject text, logon wait duration, TCP/TLS flags, and a runtime-vs-Connectivity-Lab profile comparison. It never emits username, password, sender/target comp values, raw Logon FIX, tag `553`, or tag `554`.

Logon failure classifications now include `FailedSafeLogonLogoutReceived`, `FailedSafeLogonRejectReceived`, `FailedSafeLogonTimeout`, `FailedSafeLogonProfileMismatchSuspected`, `FailedSafeLogonTargetCompIdSuspected`, `FailedSafeLogonSenderCompIdSuspected`, `FailedSafeLogonCredentialsSuspected`, and `FailedSafeLogonUnknown`. No order message, trade capture, order status request, scheduler, gateway registration, shadow replay submit, persistence, or trading-state mutation is introduced.

## 26. Phase 5L Successful Snapshot Closure

Phase 5L closes the first successful manual Demo read-only EURUSD / SecurityID `4001` snapshot milestone. The validated artifact confirms TCP/TLS/FIX logon succeeded, one read-only market-data snapshot was received, best bid/ask/mid were captured, logout succeeded, and all safety flags stayed closed: no order submission, no scheduler, no shadow replay submit, no trading mutation, and `CredentialValuesReturned=false`.

The new `LmaxReadOnlyDemoSnapshotArtifactValidator` and `scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1` validate sanitized runtime snapshot artifacts. The validator checks successful status, snapshot/logon/logout flags, market-data fields, redaction status, ignored artifact location, and forbidden sensitive/order content. The artifact remains operational diagnostics only; it is not shadow replay evidence and must not be submitted to shadow replay in this phase.

Phase 5L introduces no new external behavior, no scheduler, no gateway registration, no replay submit, no persistence to trading tables, and no trading-state mutation. The next eligible slice is either manual evidence-preview mapping with no shadow submit, or repeated manual snapshot stability checks, still manual-only and no scheduler.
## Phase 5M Evidence Preview Boundary

The successful Demo read-only snapshot artifact can now be transformed into a sanitized evidence-preview document, but the boundary remains preview-only. `LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper` emits `MarketDataOnly` `lmax-fix-lifecycle-evidence-v1` JSON with Demo EURUSD / SecurityID `4001` market data and empty read-only replay arrays.

The preview includes `noSensitiveContent=true`, `redactionStatus=Redacted`, and explicit no-submit/no-mutation flags. It contains no credentials, raw FIX logon, sender/target comp values, host/user/password values, account identifiers, order messages, gateway registration, scheduler activation, shadow replay submit, or trading mutation behavior.

## Phase 5N Manual Replay Dry-Run Boundary

The Phase 5M preview can be manually replayed through the existing local shadow replay API to prove that market-data-only evidence produces no observations. This remains outside the runtime adapter. Runtime code does not call shadow replay, does not create observations, and does not persist live FIX data into trading tables.

The expected replay body contains empty execution, order-status, trade-capture, and protocol-reject arrays plus `evidenceMode=MarketDataOnly`. The expected result is `Completed` with zero observations and unchanged mutation guard counts.

## Phase 5O Repeated Manual Stability Boundary

Phase 5O repeats the manual Demo EURUSD snapshot prototype only through an explicit operator-approved script. It adds no scheduler, no polling loop, and no automatic retry semantics. `AttemptCount` is capped to 1..5 and `DelaySeconds` is capped to 1..10; each iteration is treated as a planned manual stability attempt.

Successful attempts are validated as sanitized snapshot artifacts, mapped to `MarketDataOnly` previews, and summarized in an ignored stability artifact. Optional replay of previews remains manual-script-only and off by default. The runtime adapter still does not submit to shadow replay, register a gateway, persist live FIX data, submit orders, or mutate trading state.

## Phase 5P Stability Decision Boundary

Phase 5P is a review boundary over the Phase 5O stability summary. It validates that all requested attempts completed, all snapshots were received, all referenced artifacts/previews remain sanitized and market-data-only, and all no-order/no-mutation/no-runtime-submit flags remain closed.

The PASS decision means only that a separate controlled manual MarketData evidence workflow phase can be considered. It does not authorize scheduler, polling, gateway registration, order submission, runtime shadow replay submit, trading mutation, broader instruments, or production use.

## Phase 5Q Controlled Workflow Manifest Boundary

Phase 5Q introduces a local-only workflow manifest over the manual MarketData evidence path. The manifest captures operator/reason metadata, sanitized snapshot artifact paths, artifact validation results, `MarketDataOnly` preview paths and validation results, optional explicit manual replay results, no-order/no-mutation/no-secret flags, `runtimeShadowReplaySubmit=false`, `schedulerStarted=false`, and a final decision.

The workflow validator fails any manifest containing unsafe artifact flags, non-`MarketDataOnly` previews, non-empty execution/order/trade/protocol arrays, replay observations, changed mutation guards, credential values, scheduler flags, or runtime shadow replay submit. Missing optional replay is a warning, not a failure. This boundary does not add a runtime submit path and does not register a gateway, scheduler, polling loop, order path, or trading-state mutation dependency.

## Phase 5R Manual Replay Manifest Boundary

Phase 5R makes the optional replay branch explicit and testable. When replay is requested, the workflow manifest records `replayRequested=true`, `replayPerformed=true`, `manualReplayCount`, and one replay result per `MarketDataOnly` preview. Each replay result records the evidence preview file, replay run id when available, replay status, observation counts, mutation guard, and `noSensitiveContent=true`.

Replay can only be initiated by the manual workflow script against a localhost API after `-ConfirmLocalManualReplay`. Runtime adapters and prototype code still do not reference the shadow replay service or endpoint. A replay-reviewed workflow passes only when replay count equals preview count, every replay is `Completed`, all observation counts are zero, all mutation guards are unchanged, `runtimeShadowReplaySubmit=false`, and `externalConnectionAttempted=false`.

## Phase 5S Manual Release Manifest Boundary

Phase 5S packages the controlled manual workflow into a fixed release manifest. The manifest records the source Phase 5O stability summary, source workflow manifest, operator acknowledgements, artifact/previews/replay counts, referenced sanitized artifact and preview paths, optional replay results, no-order/no-mutation/no-secret flags, rollback instructions, and the final decision.

The release boundary is still artifact validation only. It does not call the runtime snapshot prototype, does not connect to LMAX, does not register a gateway, does not schedule or poll, and does not submit to shadow replay from runtime. Optional replay remains local API/manual only and is reflected in the release manifest rather than in runtime behavior.

## Phase 5T Runbook Freeze Boundary

Phase 5T freezes the manual workflow as documentation and a local review gate. The boundary is intentionally non-runtime:

- No new hosted service.
- No scheduler or polling loop.
- No runtime shadow replay submit.
- No order, trade-capture, or order-status command path.
- No gateway registration.
- No trading-state mutation dependency.

The gate validates the frozen documentation, Phase 5S manifest/report, acceptable `PASS_WITH_WARNINGS` semantics for skipped optional replay, and continued `FakeLmaxGateway` API/Worker wiring.

## Phase 5V Audit Pack Boundary

Phase 5V is the final local audit-pack boundary for the controlled manual Demo MarketData workflow. The audit pack is sanitized artifact metadata: stability summary reference, snapshot artifact references, `MarketDataOnly` evidence preview references, replay-enabled workflow manifest reference, replay results, gate report references, safety confirmations, and final decision.

It does not call LMAX, does not run replay, does not register a gateway, does not schedule or poll, does not submit runtime shadow replay, and does not mutate trading state.
## Phase 5W Operational Signoff Design Note

The controlled manual Demo MarketData workflow now has a final operational signoff layer. The signoff model is deliberately outside the runtime path: it reads the Phase 5V audit pack, validates counts and safety flags, writes sanitized ignored signoff artifacts, and produces a gate report.

The signoff does not introduce a gateway, scheduler, polling loop, runtime shadow replay submit, order command surface, trading-state dependency, or credential value flow. API and Worker continue to use `FakeLmaxGateway` only.

## Phase 5X Read-Only Status Surface

The operator summary surface is a reporting adapter over ignored signoff/audit artifacts. It produces a sanitized `LmaxReadOnlyMarketDataWorkflowStatusSummary` and serves it through `GET /lmax-readonly-runtime/marketdata-workflow/status`.

The UI panel is intentionally passive. It shows workflow status, counts, safety flags, allowed review activities, and non-authorized capabilities. It does not expose controls for sockets, credentials, scheduler, polling, replay submit, orders, or gateway registration.

## Phase 6D SecurityID Discovery Design Note

The Phase 6D SecurityID discovery manifest is a planning artifact, not a runtime source of truth. It stores placeholder candidate Demo SecurityID values for the Phase 6B instruments so reviewers can see which mappings still need real confirmation.

Design constraints:

- The manifest is not wired into API or Worker dependency injection.
- The manifest is not used by the snapshot prototype.
- All entries remain `IsApprovedForExternalRun=false`.
- The validator records no external connection, no external API call, no scheduler/polling, no runtime shadow replay submit, no order submission, no gateway registration, and no trading mutation.
- Future replacement with real Demo SecurityIDs requires a separate explicit phase and gate.

## Phase 6E SecurityID Evidence Review Design Note

The Phase 6E evidence-review layer is a planning control around SecurityID provenance. It does not provide runtime configuration and is not read by the socket prototype.

An evidence record can only be accepted for planning when it has a reviewed source reference, a non-placeholder proposed SecurityID, reviewer metadata, High or Confirmed confidence, and no sensitive content. Accepted planning still keeps `IsApprovedForExternalRun=false`; an instrument cannot become executable through evidence review alone.

The default manifest intentionally remains `NeedsMoreEvidence` for every Phase 6B candidate. That state is a known warning, not an authorization.

## Phase 6F Confirmation Record Design Note

Phase 6F records are sanitized local artifacts for human-reviewed SecurityID planning. They are not runtime configuration, not API inputs, and not gateway activation inputs.

The record validator rejects placeholders for `AcceptedForPlanning`, rejects low-confidence accepted records, rejects sensitive content, and rejects any language implying orders, external run approval, Production/UAT use, or execution authorization. Even a valid accepted record keeps `IsApprovedForExternalRun=false`.

## Phase 6G Record Entry Workflow Design Note

Phase 6G keeps SecurityID entry as an operator-controlled artifact workflow. The template generator writes JSON under ignored artifacts and is never read by runtime code. The creation script can preview records without writing and refuses to overwrite an explicit output path unless `-Force` is supplied.

Review summaries are intended for humans and gates only. They do not change allowlists, do not apply SecurityIDs to runtime configuration, and do not approve external execution.

## Phase 6H Real Record Design Note

Phase 6H keeps real SecurityID confirmation records in ignored local artifacts under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`. The records remain human-reviewed planning evidence, not runtime configuration and not API inputs.

The review decision is intentionally conservative: `PASS` requires accepted records for all four candidates, `PASS_WITH_KNOWN_WARNINGS` is used for missing or pending safe records, and `FAIL` is used for invalid, conflicting, sensitive, or externally approved records. `AcceptedForPlanning` does not authorize external runs; every record and candidate instrument remains `IsApprovedForExternalRun=false`.

## Phase 6I SecurityList Discovery Design Note

SecurityList discovery is a manual metadata lookup path, not a runtime market-data path. The only permitted outbound FIX application message is `SecurityListRequest`; `MarketDataRequest`, NewOrderSingle, Cancel/Replace, TradeCapture, and OrderStatusRequest are outside the phase.

The artifact stores only sanitized instrument metadata: symbol, slash symbol, SecurityID, SecurityIDSource, security type, currency fields, and source message type. It is used as planning evidence for later confirmation records and never changes allowlists or runtime configuration.

## Phase 6J Diagnostics Design Note

SecurityList diagnostics treat failure as useful evidence about request-profile compatibility, not as a runtime error to auto-retry. Known-rejected profiles carry metadata and are skipped by default. A future `AutoSequence` attempt remains manual, Demo-only, and operator-approved.

## Phase 6L Fallback Design Note

SecurityList fallback analysis treats an unknown reject with no attempt-level reject tag/text as insufficient evidence for further automated profile inference. The fallback decision model records the final status, attempted profiles when present, candidate match count, unmatched candidates, whether all profiles failed with the same class, and whether diagnostics are missing.

The fallback decision is planning-only. It can recommend vendor/support confirmation, official documentation, manual web GUI evidence, continued diagnostics, or blocked-pending-evidence, but it never authorizes external execution or changes candidate allowlist approval. `IsApprovedForExternalRun=false` remains invariant.

## Phase 6M CSV Source Design Note

Uploaded LMAX instrument CSVs are treated as offline source evidence, not as a runtime feed. The extractor reads only sanitized columns: `Instrument Name`, `LMAX ID`, and `LMAX symbol`. It chooses DemoLondon/NewYork 400x IDs for the current profile and records Tokyo 600x IDs only as non-selected observations.

The CSV-backed records are confirmation records, not runtime configuration. They can be accepted for planning with `OfficialLmaxDocument` evidence and `Confirmed` confidence, while every record and instrument remains `IsApprovedForExternalRun=false`.

## Phase 6N Planning Manifest Design Note

The Phase 6N planning manifest is an artifact-level application of accepted confirmation records. It is not a scheduler input, not a gateway registration source, and not an execution allowlist. Each entry carries symbol, slash symbol, planning SecurityID, SecurityIDSource=8, Demo/DemoLondon scope, evidence reference, confirmation record reference, `AcceptedForPlanning`, and `IsApprovedForExternalRun=false`.

Validation fails if any entry is missing, conflicting with accepted records, placeholder-valued, sensitive, Production/UAT/order-authorizing, or externally approved. The manifest is suitable only for later preflight design phases that preserve the same no-run boundary.

## Phase 6O Per-Instrument Safety Gate Design Note

The Phase 6O safety gate manifest is a non-executable review artifact generated from the Phase 6N planning manifest. It records one gate result per additional instrument and checks accepted planning SecurityID, SecurityIDSource=8, Demo/DemoLondon scope, MarketDataOnly intent, no external-run approval, no order capability, no runtime shadow replay submit, no scheduler/polling, no trading mutation, and a future explicit operator prompt requirement.

The aggregate manifest reports counts and global safety flags. `PASS` means planning data is safe and complete only. It never flips `IsApprovedForExternalRun` and never sets `eligibleForManualSnapshotAttempt`; both remain false until a later explicit phase changes the boundary.

## Phase 6P Additional Snapshot Preflight Design Note

The Phase 6P preflight manifest is an operator-intent design artifact, not an execution command. It binds each additional instrument to the accepted DemoLondon planning SecurityID and a future snapshot request profile: `SnapshotPlusUpdates`, `SecurityIdOnly`, SecurityIDSource=8, `MarketDepth=1`, and bounded runtime/wait/event counts.

Validation depends on the Phase 6N planning manifest and Phase 6O safety gate manifest. It fails unknown symbols, mismatched SecurityIDs, placeholders, non-Demo or non-DemoLondon scope, unsafe caps, missing operator reason, sensitive content, Production/UAT/order authorization language, or any executable flag. `canRunExternalSnapshot`, `eligibleForManualSnapshotAttempt`, and `IsApprovedForExternalRun` remain false in Phase 6P.

## Phase 6Q Approval Envelope Design Note

The Phase 6Q approval envelope is a human planning record layered on top of a PASS Phase 6P preflight. It captures operator and reviewer identity labels, reason, selected instrument, source preflight decision, and attestations that the future boundary remains Demo-only, read-only MarketData-only, single-instrument, no scheduler/polling, no runtime shadow replay submit, no orders, and no trading mutation.

The envelope is not runtime configuration. It cannot authorize a snapshot: `canRunExternalSnapshot`, `eligibleForManualSnapshotAttempt`, and `IsApprovedForExternalRun` remain false even when the envelope decision is `AcceptedForPlanning`.

## Phase 6R Dry-Run Report Design Note

The Phase 6R dry-run report is a source-chain consistency artifact for GBPUSD / SecurityID 4002. It references the planning manifest, per-instrument safety gate, preflight manifest, and approval envelope, then records the selected request profile and explicit blocking reason.

The report is intentionally non-executable. It does not connect, request market data, replay evidence, start a scheduler, submit to shadow replay, submit orders, register a gateway, persist live FIX, or mutate trading state.
### Phase 6S Design Boundary

The Phase 6S attempt gate is an artifact validation layer, not an adapter execution layer. It checks that GBPUSD `4002` planning, safety, preflight, approval envelope, and dry-run artifacts agree, while all run eligibility flags remain false.

The runtime adapter design remains read-only guarded and non-live inside API/Worker; `FakeLmaxGateway` remains the only registered gateway.

### Phase 6T Design Boundary

The Phase 6T execution plan is documentation plus artifact validation. It prepares a future manual command template and kill/rollback checklist, but does not create a runnable path in API or Worker. `FakeLmaxGateway` remains the only runtime gateway.

### Phase 6U Design Boundary

The Phase 6U operator signoff is a human-control artifact. It confirms plan review only and does not alter adapter runtime design. API and Worker remain `FakeLmaxGateway` only.

### Phase 6V Design Boundary

The Phase 6V final readiness artifact is an aggregation layer over prior planning artifacts. It does not alter adapter runtime design. API and Worker remain `FakeLmaxGateway` only.

### Phase 6W Design Boundary

The Phase 6W wrapper remains outside API/Worker runtime registration. It invokes the isolated manual prototype only when the operator supplies explicit flags. API and Worker remain `FakeLmaxGateway` only.
## Phase 6X Empty-Book Design Note

`CompletedWithEmptyBook` is a read-only MarketData result classification, not an order or reject state. The validator requires sanitized GBPUSD `4002` metadata, a received snapshot, zero entries, no bid/ask/mid values, zero reject counts, and all scheduler/order/shadow/mutation flags false.

The evidence preview representation keeps `evidenceMode=MarketDataOnly`, `marketData.status=EmptyBook`, `snapshotReceived=true`, `entryCount=0`, null top-of-book fields, and empty execution/order/trade/reject arrays. It does not submit to shadow replay or mutate trading state.

## Phase 6Y Retry Boundary Design

Market-hours retry readiness is a planning artifact only. It binds the Phase 6V final readiness and Phase 6X empty-book review to one future manual Phase 6Z attempt. The design intentionally does not add a timer, scheduler, polling loop, background job, runtime shadow replay submit, order message surface, gateway registration, or trading-state mutation.
## Phase 6Z-A Design Note

The additional-instrument planning pipeline is deliberately outside runtime execution. It is a local artifact design that confirms each DemoLondon additional instrument has complete planning evidence while remaining non-executable.

The design keeps the request profile fixed to SecurityIDSource `8`, `SnapshotPlusUpdates`, `SecurityIdOnly`, and MarketDepth `1` for GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007. The aggregate decision can be `PASS` only when the artifact chain is complete and every executable flag remains false.

This design does not add a live gateway, scheduler, polling loop, runtime shadow replay submit, order flow, trading-table persistence, or trading-state mutation.

## Phase 6Z-C Design Note

The additional-instrument planning status panel is an operator visibility surface, not an execution surface. It reads the aggregate pipeline manifest and displays aggregate/per-instrument state with explicit non-authorization language.

No control in the panel can start external connectivity, run snapshots, replay evidence, schedule work, enter credentials, configure host/port, register a gateway, submit orders, or mutate trading state.

## Phase 6Z-D Design Note

The additional-instrument planning final documentation pack freezes the Phase 6Z-A/6Z-C planning state for audit review. It records GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 with `executableCount=0` and false run flags.

The design remains non-executable. The documentation pack does not change adapter activation, transport, scheduling, shadow replay, order flow, gateway registration, credential handling, persistence, or trading-state behavior. Any future market-hours snapshot remains a separate explicit operator phase for one selected instrument.

## Phase 6Z-E Design Note

The market-hours next-action card is an operator visibility surface for the already prepared GBPUSD retry. It aggregates the final readiness, retry readiness, empty-book review, and planning freeze into a single read-only recommendation.

The card deliberately contains no execution controls. It does not alter adapter activation, transport, scheduling, shadow replay, order flow, gateway registration, credential handling, persistence, or trading-state behavior.
## Phase 7A Next Boundary Design Decision

Phase 7A records the architecture decision for the next safe boundary after the frozen EURUSD workflow and additional-instrument planning pipeline. It does not alter adapter runtime design or registration.

Selected next boundary: Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run.

The design direction remains manual, one instrument at a time, sanitized-artifact based, and API/Worker `FakeLmaxGateway` only. Scheduler/polling, runtime shadow replay submit, order path, production/UAT, real gateway registration, multi-instrument batch execution, and trading mutation remain out of scope.

## Phase 7B Controlled Manual Workflow Design

Phase 7B preserves the same adapter boundary. It adds a local workflow plan for sequencing future manual additional-instrument attempts, but it does not introduce a service, scheduler, gateway registration, or runtime execution path.

The plan is intentionally not a control plane. It is a reporting/planning artifact with `batchExecutionAllowed=false`, one-instrument-at-a-time rules, one attempt per instrument, and all run eligibility flags false.

## Phase 7C Closure Design

Phase 7C preserves the adapter boundary. It adds a post-run artifact closure workflow for GBPUSD market-hours results, not a runtime execution path.

The design classifies sanitized result artifacts, maps safe MarketData-only content to evidence preview, and permits only explicit manual local replay. Runtime code still does not submit to shadow replay, API/Worker remain `FakeLmaxGateway`, and no scheduler, order path, real gateway registration, persistence path, or trading mutation is introduced.

## Phase 7D Decision Design

Phase 7D is a planning decision boundary after GBPUSD closure. It does not alter the runtime adapter design and does not make EURGBP or any other instrument executable.

The design preserves one-instrument-at-a-time sequencing: EURGBP is only a next planning candidate after GBPUSD closes with top-of-book `PASS`; empty-book results require a new GBPUSD retry planning phase; failed-safe or unsafe results block for diagnostics. Runtime power remains unchanged.

## Phase 7E Checklist Design

Phase 7E is a runbook-pack design layer. It documents the future manual GBPUSD command and required closure sequence, but does not add a command surface to the runtime, API, Worker, or UI.

The design keeps execution human-initiated, one attempt only, no retry, no scheduler/polling, no runtime shadow replay submit, no order path, no gateway registration, and no trading mutation. The future command remains outside runtime registration and is marked not to run until market hours.

## Phase 7E2 EURGBP Readiness Design

Phase 7E2 preserves the adapter boundary. It rehydrates EURGBP planning readiness from existing manifests after Phase 7D selects `ProceedToEurgbpPlanning`, but it does not create a runtime execution path.

The design keeps EURGBP non-executable: no real gateway registration, no FIX run, no scheduler/polling, no runtime shadow replay submit, no orders, no persistence, and no trading-state mutation. API/Worker remain `FakeLmaxGateway` only.

## Phase 7G2 EURGBP Final Pre-Run Gate Design

Phase 7G2 preserves the adapter boundary by adding only a local final pre-run consistency artifact for EURGBP. The artifact aggregates the Phase 7D `ProceedToEurgbpPlanning` decision, Phase 7E2 readiness `PASS`, and Phase 7F2 checklist `PASS`.

The gate is not a control surface. It keeps `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, `batchExecutionAllowed=false`, and `oneInstrumentAtATime=true`. API/Worker remain `FakeLmaxGateway`; no scheduler, polling, runtime shadow replay submit, orders, gateway registration, persistence, or trading-state mutation is added.

## Phase 7H Generic Additional Instrument Workflow Design

Phase 7H keeps the adapter boundary unchanged. It adds a generic manual wrapper and local closure tooling for one allowlisted additional instrument at a time, but still delegates any future operator-approved socket attempt to the isolated prototype path.

## Phase 7H2 Final Pre-Run Gate Design

Phase 7H2 keeps the runtime adapter boundary unchanged. It introduces a generic local final pre-run gate model for additional instruments so the Phase 7H wrapper can validate the same explicit manual-control contract for USDJPY and AUDUSD that it validates for EURGBP.

The design intentionally does not make Phase 6Z-A final-readiness artifacts wrapper-compatible. Those artifacts remain planning-only. The final pre-run gate adds the required one-instrument, non-batch, non-executable safety fields without adding scheduler/polling, runtime shadow replay submit, orders, gateway registration, trading mutation, or API/Worker runtime power.

The wrapper validates symbol, SecurityID, SecurityIDSource, Demo/DemoLondon profile, request mode, encoding, depth, final pre-run gate decision, non-executable flags, and the one-instrument-at-a-time rule before delegation. It does not register a gateway, start a service, schedule work, submit to runtime shadow replay, submit orders, persist live FIX, or mutate trading state.
