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
    "phase-exec-paper-r002-summary.md",
    "phase-exec-paper-r002-r001-operator-action-reference.json",
    "phase-exec-paper-r002-r055-diagnostics-reference.json",
    "phase-exec-paper-r002-r009-contract-reference.json",
    "phase-exec-paper-r002-r010-planning-reference.json",
    "phase-exec-paper-r002-operator-command-safety-check.json",
    "phase-exec-paper-r002-current-manual-paper-plan-generation-result.json",
    "phase-exec-paper-r002-pms-artifact-inventory.json",
    "phase-exec-paper-r002-current-paper-plan-line-search-results.json",
    "phase-exec-paper-r002-historical-paper-plan-line-reference.json",
    "phase-exec-paper-r002-current-input-readiness-result.json",
    "phase-exec-paper-r002-present-inputs-report.json",
    "phase-exec-paper-r002-missing-inputs-diagnostics.json",
    "phase-exec-paper-r002-canonical-target-close-readiness.json",
    "phase-exec-paper-r002-readiness-binding-search-results.json",
    "phase-exec-paper-r002-risk-operator-approval-readiness.json",
    "phase-exec-paper-r002-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r002-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r002-r009-design-only-preview-contract.json",
    "phase-exec-paper-r002-r009-design-only-preview-lines.json",
    "phase-exec-paper-r002-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r002-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r002-legacy-compatibility-preservation.json",
    "phase-exec-paper-r002-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r002-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r002-cost-guidance-preservation.json",
    "phase-exec-paper-r002-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r002-no-broker-activation-audit.json",
    "phase-exec-paper-r002-no-live-marketdata-audit.json",
    "phase-exec-paper-r002-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r002-no-executable-schedule-audit.json",
    "phase-exec-paper-r002-no-child-slices-audit.json",
    "phase-exec-paper-r002-no-child-orders-audit.json",
    "phase-exec-paper-r002-no-order-created-audit.json",
    "phase-exec-paper-r002-no-real-fill-audit.json",
    "phase-exec-paper-r002-no-execution-report-audit.json",
    "phase-exec-paper-r002-no-route-no-submission-audit.json",
    "phase-exec-paper-r002-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r002-no-polygon-api-call-audit.json",
    "phase-exec-paper-r002-no-lmax-call-audit.json",
    "phase-exec-paper-r002-no-external-api-call-audit.json",
    "phase-exec-paper-r002-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r002-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r002-no-external-audit.json",
    "phase-exec-paper-r002-forbidden-actions-audit.json",
    "phase-exec-paper-r002-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R002 artifact: $file"
    }
}

$safety = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-operator-command-safety-check.json")
$generation = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-current-manual-paper-plan-generation-result.json")
$search = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-current-paper-plan-line-search-results.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-current-input-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-missing-inputs-diagnostics.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-r009-design-only-preview-lines.json")
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-r009-contract-reference.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-forbidden-actions-audit.json")
$canonical = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-usdjpy-caveat-preservation.json")
$universe = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-usd-pair-normalization-preservation.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r002-build-test-validator-evidence.json")

if (-not [bool]$safety.CommandPresent) {
    Fail "R001 operator command reference is missing."
}

if ([bool]$safety.SafeToRun -and [bool]$safety.CommandExecutedInR002) {
    if ([int]$generation.CommandRunCount -ne 1) {
        Fail "Safe operator command execution must run exactly one cycle."
    }
}

if (-not [bool]$safety.SafeToRun) {
    Assert-False $safety "CommandExecutedInR002" "Unsafe or ambiguous operator command was run."
    if ($safety.SafetyStatus -ne "BlockedMissingOrUnsafeOperatorCommand") {
        Fail "Unsafe or ambiguous command must be classified as BlockedMissingOrUnsafeOperatorCommand."
    }
}

if ([int]$generation.CommandRunCount -gt 1) {
    Fail "More than one paper cycle was run."
}

Assert-False $generation "ManualPaperPlanGenerationAttempted" "R002 claims manual plan generation was attempted despite blocked command."
Assert-False $generation "CurrentPaperPlanLinesGenerated" "R002 generated current plan lines after a blocked command."
Assert-False $generation "MoreThanOneCycleRun" "R002 ran more than one cycle."

Assert-False $search "PlanLinesInvented" "Current paper plan lines were invented."
Assert-False $search "PmsCycleRunAutomatically" "PMS/EMS/OMS cycle was run automatically."

if ([bool]$readiness.CurrentInputsReady -and -not [bool]$handoff.HandoffPackageReady) {
    Fail "Inputs are marked ready but handoff package is not ready."
}

if (-not [bool]$readiness.CurrentInputsReady) {
    if (-not [bool]$missing.MissingInputsRemain) {
        Fail "Missing-input diagnostics must be present when inputs are not ready."
    }
}

if ([int]$handoff.LineCount -ne 0 -and -not [bool]$handoff.NonExecutable) {
    Fail "Handoff lines must be non-executable."
}

if ([int]$preview.PreviewLineCount -ne 0) {
    Assert-True $preview "NoOrdersRepresented" "Preview lines are represented as orders."
    Assert-True $preview "NoSchedulesRepresented" "Preview lines are represented as schedules."
    Assert-True $preview "NoFillsRepresented" "Preview lines are represented as fills."
    Assert-True $preview "NoRoutesRepresented" "Preview lines are represented as routes."
}

Assert-True $contract "DesignOnly" "R009 contract reference must remain design-only."
Assert-True $contract "NonExecutable" "R009 contract reference must remain non-executable."
Assert-False $contract "ExecutablePromotionAuthorized" "R009 was promoted to executable use."

$falseForbidden = @(
    "PolygonCalled",
    "LmaxCalled",
    "ExternalApiCalled",
    "BrokerActivated",
    "SocketTlsFixOpened",
    "LiveMarketDataRequested",
    "UnsafeOperatorCommandRun",
    "MoreThanOneCycleRun",
    "AutomaticExecutionOccurred",
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
Assert-True $legacy "CompatibilityOnly" "Legacy timestamp mapping must remain compatibility-only."
Assert-False $legacy "LegacyUsedAsFutureCanonical" "Legacy :06 was used as future canonical."
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion was weakened."
Assert-False $cost "Universalized" "5 USD/million was universalized."
Assert-True $usdjpy "CaveatPreserved" "USDJPY caveat was weakened."
Assert-True $universe "AUDUSDNotFailed" "AUDUSD was misclassified as failed."

if (-not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/test/validator evidence is missing."
}

Write-Host "EXEC-PAPER-R002 validator passed."
