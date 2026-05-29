param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-operator-evidence-import-r006"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing required artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$Required = @(
    "r005-intake-validation.json",
    "operator-evidence-file-discovery.json",
    "operator-price-evidence-validation.json",
    "operator-metadata-evidence-validation.json",
    "expanded-fx-price-basis-manifest.json",
    "expanded-fx-metadata-manifest.json",
    "remaining-evidence-gaps.json",
    "quantity-readiness-refreshed.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

foreach ($name in $Required) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactDir $name))) {
        Fail "Missing required artifact: $name"
    }
}

$Intake = Read-Json "r005-intake-validation.json"
$Discovery = Read-Json "operator-evidence-file-discovery.json"
$Price = Read-Json "operator-price-evidence-validation.json"
$Metadata = Read-Json "operator-metadata-evidence-validation.json"
$PriceManifest = Read-Json "expanded-fx-price-basis-manifest.json"
$MetadataManifest = Read-Json "expanded-fx-metadata-manifest.json"
$Gaps = Read-Json "remaining-evidence-gaps.json"
$Quantity = Read-Json "quantity-readiness-refreshed.json"
$Decision = Read-Json "future-package-decision.json"
$Contracts = Read-Json "contract-status-update.json"
$Boundary = Read-Json "boundary-safety-evidence.json"
$Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

if ($Intake.Classification -ne "R005_READY_FOR_OPERATOR_EVIDENCE_IMPORT") { Fail "R005 intake not ready." }
if ($Discovery.Classification -ne "OPERATOR_EVIDENCE_TEMPLATE_ONLY_NO_FILLED_EVIDENCE") { Fail "expected template-only discovery." }
if ($Discovery.FilledEvidenceFound -ne $false) { Fail "filled evidence unexpectedly detected." }
if ($Price.OverallClassification -ne "OPERATOR_PRICE_EVIDENCE_MISSING_ALL_REMAINING") { Fail "price validation should be missing all remaining." }
if ($Metadata.OverallClassification -ne "OPERATOR_METADATA_EVIDENCE_MISSING_ALL_REMAINING") { Fail "metadata validation should be missing all remaining." }
if ($PriceManifest.Classification -ne "EXPANDED_FX_PRICE_BASIS_MANIFEST_READY_PARTIAL") { Fail "expanded price manifest should remain partial." }
if ($MetadataManifest.Classification -ne "EXPANDED_FX_METADATA_MANIFEST_READY_PARTIAL") { Fail "expanded metadata manifest should remain partial." }
if ($Gaps.Classification -ne "REMAINING_FX_PRICE_AND_METADATA_GAPS") { Fail "remaining gap classification mismatch." }
if ($Quantity.Classification -ne "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS") { Fail "quantity readiness should remain blocked." }
if ($Quantity.DoNotDeriveQuantitiesInR006 -ne $true) { Fail "R006 must not derive quantities." }
if ($Decision.Decision -ne "NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_R006B") { Fail "future package decision mismatch." }

$ContractsMap = @{}
foreach ($status in $Contracts.Statuses) { $ContractsMap[$status.ContractId] = $status.Status }
if ($ContractsMap["pms-execution-candidate.v1"] -ne "BLOCKED") { Fail "PMS execution candidate must remain blocked." }
if ($ContractsMap["r009-execution-readiness.v1"] -ne "BLOCKED_FOR_CORE_CANDIDATE") { Fail "R009 readiness must remain blocked." }
if ($ContractsMap["accounting-attribution.v1"] -ne "BLOCKED") { Fail "accounting attribution must remain blocked." }
if ($ContractsMap["production-readiness.v1"] -ne "BLOCKED") { Fail "production readiness must remain blocked." }

foreach ($field in @(
    "NoCoreExecution",
    "NoManager",
    "NoAnubis",
    "NoCuda",
    "NoCoreNetting",
    "NoLmax",
    "NoExternalMarketDataCall",
    "NoFreshPolygonMassiveCall",
    "NoR009",
    "NoOrderFillReport",
    "NoDbMutation",
    "NoLedger",
    "NoAccountIdInvented",
    "NoPortfolioIdInvented",
    "NoStrategyIdInvented",
    "NoSourceExecutionIntentIdInvented",
    "NoAccountCurrencyInvented",
    "NoInventedPrices",
    "NoInventedQuantities",
    "NoR010Transfer"
)) {
    if ($Boundary.$field -ne $true) { Fail "Boundary safety flag is not true: $field" }
}

if ($Summary -notmatch "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_WITH_WARNINGS_TEMPLATE_ONLY_NO_FILLED_EVIDENCE") { Fail "summary missing final classification." }
if ($Summary -notmatch "Was filled operator evidence found\? no") { Fail "summary must state no filled evidence." }
if ($Summary -notmatch "Is full quantity derivation ready next\? no") { Fail "summary must block quantity derivation." }

Write-Host "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_VALIDATOR_PASS"
