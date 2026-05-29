param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_TEST_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact under test: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$PriceSources = Read-Json "local-price-source-inventory.json"
$PriceCoverage = Read-Json "price-basis-coverage-by-core-symbol.json"
$MetadataCoverage = Read-Json "instrument-metadata-coverage-by-core-symbol.json"
$Feasibility = Read-Json "quantity-feasibility-update.json"
$Candidate = Read-Json "updated-pms-core-candidate-status.json"
$Boundary = Read-Json "boundary-safety-evidence.json"

if ($PriceSources.FalsePositiveReadinessOnlyReferencesExcluded -ne $true) { Fail "price inventory did not exclude readiness-only false positives." }
if (-not (@($PriceCoverage.SymbolCoverage) | Where-Object { $_.CoreSymbol -eq "CADUSD" -and $_.Classification -eq "PRICE_BASIS_READY_INVERSE" })) { Fail "inverse price policy did not accept explicit USDCAD source." }
if (-not (@($PriceCoverage.SymbolCoverage) | Where-Object { $_.CoreSymbol -eq "CNHUSD" -and $_.Classification -eq "PRICE_BASIS_MISSING" })) { Fail "missing price did not block CNHUSD." }
if (-not (@($MetadataCoverage.SymbolCoverage) | Where-Object { $_.CoreSymbol -eq "CNHUSD" -and $_.Classification -eq "METADATA_MISSING" })) { Fail "missing metadata did not block CNHUSD." }
if (-not (@($PriceCoverage.SymbolCoverage) | Where-Object { $_.CoreSymbol -eq "JPYUSD" -and $_.InversionApplied -eq $true })) { Fail "JPYUSD caveat/inverse handling not preserved." }
if ($Candidate.R009Ready -ne $false -or $Candidate.RiskReviewReady -ne $false) { Fail "R009/risk review readiness incorrectly granted." }
if ($Feasibility.InventedQuantities -ne $false -or $Boundary.NoInventedPrices -ne $true) { Fail "invented quantity/price boundary failed." }

Write-Host "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_TEST_PASS"
