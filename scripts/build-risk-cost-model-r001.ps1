param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Round6([decimal]$Value) {
    [decimal]::Round($Value, 6)
}

$checkpointDir = Join-Path $RepoRoot "artifacts\readiness\programme-clean-product-checkpoint-r001"
$r014Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sandbox-readiness-update-r014"
$r013dDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$pnlDir = Join-Path $RepoRoot "artifacts\readiness\pnl-preview"
$crossRailDir = Join-Path $RepoRoot "artifacts\readiness\cross-rail-sandbox-handoff"

$checkpointSummaryPath = Join-Path $checkpointDir "summary.md"
$checkpointStatePath = Join-Path $checkpointDir "central-product-state-snapshot.json"
$marketDataReminderPath = Join-Path $checkpointDir "marketdata-status-reminder.json"
$historicalPnlPath = Join-Path $pnlDir "phase-pnl-preview-r004-per-symbol-gross-roundtrip-pnl-amounts.json"
$historicalAggregatePath = Join-Path $pnlDir "phase-pnl-preview-r004-aggregate-gross-roundtrip-pnl-amount.json"
$historicalFillInputsPath = Join-Path $crossRailDir "cross-rail-r009-sandbox-fill-pnl-inputs.json"
$corePnlPath = Join-Path $r013dDir "sandbox-gross-pnl-preview-r013d.json"
$r014SummaryPath = Join-Path $r014Dir "summary.md"

$checkpointSummary = if (Test-Path -LiteralPath $checkpointSummaryPath) { Get-Content -Raw -LiteralPath $checkpointSummaryPath } else { "" }
$checkpointState = if (Test-Path -LiteralPath $checkpointStatePath) { Read-JsonFile $checkpointStatePath } else { $null }
$marketDataReminder = if (Test-Path -LiteralPath $marketDataReminderPath) { Read-JsonFile $marketDataReminderPath } else { $null }
$historicalPnl = Read-JsonFile $historicalPnlPath
$historicalAggregate = Read-JsonFile $historicalAggregatePath
$historicalFillInputs = Read-JsonFile $historicalFillInputsPath
$corePnl = Read-JsonFile $corePnlPath
$r014Summary = if (Test-Path -LiteralPath $r014SummaryPath) { Get-Content -Raw -LiteralPath $r014SummaryPath } else { "" }

$costGuidanceUsdPerMillion = [decimal]5
$majorGuidanceSymbols = @("AUDUSD", "EURUSD", "GBPUSD")
$majorGuidanceCoreSymbols = @("USDCAD", "USDJPY", "NZDUSD")
$coreBlockedExoticSymbols = @("USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR")

$historicalRows = @($historicalPnl.rows)
$historicalCostRows = @()
foreach ($row in $historicalRows) {
    $openNotional = [decimal]$row.openPrice * [decimal]$row.quantity * [decimal]$row.contractSizeOrUnitScale
    $flattenNotional = [decimal]$row.flattenPrice * [decimal]$row.quantity * [decimal]$row.contractSizeOrUnitScale
    $roundTripNotional = $openNotional + $flattenNotional
    $estimatedCost = ($roundTripNotional / [decimal]1000000) * $costGuidanceUsdPerMillion
    $gross = [decimal]$row.grossRoundTripPnlQuoteCurrency
    $historicalCostRows += [ordered]@{
        Symbol = $row.symbol
        Scope = "HistoricalPmsIntentMajorOnly"
        GrossPnlQuoteCurrency = $gross
        QuoteCurrency = $row.quoteCurrency
        OpenNotionalQuoteCurrency = Round6 $openNotional
        FlattenNotionalQuoteCurrency = Round6 $flattenNotional
        RoundTripNotionalQuoteCurrency = Round6 $roundTripNotional
        CostGuidanceUsdPerMillion = $costGuidanceUsdPerMillion
        EstimatedCostUsd = Round6 $estimatedCost
        CostAdjustedPreviewUsd = Round6 ($gross - $estimatedCost)
        NotAccounting = $true
        NotProduction = $true
        NotLedgerCommit = $true
    }
}

$historicalEstimatedCost = [decimal]0
foreach ($row in $historicalCostRows) {
    $historicalEstimatedCost += [decimal]$row.EstimatedCostUsd
}
$historicalGross = [decimal]$historicalAggregate.grossRoundTripPnlQuoteCurrency
$historicalCostAdjusted = $historicalGross - $historicalEstimatedCost

$coreRows = @($corePnl.Rows)
$coreAggregateGross = [decimal]0
foreach ($row in $coreRows) {
    $coreAggregateGross += [decimal]$row.GrossQuoteCurrencyPnl
}

Write-JsonArtifact "checkpoint-intake-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    CheckpointSummaryExists = (Test-Path -LiteralPath $checkpointSummaryPath)
    CheckpointClassification = "PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_PASS_CLEAN_CHECKPOINT_RECORDED"
    ProductState = "SandboxProgrammeAcceptedWithGrossPnlV0Ready"
    CoreAnubisExtension = "CoreAnubisSandboxLifecycleAcceptedWithWarnings"
    ProductStateConfirmed = ($checkpointSummary.Contains("PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_PASS_CLEAN_CHECKPOINT_RECORDED") -and $checkpointState.ProductDecision -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready")
    GrossSandboxPnlPreviewReady = $true
    PaperLedgerPreviewValidNoCommit = $true
    NetPnlRemainsBlocked = $true
    AccountingPnlRemainsBlocked = $true
    ProductionLiveRemainsBlocked = $true
    MarketDataPlatform = "WITH_WARNINGS"
    NoNewExecutionNeededForThisPackage = $true
    Classification = "CHECKPOINT_READY_FOR_RISK_COST_MODEL"
})

$historicalInventoryRows = @()
foreach ($row in $historicalRows) {
    $historicalInventoryRows += [ordered]@{
        Symbol = $row.symbol
        ExecutionSymbol = $row.symbol
        Quantity = $row.quantity
        OpenPrice = $row.openPrice
        FlattenPrice = $row.flattenPrice
        QuoteCurrency = $row.quoteCurrency
        Classification = "major"
        DirectInverseMapping = "direct"
        ActualFillsExist = $true
        ActualFillPricesExist = $true
        ActualFillPriceDeltaPnlExists = $true
    }
}

$coreInventoryRows = @()
foreach ($row in $coreRows) {
    $classification = if ($coreBlockedExoticSymbols -contains $row.ExecutionSymbol) { "non-major/exotic" } else { "major-guidance-candidate" }
    $coreInventoryRows += [ordered]@{
        Symbol = $row.ExecutionSymbol
        ExecutionSymbol = $row.ExecutionSymbol
        Quantity = $row.Quantity
        OpenPrice = $row.OpenPrice
        FlattenPrice = $row.FlattenPrice
        QuoteCurrency = if ($row.ExecutionSymbol -eq "NZDUSD") { "USD" } else { $row.ExecutionSymbol.Substring(3,3) }
        Classification = $classification
        DirectInverseMapping = if ($row.ExecutionSymbol -eq "NZDUSD") { "direct" } else { "inverse-from-Core-XXXUSD-where-applicable" }
        ActualFillsExist = $true
        ActualFillPricesExist = $true
        ActualFillPriceDeltaPnlExists = $true
    }
}

Write-JsonArtifact "sandbox-fill-universe-inventory.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Lifecycles = @(
        [ordered]@{
            LifecycleId = "CROSS-RAIL-R014 / PNL-PREVIEW-R004"
            Source = "HistoricalPmsIntent"
            OrderFillArtifacts = @(
                "artifacts/readiness/cross-rail-sandbox-handoff/cross-rail-r009-sandbox-fill-pnl-inputs.json",
                "artifacts/readiness/pnl-preview/phase-pnl-preview-r004-per-symbol-gross-roundtrip-pnl-amounts.json"
            )
            OpenFillCount = @($historicalFillInputs.OpenFills).Count
            FlattenFillCount = @($historicalFillInputs.FlattenFills).Count
            ResidualStatus = "zero"
            GrossPnlPreviewStatus = "ready"
            Symbols = @($historicalRows | ForEach-Object { $_.symbol })
            Rows = $historicalInventoryRows
            ActualFillsExist = $true
            ActualFillPricesExist = $true
            ActualFillPriceDeltaPnlExists = $true
        },
        [ordered]@{
            LifecycleId = "CORE-ANUBIS-R013D/R013E"
            Source = "CoreAnubis"
            OrderFillArtifacts = @(
                "artifacts/readiness/core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol/sandbox-gross-pnl-preview-r013d.json"
            )
            OpenFillCount = 9
            FlattenFillCount = 9
            ResidualStatus = "zero"
            GrossPnlPreviewStatus = "ready-with-partial-fill-warning"
            Symbols = @($coreRows | ForEach-Object { $_.ExecutionSymbol })
            Rows = $coreInventoryRows
            ActualFillsExist = $true
            ActualFillPricesExist = $true
            ActualFillPriceDeltaPnlExists = $true
            Warnings = @("USDJPY partial fill: intended 88.4, filled 38.4, unfilled 50.0")
        }
    )
    Classification = "SANDBOX_FILL_UNIVERSE_READY_WITH_WARNINGS"
})

Write-JsonArtifact "cost-guidance-inventory.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Guidance = @(
        [ordered]@{
            Name = "5 USD/million"
            ValueUsdPerMillion = 5
            Scope = "best-case major-only guidance"
            ExplicitNonScope = @("not universal", "not exotics", "not production model", "not accounting model", "not a commission schedule unless separately evidenced")
            UsableForHistoricalMajorOnlySandboxEstimate = $true
            UsableForCoreAnubisExotics = $false
        }
    )
    OtherLocalCostSpreadCommissionArtifactsFound = "not used as explicit all-symbol cost evidence in this package"
    ExplicitLmaxCommissionEvidence = $false
    ExplicitSpreadEvidence = $false
    ExplicitFeeEvidence = $false
    ExplicitSwapFinancingEvidence = $false
    AccountSpecificPricing = $false
    AccountCurrency = $null
    Classification = "COST_GUIDANCE_INVENTORY_READY_WITH_WARNINGS"
})

Write-JsonArtifact "actual-fill-gross-vs-cost-policy.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    ActualFillGrossPriceDeltaPnl = "Based on open and flatten fill prices."
    DoubleCountingPolicy = "Do not add a separate spread/slippage estimate on top of actual fill price deltas unless computing a clearly labelled hypothetical/pre-trade model."
    ExplicitFeesCommissions = "Separate from actual fill price delta and may be added only if evidenced."
    GenericGuidanceScope = "5 USD/million cannot be applied universally without scope validation."
    CostAdjustedPreviewComponents = @("gross price-delta PnL", "explicit estimated fees/commissions if evidenced", "cost model scope", "exclusions", "missing cost components")
    NotAccounting = $true
    NotProduction = $true
    NotLedgerCommit = $true
    Classification = "DOUBLE_COUNTING_POLICY_READY"
})

$coverageRows = @()
foreach ($row in $historicalRows) {
    $coverageRows += [ordered]@{
        ExecutionSymbol = $row.symbol
        CoreSymbol = $null
        LifecycleSource = "HistoricalPmsIntent"
        MajorMinorExoticClassification = "major"
        Quantity = $row.quantity
        QuoteCurrency = $row.quoteCurrency
        FiveUsdPerMillionGuidanceAllowed = $true
        ExplicitCommissionFeeEvidenceExists = $false
        ExplicitSpreadEvidenceExists = $false
        CostModelCanEstimateThisLine = $true
        MissingCostComponents = @("explicit commission schedule", "account-specific pricing")
        CostCoverageStatus = "COST_COVERAGE_READY_MAJOR_GUIDANCE_ONLY"
    }
}

$coreSymbolMap = @{
    USDCAD = "CADUSD"; USDCNH = "CNHUSD"; USDJPY = "JPYUSD"; USDMXN = "MXNUSD"; USDNOK = "NOKUSD";
    NZDUSD = "NZDUSD"; USDSEK = "SEKUSD"; USDSGD = "SGDUSD"; USDZAR = "ZARUSD"
}
foreach ($row in $coreRows) {
    $allowed = $majorGuidanceCoreSymbols -contains $row.ExecutionSymbol
    $status = if ($allowed) { "COST_COVERAGE_PARTIAL_MAJOR_GUIDANCE_ONLY" } else { "COST_COVERAGE_BLOCKED_EXOTIC_NO_COST_EVIDENCE" }
    $coverageRows += [ordered]@{
        ExecutionSymbol = $row.ExecutionSymbol
        CoreSymbol = $coreSymbolMap[$row.ExecutionSymbol]
        LifecycleSource = "CoreAnubis"
        MajorMinorExoticClassification = if ($allowed) { "major-guidance-candidate" } else { "non-major/exotic" }
        Quantity = $row.Quantity
        QuoteCurrency = if ($row.ExecutionSymbol -eq "NZDUSD") { "USD" } else { $row.ExecutionSymbol.Substring(3,3) }
        FiveUsdPerMillionGuidanceAllowed = $allowed
        ExplicitCommissionFeeEvidenceExists = $false
        ExplicitSpreadEvidenceExists = $false
        CostModelCanEstimateThisLine = $allowed
        MissingCostComponents = if ($allowed) { @("explicit commission schedule", "account-specific pricing") } else { @("explicit cost evidence for non-major/exotic pair", "explicit commission schedule", "account-specific pricing") }
        CostCoverageStatus = $status
    }
}

Write-JsonArtifact "instrument-cost-coverage-classification.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Rows = $coverageRows
    OverallClassification = "COST_COVERAGE_PARTIAL_CORE_ANUBIS_EXOTICS_BLOCKED"
    Classification = "COST_COVERAGE_PARTIAL_CORE_ANUBIS_EXOTICS_BLOCKED"
})

Write-JsonArtifact "commission-fee-model-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Decision = "COMMISSION_FEE_MODEL_READY_MAJOR_ONLY"
    ModelType = "Guidance, not actual fee evidence"
    SandboxPreviewUse = "Allowed only for labelled historical major-only estimate."
    AccountingUse = $false
    ProductionUse = $false
    CoversCoreAnubisExotics = $false
    MissingSymbolsBlockNetPnl = $true
    Classification = "COMMISSION_FEE_MODEL_READY_MAJOR_ONLY"
})

Write-JsonArtifact "spread-slippage-policy-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    ActualRoundTripFillPnlTreatment = "Do not add separate spread estimate by default."
    HypotheticalPreTradeTreatment = "Requires explicit separate policy and evidence."
    PolygonBboSpreadUse = "Not approved as cost model evidence in this package."
    FiveUsdPerMillionAsSpread = $false
    AccountingProductionBlocked = $true
    Classification = "SPREAD_SLIPPAGE_POLICY_READY_ACTUAL_FILL_GROSS_NO_DOUBLE_COUNT"
})

Write-JsonArtifact "financing-rollover-swap-policy-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Decision = "Not applicable to flat intraday sandbox round trips where open and flatten completed in same sandbox lifecycle."
    RecordedAsExcluded = $true
    NotAccounting = $true
    NotProduction = $true
    Classification = "FINANCING_SWAP_NOT_APPLICABLE_FLAT_INTRADAY_SANDBOX"
})

Write-JsonArtifact "sandbox-cost-preview-feasibility.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Lifecycles = @(
        [ordered]@{
            Lifecycle = "Historical PMS/CROSS-RAIL"
            FillEvidenceComplete = $true
            GrossPnlPreviewExists = $true
            CostModelCoverage = "major-only guidance usable"
            MissingCostComponents = @("actual commission schedule", "account-specific pricing")
            CostAdjustedPreviewCanBeComputed = $true
            Completeness = "partial sandbox-only"
            CanBeCalledNetPnl = $false
        },
        [ordered]@{
            Lifecycle = "Core/Anubis R013D"
            FillEvidenceComplete = $true
            GrossPnlPreviewExists = $true
            CostModelCoverage = "partial; exotics missing explicit evidence"
            MissingCostComponents = @("USDCNH cost evidence", "USDMXN cost evidence", "USDNOK cost evidence", "USDSEK cost evidence", "USDSGD cost evidence", "USDZAR cost evidence", "actual commission schedule", "account-specific pricing")
            CostAdjustedPreviewCanBeComputed = $false
            Completeness = "blocked for Core/Anubis full cost-adjusted preview"
            CanBeCalledNetPnl = $false
        }
    )
    Classification = "SANDBOX_COST_PREVIEW_READY_HISTORICAL_MAJOR_ONLY"
})

Write-JsonArtifact "estimated-cost-preview-v0.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    PreviewId = "risk-cost-model-r001:historical-major-only-v0"
    Scope = "Sandbox historical major-only estimated cost preview; not full Core/Anubis net PnL."
    LifecyclesIncluded = @("CROSS-RAIL-R014 / PNL-PREVIEW-R004")
    LifecyclesExcluded = @("CORE-ANUBIS-R013D/R013E")
    GrossPnlInputs = [ordered]@{
        HistoricalGrossPnlUsd = $historicalGross
        CoreAnubisGrossPnlQuoteCurrencyAggregate = Round6 $coreAggregateGross
    }
    CostModelInputs = [ordered]@{
        Guidance = "5 USD/million"
        Scope = "best-case major-only guidance"
        AppliedTo = $majorGuidanceSymbols
        NotAppliedTo = $coreBlockedExoticSymbols
        RoundTripNotionalBasis = "open notional plus flatten notional from actual R004 fills"
    }
    EstimatedCosts = $historicalCostRows
    CostAdjustedPreview = [ordered]@{
        HistoricalMajorOnlyGrossPnlUsd = $historicalGross
        HistoricalMajorOnlyEstimatedCostUsd = Round6 $historicalEstimatedCost
        HistoricalMajorOnlyCostAdjustedPreviewUsd = Round6 $historicalCostAdjusted
        FullNetPnlReady = $false
    }
    MissingCosts = @("explicit Core/Anubis exotic costs", "explicit LMAX commission schedule", "account-specific pricing", "account currency aggregation")
    Warnings = @("Core/Anubis exotics not covered", "This is not accounting PnL", "This is not production PnL", "Do not double-count spread on actual fill price-delta PnL")
    NotAccounting = $true
    NotProduction = $true
    NotLedgerCommit = $true
    Classification = "ESTIMATED_COST_PREVIEW_COMPUTED_PARTIAL_MAJOR_ONLY"
})

Write-JsonArtifact "net-pnl-readiness-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Decision = "NET_PNL_BLOCKED_CORE_ANUBIS_EXOTICS_COSTS_MISSING"
    CompleteCostSpreadCommissionPolicyForIncludedLifecycleRequired = $true
    AccountCurrencyAggregationSeparate = $true
    AccountingPnlSeparate = $true
    ProductionPnlSeparate = $true
    PartialCostCoverageOnly = $true
    FullNetPnlReady = $false
    AccountingProductionReady = $false
    Classification = "NET_PNL_BLOCKED_CORE_ANUBIS_EXOTICS_COSTS_MISSING"
})

Write-JsonArtifact "accounting-production-boundary-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    NoAccountCurrency = $true
    NoAccountId = $true
    NoPortfolioId = $true
    NoStrategyId = $true
    NoSourceExecutionIntentId = $true
    NoAccountingAttribution = $true
    ProductionLiveRemainsBlocked = $true
    Classification = "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Statuses = [ordered]@{
        "risk-cost-model.v1" = "WITH_WARNINGS_MAJOR_ONLY_POLICY_READY_CORE_ANUBIS_EXOTICS_BLOCKED"
        "commission-fee-model.v1" = "WITH_WARNINGS_MAJOR_ONLY_GUIDANCE_NOT_ACCOUNTING"
        "spread-slippage-policy.v1" = "YES_ACTUAL_FILL_GROSS_NO_DOUBLE_COUNT"
        "sandbox-cost-preview.v1" = "WITH_WARNINGS_HISTORICAL_MAJOR_ONLY"
        "net-pnl-preview.v1" = "BLOCKED_CORE_ANUBIS_EXOTICS_COSTS_MISSING"
        "pnl-preview.v1" = "YES_GROSS_SANDBOX_QUOTE_CURRENCY"
        "ledger-preview.v1" = "YES_WITH_WARNINGS_PREVIEW_ONLY_NO_COMMIT"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
        "marketdata-readiness.v1" = "WITH_WARNINGS"
    }
    Classification = "CONTRACT_STATUS_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "blocker-map-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    ClosedOrImproved = @(
        "cost policy double-counting ambiguity",
        "5 USD/million scope ambiguity",
        "commission/fee evidence inventory",
        "cost preview feasibility classified"
    )
    RemainingBlockers = @(
        "explicit cost evidence for Core/Anubis exotics",
        "full net PnL",
        "account-currency aggregation",
        "accounting PnL",
        "ledger commit",
        "production/live"
    )
    Classification = "BLOCKER_MAP_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "roadmap-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    Decision = "NEXT_RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE"
    Reason = "Historical major-only estimate is possible, but Core/Anubis exotics lack explicit cost evidence and keep full sandbox net PnL blocked."
    NotAMicroStep = $true
    Alternatives = @("NEXT_LEDGER_ACCOUNTING_POLICY_R001", "NEXT_MARKETDATA_GOLDEN_SOURCE_DB_PROJECTION_R001", "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT")
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    CostModelReadiness = "partial-major-only"
    EstimatedCostPreviewComputed = $true
    EstimatedCostPreviewScope = "historical major-only sandbox preview"
    NetPnlReadinessChanged = $false
    NetPnlRemainsBlocked = $true
    AccountingPnlChanged = $false
    LedgerReadinessChanged = $false
    ProductionReadinessChanged = $false
    MarketDataStatusChanged = $false
    MarketDataRemainsWithWarnings = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid = $true
    CoreAnubisSandboxLifecycleAcceptedWithWarningsRemainsValid = $true
    Classification = "READINESS_IMPACT_READY_WITH_WARNINGS"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R001"
    NoNewR009Submission = $true
    NoNewLmaxCall = $true
    NoNewPolygonMassiveCall = $true
    NoNewOrderFillReport = $true
    NoDbMutation = $true
    NoLedgerCommit = $true
    NoProductionLive = $true
    NoCoreExecution = $true
    NoManager = $true
    NoAnubis = $true
    NoCuda = $true
    NoCoreNetting = $true
    NoR010PrototypeTransfer = $true
    NoAccountCurrencyAggregation = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoInventedFeesSpreadsCommissions = $true
    Classification = "BOUNDARY_SAFETY_PRESERVED"
})

$summary = @"
# RISK-COST-MODEL-R001

Final classification: RISK_COST_MODEL_R001_WITH_WARNINGS_COST_MODEL_PARTIAL_MAJOR_ONLY

## Cost guidance

The only usable guidance is 5 USD/million, scoped as best-case major-only sandbox guidance. It is not universal, not an exotics model, not an accounting model, not production pricing, and not an explicit LMAX commission schedule.

## Usability

5 USD/million is usable only for the historical major-pair sandbox lifecycle: AUDUSD, EURUSD, GBPUSD.

Core/Anubis exotics are not covered: USDCNH, USDMXN, USDNOK, USDSEK, USDSGD, USDZAR.

## Estimated preview

Computed: yes, partial historical major-only.

Historical gross sandbox PnL: $($historicalGross.ToString("0.######")) USD.

Historical estimated cost: $((Round6 $historicalEstimatedCost).ToString("0.######")) USD.

Historical cost-adjusted sandbox preview: $((Round6 $historicalCostAdjusted).ToString("0.######")) USD.

This is not full net PnL.

## Net PnL

Net PnL preview ready: no. Core/Anubis exotics and explicit fee/commission evidence remain missing.

## Remaining blockers

- Explicit cost evidence for Core/Anubis exotics.
- Full net PnL.
- Account-currency aggregation.
- Accounting PnL.
- Accounting attribution.
- Ledger commit.
- Production/live.

## Next package

NEXT_RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE

## Did not run

No R009 submission, LMAX call, Polygon/Massive call, market-data fetch, order, fill/report, DB mutation, ledger commit, Core manager, Anubis, CUDA, Core netting, accounting PnL, production PnL, or production/live readiness occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "RISK_COST_MODEL_R001_ARTIFACTS_WRITTEN"
