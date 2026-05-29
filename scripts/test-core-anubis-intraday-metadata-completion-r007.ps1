$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-metadata-completion-r007"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$targets = Read-Json "metadata-target-inventory.json"
$validation = Read-Json "metadata-validation-by-symbol.json"
$template = Read-Json "metadata-operator-template.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"

$six = @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")
foreach ($symbol in $six) {
    Assert (($targets.Targets | Where-Object { $_.CoreSymbol -eq $symbol })) "metadata search target missing $symbol"
    Assert (($validation.SymbolValidation | Where-Object { $_.CoreSymbol -eq $symbol -and $_.Classification -eq "METADATA_MISSING" })) "metadata missing should keep $symbol blocked"
    Assert (($template.MissingMetadataTemplate | Where-Object { $_.CoreSymbol -eq $symbol })) "operator template missing $symbol"
}

Assert ($quantity.Classification -eq "QUANTITY_DERIVATION_BLOCKED_METADATA_GAPS") "metadata gaps must block quantity"
Assert ($quantity.QuantitiesDerivedInR007 -eq $false) "R007 must not derive quantities"
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 must remain blocked"
Assert ($boundary.NoR009 -and $boundary.NoDbMutation -and $boundary.NoLedger) "boundary failed"
Assert ($boundary.NoPolygonMassiveCall -and $boundary.NoLmax -and $boundary.NoExternalMarketDataCall) "R007 must not make external calls"

Write-Host "CORE_ANUBIS_INTRADAY_METADATA_COMPLETION_R007_TEST_PASS"
