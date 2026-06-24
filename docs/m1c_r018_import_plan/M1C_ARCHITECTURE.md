# M1C Architecture

M1C implements the execution-evidence seam only:

```text
R018/R216 artifacts -> normalized evidence -> lineage validation -> import plan
```

It deliberately does not implement the intent seam:

```text
Anubis weights -> ModelWeightBatch -> ModelRun -> TargetWeight
```

The code lives in `QQ.Production.Intraday.Application.R018ImportPlanning` and is pure/offline. File IO is isolated in `R018ArtifactBundleReader` and `R018ImportPlanSerializer`; mapping and validation are deterministic and do not call DB, broker, AccountAPI, Databento, R018, R216, R009, or `ProcessModelRunService`.

The CLI in `tools/QQ.Production.Intraday.Tools.R018ImportPlan` is a thin wrapper around the application service and requires `--no-db --no-network`.
