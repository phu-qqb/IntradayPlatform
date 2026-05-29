param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$binding = Read-Json "exact-candidate-harness-binding.json"
$open = Read-Json "open-order-batch-dry-run.json"
$flatten = Read-Json "flatten-batch-dry-run.json"
$idempotency = Read-Json "idempotency-duplicate-guard-evidence.json"
$route = Read-Json "sandbox-route-profile-harness-validation.json"
$gate = Read-Json "harness-pre-execution-gate-decision.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($binding.OperatorApprovalId -eq "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975") "OperatorApprovalId binding mismatch."
Assert-True ($binding.CandidateId -eq "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD") "CandidateId binding mismatch."
Assert-True (@($binding.ApprovedNonZeroCoreLines).Count -eq 9) "Expected 9 approved non-zero Core lines."
Assert-True (@($binding.ZeroQuantityExclusions).Count -eq 4) "Expected 4 zero quantity exclusions."

Assert-True ($open.Classification -eq "OPEN_ORDER_BATCH_DRY_RUN_READY") "Open dry-run not ready."
Assert-True (@($open.Orders).Count -eq 9) "Open dry-run must contain 9 orders."
Assert-True ($open.NoZeroQuantities -eq $true) "Open dry-run contains zero quantity."
Assert-True ($open.SubmitNow -eq $false) "Open dry-run must not submit."
Assert-True (@($open.Orders | Where-Object { $_.CoreSymbol -in @("AUDUSD","CHFUSD","EURUSD","GBPUSD") }).Count -eq 0) "Zero quantity symbols appeared in open dry-run."

$cad = $open.Orders | Where-Object { $_.CoreSymbol -eq "CADUSD" } | Select-Object -First 1
Assert-True ($cad.ExecutionSymbol -eq "USDCAD" -and $cad.Side -eq "SELL" -and $cad.RequiresInversion -eq $true) "CADUSD inversion not preserved."
$jpy = $open.Orders | Where-Object { $_.CoreSymbol -eq "JPYUSD" } | Select-Object -First 1
Assert-True ($jpy.ExecutionSymbol -eq "USDJPY" -and $jpy.Side -eq "BUY" -and $jpy.RequiresInversion -eq $true) "JPYUSD inversion not preserved."
$nzd = $open.Orders | Where-Object { $_.CoreSymbol -eq "NZDUSD" } | Select-Object -First 1
Assert-True ($nzd.ExecutionSymbol -eq "NZDUSD" -and $nzd.Side -eq "SELL" -and $nzd.RequiresInversion -eq $false) "NZDUSD direct mapping not preserved."

Assert-True ($flatten.Classification -eq "FLATTEN_BATCH_DRY_RUN_READY") "Flatten dry-run not ready."
Assert-True (@($flatten.FlattenOrders).Count -eq @($open.Orders).Count) "Flatten dry-run must mirror open order count."
foreach ($order in $open.Orders) {
    $flat = $flatten.FlattenOrders | Where-Object { $_.ExecutionSymbol -eq $order.ExecutionSymbol -and $_.Quantity -eq $order.Quantity } | Select-Object -First 1
    Assert-True ($null -ne $flat) "Missing flatten line for $($order.ExecutionSymbol)."
    Assert-True ($flat.SubmitNow -eq $false -and $flat.SandboxOnly -eq $true) "Flatten line must be dry-run sandbox only."
}

Assert-True ($idempotency.Classification -eq "IDEMPOTENCY_DUPLICATE_GUARD_READY") "Idempotency guard not ready."
Assert-True ($idempotency.PerOrderKeysUnique -eq $true) "Per-order idempotency keys are not unique."
Assert-True ($idempotency.DuplicateProtectionBeforeOpenSubmit -eq $true -and $idempotency.DuplicateProtectionBeforeFlattenSubmit -eq $true) "Duplicate protection missing."

Assert-True ($route.Classification -eq "SANDBOX_ROUTE_PROFILE_HARNESS_READY") "Route/profile harness is not ready."
Assert-True ($route.NoProductionProfileSelected -eq $true -and $route.NoProductionBrokerRoute -eq $true) "Route/profile is not sandbox-only."
Assert-True ($gate.Classification -eq "HARNESS_GATE_READY_FOR_FUTURE_R013C_EXECUTION") "Harness gate not ready for future R013C."
Assert-True ($gate.SubmissionAllowedInR013B -eq $false) "R013B must not submit."

Assert-True ($boundary.NoR009Submission -eq $true -and $boundary.NoLmaxCall -eq $true -and $boundary.NoOrders -eq $true -and $boundary.NoFillsReports -eq $true) "Execution boundary crossed."
Assert-True ($boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true -and $boundary.NoProductionLive -eq $true) "DB/ledger/production boundary crossed."

Write-Host "CORE_ANUBIS_INTRADAY_R013B_EXACT_SANDBOX_EXECUTION_HARNESS_TESTS_PASS"
