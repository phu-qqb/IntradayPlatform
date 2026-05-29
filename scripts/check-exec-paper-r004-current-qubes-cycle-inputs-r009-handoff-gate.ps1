param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-False($Object, [string]$Property, [string]$Message) {
    if ($Object.PSObject.Properties.Name -notcontains $Property) {
        Fail "Missing property '$Property' in $Message"
    }

    if ([bool]$Object.$Property) {
        Fail $Message
    }
}

function Assert-True($Object, [string]$Property, [string]$Message) {
    if ($Object.PSObject.Properties.Name -notcontains $Property) {
        Fail "Missing property '$Property' in $Message"
    }

    if (-not [bool]$Object.$Property) {
        Fail $Message
    }
}

$required = @(
    "phase-exec-paper-r004-summary.md",
    "phase-exec-paper-r004-r003-cli-contract-reference.json",
    "phase-exec-paper-r004-r009-contract-reference.json",
    "phase-exec-paper-r004-r010-planning-reference.json",
    "phase-exec-paper-r004-current-qubes-cycle-input-search-contract.json",
    "phase-exec-paper-r004-qubes-cycle-input-search-results.json",
    "phase-exec-paper-r004-cli-arg-resolution.json",
    "phase-exec-paper-r004-generated-manual-noexternal-command.json",
    "phase-exec-paper-r004-operator-command-safety-check.json",
    "phase-exec-paper-r004-current-manual-paper-plan-generation-result.json",
    "phase-exec-paper-r004-pms-artifact-inventory.json",
    "phase-exec-paper-r004-current-paper-plan-line-search-results.json",
    "phase-exec-paper-r004-current-input-readiness-result.json",
    "phase-exec-paper-r004-present-inputs-report.json",
    "phase-exec-paper-r004-missing-inputs-diagnostics.json",
    "phase-exec-paper-r004-canonical-target-close-readiness.json",
    "phase-exec-paper-r004-readiness-binding-search-results.json",
    "phase-exec-paper-r004-risk-operator-approval-readiness.json",
    "phase-exec-paper-r004-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r004-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r004-r009-design-only-preview-contract.json",
    "phase-exec-paper-r004-r009-design-only-preview-lines.json",
    "phase-exec-paper-r004-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r004-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r004-legacy-compatibility-preservation.json",
    "phase-exec-paper-r004-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r004-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r004-cost-guidance-preservation.json",
    "phase-exec-paper-r004-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r004-no-broker-activation-audit.json",
    "phase-exec-paper-r004-no-live-marketdata-audit.json",
    "phase-exec-paper-r004-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r004-no-executable-schedule-audit.json",
    "phase-exec-paper-r004-no-child-slices-audit.json",
    "phase-exec-paper-r004-no-child-orders-audit.json",
    "phase-exec-paper-r004-no-order-created-audit.json",
    "phase-exec-paper-r004-no-real-fill-audit.json",
    "phase-exec-paper-r004-no-execution-report-audit.json",
    "phase-exec-paper-r004-no-route-no-submission-audit.json",
    "phase-exec-paper-r004-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r004-no-polygon-api-call-audit.json",
    "phase-exec-paper-r004-no-lmax-call-audit.json",
    "phase-exec-paper-r004-no-external-api-call-audit.json",
    "phase-exec-paper-r004-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r004-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r004-no-external-audit.json",
    "phase-exec-paper-r004-forbidden-actions-audit.json",
    "phase-exec-paper-r004-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R004 artifact: $file"
    }
}

$searchContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-current-qubes-cycle-input-search-contract.json")
$searchResults = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-qubes-cycle-input-search-results.json")
$args = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-cli-arg-resolution.json")
$command = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-generated-manual-noexternal-command.json")
$safety = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-operator-command-safety-check.json")
$generation = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-current-manual-paper-plan-generation-result.json")
$planSearch = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-current-paper-plan-line-search-results.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-current-input-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-missing-inputs-diagnostics.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-r009-design-only-preview-lines.json")
$r009 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-r009-contract-reference.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-forbidden-actions-audit.json")
$canonical = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-usdjpy-caveat-preservation.json")
$universe = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-usd-pair-normalization-preservation.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r004-build-test-validator-evidence.json")

Assert-True $searchContract "ResolveOnlyFromExistingEvidence" "Qubes/cycle inputs must resolve only from existing evidence."
Assert-True $searchContract "NoInventedCycleIds" "Cycle IDs may not be invented."
Assert-True $searchContract "NoInventedQubesIds" "Qubes IDs may not be invented."
Assert-True $searchContract "NoInventedPlanLines" "Plan lines may not be invented."

if ([bool]$searchResults.CurrentQubesCycleInputsResolved) {
    if (-not [bool]$args.AllRequiredArgsResolved) {
        Fail "Search claims current Qubes/cycle inputs are resolved, but CLI args are not fully resolved."
    }
}

Assert-False $searchResults "InputsInvented" "Current Qubes/cycle inputs were invented."

Assert-True $safety "SafeCommandTemplate" "Generated ManualNoExternal command template is not safe."
Assert-True $safety "UsesManualNoExternal" "Command does not use ManualNoExternal."
Assert-True $safety "IncludesNoPaperLedgerCommit" "Command omits --no-paper-ledger-commit."
Assert-False $safety "UsesDeprecatedNoExternalPaperCycleMode" "Deprecated no-external-paper-cycle mode was used."
Assert-False $safety "UsesDeprecatedOutputArgument" "Deprecated --output argument was used."

if (-not [bool]$args.AllRequiredArgsResolved) {
    Assert-False $safety "CommandExecutedInR004" "Command was executed with missing current Qubes/cycle inputs."
    Assert-False $generation "CommandRunAttempted" "Generation command was attempted with missing current Qubes/cycle inputs."
    if (-not [bool]$missing.MissingInputsRemain) {
        Fail "Missing-input diagnostics must exist when inputs are missing."
    }
}

if ([bool]$safety.CommandExecutedInR004) {
    if (-not [bool]$safety.SafeToRunNow) {
        Fail "Unsafe command was run."
    }

    if ([int]$generation.CommandRunCount -ne 1) {
        Fail "Manual paper plan generation must run exactly one cycle."
    }
}

if ([int]$generation.CommandRunCount -gt 1) {
    Fail "More than one cycle was run."
}

Assert-False $generation "MoreThanOneCycleRun" "More than one cycle was run."
Assert-False $planSearch "PlanLinesInvented" "Current paper plan lines were invented."

if ([bool]$readiness.CurrentInputsReady -and -not [bool]$handoff.HandoffPackageReady) {
    Fail "Inputs are marked ready but R009 handoff package is not ready."
}

if ([int]$handoff.LineCount -ne 0 -and -not [bool]$handoff.NonExecutable) {
    Fail "Handoff lines must remain non-executable."
}

if ([int]$preview.PreviewLineCount -ne 0) {
    Assert-True $preview "NoOrdersRepresented" "Preview lines are represented as orders."
    Assert-True $preview "NoSchedulesRepresented" "Preview lines are represented as schedules."
    Assert-True $preview "NoFillsRepresented" "Preview lines are represented as fills."
    Assert-True $preview "NoRoutesRepresented" "Preview lines are represented as routes."
}

Assert-True $r009 "DesignOnly" "R009 contract must remain design-only."
Assert-True $r009 "NonExecutable" "R009 contract must remain non-executable."
Assert-False $r009 "ExecutablePromotionAuthorized" "R009 was promoted to executable."

$falseForbidden = @(
    "PolygonCalled",
    "LmaxCalled",
    "ExternalApiCalled",
    "BrokerActivated",
    "SocketTlsFixOpened",
    "LiveMarketDataRequested",
    "UnsafeCommandRun",
    "MoreThanOneCycleRun",
    "AutomaticExecutionOccurred",
    "DeprecatedModeUsed",
    "DeprecatedOutputArgumentUsed",
    "ExecutableScheduleCreated",
    "ChildSlicesCreated",
    "ChildOrdersCreated",
    "OrdersCreated",
    "FillsCreated",
    "ExecutionReportsCreated",
    "RoutesCreated",
    "SubmissionsCreated",
    "PaperLedgerCommitCreated",
    "StateMutated",
    "CurrentQubesCycleInputsInvented",
    "PlanLinesInvented",
    "R009PromotedToExecutable",
    "Legacy06UsedAsFutureCanonical",
    "FiveUsdPerMillionUniversalized",
    "USDJPYCaveatWeakened",
    "AUDUSDMisclassified"
)

foreach ($property in $falseForbidden) {
    Assert-False $forbidden $property "Forbidden action detected: $property"
}

Assert-False $canonical "LegacyUsedAsFutureCanonical" "Legacy :06 was used as future canonical."
Assert-True $legacy "CompatibilityOnly" "Legacy compatibility-only policy was weakened."
Assert-False $legacy "LegacyUsedAsFutureCanonical" "Legacy :06 was used as future canonical."
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion was weakened."
Assert-False $cost "Universalized" "5 USD/million was universalized."
Assert-True $usdjpy "CaveatPreserved" "USDJPY caveat was weakened."
Assert-True $universe "AUDUSDNotFailed" "AUDUSD was misclassified as failed."

if (-not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/test/validator evidence is missing."
}

Write-Host "EXEC-PAPER-R004 validator passed."
