# Qubes Upstream Local Audit R001

## 1. Scope

This audit is local-repository only, rooted at `C:\Users\phili\source\repos\QQ.Production.Intraday`.

No remote or GitHub repository was fetched, inspected, or assumed to match this working tree. Any prior/outdated remote analysis is explicitly excluded and is not authoritative for this document.

This is an audit-only document. No source code, schemas, validators, runners, market data, R009, LMAX, orders, fills, ledger, accounting, execution, broker, production trading, or production-readiness paths were modified for this audit.

## 2. Search Method

Commands and patterns used locally:

```powershell
git status --short
rg --files -g '!**/.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/node_modules/**' -g '!**/dist/**' | rg -i "qubes|qube|r005|marketdatasnapshot|snapshot|weight|allocation"
rg -n "QubesRunId|MarketDataSnapshotId|SandboxQubesPrototype|qubes-operationalization|prototype-output|5AB433" src tests docs scripts data artifacts --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/dist/**'
rg -n "MarketDataSnapshotId|marketDataSnapshotId|sandboxMarketDataSnapshotId|pmsApprovedQubesRunId|QubesRunIdNotPmsApprovedEconomicOutput|NotPmsApprovedEconomicOutput" src tests docs scripts artifacts data --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/dist/**'
rg -n "SandboxQubesSnapshotType|SandboxQubesInputSnapshot|SandboxQubesOutput|SandboxQubesPrototypeRunner|Run\(|MarketDataSnapshotId|WeightUnits|SandboxOnly|NotProduction|NotAccounting|NotExecuted|NotLedgerCommit|ValidateSnapshot|PrototypeDeterministicInputSnapshot" src\QQ.Production.Intraday.Application\SandboxQubesPrototype.cs
rg -n "QubesFxWeightsIngestionRequest|RawLines|QubesFxWeightsFixtureIngestionService|ParseNormalizeAndMap|CreateFakeModelWeightBatchRequest|ModelWeightSourceSystem.Qubes" src\QQ.Production.Intraday.Application\QubesFxWeightsIngestion.cs
rg -n "QubesRunId|TargetWeightsBatch|TargetWeightsBatchValidator|TargetWeight|CadenceMinutes|TargetPortfolioSnapshotFactory" src\QQ.Production.Intraday.Domain\PmsEmsOmsFoundation.cs
rg -n "MarketDataSnapshotId|MarketDataSnapshot\(|QubesWeightAuditBatch|QubesRawWeightAuditRow|QubesNormalizedWeightAuditRow|ModelWeightSourceSystem|ModelWeightBatch|TargetWeight\(" src\QQ.Production.Intraday.Domain\DomainModels.cs
rg -n "runnerStatus|runnerType|SandboxQubesPrototype|readsExternalApi|readsLiveMarketData|mutatesDb|sandboxOnly|notProduction|notAccounting|marketDataSnapshotId|inputSnapshotId|PROTOTYPE_DETERMINISTIC_INPUT_SNAPSHOT|PROTOTYPE_ONLY_NOT_PRODUCTION_QUBES|NOT_BOUND_PROTOTYPE_INPUT_NO_MARKETDATA|outputArtifactHashSha256|sandboxQubesRunId|qubesOutputId|production-readiness|accounting-attribution|BLOCKED" artifacts\readiness\qubes-operationalization-r005 --glob '*.json'
```

Included directories: `src`, `tests`, `docs`, `scripts`, `data`, `artifacts`, plus file inventory from the repository root.

Excluded directories for noise/dependency control: `.git`, `bin`, `obj`, `node_modules`, `dist`.

Required search terms were covered: `Qubes`, `Qube`, `qubes`, `SandboxQubesPrototype`, `QubesRunId`, `MarketDataSnapshotId`, `input snapshot`, `weights`, `economic weights`, `target weights`, `portfolio weights`, `allocation`, `R005`, `qubes-operationalization`, `prototype-output`, and `5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A`.

## 3. Findings

### Real Engine

Real Qubes upstream/core engine: not found locally.

The local repository contains Qubes-named ingestion, normalization, persistence, paper/sandbox consumers, and fixture workflows. The inspected code does not contain a real upstream optimizer/core engine that reads an identified operational input snapshot and produces economic weights from market prices, returns, covariance, signals, or risk inputs.

### Prototype

Prototype found locally.

`SandboxQubesPrototypeRunner` exists and deterministically copies local prototype signals into output weights. It labels its output as `SandboxQubesPrototype`, `PrototypeSignalWeight`, `SandboxOnly`, `NotProduction`, `NotAccounting`, `NotExecuted`, and `NotLedgerCommit`.

### Input Snapshot

Real local Qubes input snapshot: not found locally.

The only R005 input contract found is a prototype deterministic input snapshot. It explicitly records `marketDataSnapshotId: null`, `NOT_BOUND_PROTOTYPE_INPUT_NO_MARKETDATA`, and `PROTOTYPE_ONLY_NOT_PRODUCTION_QUBES`.

### MarketDataSnapshotId

Generic `MarketDataSnapshotId` exists in the domain and SQL persistence model. A real Qubes-bound `MarketDataSnapshotId` was not found. R005 and related PMS/Qubes artifacts keep it null.

### Run-To-Market-Data Binding

Real `QubesRunId` to `MarketDataSnapshotId` binding: not found locally.

The local Qubes audit persistence binds `QubesRunId` to Qubes audit rows, model-weight batches, promoted model runs, and target-weight instrument linkage. It does not bind `QubesRunId` to `MarketDataSnapshotId`.

### Production/Accounting Treatment

No local evidence was found that production/accounting paths correctly treat R005 as production Qubes. The local R005 artifacts and policy gates instead preserve explicit blockers: R005 is sandbox/prototype-only, production readiness is `BLOCKED`, accounting attribution is `BLOCKED`, and Qubes lineage is warning-only/not PMS-approved economic output.

## 4. Evidence Table

| Finding | File | Lines | Symbol | Classification | Notes |
|---|---|---:|---|---|---|
| Prototype runner exists | `src\QQ.Production.Intraday.Application\SandboxQubesPrototype.cs` | 111-144 | `SandboxQubesPrototypeRunner.Run` | prototype/sandbox | Emits `sandbox-qubes-prototype-r005-*`, `qubes-operationalization-r005:prototype-output:*`, copies `SignalWeight` to output, and sets sandbox/not-production/not-accounting flags. |
| Prototype input contract only | `src\QQ.Production.Intraday.Application\SandboxQubesPrototype.cs` | 5-37, 147-155 | `SandboxQubesInputSnapshot`, `ValidateSnapshot` | prototype/sandbox | Accepted snapshot types include prototype/local sandbox labels; runner rejects anything except `PrototypeDeterministicInputSnapshot`. |
| Prototype output is not economic/core engine output | `src\QQ.Production.Intraday.Application\SandboxQubesPrototype.cs` | 126-136 | `SandboxQubesOutputWeight`, `WeightUnits` | prototype/sandbox | Output weights are ordered signal weights with `WeightUnits: "PrototypeSignalWeight"`, not calculated economic weights from market/risk snapshots. |
| Prototype PMS candidate remains non-production | `src\QQ.Production.Intraday.Application\SandboxQubesPrototype.cs` | 266-305 | `SandboxQubesPmsIntentCandidateFactory.CreatePreviewOnlyCandidate` | prototype/sandbox | Candidate copies prototype identifiers and preserves `SandboxOnly`, `NotProduction`, `NotAccounting`, `NotExecuted`, and `NotLedgerCommit`. |
| Qubes weight ingestion consumes supplied rows | `src\QQ.Production.Intraday.Application\QubesFxWeightsIngestion.cs` | 7-16, 67-76, 95-116 | `QubesFxWeightsFixtureIngestionService.ParseNormalizeAndMap` | fixture/test-support style application logic | Input is caller-provided `RawLines`; parsing expects rows shaped as `<BloombergTicker>;<weight>`. No upstream input snapshot loader or optimizer is present. |
| Ingestion maps rows to fake model-weight batch request | `src\QQ.Production.Intraday.Application\QubesFxWeightsIngestion.cs` | 146-162 | `CreateFakeModelWeightBatchRequest` | fixture/sandbox/paper support | Successful parsed rows become a `CreateFakeModelWeightBatchRequest` with `ModelWeightSourceSystem.Qubes`; this is downstream mapping, not true Qubes generation. |
| Generic target-weight contract exists | `src\QQ.Production.Intraday.Domain\PmsEmsOmsFoundation.cs` | 5-41, 56-100 | `QubesRunId`, `TargetWeightsBatch`, `TargetWeightsBatchValidator` | domain contract | Validates a target-weight batch already carrying weights. It does not define real Qubes input snapshots or bind market data snapshots. |
| Generic market-data ID exists | `src\QQ.Production.Intraday.Domain\DomainModels.cs` | 58-60, 811-835 | `MarketDataSnapshotId`, `MarketDataSnapshot` | production domain storage | The repository has generic market-data snapshot storage, but this is not evidence of a Qubes-bound market-data snapshot. |
| Qubes audit persistence lacks MarketDataSnapshotId binding | `src\QQ.Production.Intraday.Application\QubesWeightPersistence.cs` | 13-28, 53-93 | `PersistQubesWeightsRequest`, `QubesWeightPersistenceService.PersistAsync` | audit/persistence | Persists Qubes run, raw rows, normalized rows, model batch/run linkage, and target-weight instrument linkage. No `MarketDataSnapshotId` is accepted or stored. |
| SQL persistence indexes QubesRunId only for audit batch | `src\QQ.Production.Intraday.Infrastructure.SqlServer\IntradayDbContext.cs` | 21-26, 178-181, 258-264, 1048-1074 | `QubesWeightAuditBatches`, `SqlServerQubesWeightAuditRepository` | production persistence | Qubes audit tables are separate from `MarketDataSnapshots`; no QubesRunId-to-MarketDataSnapshotId relationship is configured. |
| Paper diff consumes normalized weights | `src\QQ.Production.Intraday.Application\QubesTheoreticalPortfolioDiff.cs` | 82-173 | `QubesTheoreticalPortfolioDiffService.CreateDiff` | paper/theoretical | Requires already-normalized Qubes weights and builds theoretical target portfolio/diff; it explicitly does not use live broker state or create executable orders. |
| Second-cycle workflow is no-external fixture | `src\QQ.Production.Intraday.Application\QubesSecondCyclePaperBaseline.cs` | 224-270, 597-614 | `PaperBaselineSecondCycleService.RunSecondCycleAsync` | paper/fixture | Uses `rawQubesLines`, `QubesFxWeightsFixtureIngestionService`, fake batch creation, fixture marks, `UsedLiveMarketData: false`, `CalledBrokerGateway: false`, `SubmittedOrders: false`, `MutatedProductionLedgerState: false`. |
| Local Qubes fixture weights exist | `data\qubes-fixtures\r009\current-qubes-weights.txt` | 1-3 | raw weight rows | fixture data | Contains semicolon-delimited weight rows such as `NZDUSD Curncy;-0.119864`; it is an output/fixture file, not an upstream input snapshot or engine. |
| Test fixture weights exist | `tests\fixtures\qubes-fx\qubes-fx-weights-r002-sample.csv` | 1-17 | raw weight rows | test-only fixture | Contains sample Qubes FX weight rows for ingestion tests; not a production output contract or upstream input snapshot. |
| R005 output is prototype-only and MarketDataSnapshotId is null | `artifacts\readiness\qubes-operationalization-r005\phase-qubes-operationalization-r005-qubes-output.json` | 4-7, 30-35 | R005 output artifact | documentation/artifact | Contains known prototype run/output/hash, `marketDataSnapshotId: null`, `runnerType: SandboxQubesPrototype`, and sandbox/not-production/not-accounting flags. |
| R005 input contract is prototype deterministic | `artifacts\readiness\qubes-operationalization-r005\phase-qubes-operationalization-r005-input-snapshot-contract.json` | 4-6, 28, 36-38 | R005 input snapshot contract artifact | documentation/artifact | `snapshotType` is `PROTOTYPE_DETERMINISTIC_INPUT_SNAPSHOT`; `marketDataSnapshotId` is null; status is `PROTOTYPE_ONLY_NOT_PRODUCTION_QUBES`. |
| R005 runner evidence denies external/live/db behavior | `artifacts\readiness\qubes-operationalization-r005\phase-qubes-operationalization-r005-runner-adapter-evidence.json` | 3-14 | R005 runner evidence artifact | documentation/artifact | Runner type is `SandboxQubesPrototype`; `readsExternalApi`, `readsLiveMarketData`, and `mutatesDb` are false. |
| R005 production/accounting contracts blocked | `artifacts\readiness\qubes-operationalization-r005\phase-qubes-operationalization-r005-contract-status-update.json` | 8, 53-60 | R005 contract statuses | documentation/artifact | Notes the runner is not production Qubes; accounting attribution and production readiness are `BLOCKED`. |
| R004 local artifact remains negative evidence | `artifacts\readiness\pms-qubes-true-sandbox-runner-input-r004\phase-pms-qubes-true-sandbox-runner-input-r004-summary.md` | 5, 10-11, 41-42 | R004 summary artifact | documentation/artifact | Local artifact states no true Qubes-driven sandbox PMS handoff, no real local Qubes input snapshot, and no MarketData/input snapshot binding. This audit independently re-ran local searches and found the same condition. |
| R004 gate prevents invented runner/input IDs | `scripts\check-pms-qubes-true-sandbox-runner-input-r004-gate.ps1` | 124, 175, 278 | R004 gate checks | gate/test script | Fails if snapshot IDs are invented or if true dry-run/new QubesRunId are claimed. This is negative guard evidence, not a real engine. |

## 5. Decision

prototype only

## 6. R005 Classification

R005 is prototype-only.

R005 is rejected for production and accounting use. The local code and artifacts classify it as `SandboxQubesPrototype`, `PROTOTYPE_DETERMINISTIC_INPUT_SNAPSHOT`, `PROTOTYPE_ONLY_NOT_PRODUCTION_QUBES`, `NOT_BOUND_PROTOTYPE_INPUT_NO_MARKETDATA`, `sandboxOnly`, `notProduction`, `notAccounting`, `notExecuted`, and `notLedgerCommit`.

The known R005 identifiers found locally are:

- Run id: `sandbox-qubes-prototype-r005-20251217T020000Z-001`
- Output id: `qubes-operationalization-r005:prototype-output:20251217T020000Z:001`
- Output/source hash references: `5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A`

## 7. Required Negative Evidence

- Real Qubes engine: not found locally.
- Real local Qubes input snapshot: not found locally.
- Real Qubes-bound `MarketDataSnapshotId`: not found locally. Generic market-data IDs exist, but R005/Qubes binding remains null.
- `QubesRunId` to `MarketDataSnapshotId` binding: not found locally.
- Production Qubes weights output contract: not found locally. Found contracts consume or persist already-supplied weights; they do not define a production upstream Qubes output produced from identified input snapshots.
- Non-prototype runner: not found locally. Found runner is `SandboxQubesPrototype`; other Qubes surfaces are ingestion, fixture, paper, audit, or documentation artifacts.

## 8. Forbidden Changes Confirmation

Confirmed: this audit did not modify R009, LMAX, orders, fills, ledger, accounting, execution, broker, production trading, or production-readiness paths.

The only intended repository content change for this audit is this document: `docs\qubes\QUBES-UPSTREAM-LOCAL-AUDIT-R001.md`.
