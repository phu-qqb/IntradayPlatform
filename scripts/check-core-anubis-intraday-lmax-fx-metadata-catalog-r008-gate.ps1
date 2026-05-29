$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r007-intake-validation.json",
    "operator-lmax-csv-validation.json",
    "full-lmax-fx-metadata-catalog.json",
    "non-fx-instrument-classification.json",
    "core-symbol-metadata-coverage-from-catalog.json",
    "completed-core-fx-metadata-manifest.json",
    "metadata-catalog-reuse-policy.json",
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

$intake = Read-Json "r007-intake-validation.json"
$csv = Read-Json "operator-lmax-csv-validation.json"
$catalog = Read-Json "full-lmax-fx-metadata-catalog.json"
$nonFx = Read-Json "non-fx-instrument-classification.json"
$coverage = Read-Json "core-symbol-metadata-coverage-from-catalog.json"
$manifest = Read-Json "completed-core-fx-metadata-manifest.json"
$policy = Read-Json "metadata-catalog-reuse-policy.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

$requiredCore = @("AUDUSD","CADUSD","CHFUSD","CNHUSD","EURUSD","GBPUSD","JPYUSD","MXNUSD","NOKUSD","NZDUSD","SEKUSD","SGDUSD","ZARUSD")
$formerlyMissing = @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")

Assert ($intake.Classification -eq "R007_READY_FOR_LMAX_FX_METADATA_CATALOG_IMPORT") "R007 intake not ready."
Assert ($csv.Classification -in @("OPERATOR_LMAX_CSV_READY","OPERATOR_LMAX_CSV_READY_WITH_WARNINGS")) "Operator CSV not ready."
Assert ($csv.CsvHash -like "sha256:*") "CSV hash missing."
Assert ($csv.MissingColumns.Count -eq 0) "CSV missing required columns."
Assert ($csv.NoCredentialSecretsPresent) "CSV appears to contain credential/secret patterns."
Assert ($catalog.Classification -eq "FULL_LMAX_FX_METADATA_CATALOG_READY") "Full FX catalog not ready."
Assert ($catalog.FxRowCount -gt 0) "FX catalog empty."
Assert ($nonFx.Classification -in @("NON_FX_ROWS_EXCLUDED","NO_NON_FX_ROWS_FOUND")) "Non-FX classification invalid."
Assert ($coverage.Classification -eq "CORE_SYMBOL_METADATA_READY_ALL") "Core symbol metadata not fully ready."
foreach ($symbol in $requiredCore) {
    $row = $coverage.SymbolCoverage | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    Assert ($null -ne $row) "Missing Core coverage for $symbol."
    Assert ($row.ValidationStatus -in @("METADATA_READY_DIRECT","METADATA_READY_INVERSE_OR_EXECUTION_PAIR")) "$symbol metadata not ready."
    Assert ($row.ContractMultiplier -ne $null -and $row.MinOrderSize -ne $null -and -not [string]::IsNullOrWhiteSpace([string]$row.QuotedCurrency)) "$symbol missing sizing metadata fields."
    Assert (-not [string]::IsNullOrWhiteSpace([string]$row.SecurityId)) "$symbol missing SecurityId."
}
foreach ($symbol in $formerlyMissing) {
    Assert ($manifest.SymbolsCovered -contains $symbol) "Formerly missing symbol not covered: $symbol."
}
Assert ($manifest.Classification -eq "COMPLETED_CORE_FX_METADATA_MANIFEST_READY_ALL_SYMBOLS") "Completed metadata manifest not ready for all symbols."
Assert ($manifest.SymbolsStillMissing.Count -eq 0) "Completed metadata manifest still has missing symbols."
Assert ($policy.Classification -eq "METADATA_CATALOG_REUSE_POLICY_READY") "Reuse policy not ready."
Assert ($policy.Forbidden -contains "Auto-R009 submission") "Reuse policy must forbid auto-R009."
Assert ($quantity.Classification -eq "QUANTITY_DERIVATION_READY_NEXT") "Quantity derivation should be ready next."
Assert ($quantity.QuantitiesDerivedInR008 -eq $false) "R008 must not derive quantities."
Assert ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009") "Unexpected future package decision."
Assert ($contract.Statuses."core-anubis-instrument-metadata.v1" -eq "YES") "Metadata contract status must be YES."
Assert ($contract.Statuses."core-anubis-quantity-readiness.v1" -eq "YES") "Quantity readiness status must be YES."
Assert ($contract.Statuses."pms-execution-candidate.v1" -eq "BLOCKED") "PMS execution candidate must remain blocked."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 must remain blocked."
Assert ($boundary.NoExternalMarketDataCall -and $boundary.NoLmaxCall -and $boundary.NoPolygonMassiveCall) "External call boundary failed."
Assert ($boundary.NoDbMutation -and $boundary.NoR009 -and $boundary.NoLedger) "Mutation/trading boundary failed."
Assert ($boundary.NoInventedMetadata -and $boundary.NoInventedQuantities -and $boundary.NoR010Transfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_PASS_FULL_METADATA_CATALOG_READY_QUANTITY_READY") "Summary classification missing."

Write-Host "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_VALIDATOR_PASS"
