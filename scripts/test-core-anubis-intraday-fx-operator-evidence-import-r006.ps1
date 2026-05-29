param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-operator-evidence-import-r006"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_TEST_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact under test: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$Discovery = Read-Json "operator-evidence-file-discovery.json"
$Price = Read-Json "operator-price-evidence-validation.json"
$Metadata = Read-Json "operator-metadata-evidence-validation.json"
$Quantity = Read-Json "quantity-readiness-refreshed.json"
$Contracts = Read-Json "contract-status-update.json"
$Boundary = Read-Json "boundary-safety-evidence.json"

if ($Discovery.FilledEvidenceFound -ne $false) { Fail "filled evidence was not separated from template-only evidence." }
foreach ($symbol in @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")) {
    if (-not (@($Price.SymbolValidation) | Where-Object { $_.CoreSymbol -eq $symbol -and $_.ValidationStatus -eq "OPERATOR_PRICE_EVIDENCE_MISSING" })) { Fail "missing price did not keep symbol blocked: $symbol" }
    if (-not (@($Metadata.SymbolValidation) | Where-Object { $_.CoreSymbol -eq $symbol -and $_.ValidationStatus -eq "OPERATOR_METADATA_EVIDENCE_MISSING" })) { Fail "missing metadata did not keep symbol blocked: $symbol" }
}
if ($Quantity.DoNotDeriveQuantitiesInR006 -ne $true) { Fail "quantity derivation occurred in R006." }

$ContractsMap = @{}
foreach ($status in $Contracts.Statuses) { $ContractsMap[$status.ContractId] = $status.Status }
if ($ContractsMap["r009-execution-readiness.v1"] -ne "BLOCKED_FOR_CORE_CANDIDATE") { Fail "R009 readiness changed incorrectly." }
if ($Boundary.NoInventedPrices -ne $true -or $Boundary.NoInventedQuantities -ne $true -or $Boundary.NoR009 -ne $true) { Fail "boundary flags failed." }

Write-Host "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_TEST_PASS"
