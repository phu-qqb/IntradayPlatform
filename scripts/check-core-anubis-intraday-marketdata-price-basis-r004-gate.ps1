param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing required artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$Required = @(
    "r003-intake-validation.json",
    "core-symbol-price-universe.json",
    "local-price-source-inventory.json",
    "price-basis-coverage-by-core-symbol.json",
    "price-basis-manifest-candidate.json",
    "instrument-metadata-source-inventory.json",
    "instrument-metadata-coverage-by-core-symbol.json",
    "quantity-feasibility-update.json",
    "updated-pms-core-candidate-status.json",
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

$Intake = Read-Json "r003-intake-validation.json"
$Universe = Read-Json "core-symbol-price-universe.json"
$PriceSources = Read-Json "local-price-source-inventory.json"
$PriceCoverage = Read-Json "price-basis-coverage-by-core-symbol.json"
$PriceManifest = Read-Json "price-basis-manifest-candidate.json"
$MetadataSources = Read-Json "instrument-metadata-source-inventory.json"
$MetadataCoverage = Read-Json "instrument-metadata-coverage-by-core-symbol.json"
$Feasibility = Read-Json "quantity-feasibility-update.json"
$Candidate = Read-Json "updated-pms-core-candidate-status.json"
$Decision = Read-Json "future-package-decision.json"
$Contracts = Read-Json "contract-status-update.json"
$Impact = Read-Json "readiness-impact.json"
$Boundary = Read-Json "boundary-safety-evidence.json"
$Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

if ($Intake.Classification -ne "R003_READY_FOR_PRICE_BASIS_EXPANSION") { Fail "R003 intake not ready." }
if ($Universe.Classification -ne "CORE_SYMBOL_PRICE_UNIVERSE_READY") { Fail "symbol price universe not ready." }
if ($PriceSources.Classification -ne "LOCAL_PRICE_SOURCES_PARTIAL") { Fail "price source inventory should be partial." }
if ($PriceSources.ExternalApiCalls -ne 0 -or $PriceSources.DbQueried -ne $false) { Fail "external/API/DB use is not allowed." }
if ($PriceCoverage.OverallClassification -ne "PRICE_BASIS_READY_FOR_SUBSET_ONLY") { Fail "price basis should be subset only." }
if (@($PriceCoverage.SymbolsMissing).Count -le 0) { Fail "missing price symbols must be explicit." }
if ($PriceManifest.Classification -ne "PRICE_BASIS_MANIFEST_READY_PARTIAL") { Fail "price manifest should be partial." }
if ($MetadataSources.Classification -ne "INSTRUMENT_METADATA_SOURCES_PARTIAL") { Fail "metadata source inventory should be partial." }
if ($MetadataCoverage.OverallClassification -ne "INSTRUMENT_METADATA_READY_FOR_SUBSET_ONLY") { Fail "metadata coverage should be subset only." }
if (@($MetadataCoverage.SymbolsMissing).Count -le 0) { Fail "missing metadata symbols must be explicit." }
if ($Feasibility.Classification -ne "QUANTITY_FEASIBILITY_BLOCKED_PRICE_BASIS") { Fail "quantity feasibility must be blocked by price basis." }
if ($Feasibility.QuantitiesDerived -ne $false -or $Feasibility.InventedQuantities -ne $false) { Fail "quantities must not be derived or invented." }
if ($Candidate.R009Ready -ne $false -or $Candidate.ExecutionReadyPreview -ne $false -or $Candidate.RiskReviewReady -ne $false) { Fail "candidate must not be R009/execution/risk ready." }
if ($Decision.Decision -ne "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_PRICE_EVIDENCE_R005") { Fail "future package decision mismatch." }
if ($Impact.NoExecutionOccurred -ne $true -or $Impact.NoR009ReadinessGranted -ne $true -or $Impact.NoQuantitiesInvented -ne $true) { Fail "readiness impact violates no-execution/no-R009/no-invention." }

$ContractMap = @{}
foreach ($status in $Contracts.Statuses) { $ContractMap[$status.ContractId] = $status.Status }
if ($ContractMap["core-anubis-marketdata-price-basis.v1"] -ne "PARTIAL") { Fail "marketdata price basis contract should be PARTIAL." }
if ($ContractMap["core-anubis-quantity-feasibility.v1"] -ne "BLOCKED_PRICE_BASIS") { Fail "quantity feasibility contract should be blocked." }
if ($ContractMap["pms-execution-candidate.v1"] -ne "BLOCKED") { Fail "execution candidate must be blocked." }
if ($ContractMap["r009-execution-readiness.v1"] -ne "BLOCKED_FOR_CORE_CANDIDATE") { Fail "R009 must be blocked for Core candidate." }

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
    "NoInventedFxRates",
    "NoInventedQuantitiesWithoutRequiredInputs",
    "NoR010Transfer"
)) {
    if ($Boundary.$field -ne $true) { Fail "Boundary safety flag is not true: $field" }
}

if ($Summary -notmatch "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_WITH_WARNINGS_PRICE_PARTIAL") { Fail "summary missing final classification." }
if ($Summary -notmatch "Is R009 allowed\? no") { Fail "summary must say R009 is not allowed." }
if ($Summary -notmatch "Can quantities be derived now\? no") { Fail "summary must say quantities cannot be derived." }

Write-Host "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_VALIDATOR_PASS"
