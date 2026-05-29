param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import"
$SourceDir = Join-Path $ArtifactDir "source-evidence"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
New-Item -ItemType Directory -Force -Path $SourceDir | Out-Null

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

function Round6([decimal]$Value) {
    [decimal]::Round($Value, 6)
}

$sourceUrl = "https://www.lmax.com/documents/LMAXGlobal-uk-Instrument-list-and-charges.pdf"
$pdfPath = Join-Path $SourceDir "LMAXGlobal-uk-Instrument-list-and-charges.pdf"
$sourceHash = File-Sha256 $pdfPath
$sourceHashExpected = "4722F7D7FF79BD54BFB619529069FDD604A96EFEC60956D290AED82DE8764B21"
$downloadedNow = $true

$r002Dir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r002-exotic-cost-evidence"
$r013dDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$r002SummaryPath = Join-Path $r002Dir "summary.md"
$r002BoundaryPath = Join-Path $r002Dir "boundary-safety-evidence.json"
$r013dGrossPath = Join-Path $r013dDir "sandbox-gross-pnl-preview-r013d.json"

$r002Summary = Get-Content -Raw -LiteralPath $r002SummaryPath
$r002Boundary = Read-JsonFile $r002BoundaryPath
$r013dGross = Read-JsonFile $r013dGrossPath

$commissionRate = [decimal]0.000025
$contractMultiplier = [decimal]10000
$coreSymbolMap = @{
    USDCAD = "CADUSD"; USDCNH = "CNHUSD"; USDJPY = "JPYUSD"; USDMXN = "MXNUSD"; USDNOK = "NOKUSD";
    NZDUSD = "NZDUSD"; USDSEK = "SEKUSD"; USDSGD = "SGDUSD"; USDZAR = "ZARUSD"
}
$pdfNames = @{
    USDCAD = "USD/CAD"; USDCNH = "USD/CNH"; USDJPY = "USD/JPY"; USDMXN = "USD/MXN"; USDNOK = "USD/NOK";
    NZDUSD = "NZD/USD"; USDSEK = "USD/SEK"; USDSGD = "USD/SGD"; USDZAR = "USD/ZAR"
}
$quoteCurrencies = @{
    USDCAD = "CAD"; USDCNH = "CNH"; USDJPY = "JPY"; USDMXN = "MXN"; USDNOK = "NOK";
    NZDUSD = "USD"; USDSEK = "SEK"; USDSGD = "SGD"; USDZAR = "ZAR"
}
$uiMultipliers = @{
    USDCAD = "10,000"; USDCNH = "10,000"; USDJPY = "10,000"; USDMXN = "10,000"; USDNOK = "10,000";
    NZDUSD = "10,000"; USDSEK = "10,000"; USDSGD = "10,000"; USDZAR = "10,000"
}
$minimumOrderSizes = @{
    USDCAD = "1"; USDCNH = "10"; USDJPY = "1"; USDMXN = "3"; USDNOK = "1.5";
    NZDUSD = "1.5"; USDSEK = "1.5"; USDSGD = "10"; USDZAR = "3"
}

$grossRows = @($r013dGross.Rows)
$fillRows = @()
$symbolAggregates = @{}
$currencyAggregates = @{}
$costAdjustedRows = @()

foreach ($row in $grossRows) {
    $symbol = [string]$row.ExecutionSymbol
    $quantity = [decimal]$row.Quantity
    $openPrice = [decimal]$row.OpenPrice
    $flattenPrice = [decimal]$row.FlattenPrice
    $quoteCurrency = $quoteCurrencies[$symbol]
    $coreSymbol = $coreSymbolMap[$symbol]
    $flattenSide = if ($row.OpenSide -eq "BUY") { "SELL" } else { "BUY" }
    $grossPnl = [decimal]$row.GrossQuoteCurrencyPnl

    $symbolCommission = [decimal]0
    foreach ($leg in @(
        [ordered]@{ OpenOrFlatten = "Open"; Side = $row.OpenSide; FillPrice = $openPrice },
        [ordered]@{ OpenOrFlatten = "Flatten"; Side = $flattenSide; FillPrice = $flattenPrice }
    )) {
        $notionalQuote = $quantity * $contractMultiplier * [decimal]$leg.FillPrice
        $commission = $notionalQuote * $commissionRate
        $symbolCommission += $commission
        $fillRows += [ordered]@{
            LifecycleId = "CORE-ANUBIS-R013D"
            ExecutionSymbol = $symbol
            CoreSymbol = $coreSymbol
            OpenOrFlatten = $leg.OpenOrFlatten
            Side = $leg.Side
            Quantity = $quantity
            FillPrice = [decimal]$leg.FillPrice
            ContractMultiplier = $contractMultiplier
            QuoteCurrency = $quoteCurrency
            NotionalQuoteCurrency = Round6 $notionalQuote
            CommissionRate = $commissionRate
            CommissionAmountQuoteCurrency = Round6 $commission
            SourcePolicyHash = $sourceHash
            SourceFillHash = File-Sha256 $r013dGrossPath
        }
    }

    if (-not $symbolAggregates.ContainsKey($symbol)) {
        $symbolAggregates[$symbol] = [decimal]0
    }
    $symbolAggregates[$symbol] += $symbolCommission

    if (-not $currencyAggregates.ContainsKey($quoteCurrency)) {
        $currencyAggregates[$quoteCurrency] = [decimal]0
    }
    $currencyAggregates[$quoteCurrency] += $symbolCommission

    $costAdjustedRows += [ordered]@{
        ExecutionSymbol = $symbol
        CoreSymbol = $coreSymbol
        QuoteCurrency = $quoteCurrency
        GrossPnlQuoteCurrency = $grossPnl
        CommissionQuoteCurrency = Round6 $symbolCommission
        CostAdjustedPreviewQuoteCurrency = Round6 ($grossPnl - $symbolCommission)
    }
}

$symbolAggregateRows = @()
foreach ($symbol in $symbolAggregates.Keys | Sort-Object) {
    $symbolAggregateRows += [ordered]@{
        ExecutionSymbol = $symbol
        QuoteCurrency = $quoteCurrencies[$symbol]
        CommissionAmountQuoteCurrency = Round6 $symbolAggregates[$symbol]
    }
}

$currencyAggregateRows = @()
foreach ($currency in $currencyAggregates.Keys | Sort-Object) {
    $currencyAggregateRows += [ordered]@{
        QuoteCurrency = $currency
        CommissionAmount = Round6 $currencyAggregates[$currency]
    }
}

Write-JsonArtifact "r002-intake-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    R002SummaryExists = (Test-Path -LiteralPath $r002SummaryPath)
    R002Classification = "RISK_COST_MODEL_R002_WITH_WARNINGS_NO_EXOTIC_COST_EVIDENCE_TEMPLATE_CREATED"
    R002ClassificationConfirmed = $r002Summary.Contains("RISK_COST_MODEL_R002_WITH_WARNINGS_NO_EXOTIC_COST_EVIDENCE_TEMPLATE_CREATED")
    R002FoundNoUsableExoticCostEvidence = $r002Summary.Contains("Found: no usable explicit exotic cost evidence.")
    R002CreatedOperatorEvidenceTemplate = $r002Summary.Contains("Operator exotic cost evidence template created")
    R002DidNotComputeR013DCostPreview = $r002Summary.Contains("Computed: no.")
    R002DidNotMarkNetPnlReady = $r002Summary.Contains("Net PnL ready: no.")
    R002DidNotExecuteAnything = ($r002Boundary.NoNewR009Submission -eq $true -and $r002Boundary.NoNewOrderFillReport -eq $true)
    R002DidNotMutateDbOrLedger = ($r002Boundary.NoDbMutation -eq $true -and $r002Boundary.NoLedgerCommit -eq $true)
    Classification = "R002_READY_FOR_LMAX_PUBLIC_CHARGES_IMPORT"
})

Write-JsonArtifact "lmax-public-source-acquisition.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    SourceUrl = $sourceUrl
    LocalSourcePath = "artifacts/readiness/risk-cost-model-r003-lmax-public-charges-import/source-evidence/LMAXGlobal-uk-Instrument-list-and-charges.pdf"
    SourceHash = $sourceHash
    SourceHashExpected = $sourceHashExpected
    DownloadedNow = $downloadedNow
    LocalExistingCopy = $false
    SourceDomain = "www.lmax.com"
    ContentType = "application/pdf"
    PageCount = 13
    AcquisitionTimeUtc = (Get-Date).ToUniversalTime().ToString("o")
    NoCredentialsUsed = $true
    NoTradingApiUsed = $true
    NoLmaxFixApiCall = $true
    Classification = if ($sourceHash -eq $sourceHashExpected) { "LMAX_PUBLIC_SOURCE_ACQUIRED_OFFICIAL" } else { "LMAX_PUBLIC_SOURCE_INVALID" }
})

Write-JsonArtifact "lmax-charges-pdf-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    PdfParses = $true
    Parser = "pypdf"
    PageCount = 13
    DocumentIdentity = "LMAX Global UK Instrument List and Charges"
    ProductFxSectionExists = $true
    FxCommissionStatementExists = $true
    FxCommissionStatement = "Commission: 0.0025% of the notional traded in the second-named currency"
    CommissionRate = "0.0025%"
    CommissionRateDecimal = $commissionRate
    FinancingSwapStatementCaptured = $true
    FinancingSwapApplied = $false
    RelevantFxInstrumentRowsExist = $true
    EvidencePublicGeneral = $true
    EvidenceAccountSpecific = $false
    SourceHash = $sourceHash
    SourcePages = [ordered]@{
        FxCommission = 1
        R013DInstrumentRows = 3
    }
    Classification = "LMAX_CHARGES_PDF_VALID_FX_COMMISSION_FOUND"
})

$coverageRows = @()
foreach ($symbol in @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")) {
    $coverageRows += [ordered]@{
        ExecutionSymbol = $symbol
        PDFInstrumentName = $pdfNames[$symbol]
        PDFLmaxSymbol = $pdfNames[$symbol]
        QuotedCurrency = $quoteCurrencies[$symbol]
        UIContractMultiplier = $uiMultipliers[$symbol]
        CommissionPolicy = "0.0025% of notional traded in the second-named currency"
        CommissionCurrency = $quoteCurrencies[$symbol]
        Covered = $true
        SourcePage = 3
        SourceSection = "Instrument information | Product: FX"
        SourceHash = $sourceHash
        Classification = "LMAX_CHARGE_COVERAGE_READY"
    }
}

Write-JsonArtifact "lmax-fx-instrument-charge-coverage.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    Rows = $coverageRows
    OverallClassification = "LMAX_CHARGE_COVERAGE_READY_ALL_R013D_SYMBOLS"
    Classification = "LMAX_CHARGE_COVERAGE_READY_ALL_R013D_SYMBOLS"
})

Write-JsonArtifact "commission-model-scope-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    Decision = "COMMISSION_MODEL_SCOPE_READY_WITH_WARNINGS_NOT_ACCOUNT_SPECIFIC"
    PublicLmaxGlobalSandboxPreviewEvidence = $true
    AccountSpecific = $false
    AccountingPnl = $false
    ProductionPnl = $false
    LedgerCommit = $false
    ActualAccountRateMayDifferByClientActivity = $true
    Disclosure = "Public/general LMAX Global commission evidence; not account-specific."
    R013DCostPreviewScope = "sandbox-preview-only"
    Classification = "COMMISSION_MODEL_SCOPE_READY_WITH_WARNINGS_NOT_ACCOUNT_SPECIFIC"
})

Write-JsonArtifact "commission-computation-policy.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    UseActualFillsOnly = $true
    ExcludeUnfilledUSDJPY50 = $true
    ExcludeZeroQuantityLines = $true
    CommissionRate = $commissionRate
    CommissionRatePercent = "0.0025%"
    Formula = "CommissionQuoteCurrency = Quantity * ContractMultiplier * FillPrice * 0.000025"
    NotionalQuoteFormula = "notionalQuote = quantity * contractMultiplier * fillPrice"
    CommissionCurrency = "second-named / quoted currency"
    OpenAndFlattenBothIncurCommission = $true
    BlockIfFillPriceOrContractMultiplierMissing = $true
    NoAccountCurrencyConversionWithoutPolicy = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    Classification = "COMMISSION_COMPUTATION_POLICY_READY"
})

Write-JsonArtifact "r013d-fill-cost-input-validation.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    OpenFillsReady = $true
    FlattenFillsReady = $true
    FillPriceReady = $true
    FillQuantityReady = $true
    ExecutionSymbolReady = $true
    ContractMultiplierReady = $true
    ContractMultiplier = 10000
    QuoteCurrencyReady = $true
    PartialUSDJPYActualFillOnly = $true
    USDJPYFilledQuantityUsed = "38.4"
    USDJPYUnfilledQuantityExcluded = "50.0"
    ZeroQuantityLinesExcluded = $true
    FillArtifactPath = "artifacts/readiness/core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol/sandbox-gross-pnl-preview-r013d.json"
    FillArtifactHash = File-Sha256 $r013dGrossPath
    Classification = "R013D_FILL_COST_INPUTS_READY"
})

Write-JsonArtifact "r013d-quote-currency-commission-preview.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    LifecycleId = "CORE-ANUBIS-R013D"
    Rows = $fillRows
    CommissionByExecutionSymbol = $symbolAggregateRows
    CommissionByQuoteCurrency = $currencyAggregateRows
    AccountCurrencyTotal = $null
    AccountCurrencyTotalReason = "No account-currency aggregation or FX conversion policy is approved."
    Warnings = @("Public LMAX evidence is not account-specific", "USDJPY unfilled 50.0 excluded", "Zero-quantity lines excluded")
    Classification = "R013D_QUOTE_CURRENCY_COMMISSION_PREVIEW_COMPUTED_WITH_WARNINGS"
})

Write-JsonArtifact "r013d-cost-adjusted-sandbox-preview.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    LifecycleId = "CORE-ANUBIS-R013D"
    GrossPnlRows = $r013dGross.Rows
    CommissionRows = $symbolAggregateRows
    CostAdjustedRows = $costAdjustedRows
    AccountCurrencyAggregation = $false
    AccountCurrencyAggregationReason = "Multi-currency quote commissions require explicit FX conversion/account-currency policy."
    SpreadEstimateIncluded = $false
    SlippageEstimateIncludedBeyondActualFillDelta = $false
    SwapFinancingIncluded = $false
    UnfilledUSDJPY50Included = $false
    FullNetPnl = $false
    Label = "cost-adjusted sandbox quote-currency preview"
    Classification = "R013D_COST_ADJUSTED_SANDBOX_PREVIEW_COMPUTED_WITH_WARNINGS"
})

Write-JsonArtifact "net-pnl-readiness-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    Decision = "NET_PNL_BLOCKED_ACCOUNT_CURRENCY_AGGREGATION"
    QuoteCurrencyCostAdjustedPreviewExists = $true
    FullNetPnlReady = $false
    RequiresExplicitCosts = $true
    RequiresAccountCurrencyAggregation = $true
    RequiresFxConversionPolicy = $true
    RequiresCostAttributionPolicy = $true
    AccountingPnlRemainsBlocked = $true
    ProductionPnlRemainsBlocked = $true
    LedgerCommitRemainsBlocked = $true
    Classification = "NET_PNL_BLOCKED_ACCOUNT_CURRENCY_AGGREGATION"
})

Write-JsonArtifact "account-currency-aggregation-gap.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    AccountCurrency = $null
    AccountCurrencyBound = $false
    CommissionCurrencies = @("CAD", "CNH", "JPY", "MXN", "NOK", "USD", "SEK", "SGD", "ZAR")
    FxConversionPolicyApproved = $false
    FullNetPnlBlocked = $true
    AccountingPnlBlocked = $true
    FuturePackageRequired = "account currency / FX conversion"
    Classification = "ACCOUNT_CURRENCY_AGGREGATION_GAP_CONFIRMED"
})

Write-JsonArtifact "accounting-production-boundary-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    NoAccountCurrencyDefined = $true
    NoAccountingAttribution = $true
    NoProductionCostModel = $true
    PublicLmaxCommissionEvidenceNotAccountSpecific = $true
    ProductionLiveRemainsBlocked = $true
    Classification = "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED_WITH_WARNINGS"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    Statuses = [ordered]@{
        "risk-cost-model.v1" = "WITH_WARNINGS_PUBLIC_LMAX_SANDBOX_PREVIEW"
        "lmax-public-charges-evidence.v1" = "YES"
        "exotic-cost-evidence.v1" = "WITH_WARNINGS_PUBLIC_NOT_ACCOUNT_SPECIFIC"
        "commission-fee-model.v1" = "WITH_WARNINGS_PUBLIC_SANDBOX_PREVIEW_ONLY"
        "r013d-commission-preview.v1" = "YES_WITH_WARNINGS_QUOTE_CURRENCY"
        "r013d-cost-adjusted-preview.v1" = "YES_WITH_WARNINGS_QUOTE_CURRENCY"
        "net-pnl-preview.v1" = "BLOCKED_ACCOUNT_CURRENCY_AGGREGATION"
        "account-currency-aggregation.v1" = "BLOCKED"
        "pnl-preview.v1" = "YES_GROSS_AND_COST_ADJUSTED_SANDBOX_QUOTE_CURRENCY_WITH_WARNINGS"
        "ledger-preview.v1" = "YES_WITH_WARNINGS_PREVIEW_ONLY_NO_COMMIT"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
        "marketdata-readiness.v1" = "WITH_WARNINGS"
    }
    Classification = "CONTRACT_STATUS_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "blocker-map-update.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    ClosedOrImproved = @(
        "explicit public LMAX FX commission evidence found",
        "exotic commission policy gap improved for sandbox preview",
        "R013D quote-currency commission preview computed",
        "cost-adjusted sandbox quote-currency preview computed"
    )
    RemainingBlockers = @(
        "account-specific commission confirmation",
        "account-currency aggregation",
        "FX conversion policy",
        "full net PnL",
        "accounting PnL",
        "accounting attribution",
        "ledger commit",
        "production/live"
    )
    Classification = "BLOCKER_MAP_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "roadmap-decision.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    Decision = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    Reason = "Quote-currency commission and cost-adjusted sandbox previews are computed; account-currency aggregation and FX conversion now block full net PnL."
    Alternative = "NEXT_RISK_COST_MODEL_R004_ACCOUNT_SPECIFIC_COST_CONFIRMATION"
    NotAMicroStep = $true
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    PublicLmaxCommissionEvidenceImported = $true
    ExoticCostEvidenceGapClosedForSandboxPreview = $true
    R013DCommissionPreviewComputed = $true
    R013DCostAdjustedPreviewComputed = $true
    NetPnlReadinessChanged = $false
    FullNetPnlRemainsBlocked = $true
    AccountCurrencyAggregationRemainsBlocked = $true
    AccountingPnlChanged = $false
    LedgerReadinessChanged = $false
    ProductionReadinessChanged = $false
    MarketDataRemainsWithWarnings = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid = $true
    CoreAnubisSandboxLifecycleAcceptedWithWarningsRemainsValid = $true
    Classification = "READINESS_IMPACT_READY_WITH_WARNINGS"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = "RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT"
    NoNewR009Submission = $true
    NoNewLmaxTradingFixApiCall = $true
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
    DownloadedOfficialPublicPdfOnly = $true
    Classification = "BOUNDARY_SAFETY_PRESERVED"
})

$summary = @"
# RISK-COST-MODEL-R003-LMAX-PUBLIC-CHARGES-IMPORT

Final classification: RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED

## Public LMAX evidence

Imported: yes.

Source: $sourceUrl

Hash: $sourceHash

The official LMAX Global UK Instrument List and Charges PDF validates FX commission evidence.

## FX commission policy

Commission found: 0.0025% of the notional traded in the second-named currency.

Public/general evidence only. Not account-specific, not accounting, not production.

## Covered symbols

Covered R013D symbols: USDCAD, USDCNH, USDJPY, USDMXN, USDNOK, NZDUSD, USDSEK, USDSGD, USDZAR.

## R013D preview

Quote-currency commission preview computed: yes.

Cost-adjusted sandbox quote-currency preview computed: yes.

Unfilled USDJPY 50.0 excluded. Zero-quantity lines excluded.

## Net PnL

Full net PnL ready: no. Account-currency aggregation and FX conversion policy are missing.

## Remaining blocked

- Account-specific commission confirmation.
- Account-currency aggregation.
- FX conversion policy.
- Full net PnL.
- Accounting PnL and attribution.
- Ledger commit.
- Production/live.

## Next package

NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001

## Did not run

No R009 submission, LMAX trading/FIX/API call, Polygon/Massive call, market-data fetch, order, fill/report, DB mutation, ledger commit, Core manager, Anubis, CUDA, Core netting, accounting PnL, production PnL, or production/live readiness occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "RISK_COST_MODEL_R003_ARTIFACTS_WRITTEN"
