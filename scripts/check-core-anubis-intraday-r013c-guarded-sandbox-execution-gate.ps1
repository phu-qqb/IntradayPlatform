param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013c-guarded-sandbox-execution"
$Required = @(
    "r013b-harness-intake-validation.json",
    "exact-candidate-approval-revalidation.json",
    "execution-mapping-final-validation.json",
    "sandbox-route-profile-final-validation.json",
    "idempotency-final-validation.json",
    "pre-execution-gate-decision.json",
    "open-order-plan-final.json",
    "flatten-plan-final.json",
    "guarded-r009-sandbox-open-execution.json",
    "guarded-sandbox-flatten-execution.json",
    "sandbox-reconciliation.json",
    "sandbox-gross-pnl-preview-r013c.json",
    "paper-ledger-preview-update.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R013C artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R013C artifact: $name"
}

$intake = Read-Json "r013b-harness-intake-validation.json"
$candidate = Read-Json "exact-candidate-approval-revalidation.json"
$mapping = Read-Json "execution-mapping-final-validation.json"
$route = Read-Json "sandbox-route-profile-final-validation.json"
$idempotency = Read-Json "idempotency-final-validation.json"
$gate = Read-Json "pre-execution-gate-decision.json"
$openPlan = Read-Json "open-order-plan-final.json"
$flattenPlan = Read-Json "flatten-plan-final.json"
$openExecution = Read-Json "guarded-r009-sandbox-open-execution.json"
$flattenExecution = Read-Json "guarded-sandbox-flatten-execution.json"
$recon = Read-Json "sandbox-reconciliation.json"
$pnl = Read-Json "sandbox-gross-pnl-preview-r013c.json"
$paperLedger = Read-Json "paper-ledger-preview-update.json"
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($intake.Classification -eq "R013B_HARNESS_READY_FOR_R013C_EXECUTION") "R013B harness intake not ready."
Assert-True ($candidate.Classification -eq "EXACT_CANDIDATE_APPROVAL_REVALIDATED") "Exact approval/candidate revalidation failed."
Assert-True ($candidate.R010Transferability -eq $false) "R010 prototype transferability must remain false."
Assert-True ($mapping.Classification -eq "EXECUTION_MAPPING_FINAL_READY_ALL_LINES") "Execution mapping final validation failed."
Assert-True (@($mapping.Rows).Count -eq 9) "Expected 9 final mapping rows."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -eq "JPYUSD" -and $_.ExecutionSymbol -eq "USDJPY" -and $_.ExecutionSide -eq "BUY" -and $_.RequiresInversion -eq $true }).Count -eq 1) "JPYUSD inversion not handled."
Assert-True ($route.Classification -eq "SANDBOX_ROUTE_PROFILE_FINAL_READY") "Route/profile final validation failed."
Assert-True ($route.ProductionRouteDisabled -eq $true -and $route.NoProductionAccount -eq $true -and $route.NoProductionLiveEndpoint -eq $true) "Production/live route not blocked."
Assert-True ($idempotency.Classification -eq "IDEMPOTENCY_FINAL_READY") "Idempotency final validation failed."

Assert-True ($gate.Classification -eq "PRE_EXECUTION_GATE_PASS_READY_TO_SUBMIT_R013C_SANDBOX") "Pre-execution gate did not pass."
Assert-True ($openPlan.Classification -eq "OPEN_ORDER_PLAN_FINAL_READY") "Open plan final not ready."
Assert-True (@($openPlan.Orders).Count -eq 9) "Open plan must contain 9 orders."
Assert-True ($openPlan.NoZeroQuantityOrders -eq $true -and $openPlan.NoUnapprovedSymbols -eq $true) "Open plan contains zero/unapproved order."
Assert-True ($flattenPlan.Classification -eq "FLATTEN_PLAN_FINAL_READY") "Flatten plan final not ready."

Assert-True ($openExecution.ZeroQuantityOrdersSubmitted -eq 0) "Zero quantity order submitted."
if ($openExecution.Started -eq $true) {
    Assert-True (@($openExecution.Results).Count -gt 0) "Open execution started but no result recorded."
    Assert-True (@($openExecution.Results).Count -le 9) "More than 9 open attempts recorded."
}
if ($flattenExecution.Started -eq $true) {
    Assert-True (@($flattenExecution.Results).Count -le 9) "More than 9 flatten attempts recorded."
}
if ($recon.Classification -eq "SANDBOX_RECONCILIATION_PASS_RESIDUAL_ZERO") {
    Assert-True (@($recon.Residuals | Where-Object { [decimal]$_.ResidualSignedQuantity -ne 0 }).Count -eq 0) "Residual-zero reconciliation has non-zero residuals."
}
Assert-True ($pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true -and $pnl.NoLedgerCommit -eq $true) "PnL preview boundary failed."
Assert-True ($paperLedger.Commit -eq $false -and $paperLedger.ProductionFill -eq $false) "Paper-ledger preview boundary failed."

Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production status must remain blocked."
Assert-True ($readiness.NoAccountingNetProductionPnl -eq $true -and $readiness.NoLedgerCommit -eq $true -and $readiness.ProductionLiveRemainsBlocked -eq $true) "Readiness impact crossed forbidden boundary."

Assert-True ($boundary.SandboxDemoOnly -eq $true) "Boundary does not confirm sandbox/demo only."
Assert-True ($boundary.NoProductionLiveLmax -eq $true -and $boundary.NoProductionBrokerRoute -eq $true -and $boundary.NoProductionOrderFillReport -eq $true) "Production/live boundary failed."
Assert-True ($boundary.NoLedgerCommit -eq $true -and $boundary.NoAccountingLedgerMutation -eq $true -and $boundary.NoProductionStateMutation -eq $true) "Ledger/production mutation boundary failed."
Assert-True ($boundary.NoZeroQuantityOrderSubmitted -eq $true) "Zero quantity boundary failed."
Assert-True ($boundary.R010PrototypeApprovalNotReused -eq $true) "R010 prototype approval was reused."
Assert-True ($boundary.JPYUSDInversionHandled -eq $true) "JPYUSD inversion not handled."
Assert-True ($boundary.NoAccountCurrencyAggregation -eq $true -and $boundary.NoNetPnl -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Forbidden PnL/account aggregation boundary failed."

$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")
Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_R013C_")) "Summary missing R013C classification."

Write-Host "CORE_ANUBIS_INTRADAY_R013C_GUARDED_SANDBOX_EXECUTION_GATE_PASS"
