$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-polygon-fx-data-r006b"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r006-intake-validation.json",
    "polygon-massive-pipeline-discovery.json",
    "polygon-fetch-safety-gate.json",
    "polygon-fx-fetch-evidence.json",
    "derived-core-symbol-price-validation.json",
    "instrument-metadata-completion.json",
    "expanded-core-fx-price-basis-manifest.json",
    "expanded-core-fx-metadata-manifest.json",
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

$intake = Read-Json "r006-intake-validation.json"
$pipeline = Read-Json "polygon-massive-pipeline-discovery.json"
$gate = Read-Json "polygon-fetch-safety-gate.json"
$fetch = Read-Json "polygon-fx-fetch-evidence.json"
$derived = Read-Json "derived-core-symbol-price-validation.json"
$metadata = Read-Json "instrument-metadata-completion.json"
$priceManifest = Read-Json "expanded-core-fx-price-basis-manifest.json"
$quantity = Read-Json "quantity-readiness-refreshed.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert ($intake.Classification -in @("R006_READY_FOR_POLYGON_FX_DATA_FETCH","R006_READY_WITH_WARNINGS")) "R006 intake is not ready."
Assert ($pipeline.Classification -in @("POLYGON_PIPELINE_FOUND_READY_FOR_BOUNDED_FETCH","POLYGON_PIPELINE_FOUND_WITH_WARNINGS","POLYGON_PIPELINE_FOUND_BUT_NOT_SAFE_TO_RUN","POLYGON_PIPELINE_NOT_FOUND")) "Invalid pipeline classification."
Assert ($gate.ExactPairList.Count -eq 6) "Provider pair list is not bounded to six pairs."
@("USDCNH","USDMXN","USDNOK","USDSEK","USDSGD","USDZAR") | ForEach-Object {
    Assert ($gate.ExactPairList -contains $_) "Missing bounded provider pair $_."
}
Assert ($gate.NoLmax -and $gate.NoR009 -and $gate.NoDbMutation -and $gate.NoCredentialPrinting) "Safety gate boundary flags failed."
Assert ($fetch.PairEvidence.Count -eq 6) "Fetch evidence must contain six rows."
Assert ($derived.SymbolValidation.Count -eq 6) "Derived price validation must contain six rows."
Assert ($metadata.SymbolMetadata.Count -eq 6) "Metadata completion must contain six rows."
Assert ($priceManifest.ForbiddenUses -contains "ProductionLive") "Production forbidden use missing."
Assert ($quantity.Classification -ne "QUANTITY_DERIVATION_READY_NEXT") "R006B must not mark quantity derivation ready while metadata is missing."
Assert ($future.Decision -in @("NEXT_CORE_ANUBIS_INTRADAY_METADATA_COMPLETION_R007","NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_EVIDENCE_R006C","NEXT_BLOCKED_POLYGON_PIPELINE_NOT_FOUND_OR_UNSAFE")) "Unexpected future package decision."
Assert ($contract.Statuses."pms-execution-candidate.v1" -eq "BLOCKED") "PMS execution candidate must remain blocked."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 must remain blocked for Core candidate."
Assert ($boundary.NoLmax -and $boundary.NoR009 -and $boundary.NoDbMutation -and $boundary.NoLedger) "Boundary safety failed."
Assert ($boundary.NoCredentialValuesPrintedOrPersisted -and $boundary.NoInventedPrices -and $boundary.NoInventedQuantities) "Secret/invention boundary failed."
if ($fetch.FetchAttempted) {
    Assert ($boundary.ProviderCallBoundedToSixMissingPairsOnly) "Provider call was not recorded as bounded."
    $fetch.PairEvidence | ForEach-Object {
        if ($_.RawResponsePath) {
            Assert ($_.RawResponsePath -like (Join-Path $ArtifactDir "raw-provider-evidence*")) "Raw response path is outside R006B raw-provider-evidence."
            Assert ($_.RawResponseHash -like "sha256:*") "Fetched response is missing hash."
        }
    }
}
Assert ($summary -notmatch "apiKey=") "Summary appears to contain an API key query parameter."
Assert ($summary -notmatch "POLYGON_API_KEY=.*[A-Za-z0-9]") "Summary appears to contain a credential value."
Assert ($summary -match "Classification: CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_") "Summary missing final classification."

Write-Host "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_VALIDATOR_PASS"
