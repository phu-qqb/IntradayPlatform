# LMAX Read-Only Runtime Phase 7 Next Boundary ADR

Status: Accepted for planning  
Phase: 7A  
Decision date: 2026-05-10  
Scope: architecture decision only; no runtime capability

## Executive Summary

Phase 7A chooses the next safe technical frontier after the validated manual LMAX Demo read-only MarketData workflow and the frozen additional-instrument planning pipeline. The decision is planning-only and does not authorize an external run.

Recommended next boundary:

**Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run**

This keeps the system inside the proven manual-read-only envelope while preparing a controlled way to reason about market-hours attempts for GBPUSD, EURGBP, USDJPY, and AUDUSD. It does not add scheduler, polling, runtime shadow replay submit, order path, production/UAT, or batch execution.

## Current Validated State

- EURUSD Demo read-only MarketData workflow is complete, audited, signed, documented, and frozen.
- EURUSD has three successful manual Demo snapshots, sanitized artifacts, MarketDataOnly evidence previews, three local manual replays, zero observations, unchanged mutation guard, and PASS audit/signoff/status panels.
- Additional-instrument planning is complete and frozen for:
  - GBPUSD / GBP/USD / SecurityID 4002
  - EURGBP / EUR/GBP / SecurityID 4003
  - USDJPY / USD/JPY / SecurityID 4004
  - AUDUSD / AUD/USD / SecurityID 4007
- Additional-instrument aggregate planning decision is PASS.
- `executableCount=0`.
- `IsApprovedForExternalRun=false`, `canRunExternalSnapshot=false`, and `eligibleForManualSnapshotAttempt=false` remain false for every additional instrument.
- GBPUSD has final readiness PASS and market-hours retry readiness PASS.
- The prior GBPUSD attempt was outside market hours and closed as `CompletedWithEmptyBook`, reviewed as a safe `PASS_WITH_KNOWN_WARNINGS`.
- Operator console visibility exists for:
  - LMAX Read-Only Demo MarketData Workflow status.
  - Additional instruments planning status.
  - Market-hours next-action status.

## Current Non-Authorizations

The current state still does not authorize:

- Scheduler or polling service.
- Timers, background jobs, or hosted services for LMAX.
- Runtime shadow replay submit.
- Order submission.
- NewOrderSingle, Cancel/Replace, TradeCapture, or OrderStatusRequest.
- Gateway registration for a real LMAX runtime gateway in API or Worker.
- Trading-state mutation or live FIX persistence to trading tables.
- Production or UAT use.
- Multi-instrument batch execution.
- UI-triggered external connections, snapshots, replay, scheduler, or orders.

API and Worker must remain `FakeLmaxGateway` only.

## Candidate Next Boundaries

### A. Continue Manual One-Instrument-at-a-Time Snapshot Attempts

- Operational risk: Low if each attempt remains an explicit manual command with one symbol and one artifact.
- Technical risk: Low because it reuses the isolated prototype path and existing result validators.
- Reversibility: High; no runtime registration or persistent trading mutation is introduced.
- Auditability: High when each attempt writes sanitized artifacts and gate reports.
- Value: Medium; it builds empirical coverage for additional instruments.
- Why now: GBPUSD market-hours retry is already prepared.
- Why not now: Running is an operational action and must wait for market hours and explicit operator choice.

### B. Controlled Manual Evidence Workflow Hardening

- Operational risk: Low; it improves review/reporting around already-sanitized artifacts.
- Technical risk: Low to medium depending on whether comparison reports or preview mapping are expanded.
- Reversibility: High; docs/scripts/models can be removed without runtime effect.
- Auditability: High; richer reports improve traceability.
- Value: Medium; useful once more additional-instrument artifacts exist.
- Why now: It strengthens what already works.
- Why not now: It has less immediate value than defining a controlled multi-instrument manual plan.

### C. Manual Runtime-to-Shadow Submit Planning Gate

- Operational risk: Medium; even if planning-only, it points toward a runtime bridge that must be treated carefully.
- Technical risk: Medium; it requires explicit boundary controls so runtime never submits automatically.
- Reversibility: Medium; conceptual work can be reversed, but accidental implementation would raise risk.
- Auditability: Medium to high if kept as planning-only.
- Value: Medium; future evidence workflows may benefit.
- Why now: It is a known future concern.
- Why not now: The current safe frontier should avoid runtime shadow submit until more manual instrument evidence exists.

### D. Read-Only Service Planning, No Implementation

- Operational risk: Medium; service framing can drift toward scheduling or polling.
- Technical risk: Medium; service designs need strong anti-automation constraints.
- Reversibility: High if documentation-only, lower if implementation begins.
- Auditability: Medium; planning docs can be audited, but service assumptions need careful review.
- Value: Medium to high long-term.
- Why now: It may shape a future architecture.
- Why not now: Scheduler/polling remains explicitly rejected, so this should wait.

### E. Operator UI/Readiness Hardening

- Operational risk: Low if UI remains read-only.
- Technical risk: Low; current panels already expose sanitized local state.
- Reversibility: High.
- Auditability: Medium; UI visibility helps operator review but should not be the control plane for execution.
- Value: Medium; useful for operational clarity.
- Why now: The console already has safe read-only panels.
- Why not now: The most important next boundary is workflow structure, not more visibility alone.

### F. Order Path Planning, Explicitly Deferred

- Operational risk: High.
- Technical risk: High.
- Reversibility: Low to medium once concepts, contracts, or controls appear in the codebase.
- Auditability: High only after a separate risk program.
- Value: Out of scope for the current read-only MarketData program.
- Why now: No current reason.
- Why not now: Orders are explicitly outside the validated read-only runtime boundary.

## Decision

Select:

**Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run**

This phase should be a plan, not execution. It should define how future market-hours attempts for GBPUSD, EURGBP, USDJPY, and AUDUSD are handled without increasing runtime power.

## Rationale

Phase 7B is the safest next boundary because it:

- Extends what already works.
- Remains manual.
- Remains one instrument at a time.
- Requires explicit operator command for any future attempt.
- Adds no scheduler or polling.
- Adds no runtime shadow replay submit.
- Adds no order path.
- Adds no production/UAT scope.
- Prepares controlled reporting and comparison across additional instruments without adding automation power.
- Keeps API/Worker on `FakeLmaxGateway`.

## Explicitly Rejected For Now

- Scheduler or polling service.
- Runtime-to-shadow submit implementation.
- Order path planning or implementation.
- Production or UAT scope.
- Multi-instrument batch execution.
- UI-triggered run controls.
- Real gateway registration in API or Worker.

## Required Invariants For Phase 7B

Any Phase 7B implementation must:

- Be planning-only unless a later operator prompt explicitly says otherwise.
- Remain manual and one instrument at a time.
- Require explicit operator command for any future external attempt.
- Add no scheduler, polling, timer, or hosted service.
- Add no runtime shadow replay submit.
- Add no order submission or order message type.
- Add no gateway registration.
- Add no trading-state mutation or live FIX persistence to trading tables.
- Produce sanitized artifacts only.
- Preserve rollback/abort instructions.
- Preserve `executableCount=0` and false run eligibility flags during planning.

## Phase 7A Outcome

Phase 7A is complete when the ADR, Phase 7 boundary checklist, and Phase 7A local gate exist and pass. A PASS means the architecture decision is documented and the current runtime boundary remains unchanged. It does not authorize LMAX connection, snapshots, replay, scheduler/polling, orders, gateway registration, production/UAT, or trading mutation.

## Phase 7B Follow-On

Phase 7B implements the selected recommendation as a planning-only workflow plan. It sequences GBPUSD, EURGBP, USDJPY, and AUDUSD for future manual consideration while preserving `executableCount=0`, `batchExecutionAllowed=false`, one-instrument-at-a-time operation, no scheduler/polling, no runtime shadow replay submit, no orders, no gateway registration, and no trading mutation.
