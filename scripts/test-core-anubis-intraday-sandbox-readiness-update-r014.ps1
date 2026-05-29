param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sandbox-readiness-update-r014"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
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

Assert-True ($intake.Classification -eq "R013E_READY_FOR_SANDBOX_READINESS_UPDATE") "R013E intake not ready."
Assert-True ($intake.R013EDidNotSubmitNewOrders -eq $true -and $intake.R013EDidNotCallLmax -eq $true) "R013E claimed new execution."
Assert-True ($intake.R013EDidNotMutateDb -eq $true -and $intake.R013EDidNotCommitLedger -eq $true -and $intake.R013EKeptProductionLiveBlocked -eq $true) "R013E crossed DB/ledger/production boundary."

Assert-True ($evidence.Classification -eq "CORE_ANUBIS_SANDBOX_LIFECYCLE_EVIDENCE_READY_WITH_WARNINGS") "Lifecycle evidence summary not ready with warnings."
Assert-True ($evidence.Source -eq "CoreAnubisNettedUsdWeights") "Wrong lifecycle source."
Assert-True ($evidence.ActualOpenAttemptCount -eq 9 -and $evidence.FillCount -eq 9 -and $evidence.FlattenCount -eq 9) "Lifecycle counts are wrong."
Assert-True ($evidence.ResidualStatus -eq "zero") "Residual status not zero."
Assert-True ($evidence.NotProduction -eq $true -and $evidence.NotAccounting -eq $true -and $evidence.NotLedgerCommit -eq $true -and $evidence.SandboxOnly -eq $true) "Lifecycle evidence crossed scope boundary."

Assert-True ($partial.Classification -eq "PARTIAL_FILL_WARNING_PRESERVED") "Partial-fill warning not preserved."
Assert-True ($partial.USDJPYIntendedQuantity -eq "88.4" -and $partial.USDJPYFilledQuantity -eq "38.4" -and $partial.USDJPYUnfilledQuantity -eq "50.0") "USDJPY partial quantities wrong."
Assert-True ($partial.UnfilledQuantityNotTreatedAsFilled -eq $true -and $partial.UnfilledQuantityNotApprovedForRetry -eq $true) "USDJPY unfilled quantity mishandled."
Assert-True ($partial.FutureRetryRequiresNewExplicitOperatorApproval -eq $true -and $partial.FutureRetryRequiresSeparateExecutionPackage -eq $true) "Future retry guard missing."

Assert-True ($central.Classification -eq "CENTRAL_READINESS_UPDATED_CORE_ANUBIS_SANDBOX_ACCEPTED_WITH_WARNINGS") "Central readiness not updated correctly."
Assert-True ($central.ProductDecisionAnchor -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready") "Product anchor not preserved."
Assert-True ($central.CrossRailR014RemainsPmsIntentDriven -eq $true -and $central.R010PrototypeApprovalNonTransferable -eq $true) "CROSS-RAIL/R010 guard failed."
Assert-True (@($central.StillBlocked | Where-Object { $_ -eq "Production/live" }).Count -eq 1) "Production/live blocker missing."

Assert-True ($product.Decision -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready_CoreAnubisSandboxLifecycleAcceptedWithWarnings") "Product decision wrong."
Assert-True ($product.NotProduction -eq $true -and $product.NotAccounting -eq $true -and $product.NotLedgerCommit -eq $true) "Product decision crossed scope boundary."
Assert-True ($product.NoNetPnl -eq $true -and $product.NoAccountingPnl -eq $true) "Product decision claimed forbidden PnL."

Assert-True ($pnl.Classification -eq "PNL_READINESS_UPDATED_GROSS_SANDBOX_ONLY") "PnL readiness not gross-only."
Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true -and $pnl.NoCosts -eq $true -and $pnl.NoCommissions -eq $true) "PnL gross/quote/no-cost boundary failed."
Assert-True ($pnl.NoFxConversion -eq $true -and $pnl.NoAccountCurrencyAggregation -eq $true -and $pnl.NoNetPnl -eq $true -and $pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true) "PnL forbidden boundary failed."
Assert-True ($pnl.PartialUSDJPYHandledUsingActualFillsOnly -eq $true -and $pnl.UnfilledUSDJPYNotIncludedAsFilled -eq $true) "USDJPY partial PnL handling wrong."

Assert-True ($ledger.Classification -eq "LEDGER_READINESS_UPDATED_PREVIEW_ONLY_NO_COMMIT") "Ledger readiness not preview-only."
Assert-True ($ledger.PreviewOnly -eq $true -and $ledger.NoCommit -eq $true -and $ledger.NoAccountingLedgerMutation -eq $true -and $ledger.NoProductionLedgerMutation -eq $true) "Ledger commit/mutation boundary failed."
Assert-True ($ledger.DoesNotAssumeUnfilledUSDJPYQuantity -eq $true -and $ledger.LedgerCommitRemainsBlocked -eq $true) "Ledger assumed unfilled quantity or unblocked commit."

Assert-True ($execution.Classification -eq "EXECUTION_READINESS_UPDATED_SANDBOX_LIFECYCLE_ACCEPTED_WITH_WARNINGS") "Execution readiness wrong."
Assert-True ($execution.AnyNewExecutionRequiresNewPackage -eq $true -and $execution.AnyUSDJPYRemainingQuantityRetryRequiresNewOperatorApproval -eq $true -and $execution.NoAutomaticRetries -eq $true) "Execution retry guard missing."
Assert-True ($execution.ProductionLiveRemainsBlocked -eq $true) "Execution readiness unblocked production/live."

Assert-True ($contract.Statuses."core-anubis-sandbox-lifecycle.v1" -eq "YES_WITH_WARNINGS") "Sandbox lifecycle contract not YES_WITH_WARNINGS."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production contracts not blocked."
Assert-True ($contract.Statuses."r009-execution-readiness.v1" -eq "SANDBOX_ONLY_COMPLETED_WITH_WARNINGS_NOT_PRODUCTION") "R009 readiness implies wrong scope."

Assert-True ($blocker.Classification -eq "BLOCKER_MAP_UPDATED_WITH_WARNINGS") "Blocker map not updated with warnings."
Assert-True (@($blocker.ClosedBlockers | Where-Object { $_ -eq "FIX tag 22 protocol blocker" }).Count -eq 1) "FIX blocker not closed."
Assert-True (@($blocker.RemainingBlockers | Where-Object { $_ -eq "future retry requires new approval" }).Count -eq 1) "Future retry blocker missing."
Assert-True ($roadmap.Decision -eq "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT" -and $roadmap.NotAMicroStep -eq $true) "Roadmap decision wrong."

Assert-True ($readiness.CoreAnubisSandboxLifecycleAcceptedWithPartialFillWarning -eq $true) "Readiness impact missing lifecycle acceptance."
Assert-True ($readiness.NoNetPnlReadiness -eq $true -and $readiness.NoAccountingPnlReadiness -eq $true -and $readiness.NoLedgerCommitReadiness -eq $true -and $readiness.NoProductionLiveReadiness -eq $true) "Readiness impact unblocked forbidden readiness."
Assert-True ($readiness.NoNewExecutionOccurredInR014 -eq $true) "R014 claimed new execution."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxCall -eq $true -and $boundary.NoNewOrderFillReport -eq $true) "R014 claimed new execution activity."
Assert-True ($boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true -and $boundary.NoProductionLive -eq $true) "R014 crossed DB/ledger/production boundary."
Assert-True ($boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "R014 crossed Core/manager/Anubis/CUDA/netting boundary."
Assert-True ($boundary.NoR010PrototypeTransfer -eq $true -and $boundary.NoUSDJPYRemainingRetryApproval -eq $true) "R010 transfer or USDJPY retry approved."

Write-Host "CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_TESTS_PASS"
