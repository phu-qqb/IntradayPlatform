param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-data-basis-r005"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing required artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$Required = @(
    "r004-intake-validation.json",
    "core-fx-universe-basis.json",
    "local-fx-price-evidence-inventory.json",
    "local-fx-metadata-inventory.json",
    "core-symbol-price-basis-validation.json",
    "core-symbol-metadata-validation.json",
    "core-fx-price-basis-manifest.json",
    "core-fx-metadata-manifest.json",
    "operator-fx-price-metadata-evidence-template.json",
    "quantity-readiness-decision.json",
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

$Intake = Read-Json "r004-intake-validation.json"
$Universe = Read-Json "core-fx-universe-basis.json"
$PriceInventory = Read-Json "local-fx-price-evidence-inventory.json"
$MetadataInventory = Read-Json "local-fx-metadata-inventory.json"
$PriceValidation = Read-Json "core-symbol-price-basis-validation.json"
$MetadataValidation = Read-Json "core-symbol-metadata-validation.json"
$PriceManifest = Read-Json "core-fx-price-basis-manifest.json"
$MetadataManifest = Read-Json "core-fx-metadata-manifest.json"
$Template = Read-Json "operator-fx-price-metadata-evidence-template.json"
$Quantity = Read-Json "quantity-readiness-decision.json"
$Decision = Read-Json "future-package-decision.json"
$Contracts = Read-Json "contract-status-update.json"
$Boundary = Read-Json "boundary-safety-evidence.json"
$Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

if ($Intake.Classification -ne "R004_READY_FOR_FX_DATA_BASIS") { Fail "R004 intake not ready." }
if ($Universe.Classification -ne "CORE_FX_UNIVERSE_BASIS_READY") { Fail "FX universe basis not ready." }
if ($PriceInventory.Classification -ne "LOCAL_FX_PRICE_EVIDENCE_FOUND_PARTIAL") { Fail "price inventory should be partial." }
if ($PriceInventory.InternetCalled -ne $false -or $PriceInventory.ExternalApiCalled -ne $false -or $PriceInventory.DbQueried -ne $false) { Fail "forbidden price discovery boundary crossed." }
if ($MetadataInventory.Classification -ne "LOCAL_FX_METADATA_FOUND_PARTIAL") { Fail "metadata inventory should be partial." }
if ($PriceValidation.OverallClassification -ne "PRICE_BASIS_READY_PARTIAL") { Fail "price validation should be partial." }
if ($MetadataValidation.OverallClassification -ne "METADATA_READY_PARTIAL") { Fail "metadata validation should be partial." }
if ($PriceManifest.Classification -ne "CORE_FX_PRICE_BASIS_MANIFEST_READY_PARTIAL") { Fail "price manifest should be partial." }
if ($MetadataManifest.Classification -ne "CORE_FX_METADATA_MANIFEST_READY_PARTIAL") { Fail "metadata manifest should be partial." }
if ($Template.Classification -ne "OPERATOR_FX_EVIDENCE_TEMPLATE_CREATED_FOR_REMAINING_GAPS") { Fail "operator evidence template required." }
if (@($Template.TemplateRows).Count -le 0) { Fail "operator template must contain remaining gaps." }
if ($Quantity.Classification -ne "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS") { Fail "quantity readiness should be blocked by price and metadata gaps." }
if ($Quantity.DoNotDeriveQuantitiesInR005 -ne $true) { Fail "R005 must not derive quantities." }
if ($Decision.Decision -ne "NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006") { Fail "future package decision mismatch." }

$ContractsMap = @{}
foreach ($status in $Contracts.Statuses) { $ContractsMap[$status.ContractId] = $status.Status }
if ($ContractsMap["pms-execution-candidate.v1"] -ne "BLOCKED") { Fail "PMS execution candidate must remain blocked." }
if ($ContractsMap["r009-execution-readiness.v1"] -ne "BLOCKED_FOR_CORE_CANDIDATE") { Fail "R009 must remain blocked." }
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

if ($Summary -notmatch "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_WITH_WARNINGS_PARTIAL_DATA_BASIS_TEMPLATE_CREATED") { Fail "summary missing final classification." }
if ($Summary -notmatch "Was an operator evidence template created\? yes") { Fail "summary must confirm operator template." }
if ($Summary -notmatch "Is quantity derivation ready next\? no") { Fail "summary must block quantity derivation." }

Write-Host "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_VALIDATOR_PASS"
