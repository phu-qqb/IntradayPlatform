$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-polygon-fx-data-r006b"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$gate = Read-Json "polygon-fetch-safety-gate.json"
$fetch = Read-Json "polygon-fx-fetch-evidence.json"
$derived = Read-Json "derived-core-symbol-price-validation.json"
$metadata = Read-Json "instrument-metadata-completion.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$boundary = Read-Json "boundary-safety-evidence.json"

$expectedPairs = @("USDCNH","USDMXN","USDNOK","USDSEK","USDSGD","USDZAR")
Assert ($gate.ExactPairList.Count -eq 6) "provider call list must be bounded to six pairs"
foreach ($pair in $expectedPairs) {
    Assert ($gate.ExactPairList -contains $pair) "missing expected provider pair $pair"
}

Assert ($gate.NoCredentialPrinting) "credentials must be redacted"
Assert ($boundary.NoCredentialValuesPrintedOrPersisted) "credential values must not be printed or persisted"

foreach ($row in @($derived.SymbolValidation | Where-Object { $_.Classification -eq "DERIVED_CORE_PRICE_READY_INVERSE" })) {
    $expected = [math]::Round(1.0 / [double]$row.SourcePrice, 12)
    Assert ([math]::Abs($expected - [double]$row.DerivedCorePrice) -lt 0.000000000001) "inverse price formula failed for $($row.CoreSymbol)"
}

foreach ($row in @($fetch.PairEvidence | Where-Object { $_.Classification -ne "PROVIDER_PRICE_FETCHED_QUOTE_MID" })) {
    $matching = $derived.SymbolValidation | Where-Object { $_.CoreSymbol -eq $row.CoreSymbol } | Select-Object -First 1
    Assert ($matching.Classification -eq "DERIVED_CORE_PRICE_MISSING") "missing provider response should keep $($row.CoreSymbol) blocked"
}

Assert (($metadata.SymbolMetadata | Where-Object { $_.Classification -eq "METADATA_MISSING" }).Count -eq 6) "metadata missing must still block quantity"
Assert ($quantity.Classification -ne "QUANTITY_DERIVATION_READY_NEXT") "R006B must not derive or ready quantities while metadata is missing"
Assert ($boundary.NoInventedQuantities -and $boundary.NoR009) "no quantities and no R009 boundary failed"

Write-Host "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_TEST_PASS"
