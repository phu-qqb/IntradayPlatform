param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure"
$Required = @(
    "r013d-intake-validation.json",
    "intended-vs-actual-execution-review.json",
    "partial-fill-impact-review.json",
    "flatten-residual-closure-validation.json",
    "gross-sandbox-pnl-preview-validation.json",
    "paper-ledger-preview-validation.json",
    "remaining-quantity-decision.json",
    "lifecycle-acceptance-decision.json",
    "future-package-decision.json",
    "contract-status-update.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R013E artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R013E artifact: $name"
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
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($intake.Classification -eq "R013D_READY_FOR_POST_EXECUTION_REVIEW") "R013D intake not ready."
Assert-True ($intake.RetryExecutedExactlyOnce -eq $true -and $intake.ProducedFills -eq $true) "R013D retry/fill evidence missing."
Assert-True ($intake.USDJPYPartialFill -eq "38.4 / 88.4") "USDJPY partial fill evidence missing."
Assert-True ($intake.ResidualsZero -eq $true -and $intake.NoLedgerCommit -eq $true -and $intake.NoProductionLiveRoute -eq $true) "R013D residual/boundary evidence failed."

Assert-True ($review.Classification -eq "INTENDED_ACTUAL_REVIEW_READY_PARTIAL_USDJPY_ONLY") "Intended-vs-actual review not ready."
Assert-True (@($review.Rows).Count -eq 9) "Expected 9 intended/actual rows."
Assert-True (@($review.Rows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.IntendedQuantity -eq "88.4" -and $_.FilledQuantity -eq "38.4" -and $_.UnfilledQuantity -eq "50.0" -and $_.FillStatus -eq "PARTIAL" }).Count -eq 1) "USDJPY unfilled quantity not represented correctly."
Assert-True (@($review.Rows | Where-Object { $_.ExecutionSymbol -ne "USDJPY" -and $_.FillStatus -ne "FULL" }).Count -eq 0) "Unexpected non-USDJPY partial or non-full fill."
Assert-True (@($review.Rows | Where-Object { $_.ResidualStatus -ne "ZERO" }).Count -eq 0) "Residuals must be zero for acceptance."
Assert-True ($review.ZeroQuantityLinesWereNotSubmitted -eq $true) "Zero-quantity line was submitted."

Assert-True ($partial.Classification -eq "PARTIAL_FILL_IMPACT_READY_RESIDUAL_ZERO") "Partial fill impact review failed."
Assert-True ($partial.UnfilledQuantity -eq "50.0") "Unfilled USDJPY quantity should be 50.0."
Assert-True ($partial.UnfilledQuantityRemainsUnexecutedAndMustNotBeAssumedFilled -eq $true) "Unfilled USDJPY treated as filled."
Assert-True ($partial.ApprovedCandidateEconomicsChanged -eq $false) "Approved candidate economics changed."

Assert-True ($flatten.Classification -eq "FLATTEN_RESIDUAL_CLOSURE_PASS_ZERO_RESIDUAL") "Flatten residual closure failed."
Assert-True ($flatten.FlattenSubmittedOnlyForFilledQuantities -eq $true -and $flatten.FlattenOrdersMatchedFilledQuantities -eq $true) "Flatten did not match filled quantities."
Assert-True ($flatten.NoOpenPositionRemains -eq $true -and $flatten.NoFailedFlattenRiskRemains -eq $true) "Open position/flatten risk remains."

Assert-True ($pnl.Classification -eq "GROSS_SANDBOX_PNL_PREVIEW_VALID_WITH_WARNINGS") "Gross PnL preview invalid."
Assert-True ($pnl.BasedOnlyOnActualOpenAndFlattenFills -eq $true -and $pnl.PartialUSDJPYHandledUsingActualFillOnly -eq $true) "PnL preview did not use actual fills only."
Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true -and $pnl.NoFxConversion -eq $true -and $pnl.NoAccountCurrencyAggregation -eq $true) "PnL scope boundary failed."
Assert-True ($pnl.NoNetPnl -eq $true -and $pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true -and $pnl.NoLedgerCommit -eq $true) "Forbidden PnL/ledger claim found."

Assert-True ($ledger.Classification -eq "PAPER_LEDGER_PREVIEW_VALID_NO_COMMIT") "Paper-ledger preview invalid."
Assert-True ($ledger.NoCommit -eq $true -and $ledger.NoAccountingLedgerMutation -eq $true -and $ledger.NoProductionLedgerMutation -eq $true) "Ledger preview crossed commit/mutation boundary."
Assert-True ($ledger.UsesActualFillsOnly -eq $true -and $ledger.NoAssumptionOfUnfilledUSDJPYQuantity -eq $true) "Ledger preview assumed unfilled USDJPY quantity."

Assert-True ($remaining.Decision -eq "REMAINING_USDJPY_NOT_RETRIED_LIFECYCLE_ACCEPTABLE_WITH_WARNING") "Remaining USDJPY decision wrong."
Assert-True ($remaining.NoRetryApprovedInThisPackage -eq $true -and $remaining.AutomaticRemainingQuantityRetryForbidden -eq $true) "R013E approved/automated a remaining retry."
Assert-True ($remaining.FutureRetryWouldRequireNewOperatorApproval -eq $true -and $remaining.FutureRetryWouldRequireSeparateBoundedExecutionPackage -eq $true) "Future retry guard missing."

Assert-True ($acceptance.Decision -eq "CORE_ANUBIS_SANDBOX_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING") "Lifecycle not accepted with partial warning."
Assert-True ($acceptance.ResidualsZero -eq $true -and $acceptance.NoFurtherExecutionApproved -eq $true) "Acceptance requires zero residuals and no further execution."
Assert-True ($acceptance.NotProduction -eq $true -and $acceptance.NotAccounting -eq $true -and $acceptance.NotLedgerCommit -eq $true) "Acceptance crossed readiness boundary."
Assert-True ($acceptance.R010Transferability -eq $false -and $acceptance.CROSS_RAIL_R014_Unchanged -eq $true) "R010/CROSS-RAIL guard missing."
Assert-True ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014") "Future package decision wrong."

Assert-True ($contract.Statuses."sandbox-reconciliation.v1" -eq "YES_RESIDUAL_ZERO") "Sandbox reconciliation contract must be YES."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production must remain blocked."
Assert-True ($readiness.NoNetPnl -eq $true -and $readiness.NoAccountingPnl -eq $true -and $readiness.NoProductionPnl -eq $true -and $readiness.NoLedgerCommit -eq $true) "Readiness impact crossed PnL/ledger boundary."
Assert-True ($readiness.ProductionLiveRemainsBlocked -eq $true) "Production/live must remain blocked."

Assert-True ($boundary.NoNewR009SubmissionInR013E -eq $true -and $boundary.NoNewLmaxCallInR013E -eq $true -and $boundary.NoNewOrderFillReport -eq $true) "R013E claimed new R009/LMAX/order/fill/report."
Assert-True ($boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "R013E claimed DB mutation or ledger commit."
Assert-True ($boundary.NoProductionLive -eq $true -and $boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true) "Forbidden production/Core/manager/Anubis/CUDA boundary crossed."
Assert-True ($boundary.NoR010PrototypeTransfer -eq $true -and $boundary.NoRemainingUSDJPYRetryApprovedInThisPackage -eq $true) "R010 transfer or remaining retry was approved."

Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_R013E_PASS_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING")) "Summary missing final pass classification."
Assert-True ($summary.Contains("USDJPY 50.0 remains unexecuted")) "Summary must disclose unfilled USDJPY quantity."
Assert-True ($summary.Contains("Is production/live still blocked? yes.")) "Summary must confirm production/live blocked."

Write-Host "CORE_ANUBIS_INTRADAY_R013E_POST_EXECUTION_PARTIAL_REVIEW_AND_CLOSURE_GATE_PASS"
