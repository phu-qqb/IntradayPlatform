param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sandbox-readiness-update-r014"
$Required = @(
    "r013e-intake-validation.json",
    "core-anubis-sandbox-lifecycle-evidence-summary.json",
    "partial-fill-warning-preservation.json",
    "central-readiness-status-update.json",
    "product-decision-update.json",
    "pnl-readiness-update.json",
    "ledger-readiness-update.json",
    "execution-readiness-update.json",
    "contract-status-update.json",
    "blocker-map-update.json",
    "roadmap-decision.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R014 artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R014 artifact: $name"
}

$intake = Read-Json "r013e-intake-validation.json"
$evidence = Read-Json "core-anubis-sandbox-lifecycle-evidence-summary.json"
$partial = Read-Json "partial-fill-warning-preservation.json"
$central = Read-Json "central-readiness-status-update.json"
$product = Read-Json "product-decision-update.json"
$pnl = Read-Json "pnl-readiness-update.json"
$ledger = Read-Json "ledger-readiness-update.json"
$execution = Read-Json "execution-readiness-update.json"
$contract = Read-Json "contract-status-update.json"
$blocker = Read-Json "blocker-map-update.json"
$roadmap = Read-Json "roadmap-decision.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($intake.Classification -eq "R013E_READY_FOR_SANDBOX_READINESS_UPDATE") "R013E intake not ready."
Assert-True ($intake.R013EDidNotSubmitNewOrders -eq $true -and $intake.R013EDidNotCallLmax -eq $true -and $intake.R013EDidNotMutateDb -eq $true -and $intake.R013EDidNotCommitLedger -eq $true) "R013E intake crossed execution/DB/ledger boundary."
Assert-True ($evidence.Classification -eq "CORE_ANUBIS_SANDBOX_LIFECYCLE_EVIDENCE_READY_WITH_WARNINGS") "Lifecycle evidence missing or contradictory."
Assert-True ($evidence.ActualOpenAttemptCount -eq 9 -and $evidence.FillCount -eq 9 -and $evidence.FlattenCount -eq 9 -and $evidence.ResidualStatus -eq "zero") "Lifecycle counts/residual status wrong."
Assert-True ($evidence.NotProduction -eq $true -and $evidence.NotAccounting -eq $true -and $evidence.NotLedgerCommit -eq $true -and $evidence.SandboxOnly -eq $true) "Lifecycle evidence crossed sandbox boundary."

Assert-True ($partial.Classification -eq "PARTIAL_FILL_WARNING_PRESERVED") "Partial fill warning not preserved."
Assert-True ($partial.USDJPYIntendedQuantity -eq "88.4" -and $partial.USDJPYFilledQuantity -eq "38.4" -and $partial.USDJPYUnfilledQuantity -eq "50.0") "USDJPY partial warning quantities wrong."
Assert-True ($partial.UnfilledQuantityNotTreatedAsFilled -eq $true -and $partial.UnfilledQuantityNotApprovedForRetry -eq $true) "Unfilled USDJPY was treated as filled or approved for retry."
Assert-True ($partial.FutureRetryRequiresNewExplicitOperatorApproval -eq $true -and $partial.FutureRetryRequiresSeparateExecutionPackage -eq $true) "Future USDJPY retry guard missing."

Assert-True ($central.Classification -eq "CENTRAL_READINESS_UPDATED_CORE_ANUBIS_SANDBOX_ACCEPTED_WITH_WARNINGS") "Central readiness status update failed."
Assert-True ($central.ProductDecisionAnchor -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready") "Product anchor not preserved."
Assert-True ($central.CrossRailR014RemainsPmsIntentDriven -eq $true) "CROSS-RAIL-R014 relabelled or not preserved."
Assert-True ($central.R010PrototypeApprovalNonTransferable -eq $true) "R010 prototype transferability not preserved."
Assert-True (@($central.StillBlocked | Where-Object { $_ -eq "Production/live" }).Count -eq 1) "Production/live not blocked in central readiness."

Assert-True ($product.Decision -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready_CoreAnubisSandboxLifecycleAcceptedWithWarnings") "Product decision update wrong."
Assert-True ($product.NoProductionLive -eq $true -and $product.NoNetPnl -eq $true -and $product.NoAccountingPnl -eq $true -and $product.NotLedgerCommit -eq $true) "Product decision claimed forbidden readiness."

Assert-True ($pnl.Classification -eq "PNL_READINESS_UPDATED_GROSS_SANDBOX_ONLY") "PnL readiness update wrong."
Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true -and $pnl.NoCosts -eq $true -and $pnl.NoCommissions -eq $true) "PnL gross-only controls missing."
Assert-True ($pnl.NoFxConversion -eq $true -and $pnl.NoAccountCurrencyAggregation -eq $true -and $pnl.NoNetPnl -eq $true -and $pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true) "PnL forbidden readiness claimed."
Assert-True ($pnl.UnfilledUSDJPYNotIncludedAsFilled -eq $true) "Unfilled USDJPY included as filled in PnL."

Assert-True ($ledger.Classification -eq "LEDGER_READINESS_UPDATED_PREVIEW_ONLY_NO_COMMIT") "Ledger readiness update wrong."
Assert-True ($ledger.PreviewOnly -eq $true -and $ledger.NoCommit -eq $true -and $ledger.NoAccountingLedgerMutation -eq $true -and $ledger.NoProductionLedgerMutation -eq $true) "Ledger commit/mutation boundary failed."
Assert-True ($ledger.DoesNotAssumeUnfilledUSDJPYQuantity -eq $true -and $ledger.LedgerCommitRemainsBlocked -eq $true) "Ledger readiness assumed unfilled USDJPY or unblocked commit."

Assert-True ($execution.Classification -eq "EXECUTION_READINESS_UPDATED_SANDBOX_LIFECYCLE_ACCEPTED_WITH_WARNINGS") "Execution readiness update wrong."
Assert-True ($execution.AnyNewExecutionRequiresNewPackage -eq $true -and $execution.AnyUSDJPYRemainingQuantityRetryRequiresNewOperatorApproval -eq $true -and $execution.NoAutomaticRetries -eq $true) "Execution readiness permits retry/auto execution."
Assert-True ($execution.ProductionLiveRemainsBlocked -eq $true) "Execution readiness unblocked production/live."

Assert-True ($contract.Statuses."core-anubis-sandbox-lifecycle.v1" -eq "YES_WITH_WARNINGS") "Core/Anubis lifecycle contract wrong."
Assert-True ($contract.Statuses."sandbox-reconciliation.v1" -eq "YES") "Sandbox reconciliation contract should be YES."
Assert-True ($contract.Statuses."pnl-preview.v1" -eq "YES_WITH_WARNINGS_GROSS_SANDBOX_QUOTE_CURRENCY_ONLY") "PnL preview contract wrong."
Assert-True ($contract.Statuses."ledger-preview.v1" -eq "YES_WITH_WARNINGS_PREVIEW_ONLY_NO_COMMIT") "Ledger preview contract wrong."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production contracts not blocked."
Assert-True ($contract.Statuses."r009-execution-readiness.v1" -eq "SANDBOX_ONLY_COMPLETED_WITH_WARNINGS_NOT_PRODUCTION") "R009 readiness implies non-sandbox scope."

Assert-True ($blocker.Classification -eq "BLOCKER_MAP_UPDATED_WITH_WARNINGS") "Blocker map not updated."
Assert-True (@($blocker.RemainingBlockers | Where-Object { $_ -eq "future retry requires new approval" }).Count -eq 1) "Future retry blocker missing."
Assert-True (@($blocker.RemainingBlockers | Where-Object { $_ -eq "production/live" }).Count -eq 1) "Production/live blocker missing."
Assert-True ($roadmap.Decision -eq "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT" -and $roadmap.NotAMicroStep -eq $true) "Roadmap decision is missing or too small."

Assert-True ($readiness.CoreAnubisSandboxLifecycleAcceptedWithPartialFillWarning -eq $true) "Readiness impact missing accepted lifecycle."
Assert-True ($readiness.SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid -eq $true) "Central product anchor not preserved in readiness impact."
Assert-True ($readiness.NoNetPnlReadiness -eq $true -and $readiness.NoAccountingPnlReadiness -eq $true -and $readiness.NoLedgerCommitReadiness -eq $true -and $readiness.NoProductionLiveReadiness -eq $true) "Forbidden readiness was claimed."
Assert-True ($readiness.NoNewExecutionOccurredInR014 -eq $true) "R014 claimed new execution."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxCall -eq $true -and $boundary.NoNewOrderFillReport -eq $true) "R014 claimed new R009/LMAX/order/fill."
Assert-True ($boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true -and $boundary.NoProductionLive -eq $true) "R014 crossed DB/ledger/production boundary."
Assert-True ($boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "R014 crossed Core/manager/Anubis/CUDA/netting boundary."
Assert-True ($boundary.NoR010PrototypeTransfer -eq $true -and $boundary.NoUSDJPYRemainingRetryApproval -eq $true) "R014 allowed R010 transfer or USDJPY retry."
Assert-True ($boundary.NoAccountingNetProductionPnl -eq $true -and $boundary.NoAccountCurrencyAggregation -eq $true) "R014 claimed accounting/net/production PnL or account-currency aggregation."

Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_PASS_PRODUCT_STATE_UPDATED_CORE_ANUBIS_LIFECYCLE_ACCEPTED_WITH_WARNINGS")) "Summary missing final classification."
Assert-True ($summary.Contains("unfilled 50.0")) "Summary missing USDJPY unfilled warning."
Assert-True ($summary.Contains("NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT")) "Summary missing next package."

Write-Host "CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_GATE_PASS"
