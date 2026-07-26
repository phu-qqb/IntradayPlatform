# Hedge Fund Institutional Reporting Roadmap

## 1. Document Identity

| Field | Value |
| --- | --- |
| Title | Hedge Fund Institutional Reporting Roadmap |
| ManifestId | `hedge_fund_institutional_reporting_roadmap` |
| ManifestVersion | `v1` |
| Status | `AUTHORITATIVE_REPORTING_ROADMAP` |
| Repository | `phu-qqb/IntradayPlatform` |
| CreatedAtUtc | `2026-07-26T00:00:00Z` |
| CurrentMasterAtCreation | `60c79bfbd5827919eaf1299e045ef9918baef720` |
| ReportingScope | Anubis, INFX7-INFX10, PMS PostgreSQL, execution and institutional reporting |
| AuthorityRelationship | Reporting implementation authority constrained by the general QQ Fund Platform coverage authority |

The general roadmap `QQ_Fund_Platform_Objectifs_Restants.md` was supplied by
program leadership as the global coverage and non-regression authority. It was
not versioned in this repository when this manifest was created. Its absence
from Git does not weaken its requirements and does not authorize a substitute
or reconstructed copy.

## 2. Relationship With The General Manifest

The general manifest defines global coverage, safety constraints and
non-regressions. This precise manifest defines the reporting architecture,
phases, data contracts, deliverables and implementation order.

This manifest cannot weaken the general safeguards. When the general manifest
does not specify an implementation detail, this manifest governs reporting.
When rules conflict on safety, authority or evidence, the most restrictive
rule prevails.

## 3. Reporting Objectives

The reporting system serves trading, portfolio management, risk management,
operations, control, management, the investment committee, investors,
institutional partners including JPM, audit and future execution calibration.

Reporting is not merely a presentation layer. Every result preserves:

- lineage and source authority;
- as-of time and reporting period;
- formula and contract version;
- evidence and content SHA-256;
- availability and data-quality status;
- breaks and caveats;
- deterministic reproducibility.

## 4. Non-Negotiable Principles

- There is no second economic truth, second PMS or second OMS.
- No ModelRun, Fill, position or ledger event may be invented.
- Missing, absent or unknown data is never converted to zero.
- Performance is never reconstructed without a complete versioned contract.
- Real TCA requires real authoritative Fills and benchmarks.
- A broker position is never inferred from UI evidence.
- AUM and NAV are unusable until a versioned authority exists.
- `TargetCloseUtc` remains a first-class field.
- Unknown working leaves remain unknown.
- Corrections are append-only.
- Timestamps are UTC and decimals use invariant culture.
- Outputs are deterministic and content-addressed with SHA-256.
- Reports contain no secrets.
- Databento is prohibited: no Databento data, download or API request.

## 5. Four-Layer Architecture

### 5.1 `reporting_source`

`reporting_source` exposes canonical facts read-only with identity, lineage,
authority, timestamps and evidence SHA. It performs no silent economic
transformation.

Current and planned sources include Ingestions, QubesInputSnapshots,
ModelRuns, TargetWeights, TargetPositions, PositionOnlyDrifts, market
observations, economic revisions, TradeIntents, RiskDecisions, ParentOrders,
ChildOrders, ARCH7B lifecycle facts, ExecutionReports, Fills,
PositionLedgerEvents, reconciliations, breaks and future versioned
NAV/AUM/cash/cost authorities.

### 5.2 `reporting_mart`

`reporting_mart` produces versioned institutional metrics with explicit
grains, formulas, controlled aggregations, stable dimensions and reproducible
as-of semantics. The mart is never a source authority, and a derived metric
never replaces its source fact.

### 5.3 `reporting_control`

`reporting_control` owns data quality, completeness, freshness, metric
availability, breaks, formula validation, reconciliations, thresholds,
publication status, supersession and deterministic evidence.

### 5.4 `reporting_publication`

`reporting_publication` creates versioned publications with audience, period,
as-of, status, file inventory, SHA-256, repository commit, source snapshot,
manifest identity and immutable published artifacts.

Publication statuses are `DRAFT`, `REVIEWED`, `APPROVED`, `PUBLISHED` and
`SUPERSEDED`.

## 6. Delivery Phases

### RPT1 - Operational Reporting And Breaks

Status: `COMPLETED`.

Evidence:

- PR `#39`;
- merge commit `60c79bfbd5827919eaf1299e045ef9918baef720`;
- master bundle `e3b8f026a1978463e9aaac24e8a7a751e6d5d475ebf775bdfa16f5659b9a37f2`.

RPT1 covers the operational calendar, selected INFX7-INFX10 models, source
lineage, slots, economic revisions, FX net lines, strategy contributions,
ARCH7A, ARCH7B, reconciliation, breaks, code inventory, deterministic bundles
and RDS read-only access.

### RPT2 - Performance And Risk Mart

Status: `IN PROGRESS`.

RPT2 builds institutional PMS, portfolio, risk, performance, attribution,
cost and TCA metrics with explicit availability and authority:

- A: metrics immediately provable from current facts;
- B: metrics derivable under versioned formulas and explicit caveats;
- C: metrics blocked until new source authorities exist.

Planned metrics include target and current exposures, gross/net, notionals,
concentration, target and executed turnover, leverage, attribution by INFX,
pair and session, PnL, gross and net performance, volatility, Sharpe, Sortino,
drawdown, recovery, hit rate, profit factor, costs, slippage, implementation
shortfall, TCA, capacity, live versus backtest, live versus expectation and
actual execution versus cost model.

An unavailable metric is never presented as available and never receives a
fabricated numeric value.

### RPT3 - Daily Management Pack

Status: `NOT STARTED`.

The Daily Management Pack will include an executive summary, operational
status, calendar, INFX models, exposure, risk, available performance,
available execution and TCA, breaks, incidents, reconciliation,
infrastructure, decisions and actions.

Target formats are HTML, PDF, Excel/CSV, Power BI data and a versioned
publication.

### RPT4 - Monthly Investment Committee Pack

Status: `NOT STARTED`.

The Monthly Investment Committee Pack will cover performance, attribution,
risk, drawdowns, stability, exposure, turnover, costs, TCA, capacity, live
versus backtest, live versus expectation, management commentary, material
events, data quality, material breaks, decisions and auditable appendices.

### Institutional Publications

Status: `NOT STARTED`.

Planned publications include investor reporting, JPM reporting, other
partner reports, audit exports, PDF, PPTX, XLSX and machine-readable JSON/CSV.
Every publication is immutable, versioned, content-addressed and explicitly
superseded when replaced.

## 7. Metric Authority Contract

Every metric carries:

| Field | Contract |
| --- | --- |
| MetricCode | Stable uppercase identifier |
| MetricVersion | Version of the metric contract |
| Category | Portfolio, risk, performance, cost, TCA or control |
| Grain | Exact dimensional grain |
| AsOfUtc | Reproducible observation time |
| PeriodStartUtc / PeriodEndUtc | Explicit period or NULL |
| Value | Decimal value or explicit NULL |
| Unit / Currency | Explicit unit and currency, or NULL |
| FormulaVersion | Versioned formula identity |
| SourceFacts / SourceIds / SourceTables | Complete source lineage |
| EvidenceSha256 | Content evidence or NULL with caveat |
| AuthorityStatus | Source authority assessment |
| AvailabilityStatus | Metric availability assessment |
| DataQualityStatus | Quality assessment |
| Caveats | Explicit limitations |

`AuthorityStatus` is one of `PROUVÉ`, `PROBABLE`, `INCONNU`, `ABSENT`,
`OBSOLÈTE`.

`AvailabilityStatus` is one of `SOURCE_PROVEN`, `DERIVABLE_PROVEN`,
`DERIVABLE_PROBABLE`, `BLOCKED_MISSING_SOURCE`,
`BLOCKED_AUTHORITY_UNPROVEN`, `NOT_APPLICABLE`, `UNKNOWN`.

## 8. Dependency Matrix

| Metric | Required source | Current availability | Activation phase | Current blocker | Required authority |
| --- | --- | --- | --- | --- | --- |
| Target exposure | TargetPositions and qualified revision | SOURCE_PROVEN | RPT2 | None for target-only exposure | PMS economic revision |
| Target turnover | Successive qualified revisions | DERIVABLE_PROVEN | RPT2 | Requires two revisions | PMS economic revisions |
| Current broker position | Broker position fact | BLOCKED_MISSING_SOURCE | RPT2+ | No authoritative broker position | Broker position authority |
| Realized PnL | Fills and accounting ledger | BLOCKED_MISSING_SOURCE | RPT2+ | No authoritative Fills/ledger | Execution and accounting authority |
| Unrealized PnL | Authoritative position and qualified marks | BLOCKED_MISSING_SOURCE | RPT2+ | Position authority absent | Position and mark authority |
| Net performance | NAV/AUM and fees | BLOCKED_MISSING_SOURCE | RPT2+ | NAV/AUM/fee authority absent | Fund accounting authority |
| Live TCA | Real Fills and versioned benchmarks | BLOCKED_MISSING_SOURCE | RPT2+ | No real Fill authority | Broker execution authority |
| Leverage | Exposure and authoritative NAV/AUM | BLOCKED_MISSING_SOURCE | RPT2+ | NAV/AUM absent | Fund accounting authority |
| Capacity used | AUM and capacity contract | BLOCKED_MISSING_SOURCE | RPT2+ | AUM and capacity contract absent | Capacity authority |
| Live versus backtest | Live performance and comparison contract | BLOCKED_MISSING_SOURCE | RPT2+ | Live performance absent | Performance authority |

## 9. Power BI And Grains

Dimensions include date/time, economic revision, slot, strategy, ModelRun,
instrument, canonical pair, currency, account scope, metric, authority,
availability and publication.

Fact tables use explicit grains and logical keys. Account-level facts are not
repeated on strategy-level rows. Additive metrics may be summed only across
their allowed dimensions. Semi-additive metrics preserve their as-of
dimension. Ratios, concentrations, Sharpe, Sortino and drawdown are
non-additive.

All times are UTC. Missing values use explicit `NULL`. Decimals use invariant
culture. CSV grains, keys, relations, units, currencies, null policy and
as-of behavior are versioned with each output contract.

## 10. Publication And Immutability

Every publication records:

- deterministic `PublicationId`;
- `ReportType`, `Audience`, `Period` and `AsOfUtc`;
- `SourceSnapshotId` and `RepositoryCommit`;
- `ManifestVersion` and all `FormulaVersions`;
- publication `Status`;
- `Files`, per-file `FileSha256` and `BundleSha256`;
- `CreatedAtUtc`, optional `ApprovedAtUtc`;
- optional `SupersedesPublicationId`;
- `NoSecrets=true`;
- `ImmutableAfterPublished=true`.

## 11. General Manifest Traceability

| General coverage | Reporting materialization |
| --- | --- |
| General reporting | RPT1, RPT2, RPT3, RPT4 |
| Trading and execution | RPT1, RPT2, RPT3 |
| TCA | RPT2, RPT3, RPT4 |
| PMS and portfolio | RPT2, RPT3 |
| Risk | RPT2, RPT3, RPT4 |
| Operations | RPT1, RPT3 |
| Fund, management and investors | RPT2, RPT3, RPT4, institutional publications |
| Dashboard | RPT1/RPT3 and Power BI consumption |
| Reconciliation | RPT1, RPT2, RPT3 |
| Audit | All phases and `reporting_publication` |

The traceability matrix preserves reporting, trading/execution, TCA,
PMS/portfolio, risk, operations, fund/management/investor, dashboard,
reconciliation and audit coverage from the unversioned general authority.

## 12. Phase Completion Criteria

RPT2 is complete only when the metric catalog is complete,
authority/availability statuses are exact, formulas are versioned, marts are
deterministic, Power BI grains are documented, unavailable metrics are not
invented, tests and double qualification are green, data-quality breaks are
reported and the technical publication is reproducible.

RPT3 is complete only when the daily pack is deterministic, current data and
caveats are explicit, prior-day comparison, breaks and reconciliation are
included, and publication is versioned.

RPT4 is complete only when the monthly pack is deterministic, periods are
locked, performance/risk/attribution and appendices are complete, commentary
is versioned, review/approval is recorded and publication is immutable.

## 13. Current State And Next Action

| Phase | Status |
| --- | --- |
| RPT1 | COMPLETED |
| RPT2 | IN PROGRESS |
| RPT3 | NOT STARTED |
| RPT4 | NOT STARTED |
| Institutional publications | NOT STARTED |

Next action: `RPT2_METRIC_AUTHORITY_AND_AVAILABILITY_FOUNDATION`.
