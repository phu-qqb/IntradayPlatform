# Architecture review 5023147928 amendment

This addendum supersedes the combined-control row in
`CANONICAL_POST_RISK_CONSUMER_ADAPTATION_V0_1.md`.

| Retained check | Classification | Evidence | Reason |
|---|---|---|---|
| Kill switch; global/local trading state; venue/instrument enabled state; position reconciliation; market-data freshness; trading window/no-new-orders cutoff; OMS/EMS venue/order safety | `CONTEXT_SPECIFIC_TRADING_SAFETY` | `ApplicationServices.cs`, `RiskEngine.EvaluateDetailed` and `ProcessModelRunService.ProcessAsync` | Retained post-Risk Trading release controls; no later canonical entry may silently disable them. |
| Minimum quantity; maximum trade notional; maximum instrument exposure; maximum gross exposure | `UNRESOLVED_STOP` | `ApplicationServices.cs`, `RiskEngine.EvaluateDetailed`; `ProcessModelRunService.ProcessAsync` highest-version/fallback `RiskLimitSet` selection | Philippe must decide whether these are explicit versioned non-target-modifying release controls or retired/bypassed where canonical Risk is authoritative. |

The amendment implements only the safe preceding v1 parser/fingerprint validator,
in-memory idempotency receipt boundary, and explicit in-memory execution-context
resolver. It does not implement an order-side entry, call the legacy promotion or
processor, create a local target/Risk authority, or select any production persistence.
