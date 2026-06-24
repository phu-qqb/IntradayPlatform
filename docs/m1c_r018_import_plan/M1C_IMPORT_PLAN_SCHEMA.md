# M1C Import Plan Schema

Schema version: `r018_intraday_import_plan_v1`

Required output files:

- `bundle_manifest.json`
- `validation_report.json`
- `normalized_events.jsonl`
- `lineage_report.json`
- `identity_scope_report.json`
- `import_plan_v1.json`
- `parity_report.csv`
- `human_summary.md`

The plan is staging evidence, not a new target source. It never creates `ModelRun`, `TargetWeight`, `TargetPosition`, `DriftSnapshot`, or `PositionLedgerEvent` in M1C.

Statuses:

- `EvidenceOnly`
- `CanonicalLinked`
- `Rejected`

`CanonicalLinked` requires an explicit `model_run_id` or a unique offline catalog match. Date/symbol/size heuristics are intentionally insufficient.
