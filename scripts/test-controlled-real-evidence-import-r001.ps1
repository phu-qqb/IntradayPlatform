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

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$builder = Join-Path $RepoRoot "scripts\build-controlled-real-evidence-import-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-controlled-real-evidence-import-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001"
$main = Read-JsonFile (Join-Path $artifactDir "controlled-real-evidence-import-r001.json")
$sampleBroker = Read-JsonFile (Join-Path $artifactDir "sample-manual-broker-statement-import-r001.json")
$sampleAccounting = Read-JsonFile (Join-Path $artifactDir "sample-manual-accounting-evidence-import-r001.json")

Assert-Equal $main.status "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001" "Controlled import framework should be ready."
Assert-Equal $main.source_status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Source status mismatch."
Assert-Equal $main.controlled_import_lanes.broker_statement_manual_import.ready $true "Broker manual import lane should be ready."
Assert-Equal $main.controlled_import_lanes.accounting_evidence_manual_import.ready $true "Accounting manual import lane should be ready."
Assert-Equal $main.sample_validation.broker_statement_import "SAMPLE_MANUAL_BROKER_IMPORT_VALID" "Sample broker import validation mismatch."
Assert-Equal $main.sample_validation.accounting_evidence_import "SAMPLE_MANUAL_ACCOUNTING_IMPORT_VALID" "Sample accounting import validation mismatch."
Assert-Equal $sampleBroker.sample_only $true "Broker import must be sample only."
Assert-Equal $sampleAccounting.sample_only $true "Accounting import must be sample only."
Assert-Equal $sampleBroker.external_fetch $false "Broker sample external_fetch must remain false."
Assert-Equal $sampleBroker.broker_api_call $false "Broker sample broker_api_call must remain false."
Assert-Equal $sampleAccounting.external_fetch $false "Accounting sample external_fetch must remain false."
Assert-Equal $sampleAccounting.db_mutation $false "Accounting sample db_mutation must remain false."
Assert-Equal $sampleAccounting.ledger_commit $false "Accounting sample ledger_commit must remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden label must remain false: $($label.Name)"
}
Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

Write-Host "CONTROLLED_REAL_EVIDENCE_IMPORT_R001_TESTS_PASS"
