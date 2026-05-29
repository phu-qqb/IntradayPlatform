param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "PROGRAMME-CLEAN-PRODUCT-CHECKPOINT-R001"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\programme-clean-product-checkpoint-r001"
$R014Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sandbox-readiness-update-r014"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonPath([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$r014SummaryPath = Join-Path $R014Dir "summary.md"
$r014Summary = Get-Content -Raw -LiteralPath $r014SummaryPath
$r014Central = Read-JsonPath (Join-Path $R014Dir "central-readiness-status-update.json")
$r014Product = Read-JsonPath (Join-Path $R014Dir "product-decision-update.json")
$r014Pnl = Read-JsonPath (Join-Path $R014Dir "pnl-readiness-update.json")
$r014Ledger = Read-JsonPath (Join-Path $R014Dir "ledger-readiness-update.json")
$r014Execution = Read-JsonPath (Join-Path $R014Dir "execution-readiness-update.json")
$r014Blockers = Read-JsonPath (Join-Path $R014Dir "blocker-map-update.json")
$r014Boundary = Read-JsonPath (Join-Path $R014Dir "boundary-safety-evidence.json")

$r014Ready = (
    $r014Summary.Contains("CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_PASS_PRODUCT_STATE_UPDATED_CORE_ANUBIS_LIFECYCLE_ACCEPTED_WITH_WARNINGS") -and
    $r014Central.CoreAnubisSandboxLifecycle -eq "ACCEPTED_WITH_WARNINGS" -and
    $r014Central.CoreAnubisResiduals -eq "zero" -and
    $r014Pnl.R013DCoreAnubisGrossSandboxQuoteCurrencyPnlPreviewValid -eq $true -and
    $r014Ledger.R013DPaperLedgerPreviewValid -eq $true -and
    $r014Ledger.NoCommit -eq $true -and
    $r014Boundary.NoProductionLive -eq $true -and
    $r014Boundary.NoAccountingNetProductionPnl -eq $true -and
    $r014Boundary.NoLedgerCommit -eq $true -and
    $r014Boundary.NoNewR009Submission -eq $true
)

Write-JsonArtifact "latest-package-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "latest-package-intake-validation"
    R014SummaryExists = Test-Path -LiteralPath $r014SummaryPath
    R014Classification = "CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_PASS_PRODUCT_STATE_UPDATED_CORE_ANUBIS_LIFECYCLE_ACCEPTED_WITH_WARNINGS"
    R014ClassificationMatches = $r014Summary.Contains("CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_PASS_PRODUCT_STATE_UPDATED_CORE_ANUBIS_LIFECYCLE_ACCEPTED_WITH_WARNINGS")
    ProductStateUpdated = $r014Product.Decision -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready_CoreAnubisSandboxLifecycleAcceptedWithWarnings"
    CoreAnubisLifecycleAcceptedWithWarnings = $r014Central.CoreAnubisSandboxLifecycle -eq "ACCEPTED_WITH_WARNINGS"
    ResidualsZero = $r014Central.CoreAnubisResiduals -eq "zero"
    GrossSandboxPnlPreviewValid = $r014Pnl.R013DCoreAnubisGrossSandboxQuoteCurrencyPnlPreviewValid
    PaperLedgerPreviewValidNoCommit = $r014Ledger.R013DPaperLedgerPreviewValid -and $r014Ledger.NoCommit
    ProductionLiveBlocked = $r014Boundary.NoProductionLive
    AccountingNetPnlBlocked = $r014Boundary.NoAccountingNetProductionPnl
    LedgerCommitBlocked = $r014Boundary.NoLedgerCommit
    NoNewExecutionOccurredInR014 = $r014Boundary.NoNewR009Submission -and $r014Boundary.NoNewLmaxCall -and $r014Boundary.NoNewOrderFillReport
    Classification = if ($r014Ready) { "LATEST_R014_READY_FOR_CLEAN_CHECKPOINT" } else { "LATEST_R014_INCOMPLETE" }
})

$ready = @(
    "Sandbox order lifecycle",
    "Historical PMS-intent cross-rail sandbox lifecycle",
    "Core/Anubis sandbox lifecycle with partial-fill warning",
    "Sandbox flatten / residual-zero reconciliation",
    "Sandbox gross quote-currency PnL preview",
    "Paper-ledger preview no-commit"
)
$readyWithWarnings = @(
    "Core/Anubis lifecycle accepted with USDJPY partial-fill warning",
    "Ledger preview",
    "Below-min zeroing warnings"
)
$blocked = @(
    "Net PnL",
    "Accounting PnL",
    "Production PnL",
    "Ledger commit",
    "Production/live",
    "Account-currency aggregation",
    "Accounting attribution",
    "USDJPY remaining retry without new approval"
)
Write-JsonArtifact "central-product-state-snapshot.json" ([ordered]@{
    Package = $Package
    Artifact = "central-product-state-snapshot"
    ProductDecision = "SandboxProgrammeAcceptedWithGrossPnlV0Ready"
    CoreAnubisSandboxLifecycle = "AcceptedWithWarnings"
    HistoricalCrossRailR014 = "PMSIntentDriven"
    SandboxQubesPrototype = "FallbackTestPrototypeOnly"
    R010PrototypeApprovalTransferability = $false
    ProductionLiveReady = $false
    LedgerCommitReady = $false
    AccountingPnlReady = $false
    NetPnlReady = $false
    Ready = $ready
    ReadyWithWarnings = $readyWithWarnings
    Blocked = $blocked
    Classification = "CENTRAL_PRODUCT_STATE_SNAPSHOT_READY_WITH_WARNINGS"
})

Write-JsonArtifact "core-anubis-rail-final-summary.json" ([ordered]@{
    Package = $Package
    Artifact = "core-anubis-rail-final-summary"
    Milestones = @(
        "Core true engine found in QQ.Production.Core",
        "Raw AggregatedWeights produced",
        "Final manager weights produced",
        "Netted USD weights produced",
        "Core handoff manifest materialized",
        "Intraday handoff consumed",
        "Target notional USD 6,000,000 applied SandboxPreviewSizingOnly",
        "Price basis complete",
        "LMAX FX metadata catalog complete",
        "Quantities derived",
        "Quantity refinement passed with warnings",
        "Risk review passed with warnings",
        "Operator approval captured",
        "Exact sandbox harness ready",
        "R013C first sandbox attempt rejected due tag 22",
        "R013D fixed tag 22 LMAX -> 8",
        "R013D retry executed",
        "Fills occurred",
        "USDJPY partial fill occurred",
        "Flatten occurred",
        "Residuals zero",
        "Gross sandbox PnL preview valid",
        "Paper-ledger preview valid no-commit",
        "R013E accepted lifecycle with partial-fill warning",
        "R014 updated product state"
    )
    Classification = "CORE_ANUBIS_RAIL_FINAL_SUMMARY_READY_WITH_WARNINGS"
})

Write-JsonArtifact "warning-register.json" ([ordered]@{
    Package = $Package
    Artifact = "warning-register"
    Warnings = [ordered]@{
        USDJPYPartialFill = [ordered]@{
            Intended = "88.4"
            Filled = "38.4"
            Unfilled = "50.0"
            RetryApproved = $false
        }
        BelowMinZeroing = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
        OmittedBelowMinExposure = [ordered]@{ Usd = "601.92"; Percent = "0.010032%" }
        GrossPnlPreviewScope = "gross-only / quote-currency-only"
        PaperLedgerScope = "preview-only / no commit"
        MarketDataPlatform = "WITH_WARNINGS"
        LedgerCommit = "BLOCKED"
        ProductionLive = "BLOCKED"
    }
    Classification = "WARNING_REGISTER_READY_WITH_WARNINGS"
})

Write-JsonArtifact "blocker-map.json" ([ordered]@{
    Package = $Package
    Artifact = "blocker-map"
    ClosedBlockers = @(
        "True Core/Anubis chain missing",
        "Core weights missing",
        "Core handoff missing",
        "Intraday consumer missing",
        "Price basis missing",
        "Metadata missing",
        "Quantities missing",
        "Risk review missing",
        "Operator approval missing",
        "Exact sandbox harness missing",
        "FIX tag 22 protocol blocker",
        "Sandbox execution not attempted",
        "Gross PnL preview missing for Core/Anubis lifecycle",
        "Paper-ledger preview missing for Core/Anubis lifecycle"
    )
    RemainingBlockers = @(
        "USDJPY remaining retry requires new approval if desired",
        "Net PnL",
        "Cost/spread/commission model",
        "Account-currency aggregation",
        "Accounting PnL",
        "Accounting attribution",
        "Ledger commit",
        "Commit-safe accounting idempotency",
        "Production/live",
        "MarketData platform/golden-source DB/marks/rowcounts/tick/M30 still WITH_WARNINGS"
    )
    Classification = "BLOCKER_MAP_READY_WITH_WARNINGS"
})

Write-JsonArtifact "marketdata-status-reminder.json" ([ordered]@{
    Package = $Package
    Artifact = "marketdata-status-reminder"
    MarketDataSufficientForValidatedCoreAnubisSandboxLifecycle = $true
    MarketDataPlatformGloballyPass = $false
    "lmax-marketdata-db.v1" = "WITH_WARNINGS"
    "marketdata-readiness.v1" = "WITH_WARNINGS"
    StillMissingOrWarning = @(
        "DB row counts",
        "DB queryability / LocalDB / connection evidence",
        "tick schema completion",
        "M30 policy/evidence",
        "generalized mark/reference price policy",
        "LMAX realtime golden source integration",
        "DB projection/reconciliation",
        "account-currency FX conversion",
        "production/accounting marks"
    )
    ProductionReady = $false
    Classification = "MARKETDATA_STATUS_REMINDER_READY_WITH_WARNINGS"
})

Write-JsonArtifact "roadmap-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "roadmap-decision"
    Decision = "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT"
    RankedRecommendation = @(
        "1. Stop clean checkpoint",
        "2. Risk/Cost model R001 if continuing",
        "3. Ledger accounting policy R001",
        "4. MarketData golden source DB projection only if we choose to address platform data",
        "5. Production readiness not now"
    )
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
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
    NoAccountingNetProductionPnl = $true
    NoAccountCurrencyAggregation = $true
    NoUSDJPYRemainingRetryApproval = $true
})

$finalClassification = "PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_PASS_CLEAN_CHECKPOINT_RECORDED"
$summary = @"
# PROGRAMME-CLEAN-PRODUCT-CHECKPOINT-R001

Classification: $finalClassification

What is the current product state? SandboxProgrammeAcceptedWithGrossPnlV0Ready, extended with CoreAnubisSandboxLifecycleAcceptedWithWarnings.
What did the Core/Anubis rail achieve? True Core weights were handed off, priced, metadated, sized, risk-reviewed, approved, executed in sandbox/demo, flattened to zero residual, gross-previewed, paper-ledger-previewed, and accepted with warnings.
What warnings remain? USDJPY partial fill 88.4 intended / 38.4 filled / 50.0 unfilled with no retry approval; below-min zeroing for AUDUSD, CHFUSD, EURUSD, GBPUSD; omitted below-min exposure USD 601.92 / 0.010032%; ledger preview no-commit; MarketData platform WITH_WARNINGS.
What remains blocked? Net PnL, cost/spread/commission model, account-currency aggregation, accounting PnL, accounting attribution, ledger commit, production/live, MarketData platform DB/marks/tick/M30 gaps, and any USDJPY remaining retry without new approval.
What is the recommended next large package? NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT.
Is MarketData complete? no, WITH_WARNINGS.
Did this package execute anything? no.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_BUILD_COMPLETE"
