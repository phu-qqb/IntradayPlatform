param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\programme-clean-product-checkpoint-r001"
$Required = @(
    "latest-package-intake-validation.json",
    "central-product-state-snapshot.json",
    "core-anubis-rail-final-summary.json",
    "warning-register.json",
    "blocker-map.json",
    "marketdata-status-reminder.json",
    "roadmap-decision.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Checkpoint artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required checkpoint artifact: $name"
}

$intake = Read-Json "latest-package-intake-validation.json"
$state = Read-Json "central-product-state-snapshot.json"
$rail = Read-Json "core-anubis-rail-final-summary.json"
$warnings = Read-Json "warning-register.json"
$blockers = Read-Json "blocker-map.json"
$marketData = Read-Json "marketdata-status-reminder.json"
$roadmap = Read-Json "roadmap-decision.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($intake.Classification -eq "LATEST_R014_READY_FOR_CLEAN_CHECKPOINT") "Latest R014 intake is not ready."
Assert-True ($intake.R014ClassificationMatches -eq $true) "R014 classification mismatch."
Assert-True ($intake.ProductStateUpdated -eq $true -and $intake.CoreAnubisLifecycleAcceptedWithWarnings -eq $true) "Product state/lifecycle acceptance not preserved."
Assert-True ($intake.ResidualsZero -eq $true -and $intake.GrossSandboxPnlPreviewValid -eq $true -and $intake.PaperLedgerPreviewValidNoCommit -eq $true) "Residual/PnL/ledger preview readiness missing."
Assert-True ($intake.ProductionLiveBlocked -eq $true -and $intake.AccountingNetPnlBlocked -eq $true -and $intake.LedgerCommitBlocked -eq $true -and $intake.NoNewExecutionOccurredInR014 -eq $true) "Latest intake crossed forbidden readiness."

Assert-True ($state.Classification -eq "CENTRAL_PRODUCT_STATE_SNAPSHOT_READY_WITH_WARNINGS") "Central product state snapshot not ready with warnings."
Assert-True ($state.ProductDecision -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready") "Product decision anchor not preserved."
Assert-True ($state.CoreAnubisSandboxLifecycle -eq "AcceptedWithWarnings") "Core/Anubis lifecycle warning state missing."
Assert-True ($state.HistoricalCrossRailR014 -eq "PMSIntentDriven") "Historical CROSS-RAIL-R014 was relabelled."
Assert-True ($state.SandboxQubesPrototype -eq "FallbackTestPrototypeOnly" -and $state.R010PrototypeApprovalTransferability -eq $false) "Prototype/R010 transferability wrong."
Assert-True ($state.ProductionLiveReady -eq $false -and $state.LedgerCommitReady -eq $false -and $state.AccountingPnlReady -eq $false -and $state.NetPnlReady -eq $false) "Blocked readiness was marked ready."
Assert-True (@($state.Blocked | Where-Object { $_ -eq "Production/live" }).Count -eq 1) "Production/live missing from blocked list."

Assert-True ($rail.Classification -eq "CORE_ANUBIS_RAIL_FINAL_SUMMARY_READY_WITH_WARNINGS") "Core/Anubis final summary not ready."
Assert-True (@($rail.Milestones | Where-Object { $_ -eq "R013D fixed tag 22 LMAX -> 8" }).Count -eq 1) "Tag22 fix milestone missing."
Assert-True (@($rail.Milestones | Where-Object { $_ -eq "R013E accepted lifecycle with partial-fill warning" }).Count -eq 1) "R013E acceptance milestone missing."

Assert-True ($warnings.Classification -eq "WARNING_REGISTER_READY_WITH_WARNINGS") "Warning register not ready."
Assert-True ($warnings.Warnings.USDJPYPartialFill.Intended -eq "88.4" -and $warnings.Warnings.USDJPYPartialFill.Filled -eq "38.4" -and $warnings.Warnings.USDJPYPartialFill.Unfilled -eq "50.0") "USDJPY partial warning not preserved."
Assert-True ($warnings.Warnings.USDJPYPartialFill.RetryApproved -eq $false) "Unfilled USDJPY retry was approved."
Assert-True (@($warnings.Warnings.BelowMinZeroing).Count -eq 4) "Below-min zeroing warning incomplete."
Assert-True ($warnings.Warnings.MarketDataPlatform -eq "WITH_WARNINGS" -and $warnings.Warnings.LedgerCommit -eq "BLOCKED" -and $warnings.Warnings.ProductionLive -eq "BLOCKED") "MarketData/ledger/production warnings wrong."

Assert-True ($blockers.Classification -eq "BLOCKER_MAP_READY_WITH_WARNINGS") "Blocker map not ready."
Assert-True (@($blockers.ClosedBlockers | Where-Object { $_ -eq "FIX tag 22 protocol blocker" }).Count -eq 1) "FIX tag22 blocker not closed."
Assert-True (@($blockers.RemainingBlockers | Where-Object { $_ -eq "USDJPY remaining retry requires new approval if desired" }).Count -eq 1) "USDJPY retry blocker missing."
Assert-True (@($blockers.RemainingBlockers | Where-Object { $_ -eq "Production/live" }).Count -eq 1) "Production/live blocker missing."

Assert-True ($marketData.Classification -eq "MARKETDATA_STATUS_REMINDER_READY_WITH_WARNINGS") "MarketData reminder not ready with warnings."
Assert-True ($marketData.MarketDataSufficientForValidatedCoreAnubisSandboxLifecycle -eq $true) "MarketData sufficiency for lifecycle missing."
Assert-True ($marketData.MarketDataPlatformGloballyPass -eq $false -and $marketData."lmax-marketdata-db.v1" -eq "WITH_WARNINGS" -and $marketData."marketdata-readiness.v1" -eq "WITH_WARNINGS") "MarketData was incorrectly promoted."
Assert-True ($marketData.ProductionReady -eq $false) "MarketData was marked production-ready."

Assert-True ($roadmap.Decision -eq "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT") "Roadmap decision is not stop clean checkpoint."
Assert-True (@($roadmap.RankedRecommendation).Count -eq 5) "Roadmap ranked recommendation incomplete."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxCall -eq $true -and $boundary.NoNewPolygonMassiveCall -eq $true) "Checkpoint claimed an external/execution call."
Assert-True ($boundary.NoNewOrderFillReport -eq $true -and $boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "Checkpoint claimed order/fill/DB/ledger mutation."
Assert-True ($boundary.NoProductionLive -eq $true -and $boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "Checkpoint crossed production/Core boundary."
Assert-True ($boundary.NoR010PrototypeTransfer -eq $true -and $boundary.NoAccountingNetProductionPnl -eq $true -and $boundary.NoAccountCurrencyAggregation -eq $true -and $boundary.NoUSDJPYRemainingRetryApproval -eq $true) "Checkpoint crossed R010/PnL/account/retry boundary."

Assert-True ($summary.Contains("PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_PASS_CLEAN_CHECKPOINT_RECORDED")) "Summary missing final classification."
Assert-True ($summary.Contains("SandboxProgrammeAcceptedWithGrossPnlV0Ready")) "Summary missing product state."
Assert-True ($summary.Contains("MarketData complete? no, WITH_WARNINGS") -or $summary.Contains("Is MarketData complete? no, WITH_WARNINGS")) "Summary missing MarketData warning."
Assert-True ($summary.Contains("Did this package execute anything? no.")) "Summary must confirm no execution."

Write-Host "PROGRAMME_CLEAN_PRODUCT_CHECKPOINT_R001_GATE_PASS"
