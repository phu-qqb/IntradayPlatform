param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-data-basis-r005"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_TEST_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact under test: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$Universe = Read-Json "core-fx-universe-basis.json"
$PriceValidation = Read-Json "core-symbol-price-basis-validation.json"
$MetadataValidation = Read-Json "core-symbol-metadata-validation.json"
$Template = Read-Json "operator-fx-price-metadata-evidence-template.json"
$Quantity = Read-Json "quantity-readiness-decision.json"
$Contracts = Read-Json "contract-status-update.json"
$Boundary = Read-Json "boundary-safety-evidence.json"

if (-not (@($Universe.Symbols) | Where-Object { $_.CoreSymbol -eq "CADUSD" -and $_.PreferredInversePriceSymbol -eq "USDCAD" })) { Fail "direct/inverse symbol mapping failed for CADUSD." }
if (-not (@($PriceValidation.SymbolValidation) | Where-Object { $_.CoreSymbol -eq "CADUSD" -and $_.PriceStatus -eq "PRICE_READY_INVERSE" -and $_.SourceArtifact })) { Fail "inverse price did not require explicit source." }
foreach ($symbol in @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")) {
    if (-not (@($PriceValidation.SymbolValidation) | Where-Object { $_.CoreSymbol -eq $symbol -and $_.PriceStatus -eq "PRICE_MISSING" })) { Fail "missing exotic price not blocked: $symbol" }
    if (-not (@($MetadataValidation.SymbolValidation) | Where-Object { $_.CoreSymbol -eq $symbol -and $_.MetadataStatus -eq "METADATA_MISSING" })) { Fail "missing exotic metadata not blocked: $symbol" }
    if (-not (@($Template.TemplateRows) | Where-Object { $_.CoreSymbol -eq $symbol })) { Fail "operator template missing gap: $symbol" }
}
if ($Quantity.DoNotDeriveQuantitiesInR005 -ne $true) { Fail "R005 derived quantities unexpectedly." }

$ContractsMap = @{}
foreach ($status in $Contracts.Statuses) { $ContractsMap[$status.ContractId] = $status.Status }
if ($ContractsMap["r009-execution-readiness.v1"] -ne "BLOCKED_FOR_CORE_CANDIDATE") { Fail "R009 readiness incorrectly changed." }
if ($Boundary.NoR009 -ne $true -or $Boundary.NoInventedQuantities -ne $true) { Fail "boundary flags failed." }

Write-Host "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_TEST_PASS"
