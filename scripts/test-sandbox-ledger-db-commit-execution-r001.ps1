param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-Authorization([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "ledger_db_commit_authorization"
        environment = "sandbox"
        authorization_mode = "offline_manual"
        sample_only = $false
        commit_authorization = $true
        authorized_by = "sandbox-operator"
        authorized_at_utc = "fixture"
        authorization_id = "sandbox-ledger-db-commit-execution-r001:test"
        approved_source_artifact_hashes = [ordered]@{
            real_accounting_evidence_and_close_acceptance = "sha256:c582c7e1227903f849032266d3ddc1628c35d08e1b3bf8b2bb0a47331b8963fa"
            ledger_commit_candidate = "sha256:fa9c126c04042476cfb9c1f3ff5bd9d192641f96806ebfdcc331f18dd2194ec8"
            db_mutation_plan_preview = "sha256:bc4f4f7026106685b660b05c67f37b0cd677ce6008a8797e87614dc29bcb2874"
            broker_statement_confirmed_pnl = "sha256:84c0e246bd5a859500cd38a5dad1fc7307d240630896995e6e61ac5bdc255f66"
        }
        approved_values = [ordered]@{
            realized_pnl_before_costs_usd = [decimal]6015.14
            commission_expense_usd = [decimal]225.63
            financing_expense_usd = [decimal]40.60
            realized_net_after_costs_usd = [decimal]5748.91
            unrealized_open_pnl_usd = [decimal]463.61
            equity_pnl_including_open_pnl_usd = [decimal]6212.52
        }
        idempotency_key = "sandbox-ledger-db-commit-execution-r001-test"
        rollback_plan = [ordered]@{ required = $true; mode = "test" }
        audit_log_plan = [ordered]@{ required = $true; mode = "test" }
        ledger_db_commit_ready_for_future_commit_package = $true
        ledger_commit = $false
        db_mutation = $false
        production_live_authorized = $false
        trading_authorized = $false
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

$authBuilder = Join-Path $RepoRoot "scripts\build-ledger-db-commit-authorization-gate-r001.ps1"
$builder = Join-Path $RepoRoot "scripts\build-sandbox-ledger-db-commit-execution-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-sandbox-ledger-db-commit-execution-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
$liveMain = Read-JsonFile (Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001\sandbox-ledger-db-commit-execution-r001.json")
Assert-True (@("SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001", "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001") -contains $liveMain.status) "Live current execution status must be executed or idempotent."
Assert-Equal $liveMain.readiness.sandbox_ledger_commit $true "Live sandbox ledger commit must be true."
Assert-Equal $liveMain.readiness.sandbox_db_mutation $true "Live sandbox DB mutation must be true."
Assert-Equal $liveMain.readiness.production_live $false "Live production must remain false."
Assert-Equal $liveMain.readiness.trading_readiness $false "Live trading readiness must remain false."
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$missingAuthSubdir = "ledger-db-commit-authorization-gate-r001-missing-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $authBuilder -RepoRoot $RepoRoot -OutputSubdir $missingAuthSubdir | Out-Null
$missingExecSubdir = "sandbox-ledger-db-commit-execution-r001-missing-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $missingExecSubdir -AuthorizationGateSubdir $missingAuthSubdir | Out-Null
$missingMain = Read-JsonFile (Join-Path $RepoRoot "artifacts\readiness\$missingExecSubdir\sandbox-ledger-db-commit-execution-r001.json")
Assert-Equal $missingMain.status "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "Missing authorization should block."
Assert-Equal $missingMain.blocked_reason "LEDGER_DB_COMMIT_AUTHORIZATION_NOT_READY" "Missing authorization blocked reason mismatch."

$readyAuthSubdir = "ledger-db-commit-authorization-gate-r001-ready-exec-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $authBuilder -RepoRoot $RepoRoot -OutputSubdir $readyAuthSubdir | Out-Null
$readyAuthDir = Join-Path $RepoRoot "artifacts\readiness\$readyAuthSubdir"
Write-JsonFile (Join-Path $readyAuthDir "staging\commit-authorization\valid.json") (New-Authorization @{})
& powershell -ExecutionPolicy Bypass -File $authBuilder -RepoRoot $RepoRoot -OutputSubdir $readyAuthSubdir | Out-Null

$execSubdir = "sandbox-ledger-db-commit-execution-r001-ready-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $execSubdir -AuthorizationGateSubdir $readyAuthSubdir | Out-Null
$execDir = Join-Path $RepoRoot "artifacts\readiness\$execSubdir"
$firstMain = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-commit-execution-r001.json")
$firstIdempotency = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-idempotency-report-r001.json")
$firstRollback = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-rollback-preview-r001.json")
Assert-Equal $firstMain.status "SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001" "First commit should execute."
Assert-Equal $firstMain.rows_inserted 8 "First commit should insert eight sandbox DB rows."
Assert-Equal $firstMain.ledger_rows_for_key 6 "First commit should write six ledger rows."
Assert-Equal $firstMain.db_rows_for_key 8 "First commit should write eight DB rows."
Assert-Equal $firstIdempotency.ledger_rows_for_key 6 "First idempotency ledger row count mismatch."
Assert-Equal $firstIdempotency.audit_rows_for_key 1 "First idempotency audit row count mismatch."
Assert-Equal $firstRollback.rollback_preview_created $true "Rollback preview must be created."
Assert-Equal @($firstRollback.reversal_entries).Count 6 "Rollback preview must contain six reversal entries."

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $execSubdir -AuthorizationGateSubdir $readyAuthSubdir | Out-Null
$secondMain = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-commit-execution-r001.json")
$secondIdempotency = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-idempotency-report-r001.json")
Assert-Equal $secondMain.status "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001" "Second run should be idempotent."
Assert-Equal $secondMain.rows_already_present 8 "Second run should report eight rows already present."
Assert-Equal $secondMain.ledger_rows_for_key 6 "Second run must not duplicate ledger rows."
Assert-Equal $secondMain.db_rows_for_key 8 "Second run must not duplicate DB rows."
Assert-Equal $secondIdempotency.idempotent $true "Second idempotency report mismatch."

$driftAuthSubdir = "ledger-db-commit-authorization-gate-r001-drift-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $authBuilder -RepoRoot $RepoRoot -OutputSubdir $driftAuthSubdir | Out-Null
$driftAuthDir = Join-Path $RepoRoot "artifacts\readiness\$driftAuthSubdir"
Write-JsonFile (Join-Path $driftAuthDir "staging\commit-authorization\valid.json") (New-Authorization @{ idempotency_key = "sandbox-ledger-db-commit-execution-r001-test" })
& powershell -ExecutionPolicy Bypass -File $authBuilder -RepoRoot $RepoRoot -OutputSubdir $driftAuthSubdir | Out-Null
$driftGatePath = Join-Path $driftAuthDir "ledger-db-commit-authorization-gate-r001.json"
$driftGate = Read-JsonFile $driftGatePath
$driftGate.values.realized_net_after_costs_usd = [decimal]5749.91
Write-JsonFile $driftGatePath $driftGate
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $execSubdir -AuthorizationGateSubdir $driftAuthSubdir | Out-Null
$driftMain = Read-JsonFile (Join-Path $execDir "sandbox-ledger-db-commit-execution-r001.json")
Assert-Equal $driftMain.status "SANDBOX_LEDGER_DB_COMMIT_BLOCKED_R001" "Same idempotency key with changed values must block."

Assert-DecimalEqual ([decimal]$firstMain.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Realized PnL mismatch."
Assert-DecimalEqual ([decimal]$firstMain.values.commission_expense_usd) ([decimal]225.63) "Commission mismatch."
Assert-DecimalEqual ([decimal]$firstMain.values.financing_expense_usd) ([decimal]40.60) "Financing mismatch."
Assert-DecimalEqual ([decimal]$firstMain.values.realized_net_after_costs_usd) ([decimal]5748.91) "Net mismatch."
Assert-DecimalEqual ([decimal]$firstMain.values.unrealized_open_pnl_usd) ([decimal]463.61) "Unrealized mismatch."
Assert-DecimalEqual ([decimal]$firstMain.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity mismatch."
Assert-Equal $firstMain.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $firstMain.global_guards.lmax_fix_api_call $false "LMAX FIX/API must remain false."
Assert-Equal $firstMain.global_guards.broker_api_call $false "Broker API must remain false."
Assert-Equal $firstMain.global_guards.market_data_fetch $false "Market data fetch must remain false."
Assert-Equal $firstMain.global_guards.broker_fetch $false "Broker fetch must remain false."
Assert-Equal $firstMain.global_guards.account_data_fetch $false "Account fetch must remain false."
Assert-Equal $firstMain.global_guards.production_live_write $false "Production/live write must remain false."
Assert-Equal $firstMain.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $firstMain.global_guards.trading_readiness_ready $false "Trading readiness must remain false."

Write-Host "SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001_TEST_PASS"
