# LMAX Read-Only Runtime - Final No-Socket Release Gate

## 1. Purpose

This is the final no-socket release gate before any future external read-only socket/network implementation phase. It verifies that the Phase 4A-4O safety envelope is present, documented, tested, and locally checkable.

Passing this gate does not mean live connectivity exists. It means the project is ready to consider a separate future prompt for a first socket-enabled read-only prototype.

Phase 5A follows this gate as a planning-only preflight. It defines kill/rollback, abort conditions, and entry criteria before any future socket-boundary work.

## 2. Current Status

The following Phase 4 slices are closed:

- Phase 4A - External session contract stub, no socket.
- Phase 4B - Fake transport harness, no network.
- Phase 4C - Fake events to evidence preview, no shadow submit.
- Phase 4D - Fake transport preview endpoint, no shadow submit.
- Phase 4E - External skeleton behind hard-disabled gates.
- Phase 4F - Guarded transport interface, still no socket implementation.
- Phase 4G - Configuration envelope, no credential values.
- Phase 4H - Credential profile boundary, no secret reads.
- Phase 4I - Non-secret venue profile boundary.
- Phase 4J - Run intent envelope, manual reason only.
- Phase 4K - Manual preflight endpoint, validate-only.
- Phase 4L - No-network dry-run report.
- Phase 4M - Manual signoff envelope, metadata-only.
- Phase 4N - Pre-activation audit envelope, metadata-only.
- Phase 4O - Readiness snapshot, metadata-only.

## 3. Non-Negotiable Safety State

- API and Worker remain `FakeLmaxGateway` only.
- No socket implementation exists.
- No network transport exists.
- No credential values are read, used, stored, logged, or returned.
- No order submission exists.
- No scheduler auto-run exists.
- No shadow replay submit from runtime exists.
- No trading-state mutation exists.
- No host, port, user, password, account, session, or endpoint fields exist in runtime API DTOs.
- No production activation exists.

## 4. Required Commands

- `dotnet build QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false`
- `dotnet test QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false`
- `npm.cmd run typecheck` from `src/QQ.Production.Intraday.Ui`
- `npm.cmd run build` from `src/QQ.Production.Intraday.Ui`
- `npm.cmd test` from `src/QQ.Production.Intraday.Ui`
- `scripts/validate-lmax-lab-evidence-file.ps1` against all files in `tests/fixtures/lmax-shadow`
- `scripts/check-lmax-readonly-runtime-phase4-preflight.ps1`
- `scripts/smoke-lmax-readonly-runtime-external-preflight-local.ps1` when the API is available
- `scripts/smoke-lmax-readonly-runtime-fake-local.ps1` when the API is available
- `scripts/run-operational-readiness-gate.ps1 -SkipBuild -SkipFrontend`
- `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1`
- `scripts/check-lmax-readonly-runtime-phase5a-preflight.ps1` before any Phase 5B prompt

## 5. Release Gate Checklist

- [ ] All Phase 4A-4O contracts, endpoints, tests, and docs exist.
- [ ] Phase gate docs are current.
- [ ] Phase 4 preflight passes.
- [ ] Readiness snapshot final decision is `NotExecutable`.
- [ ] Dry-run report returns `canStartSession=false`.
- [ ] Signoff returns `canAuthorizeExecution=false`.
- [ ] Pre-activation audit returns `canAuthorizeExecution=false`.
- [ ] No forbidden runtime API fields are present.
- [ ] No socket or network types are present in the runtime LMAX path.
- [ ] No real LMAX gateway registration is present.
- [ ] No LMAX hosted service is present.
- [ ] Generated evidence is not tracked.
- [ ] API health reports `FakeLmaxGateway` when the API is available.
- [ ] Mutation guards are unchanged by API-dependent smokes.
- [ ] Smokes are green when the API is available.

## 6. Decision

`PASS` means all local checks passed and there were no known warnings.

`PASS WITH KNOWN WARNINGS` means all mandatory local checks passed, but one or more accepted warnings were recorded, such as API-dependent checks being skipped because the local API was unavailable.

`FAIL` means the project must not proceed into a socket-enabled phase.

## 7. Next Phase Boundary

The next implementation phase must be authorized by a separate explicit prompt after Phase 5A preflight passes. It should be treated as a new project boundary:

- First External Read-Only Transport Prototype.
- Demo only.
- Manual only.
- No order submission.
- No scheduler.
- No trading mutation.
- No shadow replay submit initially unless separately gated.
- Kill/disable and rollback plan required before any attempt.
