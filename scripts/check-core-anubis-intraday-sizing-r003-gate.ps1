param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_SIZING_R003_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact: $Name"
    }
    try {
        Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    } catch {
        Fail "Invalid JSON artifact: $Name :: $($_.Exception.Message)"
    }
}

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sizing-r003"
$Required = @(
    "r002-intake-validation.json",
    "core-netted-weights-symbol-inventory.json",
    "core-sandbox-target-notional-policy.json",
    "marketdata-price-basis-coverage.json",
    "instrument-metadata-coverage.json",
    "quantity-transformation-policy.json",
    "pms-core-candidate-preview-sizing-status.json",
    "r009-approval-readiness-decision.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

foreach ($Name in $Required) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactDir $Name))) {
        Fail "Missing required artifact: $Name"
    }
}

$AllText = ($Required | ForEach-Object { Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $_) }) -join "`n"
$Forbidden = @(
    'R009Ready"\s*:\s*true',
    'R009AllowedInR003"\s*:\s*true',
    'ExecutionReadyPreview"\s*:\s*true',
    'NoDbMutation"\s*:\s*false',
    'NoLmax"\s*:\s*false',
    'NoOrderFillReport"\s*:\s*false',
    'NoLedger"\s*:\s*false',
    'NoR010Transfer"\s*:\s*false',
    'NoInventedPrices"\s*:\s*false',
    'NoInventedQuantitiesWithoutRequiredInputs"\s*:\s*false'
)
foreach ($Pattern in $Forbidden) {
    if ($AllText -match $Pattern) {
        Fail "Forbidden readiness/action claim matched: $Pattern"
    }
}

$Intake = Read-Json "r002-intake-validation.json"
$Inventory = Read-Json "core-netted-weights-symbol-inventory.json"
$Target = Read-Json "core-sandbox-target-notional-policy.json"
$Prices = Read-Json "marketdata-price-basis-coverage.json"
$Metadata = Read-Json "instrument-metadata-coverage.json"
$Transform = Read-Json "quantity-transformation-policy.json"
$Candidate = Read-Json "pms-core-candidate-preview-sizing-status.json"
$R009 = Read-Json "r009-approval-readiness-decision.json"
$Decision = Read-Json "future-package-decision.json"
$Contracts = Read-Json "contract-status-update.json"
$Impact = Read-Json "readiness-impact.json"
$Boundary = Read-Json "boundary-safety-evidence.json"
$Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

if ($Intake.Classification -ne "R002_CORE_HANDOFF_READY_FOR_SIZING") { Fail "R002 intake not ready." }
if ($Intake.R002DidNotCreateQuantities -ne $true) { Fail "R002 must not have created quantities." }
if ($Intake.R002DidNotAllowR009Execution -ne $true) { Fail "R002 must not have allowed R009." }
if ($Inventory.Classification -ne "CORE_NETTED_WEIGHTS_SYMBOL_INVENTORY_READY") { Fail "symbol inventory not ready." }
if ($Inventory.AllSymbolsCoreCanonicalXXXUSD -ne $true) { Fail "Core symbols must be canonical XXXUSD." }
if ($Inventory.UnexpectedUSDJPYEmission -ne $false) { Fail "USDJPY emission must be absent." }
if ($Target.Classification -ne "CORE_SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED_USD_6000000") { Fail "target notional policy not ready." }
if ($Target.TargetNotionalAmount -ne 6000000 -or $Target.TargetNotionalScope -ne "SandboxPreviewSizingOnly") { Fail "target notional policy scope/amount mismatch." }
if ($Prices.Classification -ne "MARKETDATA_PRICE_BASIS_BLOCKED_MISSING_CORE_SYMBOL_PRICES") { Fail "price basis must be blocked for missing Core symbols." }
if (@($Prices.MissingCoreSymbolPrices).Count -le 0) { Fail "missing Core symbol prices must be explicit." }
if ($Prices.InventedPrices -ne $false -or $Prices.ExternalDataCalls -ne 0) { Fail "prices must not be invented or externally fetched." }
if ($Metadata.Classification -ne "INSTRUMENT_METADATA_READY_FOR_SUBSET_ONLY") { Fail "metadata must be subset only." }
if (@($Metadata.MissingCoreSymbolMetadata).Count -le 0) { Fail "missing metadata must be explicit." }
if ($Transform.Classification -ne "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_BASIS") { Fail "quantity transformation must be blocked by missing price basis." }
if ($Transform.QuantitiesInvented -ne $false) { Fail "quantities must not be invented." }
if ($Candidate.Classification -ne "PMS_CORE_CANDIDATE_PREVIEW_WEIGHTS_ONLY_SIZING_BLOCKED") { Fail "candidate must remain weights-only sizing blocked." }
if ($null -ne $Candidate.Quantities) { Fail "candidate quantities must be null." }
if ($Candidate.ExecutionReadyPreview -ne $false -or $Candidate.R009Ready -ne $false) { Fail "candidate must not be execution/R009 ready." }
if ($R009.Classification -ne "CORE_CANDIDATE_PARTIAL_OR_BLOCKED_SIZING_NO_RISK_REVIEW") { Fail "R009 approval decision mismatch." }
if ($R009.R010PrototypeApprovalReusable -ne $false) { Fail "R010 must not be reusable." }
if ($Decision.Decision -ne "NEXT_CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004") { Fail "future package decision mismatch." }
if ($Impact.NoExecutionOccurred -ne $true -or $Impact.NoR009ReadinessGranted -ne $true) { Fail "readiness impact must block execution/R009." }

$ContractMap = @{}
foreach ($Status in $Contracts.Statuses) {
    $ContractMap[$Status.ContractId] = $Status.Status
}
if ($ContractMap["core-anubis-handoff-consumer.v1"] -ne "YES") { Fail "handoff consumer contract must be YES." }
if ($ContractMap["core-anubis-target-notional.v1"] -ne "YES") { Fail "target notional contract must be YES." }
if ($ContractMap["core-anubis-marketdata-price-basis.v1"] -ne "BLOCKED") { Fail "marketdata price basis contract must be BLOCKED." }
if ($ContractMap["core-anubis-pms-sizing.v1"] -ne "BLOCKED") { Fail "pms sizing contract must be BLOCKED." }
if ($ContractMap["pms-execution-candidate.v1"] -ne "BLOCKED") { Fail "pms execution candidate must be BLOCKED." }
if ($ContractMap["accounting-attribution.v1"] -ne "BLOCKED") { Fail "accounting attribution must be BLOCKED." }
if ($ContractMap["production-readiness.v1"] -ne "BLOCKED") { Fail "production readiness must be BLOCKED." }

foreach ($Field in @(
    "NoCoreExecution",
    "NoManager",
    "NoAnubis",
    "NoCuda",
    "NoCoreNetting",
    "NoLmax",
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
    "NoInventedQuantitiesWithoutRequiredInputs",
    "NoR010Transfer"
)) {
    if ($Boundary.$Field -ne $true) {
        Fail "Boundary safety flag is not true: $Field"
    }
}

if ($Summary -notmatch "CORE_ANUBIS_INTRADAY_SIZING_R003_WITH_WARNINGS_SIZING_BLOCKED_PRICE_BASIS") { Fail "summary missing final classification." }
if ($Summary -notmatch "Was target notional applied to Core weights\? yes") { Fail "summary must say target notional applied." }
if ($Summary -notmatch "Were quantities derived\? no") { Fail "summary must say quantities were not derived." }
if ($Summary -notmatch "Is R009 allowed\? no") { Fail "summary must say R009 is not allowed." }

Write-Host "CORE_ANUBIS_INTRADAY_SIZING_R003_VALIDATOR_PASS"
