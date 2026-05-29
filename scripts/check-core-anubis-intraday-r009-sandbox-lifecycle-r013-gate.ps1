param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r009-sandbox-lifecycle-r013"
$Required = @(
    "r012-approval-intake-validation.json",
    "approved-candidate-exactness-validation.json",
    "execution-symbol-mapping-inversion-validation.json",
    "sandbox-route-account-profile-validation.json",
    "idempotency-duplicate-guard.json",
    "pre-execution-gate-decision.json",
    "open-order-plan.json",
    "flatten-plan.json",
    "guarded-r009-sandbox-open-execution.json",
    "guarded-sandbox-flatten-execution.json",
    "sandbox-reconciliation.json",
    "sandbox-gross-pnl-preview-r013.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R013 artifact directory is missing: $ArtifactDir"
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R013 artifact: $name"
}

$r012 = Read-Json "r012-approval-intake-validation.json"
$candidate = Read-Json "approved-candidate-exactness-validation.json"
$mapping = Read-Json "execution-symbol-mapping-inversion-validation.json"
$route = Read-Json "sandbox-route-account-profile-validation.json"
$idempotency = Read-Json "idempotency-duplicate-guard.json"
$gate = Read-Json "pre-execution-gate-decision.json"
$open = Read-Json "open-order-plan.json"
$flatten = Read-Json "flatten-plan.json"
$openExecution = Read-Json "guarded-r009-sandbox-open-execution.json"
$flattenExecution = Read-Json "guarded-sandbox-flatten-execution.json"
$recon = Read-Json "sandbox-reconciliation.json"
$pnl = Read-Json "sandbox-gross-pnl-preview-r013.json"
$paperLedger = Read-Json "paper-ledger-preview-update.json"
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($r012.Classification -in @("R012_APPROVAL_READY_FOR_R013_EXECUTION_GATE","R012_APPROVAL_READY_WITH_WARNINGS")) "R012 approval intake is not ready."
Assert-True ($candidate.Classification -eq "APPROVED_CANDIDATE_EXACT_MATCH") "Approved candidate did not match exact R012 binding."
Assert-True ($candidate.R010Transferability -eq $false) "R010 prototype transferability was not false."
Assert-True ($mapping.Classification -eq "EXECUTION_MAPPING_READY_ALL_NONZERO_LINES") "Execution mapping was not ready for all non-zero lines."
Assert-True (@($mapping.Rows).Count -eq 9) "Expected 9 non-zero execution mappings."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -eq "JPYUSD" -and $_.ExecutionSymbol -eq "USDJPY" -and $_.ExecutionSide -eq "BUY" -and $_.RequiresInversion -eq $true }).Count -eq 1) "JPYUSD inversion mapping is missing or wrong."
Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -in @("AUDUSD","CHFUSD","EURUSD","GBPUSD") }).Count -eq 0) "Zero-quantity Core symbols appeared in execution mapping."

Assert-True ($route.Classification -eq "SANDBOX_ROUTE_PROFILE_BLOCKED") "Route/profile should be blocked for this package."
Assert-True ($route.ProductionRouteDisabled -eq $true) "Production route was not disabled."
Assert-True ($route.CredentialValuesRedacted -eq $true) "Credential redaction was not confirmed."
Assert-True ($idempotency.Classification -eq "IDEMPOTENCY_DUPLICATE_GUARD_READY") "Idempotency guard was not ready."
Assert-True ($idempotency.PreviousR013RunWithSameKey -eq $false) "Duplicate R013 lifecycle key detected."

Assert-True ($gate.Classification -eq "PRE_EXECUTION_GATE_BLOCKED_ROUTE_PROFILE") "Pre-execution gate did not block at route/profile."
Assert-True ($gate.GatePassed -eq $false) "Gate unexpectedly passed."
Assert-True ($open.Classification -eq "OPEN_ORDER_PLAN_NOT_CREATED_GATE_BLOCKED") "Open order plan should not be created when gate is blocked."
Assert-True (@($open.PlannedOrders).Count -eq 0) "Open order plan contains submitted/planned orders despite gate block."
Assert-True ($flatten.Classification -eq "FLATTEN_PLAN_NOT_CREATED_GATE_BLOCKED") "Flatten plan should not be active when gate is blocked."
Assert-True ($openExecution.Classification -eq "R009_SANDBOX_OPEN_NOT_EXECUTED_GATE_BLOCKED") "R009 open execution should not have run."
Assert-True ($openExecution.Started -eq $false) "R009 open execution started despite gate block."
Assert-True ($openExecution.ZeroQuantityOrdersSubmitted -eq 0) "Zero quantity orders were submitted."
Assert-True ($flattenExecution.Classification -eq "SANDBOX_FLATTEN_NOT_EXECUTED_GATE_BLOCKED") "Flatten execution should not have run."
Assert-True ($recon.Classification -eq "SANDBOX_RECONCILIATION_NOT_RUN_GATE_BLOCKED") "Reconciliation should be gate-blocked."
Assert-True ($recon.ZeroQuantityLinesCorrectlyExcluded -eq $true) "Zero quantity exclusion was not confirmed."
Assert-True ($pnl.Classification -eq "SANDBOX_GROSS_PNL_R013_NOT_APPLICABLE_NO_FILLS") "Gross PnL should be not applicable with no fills."
Assert-True ($paperLedger.Classification -eq "PAPER_LEDGER_PREVIEW_NOT_APPLICABLE_NO_FILLS") "Paper ledger preview should be not applicable with no fills."
Assert-True ($paperLedger.Commit -eq $false) "Paper ledger artifact claims a commit."
Assert-True ($paperLedger.DbMutation -eq $false) "Paper ledger artifact claims DB mutation."

Assert-True ($contract.Statuses."pms-core-execution-candidate.v1" -eq "BLOCKED") "PMS Core execution candidate must remain blocked."
Assert-True ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_ROUTE_PROFILE") "R009 execution readiness must remain route/profile blocked."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED") "Accounting attribution must remain blocked."
Assert-True ($contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Production readiness must remain blocked."

Assert-True ($readiness.R013Executed -eq $false) "Readiness impact claims R013 executed."
Assert-True ($readiness.NoLedgerCommit -eq $true) "Readiness impact does not preserve no ledger commit."
Assert-True ($readiness.ProductionLiveRemainsBlocked -eq $true) "Production/live was not blocked."

Assert-True ($boundary.SandboxDemoOnly -eq $true) "Boundary safety does not confirm sandbox/demo only."
Assert-True ($boundary.NoProductionLiveLmax -eq $true) "Boundary safety does not block production/live LMAX."
Assert-True ($boundary.NoProductionBrokerRoute -eq $true) "Boundary safety does not block production broker route."
Assert-True ($boundary.NoProductionOrderFillReport -eq $true) "Boundary safety does not block production order/fill/report."
Assert-True ($boundary.NoLedgerCommit -eq $true) "Boundary safety does not block ledger commit."
Assert-True ($boundary.NoDbMutationOutsideAcceptedSandboxAuditPath -eq $true) "Boundary safety does not block unsafe DB mutation."
Assert-True ($boundary.NoZeroQuantityOrderSubmitted -eq $true) "Boundary safety does not confirm no zero quantity order."
Assert-True ($boundary.R010PrototypeApprovalNotReused -eq $true) "R010 prototype non-transferability was not preserved."
Assert-True ($boundary.JPYUSDInversionHandledOrGateBlocked -eq $true) "JPYUSD inversion handling/gate block was not confirmed."
Assert-True ($boundary.NoNetPnl -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Forbidden PnL readiness was claimed."
Assert-True ($boundary.R009ExecutionSubmitted -eq $false) "Boundary safety claims R009 execution submission."
Assert-True ($boundary.LmaxCallOccurred -eq $false) "Boundary safety claims LMAX call occurred."

$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")
Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_BLOCKED_ROUTE_OR_IDEMPOTENCY")) "Summary missing final R013 classification."
Assert-True ($summary.Contains("NEXT_CORE_ANUBIS_INTRADAY_R013B_EXACT_SANDBOX_EXECUTION_HARNESS")) "Summary missing next package."

Write-Host "CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_GATE_PASS"
