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
    $Value | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-CommitAuthorization([hashtable]$Overrides) {
    $fixture = [ordered]@{
        artifact_type = "ledger_db_commit_authorization"
        environment = "sandbox"
        authorization_mode = "offline_manual"
        sample_only = $false
        commit_authorization = $true
        authorized_by = "sandbox-operator"
        authorized_at_utc = "fixture"
        authorization_id = "ledger-db-commit-authorization-r001:test"
        approved_source_artifact_hashes = [ordered]@{ real_accounting_evidence_and_close_acceptance = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" }
        approved_values = [ordered]@{
            realized_pnl_before_costs_usd = [decimal]6015.14
            commission_expense_usd = [decimal]225.63
            financing_expense_usd = [decimal]40.60
            realized_net_after_costs_usd = [decimal]5748.91
            unrealized_open_pnl_usd = [decimal]463.61
            equity_pnl_including_open_pnl_usd = [decimal]6212.52
        }
        idempotency_key = "ledger-db-commit-r001:test"
        rollback_plan = [ordered]@{ plan = "future_package_required"; approved = $true }
        audit_log_plan = [ordered]@{ plan = "future_append_only_audit"; approved = $true }
        ledger_commit = $false
        db_mutation = $false
        external_fetch = $false
        market_data_fetch = $false
        account_data_fetch = $false
        production_live_authorized = $false
        trading_authorized = $false
    }
    foreach ($key in $Overrides.Keys) { $fixture[$key] = $Overrides[$key] }
    return $fixture
}

$builder = Join-Path $RepoRoot "scripts\build-ledger-db-commit-authorization-gate-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-ledger-db-commit-authorization-gate-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\ledger-db-commit-authorization-gate-r001"
$main = Read-JsonFile (Join-Path $ArtifactDir "ledger-db-commit-authorization-gate-r001.json")
$candidate = Read-JsonFile (Join-Path $ArtifactDir "ledger-commit-candidate-r001.json")
$dbPlan = Read-JsonFile (Join-Path $ArtifactDir "db-mutation-plan-preview-r001.json")

Assert-True (@("LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001", "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001") -contains $main.status) "Current authorization status must be valid."
Assert-Equal $candidate.committed $false "Current candidate must not be committed."
Assert-Equal $candidate.ledger_commit $false "Current candidate must not commit ledger."
Assert-Equal $candidate.db_mutation $false "Current candidate must not mutate DB."
Assert-Equal $dbPlan.dry_run_only $true "Current DB plan must be dry-run only."
Assert-Equal $dbPlan.db_mutation $false "Current DB plan must not mutate DB."
Assert-Equal $dbPlan.ledger_commit $false "Current DB plan must not commit ledger."
Assert-DecimalEqual ([decimal]$main.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Current realized PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.commission_expense_usd) ([decimal]225.63) "Current commission mismatch."
Assert-DecimalEqual ([decimal]$main.values.financing_expense_usd) ([decimal]40.60) "Current financing mismatch."
Assert-DecimalEqual ([decimal]$main.values.realized_net_after_costs_usd) ([decimal]5748.91) "Current net mismatch."
Assert-DecimalEqual ([decimal]$main.values.unrealized_open_pnl_usd) ([decimal]463.61) "Current unrealized mismatch."
Assert-DecimalEqual ([decimal]$main.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Current equity mismatch."

$emptySubdir = "ledger-db-commit-authorization-gate-r001-empty-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $emptySubdir | Out-Null
$emptyDir = Join-Path $RepoRoot "artifacts\readiness\$emptySubdir"
$emptyMain = Read-JsonFile (Join-Path $emptyDir "ledger-db-commit-authorization-gate-r001.json")
$emptyCandidate = Read-JsonFile (Join-Path $emptyDir "ledger-commit-candidate-r001.json")
$emptyDbPlan = Read-JsonFile (Join-Path $emptyDir "db-mutation-plan-preview-r001.json")
Assert-Equal $emptyMain.status "LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001" "Empty authorization staging must block."
Assert-Equal $emptyMain.blocked_reason "NO_LEDGER_DB_COMMIT_AUTHORIZATION_STAGED" "Empty authorization blocked reason mismatch."
Assert-Equal $emptyMain.readiness.ledger_db_commit_ready_for_future_commit_package $false "Empty authorization must not mark future commit ready."
Assert-Equal $emptyCandidate.committed $false "Empty candidate must not be committed."
Assert-Equal $emptyDbPlan.dry_run_only $true "Empty DB plan must be dry-run only."

$invalidSubdir = "ledger-db-commit-authorization-gate-r001-invalid-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $invalidSubdir | Out-Null
$invalidDir = Join-Path $RepoRoot "artifacts\readiness\$invalidSubdir"
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\sample.json") (New-CommitAuthorization @{ sample_only = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\missing-approval.json") (New-CommitAuthorization @{ authorized_by = $null })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\totals-mismatch.json") (New-CommitAuthorization @{ approved_values = [ordered]@{ realized_pnl_before_costs_usd = [decimal]6015.14; commission_expense_usd = [decimal]225.63; financing_expense_usd = [decimal]40.60; realized_net_after_costs_usd = [decimal]5749.91; unrealized_open_pnl_usd = [decimal]463.61; equity_pnl_including_open_pnl_usd = [decimal]6212.52 } })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\ledger-commit.json") (New-CommitAuthorization @{ ledger_commit = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\db-mutation.json") (New-CommitAuthorization @{ db_mutation = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\external-fetch.json") (New-CommitAuthorization @{ external_fetch = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\market-data.json") (New-CommitAuthorization @{ market_data_fetch = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\account-data.json") (New-CommitAuthorization @{ account_data_fetch = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\production-live.json") (New-CommitAuthorization @{ production_live_authorized = $true })
Write-JsonFile (Join-Path $invalidDir "staging\commit-authorization\trading.json") (New-CommitAuthorization @{ trading_authorized = $true })
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $invalidSubdir | Out-Null
$invalidMain = Read-JsonFile (Join-Path $invalidDir "ledger-db-commit-authorization-gate-r001.json")
$invalidQuarantine = Read-JsonFile (Join-Path $invalidDir "ledger-db-commit-authorization-quarantine-preview-r001.json")
Assert-Equal $invalidMain.status "LEDGER_DB_COMMIT_AUTHORIZATION_BLOCKED_R001" "Invalid authorizations must block."
Assert-Equal $invalidMain.readiness.ledger_db_commit_ready_for_future_commit_package $false "Invalid authorizations must not mark future commit ready."
Assert-True ($invalidQuarantine.quarantined_count -ge 10) "Invalid authorizations should be quarantined."

$readySubdir = "ledger-db-commit-authorization-gate-r001-ready-test-$([Guid]::NewGuid().ToString('N'))"
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $readySubdir | Out-Null
$readyDir = Join-Path $RepoRoot "artifacts\readiness\$readySubdir"
Write-JsonFile (Join-Path $readyDir "staging\commit-authorization\valid-authorization.json") (New-CommitAuthorization @{})
& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot -OutputSubdir $readySubdir | Out-Null
$readyMain = Read-JsonFile (Join-Path $readyDir "ledger-db-commit-authorization-gate-r001.json")
$readyCandidate = Read-JsonFile (Join-Path $readyDir "ledger-commit-candidate-r001.json")
$readyDbPlan = Read-JsonFile (Join-Path $readyDir "db-mutation-plan-preview-r001.json")
Assert-Equal $readyMain.status "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001" "Valid authorization should mark authorization ready."
Assert-Equal $readyMain.readiness.ledger_db_commit_ready_for_future_commit_package $true "Valid authorization should mark future commit package ready."
Assert-Equal $readyMain.readiness.ledger_commit $false "Valid authorization must not execute ledger commit."
Assert-Equal $readyMain.readiness.db_mutation $false "Valid authorization must not mutate DB."
Assert-Equal $readyMain.readiness.production_live $false "Valid authorization must not mark production/live ready."
Assert-Equal $readyMain.readiness.trading_readiness $false "Valid authorization must not mark trading ready."
Assert-Equal $readyCandidate.committed $false "Ready candidate still must not be committed."
Assert-Equal $readyCandidate.ledger_commit $false "Ready candidate ledger commit must remain false."
Assert-Equal $readyCandidate.db_mutation $false "Ready candidate DB mutation must remain false."
Assert-Equal $readyDbPlan.dry_run_only $true "Ready DB plan must remain dry-run."
Assert-Equal $readyDbPlan.db_mutation $false "Ready DB plan DB mutation must remain false."
Assert-Equal $readyDbPlan.ledger_commit $false "Ready DB plan ledger commit must remain false."
Assert-Equal $readyMain.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $readyMain.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $readyMain.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $readyMain.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $readyMain.global_guards.ledger_commit $false "Ledger commit guard must remain false."
Assert-Equal $readyMain.global_guards.db_mutation $false "DB mutation guard must remain false."
Assert-Equal $readyMain.global_guards.production_live_ready $false "Production/live guard must remain false."
Assert-Equal $readyMain.global_guards.trading_readiness_ready $false "Trading readiness guard must remain false."

Write-Host "LEDGER_DB_COMMIT_AUTHORIZATION_GATE_R001_TEST_PASS"
