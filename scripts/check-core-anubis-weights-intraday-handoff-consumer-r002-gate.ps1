param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_VALIDATOR_FAIL: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    } catch {
        Fail "Invalid JSON artifact: $Path :: $($_.Exception.Message)"
    }
}

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-weights-intraday-handoff-consumer-r002"
$Required = @(
    "core-handoff-intake-validation.json",
    "netted-weights-semantic-validation.json",
    "intraday-symbol-policy-validation.json",
    "r010-prototype-separation.json",
    "core-handoff-consumer-evidence.json",
    "pms-core-weights-candidate-preview.json",
    "future-package-decision.json",
    "readiness-impact.json",
    "contract-status-update.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

foreach ($Name in $Required) {
    $Path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Name"
    }
}

$AllText = ($Required | ForEach-Object { Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $_) }) -join "`n"
$ForbiddenPositiveClaims = @(
    'R009Ready"\s*:\s*true',
    'R009SubmissionAllowed"\s*:\s*true',
    'ExecutionReadinessGranted"\s*:\s*true',
    'LmaxCallOccurred"\s*:\s*true',
    'NoIntradayDbMutation"\s*:\s*false',
    'NotProduction"\s*:\s*false',
    'NotAccounting"\s*:\s*false',
    'NotExecuted"\s*:\s*false',
    'NotLedgerCommit"\s*:\s*false',
    'R010Transferability"\s*:\s*true',
    'QuantityStatus"\s*:\s*"(?!MissingSizingAndMarketDataBinding)'
)
foreach ($Pattern in $ForbiddenPositiveClaims) {
    if ($AllText -match $Pattern) {
        Fail "Forbidden readiness/action claim matched: $Pattern"
    }
}

$Intake = Read-Json (Join-Path $ArtifactDir "core-handoff-intake-validation.json")
$Weights = Read-Json (Join-Path $ArtifactDir "netted-weights-semantic-validation.json")
$Symbols = Read-Json (Join-Path $ArtifactDir "intraday-symbol-policy-validation.json")
$R010 = Read-Json (Join-Path $ArtifactDir "r010-prototype-separation.json")
$Evidence = Read-Json (Join-Path $ArtifactDir "core-handoff-consumer-evidence.json")
$Candidate = Read-Json (Join-Path $ArtifactDir "pms-core-weights-candidate-preview.json")
$Decision = Read-Json (Join-Path $ArtifactDir "future-package-decision.json")
$Impact = Read-Json (Join-Path $ArtifactDir "readiness-impact.json")
$Contracts = Read-Json (Join-Path $ArtifactDir "contract-status-update.json")
$Boundary = Read-Json (Join-Path $ArtifactDir "boundary-safety-evidence.json")
$Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

if ($Intake.Classification -ne "CORE_HANDOFF_INTAKE_READY") { Fail "Core handoff intake is not ready." }
if ($Intake.CoreHandoffManifestHashMatchesExpected -ne $true) { Fail "Core handoff manifest hash mismatch." }
if ($Intake.NettedUsdWeightsHashMatchesExpected -ne $true) { Fail "Netted weights hash mismatch." }
if ($Intake.R010Transferability -ne $false) { Fail "R010 transferability must be false." }

if ($Weights.Classification -ne "NETTED_WEIGHTS_SEMANTICS_READY") { Fail "Netted weights semantics are not ready." }
if ($Weights.DirectCrossesAbsent -ne $true) { Fail "Direct crosses must be absent." }
if ($Weights.USDJPYNotEmittedByCore -ne $true) { Fail "USDJPY must not be emitted by Core." }
if ($Weights.JPYUSDCaveatPresent -ne $true) { Fail "JPYUSD caveat must be explicit." }
if ($Weights.WeightsParseNumerically -ne $true) { Fail "Weights must parse numerically." }
if (@($Weights.DuplicateSymbols).Count -ne 0) { Fail "Duplicate symbols found." }

if ($Symbols.Classification -ne "INTRADAY_SYMBOL_POLICY_READY_FOR_PMS_CANDIDATE_PREVIEW") { Fail "Intraday symbol policy is not ready." }
if ($Symbols.JPYUSDLaterMapsToUSDJPY.RequiresInversion -ne $true) { Fail "JPYUSD inversion caveat missing." }
if ($Symbols.CoreHandoffCreatesExecutionSymbols -ne $false) { Fail "Core handoff must not create execution symbols." }
if ($Symbols.R009ReadyIntentCreatedInThisPackage -ne $false) { Fail "R009-ready intent must not be created." }

if ($R010.Classification -ne "R010_PROTOTYPE_SEPARATION_CONFIRMED") { Fail "R010 separation is not confirmed." }
if ($R010.R010TransferableToCoreAnubisOutput -ne $false) { Fail "R010 must not transfer." }
if ($R010.CrossRailR014RemainsPmsIntentDrivenAndUnchanged -ne $true) { Fail "CROSS-RAIL-R014 preservation missing." }

if ($Evidence.Classification -ne "CORE_HANDOFF_CONSUMER_EVIDENCE_CREATED") { Fail "Consumer evidence not created." }
if ($Evidence.IntradayConnected -ne "evidence-only / no execution") { Fail "Intraday connection must be evidence-only." }
if ($Evidence.RequiresPmsSizing -ne $true -or $Evidence.RequiresRiskReview -ne $true -or $Evidence.RequiresOperatorApproval -ne $true) {
    Fail "Future PMS sizing/risk/operator approval requirements missing."
}

if ($Candidate.Classification -ne "PMS_CORE_WEIGHTS_CANDIDATE_PREVIEW_CREATED_WEIGHTS_ONLY") { Fail "Weights-only candidate preview missing." }
if ($null -ne $Candidate.Quantities) { Fail "Candidate preview must not contain quantities." }
if ($Candidate.QuantityStatus -ne "MissingSizingAndMarketDataBinding") { Fail "Candidate preview quantity status must block sizing." }
foreach ($Field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency")) {
    if ($null -ne $Candidate.$Field) {
        Fail "$Field must not be invented."
    }
}
if ($Candidate.R009Ready -ne $false) { Fail "Candidate must not be R009-ready." }

if ($Decision.Decision -ne "NEXT_CORE_ANUBIS_INTRADAY_SIZING_R003") { Fail "Unexpected next package decision." }
if ($Impact.Classification -ne "INTRADAY_CORE_HANDOFF_CONSUMED_NO_EXECUTION_READINESS_CHANGE") { Fail "Readiness impact classification mismatch." }
if ($Impact.ExecutionReadinessGranted -ne $false) { Fail "Execution readiness must not be granted." }
if ($Impact.R009SubmissionAllowed -ne $false) { Fail "R009 submission must not be allowed." }

$ContractMap = @{}
foreach ($Status in $Contracts.Statuses) {
    $ContractMap[$Status.ContractId] = $Status.Status
}
if ($ContractMap["core-anubis-handoff-consumer.v1"] -ne "YES") { Fail "Core handoff consumer contract must be YES." }
if ($ContractMap["core-anubis-netted-weights.v1"] -ne "YES") { Fail "Core netted weights contract must be YES." }
if ($ContractMap["pms-core-weights-candidate.v1"] -ne "WITH_WARNINGS") { Fail "PMS Core weights candidate must be WITH_WARNINGS." }
if ($ContractMap["pms-sizing-for-core-weights.v1"] -ne "BLOCKED") { Fail "PMS sizing must be BLOCKED." }
if ($ContractMap["pms-risk-approval-for-core-weights.v1"] -ne "BLOCKED") { Fail "Risk approval must be BLOCKED." }
if ($ContractMap["pms-execution-candidate.v1"] -ne "BLOCKED") { Fail "PMS execution candidate must be BLOCKED." }
if ($ContractMap["accounting-attribution.v1"] -ne "BLOCKED") { Fail "Accounting attribution must be BLOCKED." }
if ($ContractMap["production-readiness.v1"] -ne "BLOCKED") { Fail "Production readiness must be BLOCKED." }

foreach ($Field in @(
    "NoManagerExecution",
    "NoAnubisExecution",
    "NoCuda",
    "NoNettingExecution",
    "NoCoreArtifactMutation",
    "NoIntradayDbMutation",
    "NoPmsEmsOms",
    "NoR009",
    "NoLmax",
    "NoOrdersFills",
    "NoLedger",
    "NoProductionLive",
    "NoAccountIdInvented",
    "NoPortfolioIdInvented",
    "NoStrategyIdInvented",
    "NoSourceExecutionIntentIdInvented",
    "NoAccountCurrencyInvented",
    "NoQuantitiesInvented",
    "NoR010Transfer"
)) {
    if ($Boundary.$Field -ne $true) {
        Fail "Boundary safety flag is not true: $Field"
    }
}

if ($Summary -notmatch "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_WITH_WARNINGS_HANDOFF_CONSUMED_SIZING_BLOCKED") {
    Fail "Summary missing final classification."
}
if ($Summary -notmatch "Are quantities present\? no") { Fail "Summary must state quantities are absent." }
if ($Summary -notmatch "Is R010 transferable\? no") { Fail "Summary must state R010 is not transferable." }
if ($Summary -notmatch "Is Intraday execution-ready from Core weights\? no") { Fail "Summary must state execution is not ready." }

Write-Host "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_VALIDATOR_PASS"
