$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$coverage = Read-Json "core-symbol-metadata-coverage-from-catalog.json"
$nonFx = Read-Json "non-fx-instrument-classification.json"
$policy = Read-Json "metadata-catalog-reuse-policy.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$contract = Read-Json "contract-status-update.json"

$expected = @{
    CNHUSD = "100892"
    MXNUSD = "100507"
    NOKUSD = "100513"
    SEKUSD = "100529"
    SGDUSD = "100535"
    ZARUSD = "100547"
}
foreach ($symbol in $expected.Keys) {
    $row = $coverage.SymbolCoverage | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    Assert ($null -ne $row) "missing coverage row for $symbol"
    Assert ($row.ValidationStatus -eq "METADATA_READY_INVERSE_OR_EXECUTION_PAIR") "$symbol should use inverse/execution-pair metadata"
    Assert ([string]$row.SecurityId -eq $expected[$symbol]) "$symbol unexpected LMAX ID"
    Assert ([decimal]$row.ContractMultiplier -eq 10000) "$symbol contract multiplier mismatch"
    Assert ([decimal]$row.MinOrderSize -eq 0.1) "$symbol min order size mismatch"
}

Assert ($nonFx.Classification -eq "NON_FX_ROWS_EXCLUDED") "non-FX rows should be excluded separately"
Assert ($policy.Forbidden -contains "Treating presence in catalog as execution approval") "metadata must not imply execution approval"
Assert ($quantity.QuantitiesDerivedInR008 -eq $false) "R008 must not derive quantities"
Assert ($quantity.Classification -eq "QUANTITY_DERIVATION_READY_NEXT") "quantity derivation should be ready next"
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 must remain blocked"

$jpy = $coverage.SymbolCoverage | Where-Object { $_.CoreSymbol -eq "JPYUSD" } | Select-Object -First 1
Assert ($jpy.CatalogSymbol -eq "USD/JPY") "JPYUSD must be supported by USD/JPY metadata"
Assert ($jpy.Relationship -eq "INVERSE_OR_EXECUTION_PAIR") "JPYUSD caveat relationship must be preserved"

Write-Host "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_TEST_PASS"
