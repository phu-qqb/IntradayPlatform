param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013c-guarded-sandbox-execution"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

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
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($candidate.Classification -eq "EXACT_CANDIDATE_APPROVAL_REVALIDATED") "Exact candidate approval did not revalidate."
Assert-True ($candidate.R010Transferability -eq $false) "R010 prototype transferability must be false."
Assert-True ($mapping.Classification -eq "EXECUTION_MAPPING_FINAL_READY_ALL_LINES") "Execution mapping final validation failed."
Assert-True (@($mapping.Rows).Count -eq 9) "Expected 9 execution mapping rows."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -in @("AUDUSD","CHFUSD","EURUSD","GBPUSD") }).Count -eq 0) "Zero quantity symbols appeared in final mapping."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -eq "JPYUSD" -and $_.ExecutionSymbol -eq "USDJPY" -and $_.ExecutionSide -eq "BUY" -and $_.RequiresInversion -eq $true }).Count -eq 1) "JPYUSD inversion missing."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -eq "NZDUSD" -and $_.ExecutionSymbol -eq "NZDUSD" -and $_.ExecutionSide -eq "SELL" -and $_.RequiresInversion -eq $false }).Count -eq 1) "NZDUSD direct mapping missing."

Assert-True ($route.Classification -eq "SANDBOX_ROUTE_PROFILE_FINAL_READY") "Sandbox route/profile final validation failed."
Assert-True ($route.ProductionRouteDisabled -eq $true -and $route.NoProductionAccount -eq $true -and $route.NoProductionLiveEndpoint -eq $true) "Route/profile production boundary failed."
Assert-True ($idempotency.Classification -eq "IDEMPOTENCY_FINAL_READY") "Idempotency final validation failed."
Assert-True ($idempotency.FailClosedPolicy -eq $true) "Idempotency fail-closed policy missing."
Assert-True ($gate.Classification -eq "PRE_EXECUTION_GATE_PASS_READY_TO_SUBMIT_R013C_SANDBOX") "Pre-execution gate did not pass."

Assert-True ($openPlan.Classification -eq "OPEN_ORDER_PLAN_FINAL_READY") "Open order final plan not ready."
Assert-True (@($openPlan.Orders).Count -eq 9) "Open final plan must contain 9 orders."
Assert-True ($openPlan.NoZeroQuantityOrders -eq $true -and $openPlan.NoUnapprovedSymbols -eq $true) "Open final plan contains zero or unapproved order."
Assert-True ($flattenPlan.Classification -eq "FLATTEN_PLAN_FINAL_READY") "Flatten final plan not ready."
Assert-True (@($flattenPlan.Orders).Count -eq 9) "Flatten final plan must contain 9 orders."

Assert-True ($openExecution.ZeroQuantityOrdersSubmitted -eq 0) "Zero quantity order was submitted."
if ($openExecution.Started -eq $true) {
    Assert-True (@($openExecution.Results).Count -le 9) "More than 9 open attempts were made."
}
if ($flattenExecution.Started -eq $true) {
    Assert-True (@($flattenExecution.Results).Count -le 9) "More than 9 flatten attempts were made."
}
if ($recon.Classification -eq "SANDBOX_RECONCILIATION_PASS_RESIDUAL_ZERO") {
    Assert-True (@($recon.Residuals | Where-Object { [decimal]$_.ResidualSignedQuantity -ne 0 }).Count -eq 0) "Residual-zero classification has non-zero residual."
}

Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true -and $pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true) "Gross-only PnL policy failed."
Assert-True ($boundary.SandboxDemoOnly -eq $true -and $boundary.NoProductionLiveLmax -eq $true -and $boundary.NoProductionBrokerRoute -eq $true) "Sandbox/demo boundary failed."
Assert-True ($boundary.NoZeroQuantityOrderSubmitted -eq $true -and $boundary.R010PrototypeApprovalNotReused -eq $true -and $boundary.JPYUSDInversionHandled -eq $true) "Approval/mapping boundary failed."
Assert-True ($boundary.NoLedgerCommit -eq $true -and $boundary.NoNetPnl -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Ledger/PnL boundary failed."

Write-Host "CORE_ANUBIS_INTRADAY_R013C_GUARDED_SANDBOX_EXECUTION_TESTS_PASS"
