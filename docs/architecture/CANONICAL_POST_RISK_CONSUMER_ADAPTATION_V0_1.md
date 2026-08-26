# Canonical post-Risk consumer adaptation v0.1

Status: `UNRESOLVED_STOP` evidence checkpoint; no production activation or shared-contract change.

Canonical semantic authority inspected read-only: `quantum-qb/QQ.Investment.Platform@47d5d10cf4ee914621526e687c707657324730a1`.

Retained source inspected: `phu-qqb/IntradayPlatform@38b25aa7f579863acd87f3eb2982201a81daa1de`.

## Safety classification for the proposed canonical path

| Retained stage/check | Classification | Exact evidence | Reason |
|---|---|---|---|
| Batch validation and promotion | `DUPLICATE_TARGET_OR_RISK_AUTHORITY` | `src/QQ.Production.Intraday.Application/ApplicationServices.cs`, `ModelWeightPromotionService.BuildValidationIssuesAsync` and `PromoteBatchAsync` | It accepts a local `FundCode`, local symbols, model name and local target rows, then creates the local `ModelRun` and `TargetWeight` authority. Canonical post-Risk input must not recreate its target through this boundary. |
| Local `ModelRun` / `TargetWeight` records | `OPERATIONAL_PROJECTION_OR_AUDIT` | `src/QQ.Production.Intraday.Domain/DomainModels.cs`, `ModelRun`, `TargetWeight` | These are retained operational records. They contain `FundId`, NAV, frequency and quantity mode but do not carry the canonical Mandate/Risk identity, revision or fingerprint, so they cannot be canonical ownership or Risk authority. |
| Reference-data integrity check | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `ApplicationServices.cs`, `ProcessModelRunService.ProcessAsync`, `IReferenceDataIntegrityService.CheckAsync` | Ambiguous/disabled local fund, account, venue, instrument, mapping and control data must block local order-side processing. |
| Local fund/account/venue selection | `UNRESOLVED_STOP` | `ApplicationServices.cs`, `ProcessModelRunService.ProcessAsync` selects the enabled broker account and the venue named `LMAX` | The existing route does not receive an explicit effective-dated Mandate-to-Fund, canonical-Instrument-to-local-instrument/venue, or routing context. Reusing this selection would infer execution context and hard-code a venue. |
| Pre-trade position reconciliation | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `ApplicationServices.cs`, `ProcessModelRunService.ProcessAsync` and `Reconcile` | Internal/broker position mismatch blocks order-side processing and remains a retained Trading safety control. |
| Target sizing and drift | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `ApplicationServices.cs`, `TargetPositionCalculator.Calculate`, `ProcessModelRunService.ProcessAsync` | This is retained position-aware execution sizing/drift after a target; it does not establish Mandate ownership. It needs explicit local NAV, quantity-mode and local venue mapping context. |
| `RiskEngine.EvaluateDetailed` target/Risk decision | `DUPLICATE_TARGET_OR_RISK_AUTHORITY` | `ApplicationServices.cs`, `RiskEngine.EvaluateDetailed`, called by `ProcessModelRunService.ProcessAsync` | It creates a local `RiskDecision` from the local `TradeIntent`. Canonical Risk is already the approved-target authority for this path. |
| Kill switch, global trading, fund/instrument/venue enabled state, trading window, market-data freshness, quantity and local exposure limits | `UNRESOLVED_STOP` | `ApplicationServices.cs`, `RiskEngine.EvaluateDetailed` | The same method that creates the duplicate local Risk decision enforces these retained safety checks. Its required `RiskLimitSet` is selected using descending version/fallback lookup in `ProcessModelRunService.ProcessAsync`; canonical input and the retained route supply no explicit versioned pre-trade-control context. Extracting or selecting those controls would change accepted pre-trade semantics unless a separate decision defines the safe post-Risk boundary. |
| Parent/child order creation and venue send | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `ApplicationServices.cs`, `ProcessModelRunService.ProcessAsync`, `ParentOrder`, `ChildOrder`, `IVenueExecutionGateway.SendOrderAsync` | These are retained OMS/EMS and venue controls, but cannot be reached until the unresolved control-selection issue is resolved. |
| Post-trade reconciliation and fill/position audit | `OPERATIONAL_PROJECTION_OR_AUDIT` | `ApplicationServices.cs`, `ProcessModelRunService.ProcessAsync`, `Reconcile`, fill and position-ledger writes | These preserve retained post-order operational evidence; they do not define canonical ownership or Risk authority. |

## STOP condition

The required post-Risk route contains an `UNRESOLVED_STOP`. The retained processor has no explicit consumer-side execution-context resolver and couples the local Risk decision with retained kill-switch, trading-window, venue, staleness, quantity and exposure checks. It also resolves a local RiskLimitSet through an implicit highest-version/fallback lookup.

Implementing a new canonical entry by bypassing that method would silently disable retained safety. Calling it would perform a second local Risk decision and retain implicit control/context selection. Supplying a different control selection would change pre-trade semantics. Per Package #6B2, no parser, mapping resolver, projection or order-side entry is implemented past this stop without a separate human architecture decision that defines the versioned retained execution-context and pre-trade-control boundary.

The legacy `ModelWeightBatch -> ModelRun -> ProcessModelRunService` path is unchanged.
