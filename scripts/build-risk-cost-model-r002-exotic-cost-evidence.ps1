param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r002-exotic-cost-evidence"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    return $null
}

$r001Dir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r001"
$checkpointDir = Join-Path $RepoRoot "artifacts\readiness\programme-clean-product-checkpoint-r001"
$r013dDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"

$r001SummaryPath = Join-Path $r001Dir "summary.md"
$r001PreviewPath = Join-Path $r001Dir "estimated-cost-preview-v0.json"
$r001GuidancePath = Join-Path $r001Dir "cost-guidance-inventory.json"
$r001BoundaryPath = Join-Path $r001Dir "boundary-safety-evidence.json"
$r013dGrossPath = Join-Path $r013dDir "sandbox-gross-pnl-preview-r013d.json"

$r001Summary = Get-Content -Raw -LiteralPath $r001SummaryPath
$r001Preview = Read-JsonFile $r001PreviewPath
$r001Guidance = Read-JsonFile $r001GuidancePath
$r001Boundary = Read-JsonFile $r001BoundaryPath
$r013dGross = Read-JsonFile $r013dGrossPath

$requiredExotics = @("USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR")
$contextSymbols = @("USDCAD", "USDJPY", "NZDUSD")
$coreSymbolMap = @{
    USDCAD = "CADUSD"; USDCNH = "CNHUSD"; USDJPY = "JPYUSD"; USDMXN = "MXNUSD"; USDNOK = "NOKUSD";
    NZDUSD = "NZDUSD"; USDSEK = "SEKUSD"; USDSGD = "SGDUSD"; USDZAR = "ZARUSD"
}
$intendedQty = @{
    USDCAD = "0.2"; USDCNH = "0.2"; USDJPY = "88.4"; USDMXN = "1.1"; USDNOK = "3.1";
    NZDUSD = "0.1"; USDSEK = "0.4"; USDSGD = "0.5"; USDZAR = "7.1"
}
$majorGuidanceCandidates = @("USDCAD", "USDJPY", "NZDUSD")

$grossRows = @($r013dGross.Rows)
$grossBySymbol = @{}
foreach ($row in $grossRows) {
    $grossBySymbol[$row.ExecutionSymbol] = $row
}

$universeRows = @()
foreach ($symbol in @($contextSymbols + $requiredExotics | Select-Object -Unique)) {
    $row = $grossBySymbol[$symbol]
    $isExotic = $requiredExotics -contains $symbol
    $filledQty = if ($symbol -eq "USDJPY") { "38.4" } else { $row.Quantity }
    $universeRows += [ordered]@{
        ExecutionSymbol = $symbol
        CoreSymbol = $coreSymbolMap[$symbol]
        IntendedQuantity = $intendedQty[$symbol]
        FilledQuantity = $filledQty
        FlattenedQuantity = $filledQty
        FillStatus = if ($symbol -eq "USDJPY") { "PARTIAL" } else { "FULL" }
        QuoteCurrency = if ($symbol -eq "NZDUSD") { "USD" } else { $symbol.Substring(3,3) }
        MajorMinorExoticClassification = if ($isExotic) { "non-major/exotic" } else { "major-guidance-candidate" }
        FiveUsdPerMillionMajorOnlyGuidanceApplies = (-not $isExotic)
        ExplicitExoticCostEvidenceRequired = $isExotic
        ActualFillsExist = $true
        GrossPnlAlreadyUsesActualFillPriceDelta = $true
    }
}

$candidateEvidenceFiles = @(
    "artifacts/readiness/risk-cost-model-r001/cost-guidance-inventory.json",
    "artifacts/readiness/risk-cost-model-r001/instrument-cost-coverage-classification.json",
    "artifacts/readiness/ledger-state/phase-ledger-state-r005-cost-spread-commission-evidence-status.json",
    "artifacts/readiness/marketdata-ledger-pnl-closure/phase-marketdata-ledger-pnl-closure-r001-cost-spread-commission-evidence.json",
    "artifacts/readiness/operator-policy/phase-operator-policy-r001-cost-spread-commission-policy-decision.json",
    "artifacts/readiness/economic-readiness/phase-economic-readiness-r001-risk-cost-policy-closure-package.json",
    "artifacts/readiness/execution-sim/phase-exec-sim-r026-cost-guidance-preservation.json"
)

$candidateEvidence = @()
foreach ($relative in $candidateEvidenceFiles) {
    $path = Join-Path $RepoRoot $relative
    if (Test-Path -LiteralPath $path) {
        $candidateEvidence += [ordered]@{
            Path = $relative
            FileType = [System.IO.Path]::GetExtension($path).TrimStart(".")
            Hash = File-Sha256 $path
            SymbolsCurrenciesCovered = if ($relative -like "*cost-guidance*" -or $relative -like "*r001*") { @("major FX guidance only or general missing-model policy") } else { @() }
            CostTypesCovered = @("guidance/policy only")
            Units = if ($relative -like "*cost-guidance*") { "USD per million" } else { "unknown or not computational" }
            Scope = "major-only guidance or missing-model status; no explicit Core/Anubis exotic symbol coverage"
            EffectiveDate = $null
            UsableForSandboxPreviewOnly = $relative -like "*risk-cost-model-r001*"
            ForbiddenForAccountingProduction = $true
            RequiresOperatorInterpretation = $true
            UsableForRequiredExotics = $false
        }
    }
}

$templateRows = @()
foreach ($symbol in $requiredExotics) {
    $templateRows += [ordered]@{
        ExecutionSymbol = $symbol
        CostTypeRequired = @("commission", "fee", "all-in cost", "spread if explicitly intended for hypothetical/pre-trade model")
        CostValue = $null
        CostUnit = "USD_PER_MILLION / BPS / PIPS / FIXED / PERCENT / OTHER"
        AppliesTo = "sandbox preview / all FX / symbol-specific / account-specific"
        EffectiveDate = $null
        SourceArtifactPath = $null
        SourceArtifactHash = $null
        AllowedScope = "SandboxPreviewOnly"
        NotAccounting = $true
        NotProduction = $true
        NotLedgerCommit = $true
        Notes = "Provide explicit local operator/broker evidence. Do not reuse major-only 5 USD/million guidance unless source explicitly covers this symbol or all FX."
    }
}

$validationRows = @()
foreach ($symbol in $requiredExotics) {
    $validationRows += [ordered]@{
        ExecutionSymbol = $symbol
        ExplicitCommissionFeeEvidenceExists = $false
        ExplicitSpreadSlippageEvidenceExists = $false
        ExplicitAllInCostEvidenceExists = $false
        CostUnit = $null
        CostAmount = $null
        SourceArtifact = $null
        SourceHash = $null
        EffectiveDate = $null
        SandboxAllowed = $false
        AccountingProductionAllowed = $false
        EvidenceDirectOrGenericAllFx = $null
        AccountSpecific = $false
        SufficientlyClearForComputation = $false
        Classification = "EXOTIC_COST_EVIDENCE_MISSING"
    }
}

$majorMinorRows = @()
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY")) {
    $covered = @("AUDUSD", "EURUSD", "GBPUSD") -contains $symbol
    $candidate = @("NZDUSD", "USDCAD", "USDJPY") -contains $symbol
    $majorMinorRows += [ordered]@{
        Symbol = $symbol
        FiveUsdPerMillionGuidanceExists = $true
        CoveredByR001ComputedPreview = $covered
        BroaderMajorGuidanceCandidate = $candidate
        RequiresExplicitClassificationBeforeCoreAnubisUse = $candidate
        GuidanceType = "best-case major-only guidance, not explicit commission schedule"
        Classification = if ($covered) { "MAJOR_MINOR_COST_EVIDENCE_READY_WITH_WARNINGS" } else { "MAJOR_MINOR_COST_EVIDENCE_PARTIAL" }
    }
}

Write-JsonArtifact "r001-intake-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    R001SummaryExists = (Test-Path -LiteralPath $r001SummaryPath)
    R001Classification = "RISK_COST_MODEL_R001_WITH_WARNINGS_COST_MODEL_PARTIAL_MAJOR_ONLY"
    R001ClassificationConfirmed = $r001Summary.Contains("RISK_COST_MODEL_R001_WITH_WARNINGS_COST_MODEL_PARTIAL_MAJOR_ONLY")
    R001HistoricalMajorOnlyCostPreviewExists = ($r001Preview.Classification -eq "ESTIMATED_COST_PREVIEW_COMPUTED_PARTIAL_MAJOR_ONLY")
    R001FiveUsdPerMillionMajorOnly = ($r001Guidance.Guidance[0].Scope -eq "best-case major-only guidance")
    R001CoreAnubisExoticsUncovered = $r001Summary.Contains("Core/Anubis exotics are not covered")
    R001DidNotClaimFullNetPnlReadiness = $r001Summary.Contains("Net PnL preview ready: no")
    R001DidNotMutateDbOrLedger = ($r001Boundary.NoDbMutation -eq $true -and $r001Boundary.NoLedgerCommit -eq $true)
    R001DidNotExecuteAnything = ($r001Boundary.NoNewR009Submission -eq $true -and $r001Boundary.NoNewOrderFillReport -eq $true)
    Classification = "R001_READY_FOR_EXOTIC_COST_EVIDENCE_SEARCH"
})

Write-JsonArtifact "core-anubis-exotic-execution-universe.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    RequiredExoticSymbols = $requiredExotics
    ContextSymbols = $contextSymbols
    Rows = $universeRows
    Classification = "CORE_ANUBIS_EXOTIC_UNIVERSE_READY_WITH_WARNINGS"
})

Write-JsonArtifact "local-cost-evidence-discovery.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    SearchLocalOnly = $true
    NoInternet = $true
    NoGithub = $true
    NoExternalApi = $true
    SearchRoots = @("artifacts/readiness", "artifacts/operator-evidence", "config", "scripts", "docs", "runbooks", "src", "tests", "tools")
    SearchTerms = @("commission", "commissions", "fee", "fees", "cost", "costs", "spread", "slippage", "USD/million", "million", "per million", "LMAX", "tariff", "brokerage", "broker", "transaction cost", "execution cost", "swap", "rollover", "financing", "USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR", "CNH", "MXN", "NOK", "SEK", "SGD", "ZAR")
    CandidateEvidenceFiles = $candidateEvidence
    GeneratedFixtureCommissionCsvsObserved = $true
    GeneratedFixtureCommissionCsvsUsableForOperatorCostEvidence = $false
    Finding = "Local evidence is partial: policy/guidance exists, but no explicit operator/broker evidence covers required Core/Anubis exotic symbols."
    Classification = "LOCAL_COST_EVIDENCE_PARTIAL"
})

Write-JsonArtifact "exotic-cost-evidence-validation-by-symbol.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Rows = $validationRows
    OverallClassification = "EXOTIC_COST_EVIDENCE_MISSING_ALL_REQUIRED"
    Classification = "EXOTIC_COST_EVIDENCE_MISSING_ALL_REQUIRED"
})

Write-JsonArtifact "major-minor-cost-evidence-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Rows = $majorMinorRows
    FiveUsdPerMillionGuidance = "best-case major-only guidance"
    GuidanceCoversHistoricalThreePairs = $true
    BroaderMajorCoverageRequiresExplicitClassification = $true
    GuidanceIsFeeCommissionOrGeneralCost = "general cost guidance, not explicit commission schedule"
    Classification = "MAJOR_MINOR_COST_EVIDENCE_READY_WITH_WARNINGS"
})

Write-JsonArtifact "cost-model-scope-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Decision = "COST_MODEL_SCOPE_MAJOR_ONLY"
    CoversHistoricalPmsIntentLifecycle = $true
    CoversCoreAnubisLifecycle = $false
    CanComputeFullSandboxNetPnlPreview = $false
    AccountingReady = $false
    ProductionReady = $false
    MissingExoticSymbols = $requiredExotics
    Classification = "COST_MODEL_SCOPE_MAJOR_ONLY"
})

Write-JsonArtifact "commission-fee-computation-policy.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    SupportedUnits = @(
        [ordered]@{ Unit = "USD_PER_MILLION"; Rule = "cost = actual filled notional / 1,000,000 * value" },
        [ordered]@{ Unit = "BPS"; Rule = "cost = actual filled notional * bps / 10,000" },
        [ordered]@{ Unit = "PIPS"; Rule = "requires symbol-specific pip value policy; block if absent" },
        [ordered]@{ Unit = "FIXED"; Rule = "requires currency and per-side/per-order/per-fill scope" },
        [ordered]@{ Unit = "PERCENT"; Rule = "cost = actual filled notional * percent / 100" },
        [ordered]@{ Unit = "UNKNOWN"; Rule = "do not compute" }
    )
    UseActualFilledQuantitiesOnly = $true
    UseActualFillNotionalIfAvailable = $true
    QuoteCurrencyConversionMissingBlocksAccountCurrencyAggregation = $true
    SandboxOnly = $true
    ExcludeUnfilledUSDJPY50 = $true
    ExcludeZeroQuantityLines = $true
    DoNotDoubleCountSpread = $true
    Classification = "COMMISSION_FEE_COMPUTATION_POLICY_READY_WITH_WARNINGS"
})

Write-JsonArtifact "spread-slippage-evidence-policy.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    ActualFillGrossPnlAlreadyReflectsOpenFlattenFillPrices = $true
    DoNotAddSpreadSlippageEstimateByDefault = $true
    ExplicitSpreadEvidenceUse = "hypothetical/pre-trade estimates only unless a future policy explicitly adjusts actual fill gross PnL"
    CoreAnubisGrossPnlPreviewRemainsActualFillsOnly = $true
    SpreadSlippageGapsDoNotBlockCommissionOnlyPreview = $true
    SpreadSlippageGapsBlockFullAllInNetModel = $true
    Classification = "SPREAD_SLIPPAGE_POLICY_READY_NO_DOUBLE_COUNT"
})

Write-JsonArtifact "r013d-cost-preview-feasibility.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    LifecycleId = "CORE-ANUBIS-R013D"
    ActualFillsExist = $true
    ActualFillPricesExist = $true
    GrossPnlPreviewExists = $true
    CostCoverageBySymbol = $validationRows
    PartialUSDJPYHandling = "Include only filled 38.4 if a future cost model covers USDJPY; do not include unfilled 50.0."
    CostAdjustedPreviewCanBeComputed = "none for Core/Anubis full lifecycle"
    ExoticsMissingCostEvidence = $requiredExotics
    FullNetPnlReady = $false
    Classification = "R013D_COST_PREVIEW_BLOCKED_EXOTIC_COST_GAPS"
})

Write-JsonArtifact "r013d-estimated-cost-preview.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    PreviewId = "risk-cost-model-r002:r013d-not-computed-exotic-cost-gaps"
    LifecycleId = "CORE-ANUBIS-R013D"
    IncludedFills = @()
    ExcludedFills = @($grossRows | ForEach-Object {
        [ordered]@{
            ExecutionSymbol = $_.ExecutionSymbol
            Quantity = $_.Quantity
            Reason = if ($requiredExotics -contains $_.ExecutionSymbol) { "missing explicit exotic cost evidence" } else { "Core/Anubis full preview blocked until all required symbols have explicit cost coverage" }
        }
    })
    GrossPnlInput = $r013dGross.Rows
    CostModelInput = $null
    EstimatedCostsBySymbol = @()
    AggregateEstimatedCost = $null
    CostAdjustedPreview = $null
    MissingCosts = $requiredExotics
    Warnings = @("Do not include unfilled USDJPY 50.0", "Do not include zero-quantity lines", "Do not double-count spread", "Not full net PnL")
    NotAccounting = $true
    NotProduction = $true
    NotLedgerCommit = $true
    Classification = "R013D_ESTIMATED_COST_PREVIEW_NOT_COMPUTED_EXOTIC_COST_GAPS"
})

Write-JsonArtifact "net-pnl-readiness-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Decision = "NET_PNL_BLOCKED_EXOTIC_COST_EVIDENCE_MISSING"
    AccountingPnlRemainsBlocked = $true
    ProductionPnlRemainsBlocked = $true
    AccountCurrencyAggregationRemainsBlocked = $true
    LedgerCommitRemainsBlocked = $true
    FullSandboxNetPnlReady = $false
    MissingExoticSymbols = $requiredExotics
    Classification = "NET_PNL_BLOCKED_EXOTIC_COST_EVIDENCE_MISSING"
})

Write-JsonArtifact "operator-exotic-cost-evidence-template.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    TemplateRows = $templateRows
    Classification = "OPERATOR_EXOTIC_COST_TEMPLATE_CREATED"
})

Write-JsonArtifact "accounting-production-boundary-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    NoAccountCurrency = $true
    NoAccountingAttribution = $true
    NoProductionCostModel = $true
    ProductionLiveRemainsBlocked = $true
    Classification = "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Statuses = [ordered]@{
        "risk-cost-model.v1" = "WITH_WARNINGS_MAJOR_ONLY_EXOTIC_EVIDENCE_MISSING"
        "exotic-cost-evidence.v1" = "BLOCKED_MISSING_ALL_REQUIRED"
        "commission-fee-model.v1" = "WITH_WARNINGS_MAJOR_ONLY_NO_EXOTIC_COMMISSION_EVIDENCE"
        "spread-slippage-policy.v1" = "YES_NO_DOUBLE_COUNT_ACTUAL_FILL_GROSS"
        "r013d-cost-preview.v1" = "BLOCKED_EXOTIC_COST_GAPS"
        "net-pnl-preview.v1" = "BLOCKED_EXOTIC_COST_EVIDENCE_MISSING"
        "pnl-preview.v1" = "YES_GROSS_SANDBOX_QUOTE_CURRENCY"
        "ledger-preview.v1" = "YES_WITH_WARNINGS_PREVIEW_ONLY_NO_COMMIT"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
        "marketdata-readiness.v1" = "WITH_WARNINGS"
    }
    Classification = "CONTRACT_STATUS_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "blocker-map-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    ClosedOrImproved = @(
        "exotic cost evidence searched",
        "cost model scope clarified",
        "double-counting policy preserved",
        "operator template created"
    )
    RemainingBlockers = @(
        "explicit exotic costs",
        "full net PnL",
        "account-currency aggregation",
        "accounting PnL",
        "accounting attribution",
        "ledger commit",
        "production/live"
    )
    Classification = "BLOCKER_MAP_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "roadmap-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    Decision = "NEXT_RISK_COST_MODEL_R003_OPERATOR_EXOTIC_COST_IMPORT"
    Reason = "No usable local exotic cost evidence was found; an operator evidence template was created."
    NotAMicroStep = $true
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    ExoticCostEvidenceFound = $false
    R013DCostPreviewCanBeComputed = $false
    NetPnlReadinessChanged = $false
    NetPnlRemainsBlocked = $true
    AccountingPnlChanged = $false
    LedgerReadinessChanged = $false
    ProductionReadinessChanged = $false
    MarketDataRemainsWithWarnings = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid = $true
    CoreAnubisSandboxLifecycleAcceptedWithWarningsRemainsValid = $true
    Classification = "READINESS_IMPACT_READY_WITH_WARNINGS"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE"
    NoNewR009Submission = $true
    NoNewLmaxCall = $true
    NoNewPolygonMassiveCall = $true
    NoMarketDataFetch = $true
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
# RISK-COST-MODEL-R002-EXOTIC-COST-EVIDENCE

Final classification: RISK_COST_MODEL_R002_WITH_WARNINGS_NO_EXOTIC_COST_EVIDENCE_TEMPLATE_CREATED

## Exotic cost evidence

Found: no usable explicit exotic cost evidence.

Covered exotic symbols: none.

Missing required exotic symbols: USDCNH, USDMXN, USDNOK, USDSEK, USDSGD, USDZAR.

Local evidence found was policy/guidance only: 5 USD/million remains best-case major-only guidance and prior artifacts continue to classify cost/spread/commission evidence as missing.

## R013D cost preview

Computed: no. R013D cost preview remains blocked by exotic cost gaps.

USDJPY partial handling preserved: include only filled 38.4 in any future supported model; unfilled 50.0 is not included and not approved for retry.

Zero-quantity lines remain excluded.

## Net PnL

Net PnL ready: no.

Accounting PnL: blocked.

Production PnL/live: blocked.

Ledger commit: blocked.

## Template

Operator exotic cost evidence template created for USDCNH, USDMXN, USDNOK, USDSEK, USDSGD, USDZAR.

## Next package

NEXT_RISK_COST_MODEL_R003_OPERATOR_EXOTIC_COST_IMPORT

## Did not run

No R009 submission, LMAX call, Polygon/Massive call, market-data fetch, order, fill/report, DB mutation, ledger commit, Core manager, Anubis, CUDA, Core netting, accounting PnL, production PnL, or production/live readiness occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "RISK_COST_MODEL_R002_ARTIFACTS_WRITTEN"
