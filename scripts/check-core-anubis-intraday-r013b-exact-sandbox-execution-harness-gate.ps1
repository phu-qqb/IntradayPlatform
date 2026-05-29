param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"
$Required = @(
    "r013-blocker-intake-validation.json",
    "exact-candidate-harness-binding.json",
    "multi-symbol-r009-sandbox-harness-design.json",
    "open-order-batch-dry-run.json",
    "flatten-batch-dry-run.json",
    "idempotency-duplicate-guard-evidence.json",
    "sandbox-route-profile-harness-validation.json",
    "harness-pre-execution-gate-decision.json",
    "future-r013c-execution-preconditions.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R013B artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R013B artifact: $name"
}

$intake = Read-Json "r013-blocker-intake-validation.json"
$binding = Read-Json "exact-candidate-harness-binding.json"
$design = Read-Json "multi-symbol-r009-sandbox-harness-design.json"
$open = Read-Json "open-order-batch-dry-run.json"
$flatten = Read-Json "flatten-batch-dry-run.json"
$idempotency = Read-Json "idempotency-duplicate-guard-evidence.json"
$route = Read-Json "sandbox-route-profile-harness-validation.json"
$gate = Read-Json "harness-pre-execution-gate-decision.json"
$future = Read-Json "future-r013c-execution-preconditions.json"
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($intake.Classification -eq "R013_BLOCKER_READY_FOR_HARNESS_BUILD") "R013 blocker intake not ready."
Assert-True ($binding.Classification -eq "EXACT_CANDIDATE_HARNESS_BOUND") "Exact candidate harness binding failed."
Assert-True ($binding.R010PrototypeTransferability -eq $false) "R010 prototype transferability not false."
Assert-True (@($binding.ZeroQuantityExclusions | Sort-Object) -join "," -eq "AUDUSD,CHFUSD,EURUSD,GBPUSD") "Zero quantity exclusions mismatch."

Assert-True ($design.Classification -eq "MULTI_SYMBOL_R009_SANDBOX_HARNESS_DESIGN_READY") "Harness design not ready."
Assert-True ($design.ExpectedOrderCount -eq 9) "Expected order count must be 9."
Assert-True ($design.ZeroQuantityLinesExcluded -eq $true) "Zero quantity exclusion not in design."
Assert-True ($design.ProductionRouteDisabled -eq $true -and $design.NoLedgerCommit -eq $true -and $design.NoProductionLive -eq $true) "Design boundary flags failed."

Assert-True ($open.Classification -eq "OPEN_ORDER_BATCH_DRY_RUN_READY") "Open dry-run not ready."
Assert-True (@($open.Orders).Count -eq 9) "Open dry-run expected 9 orders."
Assert-True ($open.NoZeroQuantities -eq $true) "Open dry-run contains zero quantity."
Assert-True ($open.NoUnapprovedSymbols -eq $true) "Open dry-run contains unapproved symbols."
Assert-True (@($open.DuplicateOrderKeys).Count -eq 0) "Duplicate open idempotency keys found."
Assert-True ($open.SubmitNow -eq $false) "Open dry-run must not submit."
Assert-True (@($open.Orders | Where-Object { $_.CoreSymbol -in @("AUDUSD","CHFUSD","EURUSD","GBPUSD") }).Count -eq 0) "Zero quantity lines included in open dry-run."

Assert-True ($flatten.Classification -eq "FLATTEN_BATCH_DRY_RUN_READY") "Flatten dry-run not ready."
Assert-True (@($flatten.FlattenOrders).Count -eq 9) "Flatten dry-run expected 9 orders."
Assert-True ($flatten.MirrorsOpenBatchCount -eq $true) "Flatten dry-run does not mirror open batch."
Assert-True ($flatten.SubmitNow -eq $false) "Flatten dry-run must not submit."

Assert-True ($idempotency.Classification -eq "IDEMPOTENCY_DUPLICATE_GUARD_READY") "Idempotency guard not ready."
Assert-True ($idempotency.PerOrderKeysUnique -eq $true) "Per-order keys not unique."
Assert-True ($idempotency.NoPreviousCompletedOrActiveLifecycleWithSameOperatorApprovalId -eq $true) "Previous active/completed lifecycle detected."
Assert-True ($idempotency.FailClosedBehavior -eq $true) "Idempotency fail-closed missing."

Assert-True ($route.Classification -eq "SANDBOX_ROUTE_PROFILE_HARNESS_READY") "Sandbox route/profile harness not ready."
Assert-True ($route.NoProductionProfileSelected -eq $true -and $route.NoProductionBrokerRoute -eq $true -and $route.NoProductionAccount -eq $true) "Route/profile not sandbox-only."
Assert-True ($route.RouteResolvableByExistingR009SandboxInfrastructure -eq $true) "Route not resolved by existing R009 sandbox infrastructure."
Assert-True ($route.CredentialValuesRedacted -eq $true) "Credential values not redacted."
Assert-True ($route.NoLmaxCallInThisPackage -eq $true) "Route artifact claims LMAX call in R013B."

Assert-True ($gate.Classification -eq "HARNESS_GATE_READY_FOR_FUTURE_R013C_EXECUTION") "Harness gate not ready for future R013C."
Assert-True ($gate.FutureR013CExecutionAllowed -eq $true -and $gate.SubmissionAllowedInR013B -eq $false) "Harness gate future/current submission flags wrong."
Assert-True ($future.Classification -eq "FUTURE_R013C_PRECONDITIONS_READY") "Future R013C preconditions not ready."
Assert-True ($future.ZeroQuantitiesStillExcluded -eq $true -and $future.ProductionRouteDisabled -eq $true -and $future.LedgerCommitDisabled -eq $true) "Future precondition boundary flags failed."

Assert-True ($contract.Statuses."core-anubis-r013-execution-harness.v1" -eq "YES") "Harness contract status not YES."
Assert-True ($contract.Statuses."r009-execution-readiness.v1" -eq "WITH_WARNINGS_FOR_FUTURE_R013C_ONLY_NOT_EXECUTED") "R009 readiness status wrong."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production statuses not blocked."

Assert-True ($readiness.HarnessOnly -eq $true -and $readiness.NoExecutionOccurred -eq $true -and $readiness.NoR009SubmissionOccurred -eq $true) "Readiness impact claims execution."
Assert-True ($readiness.NoPnlReadinessChanges -eq $true -and $readiness.NoLedgerReadinessChanges -eq $true -and $readiness.NoProductionReadinessChanges -eq $true) "Readiness impact changed forbidden readiness."

Assert-True ($boundary.NoR009Submission -eq $true) "Boundary claims R009 submission."
Assert-True ($boundary.NoLmaxCall -eq $true) "Boundary claims LMAX call."
Assert-True ($boundary.NoOrders -eq $true -and $boundary.NoFillsReports -eq $true) "Boundary claims orders/fills/reports."
Assert-True ($boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "Boundary claims DB/ledger mutation."
Assert-True ($boundary.NoProductionLive -eq $true) "Boundary claims production/live."
Assert-True ($boundary.NoCoreExecution -eq $true -and $boundary.NoManagerAnubisCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "Boundary claims Core/manager/Anubis/CUDA/netting."
Assert-True ($boundary.NoR010PrototypeTransfer -eq $true) "R010 prototype transfer boundary failed."
Assert-True ($boundary.NoAccountingNetProductionPnl -eq $true) "Boundary claims forbidden PnL readiness."

$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")
Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_R013B_PASS_EXACT_SANDBOX_EXECUTION_HARNESS_READY")) "Summary missing PASS classification."
Assert-True ($summary.Contains("NEXT_CORE_ANUBIS_INTRADAY_R013C_GUARDED_SANDBOX_EXECUTION")) "Summary missing next package."

Write-Host "CORE_ANUBIS_INTRADAY_R013B_EXACT_SANDBOX_EXECUTION_HARNESS_GATE_PASS"
