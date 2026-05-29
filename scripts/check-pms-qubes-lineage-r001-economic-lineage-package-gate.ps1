param(
    [string]$ArtifactRoot = "artifacts/readiness/pms-qubes-lineage"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS-QUBES-LINEAGE-R001 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { Fail "missing required file: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) { Fail "$Name must be true" }
}

function Assert-False($Value, [string]$Name) {
    if ($Value -ne $false) { Fail "$Name must be false" }
}

function Assert-NoCredentialValues([string[]]$Paths) {
    $patterns = @(
        '"CredentialValue"\s*:',
        '"CredentialValues"\s*:',
        '"PasswordValue"\s*:',
        '"SecretValue"\s*:',
        '554=[^|\r\n]+',
        'LMAX_DEMO_FIX_PASSWORD"\s*:\s*"[^"]+"'
    )
    foreach ($path in $Paths) {
        $content = Get-Content -LiteralPath $path -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) { Fail "credential value-like content persisted in $path" }
        }
    }
}

$requiredFiles = @(
    "phase-pms-qubes-lineage-r001-summary.md",
    "phase-pms-qubes-lineage-r001-pms-paper-r015-reference.json",
    "phase-pms-qubes-lineage-r001-cross-rail-r014-reference.json",
    "phase-pms-qubes-lineage-r001-q4e-historical-only-confirmation.json",
    "phase-pms-qubes-lineage-r001-economic-lineage-package.json",
    "phase-pms-qubes-lineage-r001-field-binding-matrix.json",
    "phase-pms-qubes-lineage-r001-qubes-output-v1-adoption.json",
    "phase-pms-qubes-lineage-r001-pms-handoff-v1-adoption.json",
    "phase-pms-qubes-lineage-r001-canonical-timing-v1-adoption.json",
    "phase-pms-qubes-lineage-r001-risk-control-v1-adoption.json",
    "phase-pms-qubes-lineage-r001-side-derivation-evidence.json",
    "phase-pms-qubes-lineage-r001-missing-field-diagnostics.json",
    "phase-pms-qubes-lineage-r001-direct-cross-netting-result.json",
    "phase-pms-qubes-lineage-r001-unsupported-instrument-diagnostics.json",
    "phase-pms-qubes-lineage-r001-ledger-readiness-impact.json",
    "phase-pms-qubes-lineage-r001-no-execution-audit.json",
    "phase-pms-qubes-lineage-r001-no-db-mutation-audit.json",
    "phase-pms-qubes-lineage-r001-no-order-fill-route-audit.json",
    "phase-pms-qubes-lineage-r001-forbidden-actions-audit.json",
    "phase-pms-qubes-lineage-r001-next-phase-recommendation.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) { Fail "missing required artifact: $file" }
    $paths += $path
}
Assert-NoCredentialValues $paths

$pmsRef = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-pms-paper-r015-reference.json")
$crossRef = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-cross-rail-r014-reference.json")
$q4e = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-q4e-historical-only-confirmation.json")
$package = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-economic-lineage-package.json")
$matrix = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-field-binding-matrix.json")
$qubes = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-qubes-output-v1-adoption.json")
$handoff = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-pms-handoff-v1-adoption.json")
$timing = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-canonical-timing-v1-adoption.json")
$risk = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-risk-control-v1-adoption.json")
$side = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-side-derivation-evidence.json")
$diag = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-missing-field-diagnostics.json")
$direct = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-direct-cross-netting-result.json")
$unsupported = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-unsupported-instrument-diagnostics.json")
$ledger = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-ledger-readiness-impact.json")
$noExec = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-no-execution-audit.json")
$noDb = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-no-db-mutation-audit.json")
$noOrder = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-no-order-fill-route-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-forbidden-actions-audit.json")

Assert-True $pmsRef.ReadOnlyReview "PMS R015 ReadOnlyReview"
Assert-True $pmsRef.ValidatorPassed "PMS R015 validator"
Assert-True $crossRef.ReadOnlyReview "CrossRail R014 ReadOnlyReview"
Assert-True $crossRef.ValidatorPassed "CrossRail R014 validator"

Assert-False $q4e.Qubes4EActiveState "Qubes4EActiveState"
Assert-False $q4e.StratTakenBootstrapActiveState "StratTakenBootstrapActiveState"
Assert-False $q4e.CurrentQubesBranchHandoffEligible "CurrentQubesBranchHandoffEligible"

if ($null -ne $package.Fields.AccountId) { Fail "AccountId must not be invented" }
if ($null -ne $package.Fields.PortfolioId) { Fail "PortfolioId must not be invented" }
if ($null -ne $package.Fields.StrategyId) { Fail "StrategyId must not be invented" }
if ($null -ne $package.Fields.SourceExecutionIntentId) { Fail "SourceExecutionIntentId must not be invented" }
if ($null -ne $package.Fields.AccountCurrency) { Fail "AccountCurrency must not be invented" }
if ($null -ne $package.Fields.AttributionPolicy) { Fail "AttributionPolicy must not be invented" }
Assert-True $package.SandboxOnly "package SandboxOnly"
Assert-False $package.ProductionLiveAllowed "package ProductionLiveAllowed"
Assert-False $package.LedgerCommitCreated "package LedgerCommitCreated"

foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy")) {
    $entry = @($matrix.Fields | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $entry) { Fail "missing matrix field $field" }
    if ([string]$entry.evidenceStatus -ne "Missing") { Fail "$field must be Missing" }
    if ($null -ne $entry.value) { Fail "$field matrix value must be null" }
}

if ([string]$qubes.Status -ne "AdoptedWithWarnings") { Fail "qubes-output status must be AdoptedWithWarnings" }
Assert-True $qubes.DirectCrossSignalOnly "qubes DirectCrossSignalOnly"
Assert-True $qubes.RequiresNetting "qubes RequiresNetting"
Assert-False $qubes.CurrentQubesBranchHandoffEligible "qubes CurrentQubesBranchHandoffEligible"

if ([string]$handoff.Status -ne "AdoptedWithWarnings") { Fail "pms-handoff status must be AdoptedWithWarnings" }
Assert-True $handoff.MustEndFlat "handoff MustEndFlat"
Assert-False $handoff.OvernightAllowed "handoff OvernightAllowed"
Assert-False $handoff.ProductionLiveAllowed "handoff ProductionLiveAllowed"

if ([string]$timing.CanonicalTargetCloseUtc -ne "2025-12-17T02:00:00Z") { Fail "canonical close mismatch" }
Assert-False $timing.LegacyFutureCanonicalTimingUsed "LegacyFutureCanonicalTimingUsed"

Assert-False $risk.DirectCrossExecutionAllowed "risk DirectCrossExecutionAllowed"
Assert-True $risk.UsdPairOnlyExecutionHandoff "risk UsdPairOnlyExecutionHandoff"
Assert-False $risk.ProductionLiveAllowed "risk ProductionLiveAllowed"
Assert-True $risk.UsdjpyCaveat.RequiresInversion "USDJPY RequiresInversion"
if ([int]$risk.UsdjpyCaveat.SecurityID -ne 4004) { Fail "USDJPY SecurityID must be 4004" }

if ([string]$side.Status -ne "SideDerivationEvidencePresent") { Fail "side derivation must be present" }
Assert-False $side.MissingExecAlgoSide "MissingExecAlgoSide"
foreach ($row in $side.SideDerivationEvidence) {
    Assert-False $row.Invented "side invented $($row.Symbol)"
}

if (@($diag.InventedFields).Count -ne 0) { Fail "invented fields must be empty" }
Assert-False $direct.DirectCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.ExecutionHandoffUsdPairOnly "USD-pair-only execution handoff"
Assert-False $unsupported.UnsupportedInstrumentsMarkedExecutable "unsupported executable"
Assert-False $unsupported.DirectCrossExecutionAllowed "unsupported direct-cross execution"

if ([string]$ledger.LedgerStateR005CurrentCeiling -ne "SandboxPriceDeltaOnlyReady") { Fail "ledger ceiling mismatch" }
Assert-False $ledger.FullTheoreticalPnlReady "FullTheoreticalPnlReady"
Assert-False $ledger.LedgerCommitAllowed "LedgerCommitAllowed"
Assert-False $ledger.LedgerCommitCreated "LedgerCommitCreated"

foreach ($name in @(
    "NoLmaxCall",
    "NoPolygonCall",
    "NoExternalApiCall",
    "NoBrokerActivation",
    "NoLiveMarketDataRequested",
    "NoQubesExecutableRun",
    "NoPythonCppCudaWorkloadRun",
    "NoPmsEmsOmsExecutionCycleRun",
    "NoManualNoExternalRun",
    "NoProductionLivePromotion"
)) { Assert-True $noExec.$name "no-execution $name" }

foreach ($name in @("NoDbMutation", "NoSqlMutationArtifact", "NoLedgerCommit", "NoPositionMutation", "NoCashMutation", "NoTradingStateMutation", "NoProductionLedgerMutation", "NoPaperLedgerMutation")) {
    Assert-True $noDb.$name "no-db $name"
}

foreach ($name in @("NoOrdersCreated", "NoRoutesCreated", "NoSubmissionsCreated", "NoFillsCreated", "NoExecutionReportsCreated", "NoExecutableSchedulesCreated", "NoBrokerSubmission")) {
    Assert-True $noOrder.$name "no-order $name"
}

foreach ($prop in $forbidden.PSObject.Properties) {
    if ($prop.Name -in @("Gate", "Status", "DirectCrossExecutionAllowed")) { continue }
    if ($prop.Name -like "No*") { continue }
    Assert-False $prop.Value "forbidden $($prop.Name)"
}
Assert-False $forbidden.DirectCrossExecutionAllowed "forbidden DirectCrossExecutionAllowed"

$evidencePath = Join-Path $ArtifactRoot "phase-pms-qubes-lineage-r001-build-test-validator-evidence.json"
if (-not (Test-Path -LiteralPath $evidencePath)) { Fail "build/tests/validator evidence missing" }
$evidence = Read-Json $evidencePath
if ($evidence.Validator.Result -ne "Passed") { Fail "validator evidence must be Passed" }

Write-Host "PMS-QUBES-LINEAGE-R001 validator passed."
