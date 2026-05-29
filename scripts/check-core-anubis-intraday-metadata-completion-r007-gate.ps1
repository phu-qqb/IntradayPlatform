$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-metadata-completion-r007"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r006b-intake-validation.json",
    "metadata-target-inventory.json",
    "local-metadata-source-search.json",
    "metadata-validation-by-symbol.json",
    "completed-metadata-manifest.json",
    "metadata-operator-template.json",
    "quantity-readiness-refreshed.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)
foreach ($name in $required) {
    Assert (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required artifact: $name"
}

$intake = Read-Json "r006b-intake-validation.json"
$targets = Read-Json "metadata-target-inventory.json"
$search = Read-Json "local-metadata-source-search.json"
$validation = Read-Json "metadata-validation-by-symbol.json"
$manifest = Read-Json "completed-metadata-manifest.json"
$template = Read-Json "metadata-operator-template.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

$six = @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")
Assert ($intake.Classification -in @("R006B_READY_FOR_METADATA_COMPLETION","R006B_READY_WITH_WARNINGS")) "R006B intake not ready."
Assert ($intake.PriceBasisComplete) "R006B price basis must be complete."
Assert ($targets.Targets.Count -eq 6) "Metadata target inventory must include six symbols."
foreach ($symbol in $six) {
    Assert (($targets.Targets | Where-Object { $_.CoreSymbol -eq $symbol })) "Metadata target missing $symbol."
}
Assert ($search.Classification -in @("LOCAL_METADATA_FOUND_ALL_REQUIRED","LOCAL_METADATA_FOUND_PARTIAL","LOCAL_METADATA_NOT_FOUND")) "Invalid local metadata search classification."
Assert ($validation.SymbolValidation.Count -eq 6) "Metadata validation must include six symbols."
foreach ($symbol in $six) {
    $row = $validation.SymbolValidation | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    Assert ($null -ne $row) "Missing validation row for $symbol."
    Assert ($row.Classification -eq "METADATA_MISSING") "$symbol should remain missing unless complete evidence exists."
}
Assert ($validation.Classification -eq "METADATA_STILL_MISSING") "Overall metadata classification should be still missing."
Assert ($manifest.Classification -eq "COMPLETED_METADATA_MANIFEST_READY_PARTIAL") "Completed manifest should be partial."
foreach ($symbol in $six) {
    Assert ($manifest.SymbolsStillMissing -contains $symbol) "Completed manifest must keep $symbol missing."
}
Assert ($template.Classification -eq "METADATA_OPERATOR_TEMPLATE_CREATED_FOR_REMAINING_GAPS") "Metadata operator template must be created."
Assert ($template.MissingMetadataTemplate.Count -eq 6) "Metadata template must contain six rows."
Assert ($quantity.Classification -eq "QUANTITY_DERIVATION_BLOCKED_METADATA_GAPS") "Quantity derivation must remain blocked by metadata."
Assert ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_METADATA_OPERATOR_IMPORT_R008") "Unexpected future package decision."
Assert ($contract.Statuses."core-anubis-marketdata-price-basis.v1" -eq "YES") "Price basis status must remain YES."
Assert ($contract.Statuses."core-anubis-instrument-metadata.v1" -eq "BLOCKED") "Metadata status must remain blocked."
Assert ($contract.Statuses."pms-execution-candidate.v1" -eq "BLOCKED") "PMS execution candidate must remain blocked."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 must remain blocked."
Assert ($boundary.NoExternalMarketDataCall -and $boundary.NoLmax -and $boundary.NoPolygonMassiveCall) "External call boundary failed."
Assert ($boundary.NoDbMutation -and $boundary.NoR009 -and $boundary.NoLedger) "Mutation/trading boundary failed."
Assert ($boundary.NoInventedMetadata -and $boundary.NoInventedQuantities -and $boundary.NoR010Transfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_METADATA_COMPLETION_R007_WITH_WARNINGS_NO_LOCAL_METADATA_TEMPLATE_CREATED") "Summary classification missing."

Write-Host "CORE_ANUBIS_INTRADAY_METADATA_COMPLETION_R007_VALIDATOR_PASS"
