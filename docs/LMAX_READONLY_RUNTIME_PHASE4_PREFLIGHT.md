# LMAX Read-Only Runtime - Phase 4 Preflight Boundary

## 1. Purpose

Phase 4 is the first future phase that may introduce an external read-only LMAX session prototype.

This preflight does not implement that prototype. It locks the boundary before any socket code, credential use, external FIX logon, or runtime reader implementation exists.

## 2. Current State

- Phase 1, Phase 2, Phase 3, and Phase 3.5 are closed.
- Phase 4A is implemented as an external-session contract/stub only.
- The fake preview endpoint works with local fixtures only.
- Phase 4D adds a fake transport preview endpoint for predefined in-memory scenarios only.
- Phase 4E adds a hard-disabled external read-only session skeleton only.
- Phase 4F adds a guarded transport interface with disabled/no-network implementation only.
- Phase 4G adds a typed configuration envelope and validator only; no credential values.
- Phase 4H adds a credential-profile boundary and disabled resolver only; no secret reads.
- Phase 4I adds a non-secret venue-profile boundary and disabled/static registry only; no endpoint values.
- Phase 4J adds a manual run-intent envelope and validator only; no run starts.
- Phase 4K exposes that run-intent validator through a local manual API preflight endpoint only; no run starts.
- Phase 4L exposes a no-network dry-run report for that intent only; no run starts.
- Phase 4M exposes a manual signoff envelope for that report only; it cannot authorize execution.
- Phase 4N exposes a pre-activation audit envelope for the intent/report/signoff chain only; it cannot authorize execution.
- Phase 4O exposes a readiness snapshot for the whole chain only; it cannot start a session.
- Phase 4P adds the final no-socket release gate only; it verifies the Phase 4A-4O boundary before any future socket-enabled prompt.
- Default API configuration remains `Enabled=false` and `ImplementationMode=DesignOnly`.
- API and Worker remain `FakeLmaxGateway` only for execution.
- No runtime external connectivity exists.
- Connectivity Lab remains the only place that can talk to LMAX today, and only through explicit lab scripts.

## 2.1 Phase 4A Contract Stub Boundary

Phase 4A adds only the shape of a future external read-only session:

- `ILmaxReadOnlyExternalSession`
- `ILmaxReadOnlyExternalSessionFactory`
- `ILmaxReadOnlyExternalSessionSafetyGate`
- `LmaxReadOnlyExternalSessionRequest`
- `LmaxReadOnlyExternalSessionResult`
- `LmaxReadOnlyExternalSessionStatus`
- `LmaxReadOnlyExternalSessionEvent`
- `LmaxReadOnlyExternalSessionReject`
- `LmaxReadOnlyExternalSessionCounters`
- `LmaxReadOnlyExternalSessionDisabled`

Allowed event types are read-only only:

- `MarketDataSnapshot`
- `TradeCaptureReport`
- `OrderStatusReport`
- `ProtocolReject`
- `SessionWarning`
- `SessionError`

Phase 4A does not add a socket implementation, FIX logon/logout, credential use, Connectivity Lab calls, evidence creation, shadow replay submit, hosted service, scheduler, gateway registration, or trading-state mutation. The disabled session always returns `Disabled` or `Blocked` and reports that the external-session implementation has not started.

## 2.2 Phase 4B Fake Transport Harness Boundary

Phase 4B adds a fake in-memory transport harness only:

- `ILmaxReadOnlyExternalSessionTransport`
- `LmaxReadOnlyExternalSessionFakeTransport`
- `LmaxReadOnlyExternalSessionFakeTransportScript`
- `LmaxReadOnlyExternalSessionFakeTransportMessage`
- `LmaxReadOnlyExternalSessionFakeTransportResult`
- `LmaxReadOnlyExternalSessionFake`

The fake transport accepts predefined in-memory read-only messages, emits deterministic `LmaxReadOnlyExternalSessionEvent` values, applies a max-event cap, and produces counters by event type. It can simulate market data snapshots, trade capture reports, order status reports, protocol rejects, session warnings, and session errors.

Phase 4B is not a network implementation. It does not open sockets, perform FIX logon/logout, use credentials, call Connectivity Lab code, create evidence batches, submit to shadow replay, persist anything, register a gateway, start a hosted service, schedule anything, or mutate trading state. Event-to-evidence preview mapping is deferred to Phase 4C.

## 2.3 Phase 4C Evidence Preview Boundary

Phase 4C maps fake transport events into sanitized evidence preview JSON only:

- `LmaxReadOnlyExternalSessionEvidencePreviewMapper`
- `LmaxReadOnlyExternalSessionEvidencePreviewResult`
- `LmaxReadOnlyExternalSessionEvidencePreviewIssue`

The mapper emits `lmax-fix-lifecycle-evidence-v1` preview JSON with `source=RuntimeFakeTransport`, `captureMode=FakeRuntimePreview`, replay-compatible arrays, explicit redaction metadata, ISO trade dates, normalized `Buy`/`Sell` sides, and explicit `tradeUti: null` when absent.

Phase 4C remains preview-only. It does not submit to shadow replay, persist evidence, connect externally, open sockets, use credentials, perform FIX logon/logout, register a gateway, start a hosted service, schedule anything, or mutate trading state.

## 2.4 Phase 4D Fake Transport Preview Endpoint Boundary

Phase 4D exposes the Phase 4B/4C fake transport preview path through a local manual diagnostic endpoint:

- `POST /lmax-readonly-runtime/fake-transport-preview`

The endpoint is disabled/blocked by default. It can complete only when the API is launched with explicit local fake-preview configuration, and even then it uses predefined in-memory scenarios only:

- `EmptyReadOnly`
- `MarketDataOnly`
- `TradeCaptureOnly`
- `OrderStatusOnly`
- `ProtocolRejectOnly`
- `MixedReadOnly`
- `WarningOnly`
- `ErrorOnly`

The request requires a reason and scenario. It rejects unknown scenarios and `SubmitToShadowReplay=true`. It does not accept raw FIX messages, file paths, host fields, user fields, password fields, credentials, external URLs, live connection controls, order controls, or scheduler controls.

The response returns safety gates, fake event counters, evidence mode/count summary, validation issue counts, `source=RuntimeFakeTransport`, `captureMode=FakeRuntimePreview`, and `submitToShadowReplay=false`. Run summaries remain in memory only. There is no database migration, no evidence persistence, no shadow replay submit, no socket, no FIX logon/logout, no credential use, no gateway registration, no hosted service, and no trading-state mutation.

## 2.5 Phase 4E External Session Skeleton Boundary

Phase 4E adds the future real external-session class boundary without activating it:

- `LmaxReadOnlyExternalSessionSkeleton`
- `LmaxReadOnlyExternalSessionSkeletonFactory`
- `LmaxReadOnlyExternalSessionSkeletonSafetyReport`

The skeleton implements the existing external-session contracts and always returns `Disabled` or `Blocked`. Its status/report explicitly states:

- `ExternalSessionImplementationMode=SkeletonOnly`
- `SocketActivation=false`
- `FixLogonImplemented=false`
- `CredentialUseImplemented=false`
- `OrderSubmissionImplemented=false`
- `ShadowReplaySubmitImplemented=false`
- `TradingMutationImplemented=false`

The skeleton does not instantiate `TcpClient`, `Socket`, `SslStream`, QuickFIX, or any network client. It does not reference Connectivity Lab implementation classes, read credentials, perform FIX logon/logout, create evidence, submit to shadow replay, register a gateway, add a hosted service, schedule work, or mutate trading state.

## 2.6 Phase 4F Guarded Transport Interface Boundary

Phase 4F adds the transport abstraction a future external read-only session would use, but still no real transport:

- `ILmaxReadOnlyGuardedTransport`
- `ILmaxReadOnlyGuardedTransportFactory`
- `LmaxReadOnlyGuardedTransportDisabled`
- `LmaxReadOnlyGuardedTransportRequest`
- `LmaxReadOnlyGuardedTransportResult`
- `LmaxReadOnlyGuardedTransportStatus`
- `LmaxReadOnlyGuardedTransportCapabilities`
- `LmaxReadOnlyGuardedTransportSafetyReport`

The interface includes future read-only transport responsibilities: `ConnectReadOnlyAsync`, `DisconnectAsync`, `ReadEventsAsync`, `GetStatusAsync`, and `EvaluateSafetyAsync`. The disabled implementation always blocks every operation and returns no events.

Phase 4F does not instantiate `TcpClient`, `Socket`, `SslStream`, QuickFIX, `ClientWebSocket`, `HttpClient`, or any network/FIX engine. It does not implement FIX logon/logout, read credentials, call Connectivity Lab code, create evidence, submit to shadow replay, register a gateway, add a hosted service, schedule work, or mutate trading state.

## 2.7 Phase 4G Configuration Envelope Boundary

Phase 4G adds a typed configuration envelope for a possible future external read-only session, but still no activation:

- `LmaxReadOnlyExternalSessionOptions`
- `LmaxReadOnlyExternalSessionEnvironmentOptions`
- `LmaxReadOnlyExternalSessionLimitsOptions`
- `LmaxReadOnlyExternalSessionCredentialProfileOptions`
- `LmaxReadOnlyExternalSessionOptionsValidator`
- `docs/examples/lmax-readonly-external-session-options.sample.json`

The envelope uses profile labels only, such as `EnvironmentName`, `VenueProfileName`, and `CredentialProfileName`. It contains no credential values and no host/user/password fields. The inactive sample keeps `Enabled=false`, `ImplementationMode=DesignOnly`, `AllowExternalConnections=false`, `AllowCredentialUse=false`, `AllowOrderSubmission=false`, `SchedulerEnabled=false`, `SubmitToShadowReplay=false`, and `DryRun=true`.

The validator returns structured issues with severity, code, path, and message. Defaults validate as safe-disabled. Future-looking enabled or external-connection configurations remain blocked in Phase 4G because no implementation or socket exists.

## 2.8 Phase 4H Credential Profile Boundary

Phase 4H adds the future credential-profile resolver boundary without resolving any credential values:

- `ILmaxReadOnlyCredentialProfileResolver`
- `LmaxReadOnlyCredentialProfileResolverDisabled`
- `LmaxReadOnlyCredentialProfileRequest`
- `LmaxReadOnlyCredentialProfileResult`
- `LmaxReadOnlyCredentialProfileStatus`
- `LmaxReadOnlyCredentialProfileDescriptor`
- `LmaxReadOnlyCredentialProfileSafetyReport`

`CredentialProfileName` is a non-secret label only, like `EnvironmentName` and `VenueProfileName`. The disabled resolver always returns `Disabled` or `Blocked`, reports `ResolverMode=Disabled`, `CredentialReadImplemented=false`, `CredentialUseImplemented=false`, `SensitiveMaterialReturned=false`, and `RedactionRequired=true`.

Phase 4H does not read user-secrets, environment variables, application settings, vaults, or any credential store. It does not log, store, return, or use credential values. It does not add socket activation, network transport, FIX logon/logout, credential UI, API credential fields, order submission, shadow replay submit, scheduler activation, gateway registration, or trading-state mutation.

## 2.9 Phase 4I Venue Profile Boundary

Phase 4I adds non-secret venue-profile labels for a possible future external read-only session:

- `LmaxReadOnlyVenueProfileName`
- `LmaxReadOnlyVenueProfileDescriptor`
- `ILmaxReadOnlyVenueProfileRegistry`
- `LmaxReadOnlyVenueProfileRegistryDisabled`
- `LmaxReadOnlyVenueProfileValidationResult`

Venue profiles are labels only. `DemoLondon` is recognized as an inactive future prototype label. `LmaxDemoReadOnly` is retained as an inactive legacy demo label for compatibility with earlier safe examples. `Uat` and `Production` labels are blocked for the current Phase 4 path. Descriptors expose only environment/profile names, inactive flags, supported purposes, safety status, redaction status, and operator-facing descriptions.

Phase 4I does not add host, port, user, password, endpoint URL, sender/target comp IDs, account IDs, session values, credential values, socket activation, network transport, FIX logon/logout, live controls, order submission, shadow replay submit, scheduler activation, gateway registration, or trading-state mutation.

## 2.10 Phase 4J Run Intent Envelope

Phase 4J adds the safe request boundary for a possible future external read-only manual run:

- `LmaxReadOnlyExternalSessionRunIntent`
- `LmaxReadOnlyExternalSessionRunIntentMode`
- `LmaxReadOnlyExternalSessionRunIntentValidator`
- `LmaxReadOnlyExternalSessionRunIntentSummary`
- `LmaxReadOnlyExternalSessionRunIntentValidationResult`

The intent envelope captures operator intent only: `intentId`, `reason`, `requestedByOperatorId`, `requestedAtUtc`, `EnvironmentName`, `VenueProfileName`, `CredentialProfileName`, run mode, dry-run flag, bounded limits, and explicit safety expectations. A manual reason is required. `FutureExternalReadOnlyManual` remains blocked with `Phase4ExternalRunImplementationNotStarted`; `ValidateOnly` and `PreviewOnly` validate the envelope only and do not start any external session.

Phase 4J does not add an API endpoint, socket activation, network transport, FIX logon/logout, credential resolution, raw FIX input, host/port/user/account/session/endpoint fields, order submission, shadow replay submit, scheduler activation, gateway registration, persistence, or trading-state mutation.

## 2.11 Phase 4K Manual Preflight Endpoint

Phase 4K exposes the run-intent validator through:

- `POST /lmax-readonly-runtime/external-run-intent/validate`

The endpoint is validate-only. It accepts only labels and safety flags from the Phase 4J run-intent envelope: reason, optional operator id, environment label, venue profile label, credential profile label, run mode, dry-run flag, capped limits, and explicit false-by-default safety booleans. It returns structured validation issues, safety gates, `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`.

`FutureExternalReadOnlyManual` remains blocked with `Phase4ExternalRunImplementationNotStarted`. `ValidateOnly` and `PreviewOnly` validate only and do not start a session. Phase 4K does not add socket activation, network transport, FIX logon/logout, credential resolution, raw FIX input, host/port/user/account/session/endpoint fields, order submission, shadow replay submit, scheduler activation, gateway registration, persistence, or trading-state mutation.

## 2.12 Phase 4L No-Network Dry-Run Report

Phase 4L exposes a validate-only/no-network dry-run report through:

- `POST /lmax-readonly-runtime/external-run-intent/dry-run-report`

The endpoint accepts the same safe Phase 4J run-intent request shape as the Phase 4K validate endpoint. It aggregates intent validation, options validation, venue profile status, credential resolver disabled status, guarded transport disabled/no-network status, external-session skeleton disabled/not-implemented status, safety gates, expected outcome, blocked reason, and next operator action.

The report always returns `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`. It includes stable markers such as `Phase4ExternalRunImplementationNotStarted`, `CredentialResolverDisabled`, `GuardedTransportImplementationDisabled`, and `ExternalSessionImplementationStarted`. Phase 4L does not add socket activation, network transport, FIX logon/logout, credential resolution, raw FIX input, host/port/user/account/session/endpoint fields, order submission, shadow replay submit, scheduler activation, gateway registration, persistence, or trading-state mutation.

## 2.13 Phase 4M Manual Signoff Envelope

Phase 4M exposes a validate-only manual signoff envelope through:

- `POST /lmax-readonly-runtime/external-run-intent/signoff/validate`

The endpoint accepts signoff metadata only: dry-run report id, intent id, requester/signer operator ids, signoff role, reason, decision, and explicit attestations that the future path remains read-only, submits no orders, mutates no trading state, enables no scheduler, submits no shadow replay, exposes no credentials, stays Demo-only, and has reviewed the dry-run report.

Even a complete signoff returns `canAuthorizeExecution=false`, `executionStillBlocked=true`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`. It preserves blockers including `Phase4ExternalRunImplementationNotStarted`, `CredentialResolverDisabled`, and `GuardedTransportImplementationDisabled`. Phase 4M does not persist approval, start a session, connect externally, read credentials, submit orders, submit to shadow replay, register a gateway, enable a scheduler, or mutate trading state.

## 2.14 Phase 4N Pre-Activation Audit Envelope

Phase 4N exposes a validate-only pre-activation audit envelope through:

- `POST /lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate`

The envelope captures the intent id, dry-run report id, signoff id, requested/reviewed/signed operator labels, stable blockers, and no-attempt flags. It is audit metadata only. Even a complete envelope returns `canAuthorizeExecution=false`, `executionStillBlocked=true`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, and `tradingMutationAttempted=false`. It preserves `Phase4ExternalRunImplementationNotStarted`, `CredentialResolverDisabled`, and `GuardedTransportImplementationDisabled`. Phase 4N does not persist execution authorization, start a session, connect externally, read credentials, submit orders, submit to shadow replay, register a gateway, enable a scheduler, or mutate trading state.

## 2.15 Phase 4O Readiness Snapshot

Phase 4O exposes a validate-only readiness snapshot through:

- `POST /lmax-readonly-runtime/external-run-intent/readiness-snapshot`

The snapshot aggregates the intent validation, dry-run report, signoff envelope, pre-activation audit envelope, options/config status, venue profile status, credential profile status, guarded transport status, external session skeleton status, safety gates, and stable blockers. It is metadata only. It always returns `canStartSession=false`, `sessionStarted=false`, `externalConnectionAttempted=false`, `credentialReadAttempted=false`, `shadowReplaySubmitAttempted=false`, `tradingMutationAttempted=false`, and `noSensitiveContent=true`. Phase 4O does not persist execution authorization, start a session, connect externally, read credentials, submit orders, submit to shadow replay, register a gateway, enable a scheduler, or mutate trading state.

## 2.16 Phase 4P Final No-Socket Release Gate

Phase 4P adds the final local-only no-socket release gate:

- `docs/LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md`
- `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`

The gate validates evidence fixtures, runs the Phase 4 preflight, scans Phase 4 runtime files for forbidden socket/network/order surfaces, confirms API/Worker remain `FakeLmaxGateway` only, checks generated evidence status, and runs API-dependent smokes only when the local API is available. It writes an ignored report under `artifacts/readiness/`.

Passing Phase 4P does not mean live connectivity exists. It means the no-socket safety envelope is complete enough to consider a separately prompted future socket prototype phase.

Phase 5A adds the planning-only first transport preflight in `docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md` and `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1`. It defines kill/rollback and abort controls before any future socket-boundary work, but it does not modify runtime behavior or add connectivity.

## 3. Phase 4 Allowed Scope, Future Only

Phase 4 may later introduce:

- a prototype read-only session type behind hard compile/config gates
- Demo-only read-only mode
- manual/local invocation only
- bounded max runtime and event limits
- sanitized evidence batch or preview output
- evidence contract validation before any downstream use

Phase 4 must still keep:

- no order submission
- no trading-state mutation
- no scheduler
- no default activation
- no production activation
- `SubmitToShadowReplay=false` unless a later explicit gate changes that

## 4. Phase 4 Forbidden Scope

Phase 4 must not introduce:

- `NewOrderSingle`
- order submission
- live trading
- order, fill, position, wallet, risk, model-run, or reconciliation mutation
- EOD reconciliation mutation
- credential UI
- host, user, or password fields in API request DTOs
- scheduler or hosted-service auto-run
- production activation
- Worker auto-start
- real gateway replacement
- shadow replay submit unless a later explicit gate allows it

Phase 4A contracts must not expose `NewOrderSingle`, `OrderCancelRequest`, `OrderCancelReplaceRequest`, submit-order, cancel-order, or replace-order methods/events.

Phase 4B fake transport scripts must follow the same restriction and may contain only read-only event types.

Phase 4C evidence preview mapping must not introduce order-command payloads or shadow replay submission.

Phase 4D endpoint integration must not introduce raw FIX request payloads, arbitrary file paths, credential/host/user/password DTO fields, order-command DTO fields, shadow replay submit, persistence, socket use, scheduler controls, or trading mutation.

Phase 4E skeleton integration must remain non-functional. It must not instantiate network types, implement FIX logon/logout, read credential values, create evidence, call the fake transport path, submit to shadow replay, register as a runtime gateway, add a hosted service, or mutate trading state.

Phase 4F guarded transport integration must remain a contract/stub only. It must not add a real transport implementation, network/FIX engine, credential value DTO, order-command method, shadow replay submit path, hosted service, gateway registration, or trading mutation.

Phase 4G configuration integration must not add credential values, host/user/password fields, live controls, socket activation, real transport, FIX logon/logout, scheduler activation, order commands, shadow replay submit, gateway registration, or trading mutation.

Phase 4H credential-profile integration must remain labels-only and disabled. It must not read user-secrets, environment variables, application settings, vaults, or credential values; it must not expose credential values in DTOs, logs, audit metadata, readiness reports, or API responses.

Phase 4I venue-profile integration must remain labels-only and inactive. It must not expose endpoint URLs, hostnames, ports, usernames, account IDs, sender/target comp IDs, session values, or credential values in runtime options, API DTOs, logs, audit metadata, readiness reports, or API responses.

Phase 4J run-intent integration must remain validate-only. It must not add live run controls, raw FIX payloads, endpoint values, credential values, order-command fields, persistence, shadow replay submit, or any external-session start path.

Phase 4K endpoint integration must remain validate-only. It may return structured blocking issues and operator guidance, but it must always report no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

Phase 4L dry-run report integration must remain validate-only/no-network. It may aggregate existing validation and disabled-boundary statuses into a report, but it must always report no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

Phase 4M signoff integration must remain validate-only/metadata-only. It may represent human signoff attestations and maker/checker validation, but it must always report no execution authorization, no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

Phase 4N pre-activation audit integration must remain validate-only/metadata-only. It may collect the intent, dry-run report, signoff metadata, safety gates, and stable blockers into an audit envelope, but it must always report no execution authorization, no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

Phase 4O readiness snapshot integration must remain validate-only/metadata-only. It may collect all Phase 4 intent/report/signoff/audit/config/profile/transport/skeleton status into one operator/developer snapshot, but it must always report no session start, no external connection attempt, no credential read attempt, no shadow replay submit attempt, and no trading mutation attempt.

## 5. Hard Gates Required Before Any External Connection

Before any future external read-only connection attempt, all of these must be true:

- operational readiness gate is `PASS`, or `PASS WITH KNOWN WARNINGS` only for documented accepted warnings
- default `appsettings.json` remains disabled/design-only
- explicit Phase 4 preview config is required
- `AllowExternalConnections=true` is allowed only in a dedicated local/manual Phase 4 script
- `AllowCredentialUse=true` is allowed only through user-secrets or environment variables, never through UI/API payloads
- the Phase 4H resolver must be replaced by a separately approved resolver before any future credential use; until then credential use is blocked
- `AllowOrderSubmission=false`
- `PersistToTradingTables=false`
- `SchedulerEnabled=false`
- `SubmitToShadowReplay=false` for the initial Phase 4 prototype
- `EnvironmentName=Demo`
- small `MaxRuntimeSeconds`
- small `MaxEventsPerRun`
- manual reason is required
- operator warning is printed

## 6. Configuration Names

Future options and safe defaults:

| Option | Safe default | Phase 4 preflight rule |
| --- | --- | --- |
| `Enabled` | `false` | Must be explicitly true only in a local/manual Phase 4 script. |
| `ImplementationMode` | `DesignOnly` | Phase 4 would require a future non-default prototype mode. |
| `ActivationLevel` | `Level1DisabledSkeleton` / local fixture levels only | Phase 4 requires `Level4RuntimeManualReadOnlyConnectionNoReplaySubmit`, but remains blocked in preflight. |
| `AllowExternalConnections` | `false` | May become true only in a dedicated local/manual Phase 4 script. |
| `AllowCredentialUse` | `false` | May become true only with user-secrets/env vars and no UI/API credential fields. |
| `AllowOrderSubmission` | `false` | Must always remain false. |
| `PersistToTradingTables` | `false` | Must always remain false. |
| `PersistRawFixMessages` | `false` | Must remain false unless a separate sanitized-retention gate exists. |
| `SchedulerEnabled` | `false` | Must always remain false in Phase 4. |
| `SubmitToShadowReplay` | `false` | Must remain false for the initial Phase 4 prototype. |
| `DryRun` | `true` | Must remain true for preflight/manual preview. |
| `EnvironmentName` | `Local` | Phase 4 prototype must be Demo only. |
| `MaxRuntimeSeconds` | small | Must remain bounded. |
| `MaxEventsPerRun` | small | Must remain bounded. |

## 7. Credential Boundary

Credentials must be handled only through future reviewed user-secrets or environment variables.

Forbidden:

- credentials in UI
- credentials in API request DTOs
- credentials in evidence
- credentials in logs
- credentials in readiness reports
- credentials in audit metadata
- raw FIX Logon messages containing password tags

## 8. API Boundary

If Phase 4 later extends an endpoint, it must:

- require a reason
- return safety gates
- state read-only, Demo, and manual mode clearly
- expose no secrets
- accept no host, username, password, token, or API key fields
- never submit orders
- never run unless all gates pass

## 9. DI / Worker Boundary

- No real execution gateway registration.
- `FakeLmaxGateway` remains the execution gateway.
- No hosted service.
- No Worker auto-run.
- The external read-only prototype must not replace or wrap the broker execution gateway.

## 10. Evidence Boundary

- Captured events must become sanitized evidence batches.
- Evidence validator must run.
- No trading-table mutation.
- No raw credential/session logon data.
- Generated evidence must not be committed.
- Submit-to-shadow remains deferred unless a future gate explicitly allows it.

## 11. Tests Required Before Phase 4 Code

Before any external session implementation, tests must prove:

- defaults disabled
- Phase 4 config blocked unless all required gates are set
- `AllowOrderSubmission=true` blocks
- `PersistToTradingTables=true` blocks
- `SchedulerEnabled=true` blocks
- `EnvironmentName != Demo` blocks
- `SubmitToShadowReplay=true` blocks for the initial prototype
- missing reason blocks
- credential-shaped DTO fields are forbidden
- no real gateway registration
- no hosted service
- mutation guard remains unchanged
- local API URL only for manual scripts
- no Connectivity Lab code path coupling

Phase 4A also requires tests proving:

- disabled external session always blocks
- external-session safety gate fails closed
- `ExternalSessionImplementationAvailable=false` keeps the session blocked
- no external-session DTO contains credential-shaped fields
- no order-submission methods or event types exist
- no socket/client/session implementation exists beyond contracts and disabled stubs

Phase 4B also requires tests proving:

- fake transport emits deterministic read-only events
- fake transport honors `MaxEventsPerRun`
- fake session returns counters by event type
- fake session remains no-network/no-credential/no-shadow-submit
- unsafe fake options are blocked
- fake transport DTOs contain no credential-shaped fields
- fake transport exposes no order-submission method/event names

Phase 4C also requires tests proving:

- empty, market-data-only, tradecapture-only, orderstatus-only, protocolreject-only, and mixed fake scripts map to valid evidence modes
- single-item arrays remain arrays
- trade dates are normalized to `yyyy-MM-dd`
- `tradeUti` is explicitly `null` when absent
- raw side values are normalized to `Buy`/`Sell`
- session warnings/errors become preview warnings only
- mapped JSON contains no sensitive content or order-command surface
- existing fixture validator accepts mapped preview JSON

Phase 4D also requires tests proving:

- default configuration blocks the fake transport preview endpoint
- explicit local fake-preview configuration can run predefined scenarios
- missing reason, unknown scenario, and `SubmitToShadowReplay=true` are rejected
- response counts and evidence modes match scenarios
- run summaries remain in memory only
- no raw FIX input, credential-shaped DTO fields, external URL fields, or order-command surface exists
- no shadow replay run is created
- no trading-state mutation occurs

Phase 4E also requires tests proving:

- skeleton status/run always return disabled or blocked
- socket activation, FIX logon, credential use, order submission, shadow replay submit, scheduler, runtime gateway registration, and trading mutation are all reported false/not implemented
- future-looking external config still remains blocked
- no `TcpClient`, `Socket`, `SslStream`, QuickFIX, Connectivity Lab implementation reference, credential-shaped DTO field, or order-command surface exists
- API/Worker remain `FakeLmaxGateway` only and no hosted service is registered

Phase 4F also requires tests proving:

- disabled guarded transport always blocks `ConnectReadOnlyAsync`
- disabled guarded transport returns no events from `ReadEventsAsync`
- status/capabilities report `NetworkTransportImplemented=false`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, and `ReadOnlyOnly=true`
- no order-submission method names exist on the guarded transport contract
- no credential-shaped DTO fields exist
- no `TcpClient`, `Socket`, `SslStream`, QuickFIX, `ClientWebSocket`, `HttpClient`, or Connectivity Lab implementation reference exists
- no hosted service or real gateway is registered

Phase 4G also requires tests proving:

- default config validates as safe-disabled
- future-looking enabled config remains blocked
- non-Demo environment, order submission, trading-table persistence, scheduler, shadow replay submit, raw FIX persistence, unsafe limits, and missing run reason are rejected
- no option/DTO exposes secret-shaped property names
- sample config remains inactive and contains no sensitive placeholders
- default appsettings remain disabled/design-only

Phase 4H also requires tests proving:

- disabled credential resolver always returns disabled or blocked
- `CredentialProfileName` is preserved only as a label
- no credential values are read from environment variables, user-secrets, appsettings, or vaults
- no credential values are used, stored, logged, or returned
- `AllowCredentialUse=true` and future activation remain blocked because the resolver is disabled
- no resolver DTO exposes password, secret, token, API key, private key, or credential-value fields
- no network, Connectivity Lab, order-submission, hosted-service, gateway-registration, shadow replay submit, or trading mutation path is introduced

Phase 4I also requires tests proving:

- venue registry returns only inactive non-secret descriptors
- `DemoLondon` is recognized as a future prototype label but remains inactive/disabled
- `Uat`, `Production`, unknown labels, and environment/profile mismatches are blocked
- venue profiles never allow external connections or credential use
- options validation blocks unknown, UAT, Production, and mismatched venue profiles
- descriptor/option DTOs contain no host, port, user, account, sender/target comp, endpoint, password, secret, token, API key, private key, or credential-value fields
- no network, Connectivity Lab, order-submission, hosted-service, gateway-registration, shadow replay submit, or trading mutation path is introduced

Phase 4J also requires tests proving:

- valid-looking Demo/DemoLondon future manual intents remain blocked because Phase 4J is intent-only
- `ValidateOnly` and `PreviewOnly` validate only and do not start a session
- missing reason, missing operator id, unknown venue, environment mismatch, UAT, Production, unsafe flags, and unsafe limits are blocked
- `AllowExternalConnections`, `AllowCredentialUse`, `AllowOrderSubmission`, `SchedulerEnabled`, `PersistToTradingTables`, and `SubmitToShadowReplay` must remain false
- intent DTOs contain no host, port, username, password, account, sender/target comp, endpoint, raw FIX, or order-command fields
- no network, Connectivity Lab, hosted-service, gateway-registration, shadow replay submit, persistence, or trading mutation path is introduced

Phase 4K also requires tests proving:

- `POST /lmax-readonly-runtime/external-run-intent/validate` requires a reason
- valid-looking Demo/DemoLondon `FutureExternalReadOnlyManual` requests return blocked with `canStartSession=false`
- `ValidateOnly` and `PreviewOnly` validate only and never start a session
- unsafe flags, UAT/Production/unknown profiles, and unsafe limits return structured validation issues
- responses expose no host, port, user, password, account, session, endpoint, raw FIX, credential value, or order-command fields
- no shadow replay run is created and mutation guards remain unchanged

## 12. Phase 4 Entry Checklist

- Phase 3.5 is closed.
- Operational readiness gate passes.
- Documentation is current.
- Backend and frontend tests are green.
- Fake fixture preview smoke is green.
- Fake transport preview smoke is green when the API is explicitly launched in local fake-preview mode.
- Safety gate tests are green.
- Operator understands this is read-only, Demo, and manual.
- A separate explicit approval/prompt authorizes Phase 4 implementation.

## 13. Phase 4 Exit Criteria, Future

- Prototype remains disabled by default.
- No scheduler exists.
- No order submission exists.
- One manual Demo read-only run can be attempted only via explicit script.
- Sanitized evidence preview is produced.
- No mutation occurs.
- Kill/disable path is documented.
- Rollback is tested.

## 14. Phase 5J Status Note

Phase 5J is outside the no-socket Phase 4 boundary, but preserves the Phase 4 safety commitments. It adds sanitized Demo MarketData logon/session diagnostics to the isolated manual prototype only. API/Worker remain `FakeLmaxGateway` only, no order submission exists, no scheduler exists, no shadow replay submit exists, and no trading-state mutation is introduced.
