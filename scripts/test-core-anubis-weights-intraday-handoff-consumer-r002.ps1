param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_TEST_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing artifact under test: $Name"
    }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-weights-intraday-handoff-consumer-r002"

$Intake = Read-Json "core-handoff-intake-validation.json"
$Weights = Read-Json "netted-weights-semantic-validation.json"
$Candidate = Read-Json "pms-core-weights-candidate-preview.json"
$R010 = Read-Json "r010-prototype-separation.json"
$Contracts = Read-Json "contract-status-update.json"

if ($Intake.CoreHandoffManifestHashMatchesExpected -ne $true) {
    Fail "manifest hash validation failed"
}
if ($Weights.DirectCrossesAbsent -ne $true) {
    Fail "direct crosses were not rejected/removed"
}
if ($Weights.USDJPYNotEmittedByCore -ne $true) {
    Fail "USDJPY Core emission was not rejected"
}
if ($Weights.JPYUSDCaveatPresent -ne $true) {
    Fail "JPYUSD caveat is missing"
}
if ($R010.R010TransferableToCoreAnubisOutput -ne $false) {
    Fail "R010 non-transferability failed"
}
if ($null -ne $Candidate.Quantities -or $Candidate.QuantityStatus -ne "MissingSizingAndMarketDataBinding") {
    Fail "candidate preview has quantities without sizing"
}

$ContractMap = @{}
foreach ($Status in $Contracts.Statuses) {
    $ContractMap[$Status.ContractId] = $Status.Status
}
if ($ContractMap["r009-execution-readiness.v1"] -ne "UNCHANGED_BLOCKED_FOR_CORE_CANDIDATE") {
    Fail "R009 executable status was incorrectly granted"
}

Write-Host "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_TEST_PASS"
