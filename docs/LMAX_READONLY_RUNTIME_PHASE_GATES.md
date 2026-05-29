# LMAX Read-Only Runtime Adapter - Phase Gates

This is the quick-review gate table for the future LMAX read-only runtime adapter. It is a checklist, not an implementation. The current approved runtime state remains `FakeLmaxGateway` only.

| Phase | Purpose | Allowed | Forbidden | Required tests | Required smokes | Exit status |
| --- | --- | --- | --- | --- | --- | --- |
| 0 - Readiness Baseline | Confirm current platform is ready before implementation. | Validation and reports only. | Code changes, runtime LMAX, credentials, scheduler, order submission. | Existing backend/frontend/evidence tests. | Operational readiness gate, shadow/evidence smokes. | Baseline recorded; known warnings documented. |
| 1 - Inert Runtime Interface Layer | Add runtime-facing interfaces/contracts only. | Interfaces, DTOs, options, safety evaluator, disabled adapter, disabled evidence sink, no-op run store, docs, tests. | Sockets, credentials, FIX logon, active DI, order submission, scheduler. | Defaults disabled, run blocked, unsafe levels blocked, no credential fields, no evidence submission, no persistence, FakeLmax-only guards. | No dedicated smoke in Phase 1; readiness gate/evidence validation only. | Implemented; contracts compile; no executable connectivity. |
| 2 - Fake/InMemory Runtime Adapter | Consume fixture evidence through fake runtime path. | Fixture reader, fake adapter, in-memory run status, fixture preview counts. | External connection, credentials, real FIX, order submission, trading-table writes, scheduler. | Fake run validates fixture evidence, missing/invalid fixtures fail, replay submit remains blocked, no credential fields, FakeLmax-only guards. | No dedicated smoke in Phase 2; service-level tests only because no endpoint exists. | Implemented; observable fixture preview; no sockets/credentials/replay submit. |
| 3 - Manual Runtime Endpoint, No External Connection | Add local endpoint for fake-only run. | `GET /lmax-readonly-runtime/status`, `POST /lmax-readonly-runtime/run`, in-memory run summaries, fake fixture selector. | Host/password/user DTOs, external calls, scheduler, order submission, shadow replay submit. | Blocked default, reason required, fixture traversal rejected, `SubmitToShadowReplay=true` blocked, mutation guard, FakeLmax-only guards. | `smoke-lmax-readonly-runtime-fake-local.ps1`. | Implemented; default blocked; fake-only preview clearly labeled. |
| 3.5 - Explicit Fake-Enabled Endpoint Test Config | Prove endpoint can execute fixture preview only under explicit safe test config. | Test-only fake config, all fixture-mode endpoint previews, per-source event counts, in-memory run summaries. | Default config changes, external calls, credentials, scheduler, order submission, shadow replay submit. | Default disabled, fake-enabled test preview completes, all fixture counts match, `SubmitToShadowReplay=true` blocked, mutation guard. | Optional `smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled` when API is launched with fake test config. | Implemented in integration tests; defaults remain disabled/design-only. |
| 4 preflight - External Read-Only Boundary Lock | Lock future Phase 4 boundary before socket code. | Boundary document, stricter safety gates, preflight script, docs/tests. | Sockets, credentials, external FIX session, shadow replay submit, scheduler, order submission, trading mutation. | Phase 4 activation remains blocked, Demo/reason/gate checks enforced, implementation-not-started gate present. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented; Phase 4 implementation not started. |
| 4A - External Session Contract Stub, No Socket | Define the future external read-only session surface while keeping it disabled. | Interfaces, DTOs, read-only event enum, disabled external session stub, external-session safety gate, docs/tests/preflight checks. | Sockets, FIX logon/logout, credentials, Connectivity Lab calls, order-command methods/events, evidence creation, shadow replay submit, scheduler, trading mutation. | Disabled session always blocks, implementation-not-started gates fail, no credential-shaped DTO fields, no order-submission surface, FakeLmax-only guards. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as contract/stub only; no external implementation started. |
| 4B - External Session Fake Transport Harness, No Network | Exercise external-session contracts with in-memory scripted read-only events. | Fake transport interface, fake script/messages/result, fake session, deterministic read-only event emission, counters, max-event cap. | Sockets, FIX logon/logout, credentials, Connectivity Lab calls, order-command methods/events, evidence creation, shadow replay submit, scheduler, trading mutation. | Deterministic emission, cap enforcement, warning/error/reject simulation, fake session counters, unsafe fake options blocked, no credential/order surface, no Connectivity Lab reference. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as no-network fake harness; evidence mapping deferred. |
| 4C - Fake Transport to Evidence Batch Preview, No Shadow Submit | Convert fake read-only events into sanitized evidence preview JSON. | Preview mapper, evidence preview result/issues, validator-compatible JSON, mode/count summaries. | Shadow replay submit, persistence, sockets, credentials, FIX logon/logout, order-command payloads, scheduler, trading mutation. | All supported preview modes validate, arrays preserved, trade date/side/tradeUti normalized, warnings/errors diagnostic-only, no sensitive/order content. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as preview-only; no shadow submit. |
| 4D - Fake Transport Preview Endpoint Integration, No Shadow Submit | Expose fake transport evidence preview through a local manual diagnostic endpoint. | `POST /lmax-readonly-runtime/fake-transport-preview`, predefined scenario selector, safety gates, evidence/count summary, in-memory run summary. | Shadow replay submit, persistence, sockets, credentials, host/user/password DTOs, raw FIX input, order commands, scheduler, trading mutation. | Default blocked, fake-enabled local config can run predefined scenarios, missing reason/unknown scenario/shadow submit rejected, no sensitive/order surface, no mutation. | Optional `smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled -ExpectFakeTransportPreviewEnabled` when launched with fake preview config. | Implemented as fake/no-network endpoint; no real external session. |
| 4E - External Read-Only Session Skeleton, Still No Socket Activation | Add the future real-session class boundary while keeping it non-functional. | `LmaxReadOnlyExternalSessionSkeleton`, skeleton factory, skeleton safety report, hard false gates for socket/logon/credentials/order/replay/mutation/scheduler/gateway registration. | TcpClient, Socket, SslStream, QuickFIX, FIX logon/logout, credentials, Connectivity Lab calls, evidence creation, shadow replay submit, scheduler, gateway registration, trading mutation. | Skeleton always disabled/blocked, future-looking config still blocked, no credential/order/network surface, no hosted service, FakeLmax-only guards. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as skeleton/gate only; external session is still not functional. |
| 4F - External Read-Only Session Guarded Transport Interface, Still No Socket Implementation | Define the transport abstraction a future real session would use while keeping implementation disabled. | `ILmaxReadOnlyGuardedTransport`, disabled guarded transport, guarded transport capabilities/status/result/safety report, blocked connect/read/disconnect methods. | TcpClient, Socket, SslStream, QuickFIX, ClientWebSocket, HttpClient transport, FIX logon/logout implementation, credentials, order methods, shadow replay submit, scheduler, trading mutation. | Disabled transport blocks connect/read/disconnect, returns no events, reports no network/socket/logon/credential/order capabilities, no credential/order/network surface. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as interface/stub only; no real network transport. |
| 4G - External Read-Only Session Configuration Envelope, No Credential Values, Still No Socket | Define typed future external-session configuration and validation without values or activation. | `LmaxReadOnlyExternalSessionOptions`, environment/limits/profile records, validator, inactive sample JSON. | Credential values, host/user/password fields, sockets, real transport, FIX logon/logout, scheduler, order commands, shadow replay submit, trading mutation. | Defaults safe-disabled, future-looking config blocked, Demo-only initial environment, limits capped, reason required for enabled attempts, no secret-shaped option fields/sample placeholders. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as config/validation only; no credential values and no socket. |
| 4H - External Read-Only Session Credential Profile Boundary, No Secret Reads, Still No Socket | Define the future credential-profile resolver boundary without reading or returning credential values. | Resolver contracts, disabled/no-op resolver, label-only descriptor/status/result/safety records. | Credential reads, credential values, user-secrets/env/appsettings/vault resolution, credential UI/API fields, sockets, FIX logon/logout, scheduler, order commands, shadow replay submit, trading mutation. | Disabled resolver blocks, `CredentialProfileName` label-only, no values read/used/stored/logged/returned, future credential use blocked, no credential/order/network surface. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as disabled credential boundary only; no secret reads and no socket. |
| 4I - External Read-Only Session Non-Secret Venue Profile Boundary, Still No Socket | Define future venue/environment labels without endpoint values or activation. | Venue profile labels, disabled/static registry, inactive descriptors, validator integration, label-only sample config. | Host, port, user, account, sender/target comp, endpoint URL, sockets, real transport, FIX logon/logout, credentials, scheduler, order commands, shadow replay submit, trading mutation. | Registry descriptors are inactive/non-secret, DemoLondon label accepted but inactive, UAT/Production/unknown/mismatch blocked, no endpoint/credential/order/network surface. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as disabled venue-label boundary only; no endpoint values and no socket. |
| 4J - External Read-Only Session Run Intent Envelope, Manual Reason Only, Still No Socket | Define the future manual run request boundary without starting anything. | Run intent records, mode enum, validator, validation summary/issues, reason/operator/profile labels, capped limits, safety flags. | Endpoint values, raw FIX, credentials, live controls, external session start, sockets, FIX logon/logout, scheduler, order commands, shadow replay submit, persistence, trading mutation. | Valid-looking future manual intent still blocked, reason/operator required, unsafe flags/limits/profile labels blocked, no forbidden DTO fields, no network/order/persistence surface. | `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as validate-only intent envelope; no endpoint and no run starts. |
| 4K - External Read-Only Session Manual Preflight Endpoint, Validate-Only, Still No Socket | Expose manual run intent validation through a local API route. | `POST /lmax-readonly-runtime/external-run-intent/validate`, safe request/response DTOs, structured validation issues, safety gates, smoke coverage. | Session start, external connection, credential read, host/port/user/account/session/endpoint fields, raw FIX, order commands, shadow replay submit, persistence, trading mutation. | Reason required, FutureExternalReadOnlyManual blocked, canStartSession=false, sessionStarted=false, externalConnectionAttempted=false, credentialReadAttempted=false, shadowReplaySubmitAttempted=false, tradingMutationAttempted=false. | `smoke-lmax-readonly-runtime-external-preflight-local.ps1`, `check-lmax-readonly-runtime-phase4-preflight.ps1`. | Implemented as validate-only endpoint; no socket and no run starts. |
| 4 - External Read-Only Session Prototype | Add real read-only session behind hard disabled gates. | Isolated read-only FIX session, fake FIX tests, parser/sanitizer. | Default activation, scheduler, order methods, credential UI, trading mutation. | Blocked config, read-only whitelist, evidence validation, mutation guard. | Lab/Demo manual only after approval. | No default activation; manual lab evidence validates. |
| 5 - Manual Demo Read-Only Runtime Capture | Manual Demo runtime read-only capture. | Bounded Demo/UAT config, evidence export, optional replay submit under explicit gate. | Production, scheduler, order submission, trading-table writes. | Demo gate tests, replay/no-mutation tests, audit/UI tests. | Manual Demo read-only smoke. | Evidence captured/replayed; rollback tested. |
| 6 - Soak / Rehearsal | Multiple manual Demo/UAT sessions and EOD comparison. | Manual sessions, monitoring, EOD comparison, playbook updates. | Scheduler, trading mutation, automatic activation. | Dedupe/fingerprint, reconnect, EOD comparison, no-mutation. | Rehearsal smoke suite. | Stable observations; operator playbook updated. |
| 7 - Scheduled Read-Only Shadow | Optional future scheduled reader. | Only after scheduler and governance gate. | Default scheduler, trading mutation, live order controls. | Scheduler safety, kill switch, no-mutation, audit, operations tests. | Scheduled shadow rehearsal. | Separate future sign-off required. |
| 8 - Trading Adapter | Separate future trading adapter project. | Separate design only. | Any inclusion in read-only reader scope. | Separate certification program. | Separate operational rehearsal. | Out of scope. |

## Universal Gate Checks

- API and Worker remain `FakeLmaxGateway` until a separate explicit activation gate.
- No phase may add `NewOrderSingle` to the read-only runtime adapter.
- No phase may write to orders, fills, positions, wallets, model runs, risk state, or reconciliation state.
- No phase may add credential forms or expose secrets in DTOs.
- No phase may enable scheduler auto-run without a separate scheduler gate.
- Every phase must update docs and smokes in the same change set.
- Phase 1 intentionally has no local runtime endpoint and no dedicated smoke script; the behavior is covered by unit tests because the layer is contracts-only.
- Phase 2 intentionally has no local runtime endpoint and no dedicated smoke script; fixture-only behavior is covered by unit tests. Manual endpoints begin in Phase 3.
- Phase 3 endpoints are local diagnostic surfaces only. They do not accept credentials, host fields, connection fields, order controls, or scheduler controls.
- Default Phase 3 smoke:
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1`
- Fake-enabled Phase 3.5 smoke:
  1. Start the API with `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1`
  2. In another terminal run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled`
- The fake-enabled launcher is local fixture-only and keeps external connections, credentials, order submission, raw FIX persistence, trading-table persistence, scheduler, and shadow replay submit disabled.
- Phase 4 preflight is checked with `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-lmax-readonly-runtime-phase4-preflight.ps1`. This script is local-only and never calls LMAX lab scripts.
- Phase 4A adds only `ILmaxReadOnlyExternalSession` contracts and `LmaxReadOnlyExternalSessionDisabled`; it still has no socket, FIX logon/logout, credential use, evidence creation, shadow replay submit, scheduler, or trading-state mutation.
- Phase 4B adds only an in-memory fake transport harness and fake session; it still has no socket, FIX logon/logout, credential use, evidence creation, shadow replay submit, scheduler, or trading-state mutation.
- Phase 4C adds only sanitized evidence preview mapping from fake transport events; it still has no shadow replay submit, persistence, socket, FIX logon/logout, credential use, scheduler, or trading-state mutation.
- Phase 4D adds only a manual fake transport preview endpoint. It supports predefined `EmptyReadOnly`, `MarketDataOnly`, `TradeCaptureOnly`, `OrderStatusOnly`, `ProtocolRejectOnly`, `MixedReadOnly`, `WarningOnly`, and `ErrorOnly` scenarios. It does not accept raw FIX, paths, external host/user/password fields, credentials, order controls, or shadow replay submit.
- Phase 4E adds only a blocked skeleton for the future real external session. It reports `SkeletonOnly`, `SocketActivation=false`, `FixLogonImplemented=false`, `CredentialUseImplemented=false`, `OrderSubmissionImplemented=false`, `ShadowReplaySubmitImplemented=false`, and `TradingMutationImplemented=false`.
- Phase 4F adds only the guarded transport abstraction and disabled transport. `ConnectReadOnlyAsync`, `ReadEventsAsync`, and `DisconnectAsync` exist as contract methods only; the disabled implementation always blocks and reads no events.
- Phase 4G adds only a typed configuration envelope and validator. The sample config is inactive, uses profile names only, and contains no credential values, host/user/password fields, socket activation, order controls, scheduler controls, or shadow replay submit.
- Phase 4H adds only the credential-profile resolver boundary and disabled/no-op resolver. `CredentialProfileName` is a label only; no credential values are read from user-secrets, environment variables, appsettings, or vaults, and nothing is used, stored, logged, or returned.
- Phase 4I adds only the venue-profile label boundary and disabled/static registry. `VenueProfileName` is a label only; no host, port, user, account, sender/target comp, endpoint URL, session value, or credential value is exposed.
- Phase 4J adds only the manual run-intent envelope and validator. A reason and operator id are required, but no external session starts.
- Phase 4K adds only the validate endpoint for that intent. It returns blocked/validated-only diagnostics and always reports that no session, connection, credential read, shadow replay submit, or trading mutation occurred.
- Phase 4L adds only a no-network dry-run report endpoint for that intent. It aggregates intent/options validation, venue profile status, disabled credential resolver status, disabled guarded transport status, blocked skeleton status, safety gates, expected outcome, and operator guidance. It always reports that no session, connection, credential read, shadow replay submit, or trading mutation occurred.
- Phase 4M adds only a manual signoff envelope endpoint for the dry-run report. It validates signer metadata and required attestations, but always returns `canAuthorizeExecution=false` and reports no session, connection, credential read, shadow replay submit, or trading mutation occurred.
- Phase 4N adds only a pre-activation audit envelope endpoint for the intent/dry-run/signoff chain. It validates stable blockers and no-attempt flags, but always returns `canAuthorizeExecution=false` and reports no session, connection, credential read, shadow replay submit, or trading mutation occurred.
- Phase 4O adds only a readiness snapshot endpoint for the complete intent/report/signoff/audit/config/profile/transport/skeleton chain. It aggregates blockers and final decision, but always returns `canStartSession=false` and reports no session, connection, credential read, shadow replay submit, or trading mutation occurred.
- Phase 4P adds only the final no-socket release gate document and script. It verifies the Phase 4A-4O boundary and produces a local ignored report, but it does not add connectivity, execution, credential read, shadow replay submit, or mutation capability.
- Phase 5A adds only first-transport prototype preflight docs, checklist, and script. It defines kill/rollback and abort controls for a future Phase 5B socket boundary, but it does not add sockets, credential reads, live runtime connectivity, order submission, shadow replay submit, scheduler, gateway registration, or mutation capability.
- Phase 5B adds only a dedicated Demo/manual prototype boundary, manual script, and prototype gate. It is not registered in API/Worker, does not submit orders, does not start a scheduler, does not submit to shadow replay, and does not mutate trading state.
- Phase 5C adds only credential availability checking and redaction. It checks local environment key labels for presence, returns labels/booleans only, and never returns values.
- Phase 5D adds the first isolated manual Demo market-data socket prototype. It is reachable only through `scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1` with explicit `-AllowExternalConnections`, `-ConfirmDemoReadOnly`, and `-Reason`; it supports only EURUSD / SecurityID `4001` market-data snapshot, uses required environment labels internally, redacts all credential material, and is not registered in API/Worker. It adds no order submission, scheduler, shadow replay submit, gateway replacement, trading-table persistence, or trading-state mutation.
- Phase 5E hardens failure and retry reporting for the Phase 5D prototype. It adds explicit blocked/failed-safe status taxonomy, disabled/no-auto-retry policy metadata, missing-credential classification, fake failure simulations, and `scripts/check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1`. Gates/tests do not make external attempts.
- Phase 5F adds operator-approved manual result capture for the Demo EURUSD snapshot prototype. The manual script prints planned safety flags, writes a sanitized JSON result under the ignored `artifacts/lmax-readonly-runtime-demo-snapshot/` tree, and keeps retry disabled. `scripts/check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1` masks credential labels before exercising the missing-credential path, so the gate cannot make an external attempt.
- Phase 5G adds sanitized transport diagnostics after the first manual Demo run completed logon/logout but timed out waiting for a snapshot. Diagnostics include request mode, safe request metadata, message-type counters, response classification, timeout timing, and sanitized session warnings/errors. `scripts/check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1` verifies the diagnostics boundary without making an external attempt.
- Phase 5H hardens MarketDataRequest compatibility. The default profile is `SnapshotPlusUpdates` plus `SecurityIdOnly`, avoiding the known rejected `SnapshotOnly` / `263=0` shape and omitting tag `55`. Known rejected profiles are encoded with rejection reasons and block locally unless the operator explicitly supplies known-rejected diagnostic approval. `scripts/check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1` verifies this without making an external attempt.
- Phase 5J adds sanitized Demo MarketData FIX logon/session diagnostics. The current blocker is logon confirmation: the runtime prototype and Connectivity Lab have reached TCP/TLS and sent Logon, but observed `MsgType=5` Logout/session behavior before logon confirmation. Results now include presence/length-only credential and comp-id diagnostics, FIX session settings, first inbound message type/text after redaction, and a runtime-vs-lab profile-label comparison. `scripts/check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1` verifies this without making an external attempt.
- Phase 5L closes the first successful Demo read-only EURUSD snapshot milestone. It adds a sanitized snapshot artifact validator and `scripts/check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1`; the gate validates a successful ignored artifact, confirms `Completed`, logon/snapshot/logout success, no order, no shadow replay submit, no trading mutation, no scheduler, `credentialValuesReturned=false`, and no secret/raw-sensitive FIX leakage. It does not make an external attempt or add new runtime behavior.
- Phase 5M maps the successful sanitized Demo snapshot artifact into a `MarketDataOnly` `lmax-fix-lifecycle-evidence-v1` preview. It adds `LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper`, `scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1`, and `scripts/check-lmax-readonly-runtime-phase5m-evidence-preview-gate.ps1`. The preview validates the Phase 5L artifact first, contains Demo EURUSD / SecurityID `4001` market data and empty execution/order/trade/reject arrays, writes only ignored sanitized preview JSON, and does not submit to shadow replay, create observations, register a gateway, start a scheduler, or mutate trading state.
- Phase 5N adds a manual/offline replay dry-run for the Phase 5M `MarketDataOnly` preview. It adds `scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1` and `scripts/check-lmax-readonly-runtime-phase5n-marketdata-replay-dryrun-gate.ps1`. The script validates the preview, confirms empty replay arrays, posts only through the existing local `/lmax-shadow/replay` API when the operator runs it, expects `Completed` with zero observations, verifies mutation counts unchanged, and does not add runtime shadow replay submit.
- Phase 5O adds a repeated manual Demo snapshot stability workflow. It adds `scripts/run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1`, `LmaxReadOnlyDemoSnapshotStabilitySummaryValidator`, and `scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1`. The workflow requires explicit operator flags, caps attempts to 1..5, caps delay to 1..10 seconds, reuses the existing manual Demo EURUSD snapshot prototype, validates successful artifacts, maps them to `MarketDataOnly` previews, and writes ignored sanitized stability summaries. It is not scheduler, not polling, not automatic retry, and does not add runtime shadow replay submit, order submission, gateway registration, or trading mutation.
- Phase 5P reviews the Phase 5O stability summary and records the readiness decision. It adds `LmaxReadOnlyDemoSnapshotStabilityClosureValidator`, `scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1`, `scripts/check-lmax-readonly-runtime-phase5p-stability-readiness-gate.ps1`, and `docs/LMAX_READONLY_RUNTIME_PHASE5P_STABILITY_DECISION.md`. The 3/3 stability run closes with `PASS`, but this does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, trading mutation, broader instruments, or production use.
- Phase 5Q hardens the controlled manual MarketData evidence workflow. It adds `LmaxReadOnlyMarketDataWorkflowValidator`, `scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1`, and `scripts/check-lmax-readonly-runtime-phase5q-workflow-hardening-gate.ps1`. The workflow validates sanitized snapshot artifacts, validates or regenerates `MarketDataOnly` previews, optionally records explicitly requested manual replay results, and writes ignored sanitized workflow manifests. Replay is off by default; runtime still does not submit to shadow replay. It adds no scheduler, polling, order submission, gateway registration, or trading mutation.
- Phase 5R closes the optional replay warning when the operator explicitly requests local manual replay. The workflow script now requires `-ReplayEvidencePreviews -ConfirmLocalManualReplay` and localhost API availability before replaying previews through the existing manual `/lmax-shadow/replay` path. Replay results must be `Completed`, zero-observation, and mutation-guard unchanged for each preview. Runtime still does not submit to shadow replay, and no scheduler, polling, order submission, gateway registration, external socket attempt, or trading mutation is added.
- Phase 5S adds the controlled manual workflow release gate. It adds `LmaxReadOnlyManualWorkflowReleaseValidator`, `scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1`, and `scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1`. The release workflow validates the Phase 5O stability summary, Phase 5L artifacts, Phase 5M previews, and optional Phase 5R replay results, then writes `artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json`. Skipped replay is `PASS_WITH_WARNINGS`; successful explicit replay can close as `PASS`. Runtime still does not submit to shadow replay and no scheduler, polling, orders, gateway registration, external snapshot attempt, or trading mutation is added.
- Phase 5T freezes the controlled manual workflow runbook. It adds `docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md` and `scripts/check-lmax-readonly-runtime-phase5t-runbook-freeze-gate.ps1`. The freeze gate validates the Phase 5S manifest/report, accepts `PASS` or `PASS_WITH_WARNINGS` only when the warning is optional replay skipped, and confirms no scheduler, polling, runtime shadow replay submit, orders, gateway registration, external socket attempt, credential exposure, or trading mutation was added.
- Phase 5V adds the final controlled manual MarketData workflow audit pack. It adds `LmaxReadOnlyMarketDataWorkflowAuditPackValidator`, `scripts/build-lmax-readonly-marketdata-workflow-audit-pack.ps1`, and `scripts/check-lmax-readonly-runtime-phase5v-final-audit-pack-gate.ps1`. The audit pack references the Phase 5O stability summary, Phase 5L artifacts, Phase 5M previews, replay-enabled Phase 5R workflow manifest, replay results, gate reports, and safety confirmations. It is local reporting only and adds no scheduler, polling, runtime shadow replay submit, orders, gateway registration, external socket attempt, credential exposure, or trading mutation.
- Phase 5W adds the operational signoff and workflow freeze. It adds `LmaxReadOnlyMarketDataOperationalSignoffValidator`, `scripts/signoff-lmax-readonly-marketdata-workflow.ps1`, `scripts/check-lmax-readonly-runtime-phase5w-operational-signoff-gate.ps1`, and `docs/LMAX_READONLY_RUNTIME_OPERATIONAL_SIGNOFF.md`. The signoff validates the Phase 5V audit pack decision `PASS`, confirms three artifacts/previews/manual replays, zero observations, unchanged safety flags, and states that `PASS` authorizes only recognition of the validated controlled manual Demo read-only MarketData workflow. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, automatic execution, or trading mutation.
- Phase 5X adds a read-only operator summary for the frozen workflow. It adds `LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator`, `GET /lmax-readonly-runtime/marketdata-workflow/status`, `scripts/show-lmax-readonly-marketdata-workflow-status.ps1`, `scripts/check-lmax-readonly-runtime-phase5x-operator-summary-gate.ps1`, and an LMAX Shadow page status panel. It reads Phase 5W signoff metadata only and exposes no live controls, credential fields, host/port fields, scheduler controls, runtime replay submit, order controls, gateway registration, or trading mutation.
- Phase 6A adds only the operationalization planning boundary after the frozen Phase 5 workflow. It adds `docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md`, `docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md`, and `scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1`. It recommends `Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run` and adds no external run, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6B adds only the manual additional MarketData instrument allowlist design. It adds `LmaxReadOnlyInstrumentAllowlist`, `LmaxReadOnlyInstrumentAllowlistValidator`, `tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyInstrumentAllowlistValidatorTests.cs`, and `scripts/check-lmax-readonly-runtime-phase6b-instrument-allowlist-gate.ps1`. Candidate instruments are planning-only, Demo-only, `MarketDataOnly`, and not approved for external runs. The gate writes `artifacts/readiness/phase6b-instrument-allowlist-gate.json` and adds no external run, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6C adds only a local SecurityID confirmation manifest for the Phase 6B allowlist. It adds `LmaxReadOnlyInstrumentSecurityIdManifest`, `LmaxReadOnlyInstrumentSecurityIdManifestValidator`, tests, and `scripts/check-lmax-readonly-runtime-phase6c-securityid-confirmation-gate.ps1`. All values are local planning placeholders and all `IsApprovedForExternalRun` flags remain false. The gate writes `artifacts/readiness/phase6c-securityid-confirmation-gate.json` and adds no external run, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6D adds only a local SecurityID discovery manifest for candidate real Demo mappings. It adds `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest`, `LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator`, tests, and `scripts/check-lmax-readonly-runtime-phase6d-securityid-discovery-gate.ps1`. Candidate values are explicit placeholders (`PHASE6D-DISCOVERY-PENDING-*`), all `IsApprovedForExternalRun` flags remain false, and the gate writes `artifacts/readiness/phase6d-securityid-discovery-gate.json`. It adds no external connection, external API call, snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6E adds only the SecurityID source evidence review process. It adds `LmaxReadOnlyInstrumentSecurityIdSourceEvidence`, `LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest`, `LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator`, tests, and `scripts/check-lmax-readonly-runtime-phase6e-securityid-evidence-review-gate.ps1`. Default records are `NeedsMoreEvidence`, `PASS_WITH_KNOWN_WARNINGS` is expected until source evidence is accepted, and all `IsApprovedForExternalRun` flags remain false. It adds no external connection, external API call, snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6F adds only local sanitized SecurityID confirmation records. It adds `LmaxReadOnlyInstrumentSecurityIdConfirmationRecord`, `LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator`, record creation/review scripts, a sample JSON template, tests, and `scripts/check-lmax-readonly-runtime-phase6f-confirmation-records-gate.ps1`. Missing records are a known warning; accepted records are planning-only and still keep `IsApprovedForExternalRun=false`. It adds no external connection, external API call, snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6G hardens the manual record-entry workflow. It adds a per-symbol template generator, preview/no-overwrite creation behavior, richer review summaries, an operator checklist, tests, and `scripts/check-lmax-readonly-runtime-phase6g-record-entry-workflow-gate.ps1`. Templates and review reports are ignored local artifacts; missing accepted records remain `PASS_WITH_KNOWN_WARNINGS`. It adds no external connection, external API call, snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6H enters real SecurityID confirmation records locally only. Real sanitized records live under `artifacts/lmax-readonly-runtime-securityid-confirmations/real/`; create them with `scripts/new-lmax-readonly-securityid-confirmation-record.ps1` after using `-WhatIfPreview`, review them with `scripts/review-lmax-readonly-securityid-confirmation-records.ps1`, and gate them with `scripts/check-lmax-readonly-runtime-phase6h-real-confirmation-records-gate.ps1`. `PASS` requires valid `AcceptedForPlanning` records for GBPUSD, USDJPY, EURGBP, and AUDUSD; `PASS_WITH_KNOWN_WARNINGS` is expected while records are missing/pending; `FAIL` covers unsafe, conflicting, sensitive, or externally approved records. `IsApprovedForExternalRun=false` remains mandatory, and this phase adds no external connection, external API call, snapshot, replay, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, credential exposure, or trading mutation.
- Phase 6I adds manual Demo-only FIX SecurityListRequest discovery. It adds `LmaxReadOnlySecurityListDiscovery`, `scripts/run-lmax-readonly-runtime-demo-securitylist-discovery.ps1`, and `scripts/check-lmax-readonly-runtime-phase6i-securitylist-discovery-gate.ps1`. The gate is local-only and returns `PASS_WITH_KNOWN_WARNINGS` until a sanitized discovery artifact exists; a valid artifact with all four matches can pass. Phase 6I sends no snapshots, no replay, no scheduler/polling, no NewOrderSingle/Cancel/Replace, no TradeCapture, no OrderStatusRequest, no gateway registration, no credential exposure, and no trading mutation.
- Phase 6J adds SecurityList failure diagnostics and request-profile compatibility. It validates the failed artifact, classifies rejects, documents safe and known-rejected profiles, and hardens the manual script with `AutoSequence` and `-AllowKnownRejectedDiagnostics`. It is local-only by default and adds no external run, snapshot, replay, scheduler, orders, gateway registration, credential exposure, or trading mutation.
- Phase 6L reviews the Phase 6K AutoSequence failed-safe artifact and produces a fallback decision. It adds `scripts/review-lmax-readonly-runtime-securitylist-discovery-failure.ps1` and `scripts/check-lmax-readonly-runtime-phase6l-securitylist-fallback-gate.ps1`; both are local-only and do not run SecurityListRequest. If no candidate matches or reject diagnostics are present, the recommended fallback is vendor/support or other official manual confirmation. It keeps `IsApprovedForExternalRun=false` and adds no snapshots, replay, scheduler/polling, orders, gateway registration, credential exposure, or trading mutation.
- Phase 6M creates planning records from uploaded LMAX instrument CSVs. It adds `LmaxReadOnlyInstrumentCsvSecurityIdExtractor`, `scripts/new-lmax-readonly-securityid-records-from-instrument-csv.ps1`, and `scripts/check-lmax-readonly-runtime-phase6m-csv-securityid-records-gate.ps1`. It selects DemoLondon/NewYork 400x IDs GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007, documents Tokyo 600x IDs as not selected, and keeps all records `IsApprovedForExternalRun=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, or trading mutation.
- Phase 6N applies accepted SecurityID planning values to a local planning manifest. It adds `LmaxReadOnlyInstrumentSecurityIdPlanningManifest`, `scripts/apply-lmax-readonly-securityid-planning-values.ps1`, and `scripts/check-lmax-readonly-runtime-phase6n-planning-values-gate.ps1`. The manifest records GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007 with SecurityIDSource=8, Demo/DemoLondon scope, accepted confirmation record references, and `IsApprovedForExternalRun=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, or trading mutation.
- Phase 6O defines per-instrument safety gates for those planning values. It adds `LmaxReadOnlyPerInstrumentSafetyGate`, `LmaxReadOnlyAdditionalInstrumentSafetyGateManifest`, `scripts/build-lmax-readonly-additional-instrument-safety-gates.ps1`, and `scripts/check-lmax-readonly-runtime-phase6o-per-instrument-safety-gate.ps1`. Each candidate gate checks accepted planning value, SecurityIDSource=8, Demo/DemoLondon scope, MarketDataOnly intent, no external-run approval, no order capability, no runtime shadow replay submit, no scheduler/polling, no trading mutation, and a requirement for a future explicit operator prompt. `PASS` means planning-safe only; every instrument remains `IsApprovedForExternalRun=false` and `eligibleForManualSnapshotAttempt=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, or trading mutation.
- Phase 6P defines the manual additional-instrument snapshot preflight boundary. It adds `LmaxReadOnlyAdditionalInstrumentSnapshotPreflight`, `scripts/build-lmax-readonly-additional-instrument-snapshot-preflights.ps1`, and `scripts/check-lmax-readonly-runtime-phase6p-additional-snapshot-preflight-gate.ps1`. The profile is fixed to `SnapshotPlusUpdates`, `SecurityIdOnly`, SecurityIDSource=8, `MarketDepth=1`, and capped runtime/wait/event limits for GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007. `PASS` means preflight design-safe only; every instrument remains `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, trading-table persistence, or trading mutation.
- Phase 6Q adds a manual additional-instrument snapshot approval envelope. It adds `LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope`, `scripts/new-lmax-readonly-additional-instrument-snapshot-approval-envelope.ps1`, `scripts/review-lmax-readonly-additional-instrument-snapshot-approval-envelopes.ps1`, and `scripts/check-lmax-readonly-runtime-phase6q-approval-envelope-gate.ps1`. `AcceptedForPlanning` means planning-complete only and keeps `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, trading-table persistence, or trading mutation.
- Phase 6R adds a GBPUSD single-instrument dry-run report. It adds `LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport`, `scripts/new-lmax-readonly-additional-instrument-snapshot-dry-run-report.ps1`, `scripts/review-lmax-readonly-additional-instrument-snapshot-dry-run-reports.ps1`, and `scripts/check-lmax-readonly-runtime-phase6r-single-instrument-dryrun-gate.ps1`. `PASS` means local source-chain consistency only and keeps `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, and `eligibleForManualSnapshotAttempt=false`. It adds no external run, SecurityListRequest, snapshot, replay, scheduler, orders, gateway registration, credential exposure, trading-table persistence, or trading mutation.
- Fake transport preview smoke:
  1. Start the API with `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1`
  2. In another terminal run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-lmax-readonly-runtime-fake-local.ps1 -ExpectFakeEnabled -ExpectFakeTransportPreviewEnabled`
### Phase 6S - Single-Instrument Attempt Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase6s-single-instrument-attempt-gate.ps1`

Inputs: optional `-AttemptGateFile`. With a supplied valid gate artifact, expected result is `PASS`. Without an artifact, source checks can only produce a warning state. Any executable flag, attempted external connection, snapshot, replay, order submission, shadow replay submit, scheduler start, or trading mutation must fail.

### Phase 6T - GBPUSD Execution Plan Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase6t-gbpusd-execution-plan-gate.ps1`

Inputs: optional `-ExecutionPlanFile`. With a valid execution plan artifact, expected result is `PASS`. The gate fails if the plan authorizes an external run, omits the `DO NOT RUN IN PHASE 6T` warning, enables snapshot eligibility, adds scheduler/polling, orders, runtime shadow replay submit, gateway registration, or trading mutation.

### Phase 6U - GBPUSD Operator Signoff Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase6u-gbpusd-operator-signoff-gate.ps1`

Inputs: optional `-ExecutionPlanFile`, `-Phase6TGateReportFile`, and `-SignoffFile`. With a valid `SignedForPlanning` signoff, expected result is `PASS`. Without a signoff, expected result is `PASS_WITH_KNOWN_WARNINGS`. Any run authorization, snapshot eligibility, scheduler/polling, orders, runtime shadow replay submit, gateway registration, or trading mutation must fail.

### Phase 6V - GBPUSD Final Readiness Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase6v-gbpusd-final-readiness-gate.ps1`

Inputs: optional `-FinalReadinessFile`. With a valid final readiness artifact, expected result is `PASS`. Without an artifact, expected result is `PASS_WITH_KNOWN_WARNINGS`. Any run authorization, snapshot eligibility, scheduler/polling, orders, runtime shadow replay submit, gateway registration, or trading mutation must fail.

### Phase 6W - GBPUSD Snapshot Result Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase6w-gbpusd-snapshot-result-gate.ps1`

Inputs: optional `-ResultArtifactFile`. With no result, expected result is `PASS_WITH_KNOWN_WARNINGS`. With a result, expected result is `PASS` if the artifact is sanitized, GBPUSD `4002`, and all unsafe flags remain false. Unsafe flags or sensitive content must fail.

### Phase 6X - GBPUSD Empty-Book Result Review Gate

Review script: `scripts/review-lmax-readonly-gbpusd-snapshot-result.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6x-gbpusd-result-review-gate.ps1`

Inputs: optional `-ArtifactFile`. With the first GBPUSD artifact, expected result is `PASS_WITH_KNOWN_WARNINGS` because `CompletedWithEmptyBook` means one MarketDataSnapshot was received with zero entries and no rejects. `PASS` remains reserved for completed bid/ask snapshots. Unsafe flags, credential leakage, order surface, scheduler/polling, runtime shadow replay submit, gateway registration, or trading mutation must fail.

### Phase 6Y - GBPUSD Market-Hours Retry Gate

Preparation script: `scripts/prepare-lmax-readonly-gbpusd-market-hours-retry.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6y-market-hours-retry-gate.ps1`

Inputs: optional `-RetryReadinessFile`. With the generated retry readiness artifact, expected result is `PASS`. Without an artifact, expected result is `PASS_WITH_KNOWN_WARNINGS`. The gate fails if the artifact is not GBPUSD `4002`, if the previous status is not `CompletedWithEmptyBook`, if retry is not manual-only and market-hours-only, if `canRunAutomatically=true`, or if scheduler/polling, runtime shadow replay submit, order surface, gateway registration, credential exposure, or trading mutation appears.

### Phase 6Z-A - Additional-Instrument Planning Pipeline Gate

Builder script: `scripts/build-lmax-readonly-additional-instrument-planning-pipeline.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6za-additional-instrument-pipeline-gate.ps1`

Inputs: optional `-PipelineManifestFile`. With a valid aggregate manifest, expected result is `PASS`. The manifest must cover GBPUSD=4002, EURGBP=4003, USDJPY=4004, and AUDUSD=4007, all with SecurityIDSource `8` and complete non-executable planning artifact chains. The gate fails if any required artifact is missing, any expected decision is not safe, any instrument is executable, `executableCount` is not `0`, or scheduler/polling, runtime shadow replay submit, order surface, gateway registration, credential exposure, or trading mutation appears.

### Phase 6Z-C - Additional-Instrument Status Panel Gate

Status script: `scripts/show-lmax-readonly-additional-instrument-planning-status.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6zc-additional-instrument-status-panel-gate.ps1`

Inputs: optional `-PipelineManifestFile`. With a valid pipeline manifest, expected result is `PASS`. The gate checks the summary model, script, API endpoint, UI panel, pipeline safety flags, `executableCount=0`, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

### Phase 6Z-D - Additional-Instrument Documentation Pack Gate

Builder script: `scripts/build-lmax-readonly-additional-instruments-planning-doc-pack.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6zd-additional-instruments-doc-pack-gate.ps1`

Inputs: required `-PipelineManifestFile` and `-PlanningStatusReportFile`; optional `-DocPackFile`. With a generated doc pack, expected result is `PASS`. The gate validates the final document, builder, aggregate pipeline manifest, operator planning status report, documentation pack, `instrumentCount=4`, `executableCount=0`, false run flags for every instrument, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means documentation and artifact-chain consistency only. It does not authorize external runs, snapshots, replay, scheduler/polling, orders, gateway registration, production/UAT, multi-instrument batch, or trading mutation.

### Phase 6Z-E - Market-Hours Action Card Gate

Status script: `scripts/show-lmax-readonly-market-hours-next-action.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase6ze-market-hours-action-card-gate.ps1`

Inputs: optional paths for the final readiness, market-hours retry readiness, Phase 6X review, and Phase 6Z-D documentation pack. With the validated source artifacts, expected result is `PASS`. The gate checks GBPUSD=4002, previous `CompletedWithEmptyBook` outside market hours, `executableCount=0`, false run flags, the read-only API endpoint, the UI card, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means next-action visibility only. It does not authorize running from the UI or by scheduler; the future action remains a separate explicit manual operator command during market hours.

### Phase 7A - Read-Only Runtime Next Boundary Gate

ADR: `docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md`

Checklist: `docs/LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md`

Gate script: `scripts/check-lmax-readonly-runtime-phase7a-next-boundary-gate.ps1`

Expected result: `PASS`.

The gate validates the ADR/checklist, verifies that the recommended next boundary is Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run, and scans API/Worker startup surfaces for forbidden scheduler/polling, runtime shadow replay submit, order surface, real gateway registration, and trading mutation markers.

`PASS` means architecture decision completeness only. It does not authorize external LMAX connection, SecurityListRequest, MarketData snapshot, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.

### Phase 7B - Controlled Manual Multi-Instrument Workflow Plan Gate

Plan builder: `scripts/build-lmax-readonly-controlled-manual-multi-instrument-workflow-plan.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7b-controlled-manual-workflow-plan-gate.ps1`

Inputs: required pipeline manifest and planning status report for the builder; optional `-WorkflowPlanFile` for the gate. With a generated workflow plan, expected result is `PASS`.

The gate validates the Phase 7B model, tests, builder, document, source pipeline/status artifacts, and workflow plan. It requires instrumentCount=4, sequence GBPUSD, EURGBP, USDJPY, AUDUSD, `executableCount=0`, `batchExecutionAllowed=false`, `oneInstrumentAtATime=true`, `maxAttemptsPerInstrument=1`, `retryRequiresNewPhase=true`, `marketHoursOnly=true`, false run eligibility flags, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means workflow plan completeness only. It does not authorize external runs, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, production/UAT, multi-instrument batch execution, or trading mutation.

### Phase 7C - GBPUSD Market-Hours Closure Gate

Review script: `scripts/review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1`

Evidence preview script: `scripts/preview-lmax-readonly-gbpusd-market-hours-snapshot-evidence.ps1`

Manual replay script: `scripts/replay-lmax-readonly-gbpusd-market-hours-evidence-preview.ps1`

Closure manifest script: `scripts/build-lmax-readonly-gbpusd-market-hours-closure-manifest.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7c-gbpusd-closure-gate.ps1`

Inputs: optional GBPUSD result artifact, review report, evidence preview file, replay report, and closure manifest. With no market-hours artifact, expected result is `PASS_WITH_KNOWN_WARNINGS` because the closure workflow is ready but no new result has been supplied.

The gate validates Phase 7C model/scripts/tests, checks supplied artifacts for GBPUSD / SecurityID `4002`, verifies safe classifications (`CompletedWithBook`, `CompletedWithEmptyBook`, or `FailedSafe`), and scans for no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means the supplied closure artifacts are safe and complete. `PASS_WITH_KNOWN_WARNINGS` means either no market-hours result exists yet or the supplied result is a safe empty-book/failed-safe warning. Phase 7C never runs LMAX, never runs snapshots, and never replays automatically.

### Phase 7D - Post-GBPUSD Next Instrument Decision Gate

Decision script: `scripts/decide-lmax-readonly-next-instrument-after-gbpusd.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7d-next-instrument-decision-gate.ps1`

Inputs: required Phase 7B workflow plan; optional GBPUSD Phase 7C closure manifest or review report. With no GBPUSD market-hours closure supplied, expected decision is `PendingGbpusdMarketHoursAttempt` and the gate returns `PASS_WITH_KNOWN_WARNINGS`.

The decision rules are:

- No GBPUSD market-hours closure: `PendingGbpusdMarketHoursAttempt`.
- GBPUSD `CompletedWithBook` / `PASS`: `ProceedToEurgbpPlanning`.
- GBPUSD `CompletedWithEmptyBook` / `PASS_WITH_KNOWN_WARNINGS`: `RetryGbpusdAtLaterMarketHours`.
- GBPUSD failed-safe or unsafe result: `BlockSequenceForDiagnostics`.

The gate requires `canRunExternalSnapshot=false`, `IsApprovedForExternalRun=false`, `eligibleForManualSnapshotAttempt=false`, `batchExecutionAllowed=false`, `executableCount=0`, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` or `PASS_WITH_KNOWN_WARNINGS` means decision completeness only. Phase 7D does not authorize external runs, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, batch execution, or trading mutation.

### Phase 7E - GBPUSD Market-Hours Execution Checklist Gate

Checklist doc: `docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_EXECUTION_CHECKLIST.md`

Checklist pack builder: `scripts/build-lmax-readonly-gbpusd-market-hours-execution-checklist-pack.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7e-execution-checklist-gate.ps1`

Inputs: optional checklist pack JSON. The builder reads known final readiness, retry readiness, Phase 7C gate, and Phase 7D decision artifacts when present, then writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/execution-checklists/`.

The gate requires the future manual GBPUSD command to be present but clearly marked `DO NOT RUN UNTIL MARKET HOURS`, requires the Phase 7C post-run sequence and Phase 7D decision step to be documented, and verifies kill-switch, rollback, and non-authorization coverage.

`PASS` means the Monday/market-hours checklist pack is complete. Phase 7E does not run LMAX, snapshots, SecurityListRequest, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, production/UAT, batch execution, or trading mutation.

### Phase 7E2 - EURGBP Readiness Rehydration Gate

Rehydration script: `scripts/rehydrate-lmax-readonly-eurgbp-manual-snapshot-readiness.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7e2-eurgbp-readiness-gate.ps1`

Inputs: corrected Phase 7D decision, Phase 6Z-A pipeline manifest, planning manifest, safety gate manifest, and preflight manifest. With the corrected GBPUSD `CompletedWithBook` / `PASS` review and Phase 7D `ProceedToEurgbpPlanning`, expected result is `PASS`.

The gate requires EURGBP / EUR/GBP / SecurityID `4003` / SecurityIDSource `8`, source decisions `PASS` or `AcceptedForPlanning` as appropriate, one-instrument-at-a-time control, `batchExecutionAllowed=false`, `executableCount=0`, false run eligibility flags, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means EURGBP readiness rehydration completeness only. Phase 7E2 does not authorize external runs, snapshots, SecurityListRequest, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, batch execution, or trading mutation.

### Phase 7F2 - EURGBP Execution Checklist Gate

Checklist doc: `docs/LMAX_READONLY_EURGBP_MANUAL_SNAPSHOT_EXECUTION_CHECKLIST.md`

Checklist builder: `scripts/new-lmax-readonly-eurgbp-manual-snapshot-execution-checklist.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7f2-eurgbp-execution-checklist-gate.ps1`

Inputs: optional checklist JSON. The builder reads the Phase 7E2 EURGBP readiness artifact and writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-execution-checklists/`.

The gate requires EURGBP / EUR/GBP / SecurityID `4003` / SecurityIDSource `8`, EURGBP readiness `PASS`, previous GBPUSD closure `PASS`, previous Phase 7D decision `ProceedToEurgbpPlanning`, command template marked `DO NOT RUN IN PHASE 7F2`, false run eligibility flags, `batchExecutionAllowed=false`, `oneInstrumentAtATime=true`, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means the EURGBP execution checklist is complete as planning only. Phase 7F2 does not authorize external runs, snapshots, SecurityListRequest, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, batch execution, or trading mutation.

### Phase 7G2 - EURGBP Final Pre-Run Gate

Final pre-run builder: `scripts/new-lmax-readonly-eurgbp-final-pre-run-gate.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7g2-eurgbp-final-prerun-gate.ps1`

Inputs: Phase 7D decision, Phase 7E2 EURGBP readiness artifact, and Phase 7F2 EURGBP execution checklist artifact. The builder writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-final-prerun/`.

The gate requires EURGBP / EUR/GBP / SecurityID `4003` / SecurityIDSource `8`, Demo/DemoLondon, `SnapshotPlusUpdates`, `SecurityIdOnly`, MarketDepth `1`, Phase 7D `ProceedToEurgbpPlanning`, GBPUSD closure `PASS`, EURGBP readiness `PASS`, EURGBP execution checklist `PASS`, `oneInstrumentAtATime=true`, `batchExecutionAllowed=false`, false run eligibility flags, no scheduler/polling, no runtime shadow replay submit, no order surface, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

`PASS` means final pre-run consistency only. Phase 7G2 does not authorize external runs, snapshots, SecurityListRequest, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, batch execution, or trading mutation.

## Phase 7H - Generic Additional Instrument One-Shot Workflow Gate

Gate script: `scripts/check-lmax-readonly-runtime-phase7h-generic-additional-snapshot-workflow-gate.ps1`

The gate verifies the generic wrapper, generic review/preview/replay/closure scripts, and model/tests exist. If artifacts are supplied, it validates that supported additional instrument results classify as `CompletedWithBook`, `CompletedWithEmptyBook`, or safe `FailedSafe` only, with no credential leakage and all order/shadow/mutation/scheduler flags false.

The Phase 7H gate also source-scans API/Worker registration. `PASS` means the reusable one-instrument manual workflow exists and remains non-runtime. It does not authorize an external run, batch execution, scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.

## Phase 7H2 - Generic Additional Instrument Final Pre-Run Gate

Builder: `scripts/new-lmax-readonly-additional-instrument-final-pre-run-gate.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7h2-additional-instrument-final-prerun-gate.ps1`

Inputs: a supported symbol and its existing non-executable final-readiness artifact. The builder writes sanitized JSON/Markdown under `artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/`.

The gate requires the generated artifact to contain the Phase 7H wrapper contract: `oneInstrumentAtATime=true`, `batchExecutionAllowed=false`, `externalRunAuthorized=false`, `canRunExternalSnapshot=false`, `eligibleForManualSnapshotAttempt=false`, `IsApprovedForExternalRun=false`, no scheduler/polling, no runtime shadow replay submit, no orders, no gateway registration, no trading mutation, and API/Worker `FakeLmaxGateway`.

The gate also verifies the old Phase 6Z-A USDJPY final-readiness artifact remains rejected as a wrapper gate substitute. `PASS` means local pre-run gate compatibility only; it does not authorize an external run.

## Phase 7K16 - Final Operator Signoff And Readiness Documentation Update

Signoff builder: `scripts/new-lmax-readonly-phase7k16-final-operator-signoff.ps1`

Gate script: `scripts/check-lmax-readonly-runtime-phase7k16-final-operator-signoff.ps1`

Inputs: Phase 7K15 final evidence pack and day-closure gate, plus the Phase 7K14/7K13/7K10/EURGBP closure artifacts used by the final evidence pack.

The gate records final operator signoff for the additional-instrument Demo read-only evidence cycle. The current cycle is closed with successful read-only evidence for GBPUSD, EURGBP, and AUDUSD; USDJPY remains parked on a separate troubleshooting rail.

`PASS_FINAL_OPERATOR_SIGNOFF_RECORDED` means signoff and documentation update are recorded only. Phase 7K16 does not authorize external runs, snapshots, replay, scheduler/polling, runtime shadow replay submit, orders, gateway registration, batch execution, or trading mutation. API/Worker remain `FakeLmaxGateway` only.
