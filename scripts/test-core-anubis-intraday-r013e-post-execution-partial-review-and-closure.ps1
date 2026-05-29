param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$intake = Read-Json "r013d-intake-validation.json"
$review = Read-Json "intended-vs-actual-execution-review.json"
$partial = Read-Json "partial-fill-impact-review.json"
$flatten = Read-Json "flatten-residual-closure-validation.json"
$pnl = Read-Json "gross-sandbox-pnl-preview-validation.json"
$ledger = Read-Json "paper-ledger-preview-validation.json"
$remaining = Read-Json "remaining-quantity-decision.json"
$acceptance = Read-Json "lifecycle-acceptance-decision.json"
$future = Read-Json "future-package-decision.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($intake.Classification -eq "R013D_READY_FOR_POST_EXECUTION_REVIEW") "R013D intake is not ready."
Assert-True ($intake.RetryExecutedExactlyOnce -eq $true -and $intake.ProducedFills -eq $true) "R013D retry/fill evidence missing."
Assert-True ($intake.USDJPYPartialFill -eq "38.4 / 88.4") "R013D intake did not preserve USDJPY partial fill."
Assert-True ($intake.NoLedgerCommit -eq $true -and $intake.NoProductionLiveRoute -eq $true) "R013D intake crossed ledger/production boundary."

Assert-True ($review.Classification -eq "INTENDED_ACTUAL_REVIEW_READY_PARTIAL_USDJPY_ONLY") "Intended-vs-actual review did not isolate USDJPY partial."
Assert-True (@($review.Rows).Count -eq 9) "Expected 9 intended-vs-actual rows."
Assert-True (@($review.Rows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.IntendedQuantity -eq "88.4" -and $_.FilledQuantity -eq "38.4" -and $_.UnfilledQuantity -eq "50.0" -and $_.FillStatus -eq "PARTIAL" }).Count -eq 1) "USDJPY partial row missing or wrong."
Assert-True (@($review.Rows | Where-Object { $_.ExecutionSymbol -ne "USDJPY" -and $_.FillStatus -ne "FULL" }).Count -eq 0) "Unexpected non-USDJPY partial/non-full fill."
Assert-True ($review.ZeroQuantityLinesWereNotSubmitted -eq $true) "Zero quantity lines were submitted."

Assert-True ($partial.Classification -eq "PARTIAL_FILL_IMPACT_READY_RESIDUAL_ZERO") "Partial fill impact not ready."
Assert-True ($partial.UnfilledQuantity -eq "50.0") "Unfilled USDJPY quantity should be 50.0."
Assert-True ($partial.UnfilledQuantityRemainsUnexecutedAndMustNotBeAssumedFilled -eq $true) "Unfilled USDJPY was treated as filled."
Assert-True ($partial.ApprovedCandidateEconomicsChanged -eq $false) "Approved candidate economics should not change."

Assert-True ($flatten.Classification -eq "FLATTEN_RESIDUAL_CLOSURE_PASS_ZERO_RESIDUAL") "Flatten/residual closure failed."
Assert-True ($flatten.FlattenSubmittedOnlyForFilledQuantities -eq $true -and $flatten.FlattenOrdersMatchedFilledQuantities -eq $true) "Flatten did not match actual filled quantities."
Assert-True ($flatten.NoOpenPositionRemains -eq $true -and $flatten.NoFailedFlattenRiskRemains -eq $true) "Open position or flatten risk remains."

Assert-True ($pnl.Classification -eq "GROSS_SANDBOX_PNL_PREVIEW_VALID_WITH_WARNINGS") "Gross sandbox PnL validation failed."
Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true) "PnL is not gross quote-currency only."
Assert-True ($pnl.NoFxConversion -eq $true -and $pnl.NoAccountCurrencyAggregation -eq $true -and $pnl.NoNetPnl -eq $true) "PnL conversion/net aggregation boundary failed."
Assert-True ($pnl.PartialUSDJPYHandledUsingActualFillOnly -eq $true) "PnL did not use actual USDJPY fill only."
Assert-True (@($pnl.GrossPnlByExecutionSymbol | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.Quantity -eq "38.4" }).Count -eq 1) "USDJPY PnL row must use 38.4 only."

Assert-True ($ledger.Classification -eq "PAPER_LEDGER_PREVIEW_VALID_NO_COMMIT") "Paper-ledger preview validation failed."
Assert-True ($ledger.NoCommit -eq $true -and $ledger.NoAccountingLedgerMutation -eq $true -and $ledger.NoProductionLedgerMutation -eq $true) "Paper ledger commit/mutation boundary failed."
Assert-True ($ledger.NoAssumptionOfUnfilledUSDJPYQuantity -eq $true) "Ledger preview assumed unfilled USDJPY quantity."

Assert-True ($remaining.Decision -eq "REMAINING_USDJPY_NOT_RETRIED_LIFECYCLE_ACCEPTABLE_WITH_WARNING") "Remaining USDJPY decision should accept lifecycle without retry."
Assert-True ($remaining.NoRetryApprovedInThisPackage -eq $true -and $remaining.AutomaticRemainingQuantityRetryForbidden -eq $true) "Remaining USDJPY retry was approved or automated."
Assert-True ($remaining.FutureRetryWouldRequireNewOperatorApproval -eq $true -and $remaining.FutureRetryWouldRequireSeparateBoundedExecutionPackage -eq $true) "Future retry approval/package requirement missing."

Assert-True ($acceptance.Decision -eq "CORE_ANUBIS_SANDBOX_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING") "Lifecycle was not accepted with partial-fill warning."
Assert-True ($acceptance.NotProduction -eq $true -and $acceptance.NotAccounting -eq $true -and $acceptance.NotLedgerCommit -eq $true) "Lifecycle acceptance crossed readiness boundary."
Assert-True ($acceptance.NoFurtherExecutionApproved -eq $true -and $acceptance.R010Transferability -eq $false -and $acceptance.CROSS_RAIL_R014_Unchanged -eq $true) "Execution/R010/CROSS-RAIL guard missing."
Assert-True ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014") "Future package decision wrong."

Assert-True ($readiness.CoreAnubisSandboxLifecycleAcceptedWithWarning -eq $true) "Readiness did not accept lifecycle with warning."
Assert-True ($readiness.NoNetPnl -eq $true -and $readiness.NoAccountingPnl -eq $true -and $readiness.NoProductionPnl -eq $true -and $readiness.NoLedgerCommit -eq $true) "Readiness crossed PnL/ledger boundary."
Assert-True ($boundary.NoNewR009SubmissionInR013E -eq $true -and $boundary.NoNewLmaxCallInR013E -eq $true -and $boundary.NoNewOrderFillReport -eq $true) "R013E claimed new execution activity."
Assert-True ($boundary.NoRemainingUSDJPYRetryApprovedInThisPackage -eq $true) "R013E approved remaining USDJPY retry."

Write-Host "CORE_ANUBIS_INTRADAY_R013E_POST_EXECUTION_PARTIAL_REVIEW_AND_CLOSURE_TESTS_PASS"
