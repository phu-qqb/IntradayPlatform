# Canonical post-Risk consumer adaptation v0.1 — Part C classification

Canonical authority: `quantum-qb/QQ.Investment.Platform@f2401b0727c54103ba308b5d7b1b0296952ff060`, ADR-007. Retained starting checkpoint: `phu-qqb/IntradayPlatform@d611ded8ae80debafe786b77f5c859a3b2d6e0e6`. Part C changes are adapter-only and carry canonical Risk identity, recorded time, knowledge cutoff, and provenance into the freshness gate.

| Retained check | Classification for canonical path | OBSERVED evidence and disposition |
|---|---|---|
| `ModelWeightPromotionService` local batch promotion and `ModelRun`/`TargetWeight` construction | `DUPLICATE_TARGET_OR_RISK_AUTHORITY` | `src/QQ.Production.Intraday.Application/ApplicationServices.cs`, `ModelWeightPromotionService.PromoteBatchAsync`; not invoked by the canonical entry. |
| Local `RiskDecision` and `RiskEngine.EvaluateDetailed` | `DUPLICATE_TARGET_OR_RISK_AUTHORITY` | `ApplicationServices.cs`, `RiskEngine.EvaluateDetailed`, called by `ProcessModelRunService.ProcessAsync`; not invoked by the canonical entry. |
| Minimum executable quantity | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `RiskEngine.EvaluateDetailed`, `instrumentLimit.MinTradeQuantity`; ADR-007 non-target-modifying release control, explicitly resolved by Part C. |
| Maximum per-order notional | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `RiskEngine.EvaluateDetailed`, `instrumentLimit.MaxTradeNotionalUsd` / `venueLimit.MaxTradeNotionalUsd`; ADR-007 non-target-modifying release control; above threshold blocks without slicing. |
| Maximum instrument and gross exposure | `DUPLICATE_TARGET_OR_RISK_AUTHORITY` | `RiskEngine.EvaluateDetailed`, `instrumentLimit.MaxExposureUsd` and `limitSet.MaxGrossExposureUsd`; not invoked locally on the canonical path. |
| Kill switch, trading window/no-new-orders, market freshness, position reconciliation, instrument/venue enabled state, OMS/EMS safety | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `RiskEngine.EvaluateDetailed` and `ProcessModelRunService.ProcessAsync`; canonical Part C keeps explicit fail-closed checks. |

The legacy `ModelWeightBatch -> ModelRun -> ProcessModelRunService` route is unchanged. Its existing classifications and local Risk behavior remain legacy behavior. The Part C `CanonicalPostRiskConsumptionService` is the single public canonical-wire entry: it records the validated receipt before obtaining retained state and bound freshness evidence, then uses the shared retained sizing primitive. An in-memory-only dormant boundary materializes retained `ParentOrder` and `ChildOrder` evidence. No persistence, OMS/EMS gateway call, live venue send, exposure calculation, or local Risk decision is added.
