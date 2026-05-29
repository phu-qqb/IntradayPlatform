param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r009-sandbox-lifecycle-r013"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$mapping = Read-Json "execution-symbol-mapping-inversion-validation.json"
$gate = Read-Json "pre-execution-gate-decision.json"
$openPlan = Read-Json "open-order-plan.json"
$openExecution = Read-Json "guarded-r009-sandbox-open-execution.json"
$flattenExecution = Read-Json "guarded-sandbox-flatten-execution.json"
$paperLedger = Read-Json "paper-ledger-preview-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True (@($mapping.Rows | Where-Object { $_.CoreSymbol -in @("AUDUSD","CHFUSD","EURUSD","GBPUSD") }).Count -eq 0) "Zero quantity symbols must be excluded from execution mappings."

$cad = $mapping.Rows | Where-Object { $_.CoreSymbol -eq "CADUSD" } | Select-Object -First 1
Assert-True ($cad.ExecutionSymbol -eq "USDCAD" -and $cad.CoreSide -eq "BUY" -and $cad.ExecutionSide -eq "SELL" -and $cad.RequiresInversion -eq $true) "CADUSD inverse side mapping failed."

$jpy = $mapping.Rows | Where-Object { $_.CoreSymbol -eq "JPYUSD" } | Select-Object -First 1
Assert-True ($jpy.ExecutionSymbol -eq "USDJPY" -and $jpy.CoreSide -eq "SELL" -and $jpy.ExecutionSide -eq "BUY" -and $jpy.RequiresInversion -eq $true) "JPYUSD inverse side mapping failed."

$nzd = $mapping.Rows | Where-Object { $_.CoreSymbol -eq "NZDUSD" } | Select-Object -First 1
Assert-True ($nzd.ExecutionSymbol -eq "NZDUSD" -and $nzd.CoreSide -eq "SELL" -and $nzd.ExecutionSide -eq "SELL" -and $nzd.RequiresInversion -eq $false) "NZDUSD direct mapping failed."

Assert-True ($gate.Classification -eq "PRE_EXECUTION_GATE_BLOCKED_ROUTE_PROFILE" -and $gate.GatePassed -eq $false) "R013 gate should block route/profile before execution."
Assert-True (@($openPlan.PlannedOrders).Count -eq 0) "No open orders should be planned while gate is blocked."
Assert-True ($openExecution.Started -eq $false -and $openExecution.ZeroQuantityOrdersSubmitted -eq 0) "Open execution should not start and must submit no zero quantity orders."
Assert-True ($flattenExecution.Started -eq $false) "Flatten should not run without open execution."
Assert-True ($paperLedger.Commit -eq $false -and $paperLedger.DbMutation -eq $false) "Paper ledger must be preview-only/no commit/no DB mutation."
Assert-True ($boundary.LmaxCallOccurred -eq $false -and $boundary.R009ExecutionSubmitted -eq $false) "No LMAX call or R009 submission should occur in blocked route/profile state."
Assert-True ($boundary.NoLedgerCommit -eq $true -and $boundary.NoNetPnl -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Ledger/PnL boundaries must remain blocked."

Write-Host "CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_TESTS_PASS"
